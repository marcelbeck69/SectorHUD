using GameOverlay.Windows;
using System.Globalization;
using System.Text.RegularExpressions;
using SectorHUDgui.Properties;
using Font = GameOverlay.Drawing.Font;
using Graphics = GameOverlay.Drawing.Graphics;
using SolidBrush = GameOverlay.Drawing.SolidBrush;

namespace SectorHUDgui
{
    public class OverlayRenderer : IDisposable
    {
        private GraphicsWindow? _window = null;
        private SolidBrush _backgroundBrush = null!;
        private SolidBrush _whiteBrush = null!;
        private Font _defaultFont = null!;
        private string _displayText = "SectorHUD<color=00FF00> " + Strings.Ready + "</color>";
        private float _startX, _startY;
        private float _globalFontSize;
        private string _fontName;
        private int _displayIndex, _transparency;

        // Cache für Farbpinsel (nullable values zulassen)
        private Dictionary<string, SolidBrush?> _customBrushes = new Dictionary<string, SolidBrush?>();
        // Cache für Schriftarten verschiedener Größen
        private Dictionary<float, Font> _fontsBySize = new Dictionary<float, Font>();

        // Thread-Sicherung für Text-Updates
        private readonly object _textLock = new object();
        private Thread _windowThread = null!;
        private bool _running;

        public OverlayRenderer(string fontName, float fontSize, float startX, float startY, int displayIndex, int transparency)
        {
            _fontName = fontName;
            _globalFontSize = fontSize;
            _startX = startX;
            _startY = startY;
            _displayIndex = displayIndex;
            _transparency = transparency;
        }

        // Startet das Overlay in einem separaten STA-Thread
        public void Start()
        {
            if (_running) return;
            _running = true;
            _windowThread = new Thread(RunWindowLoop);
            _windowThread.SetApartmentState(ApartmentState.STA);
            _windowThread.IsBackground = true;
            _windowThread.Start();
        }

        private void RunWindowLoop()
        {
            var gfx = new Graphics()
            {
                MeasureFPS = true,
                PerPrimitiveAntiAliasing = true,
                TextAntiAliasing = true
            };

            // Gewünschten Monitor ermitteln (null-sicher)
            Screen? targetScreen = null;
            if (_displayIndex >= 0 && _displayIndex < Screen.AllScreens.Length)
                targetScreen = Screen.AllScreens[_displayIndex];
            else
                targetScreen = Screen.PrimaryScreen ?? (Screen.AllScreens.Length > 0 ? Screen.AllScreens[0] : null);

            int x = targetScreen?.Bounds.X ?? 0;
            int y = targetScreen?.Bounds.Y ?? 0;
            int width = targetScreen?.Bounds.Width ?? 800;
            int height = targetScreen?.Bounds.Height ?? 600;

            _window = new GraphicsWindow(0, 0, width, height, gfx)
            {
                FPS = 30,
                IsTopmost = true,
                IsVisible = true
            };

            _window.SetupGraphics += Window_SetupGraphics;
            _window.DestroyGraphics += Window_DestroyGraphics;
            _window.DrawGraphics += Window_DrawGraphics;

            _window.Create();
            // Diese Schleife pumpt die Windows-Nachrichten und blockiert, bis das Fenster geschlossen wird
            _window.Join();
            _window.Dispose();
            _window = null;
        }

        private void Window_SetupGraphics(object? sender, SetupGraphicsEventArgs e)
        {
            var gfx = e.Graphics;
            int opacity = (int)Math.Max(0, Math.Min(255, (100 - _transparency) * 2.55));
            _backgroundBrush = gfx.CreateSolidBrush(0, 0, 0, opacity);
            _whiteBrush = gfx.CreateSolidBrush(255, 255, 255);
            _defaultFont = gfx.CreateFont(_fontName, _globalFontSize);
            _fontsBySize.Clear();
            _fontsBySize[_globalFontSize] = _defaultFont;
        }

        private void Window_DestroyGraphics(object? sender, DestroyGraphicsEventArgs e)
        {
            _backgroundBrush?.Dispose();
            _whiteBrush?.Dispose();
            _defaultFont?.Dispose();
            foreach (var brush in _customBrushes.Values) brush?.Dispose();
            _customBrushes.Clear();
            foreach (var font in _fontsBySize.Values) if (font != _defaultFont) font?.Dispose();
            _fontsBySize.Clear();
        }

        private void Window_DrawGraphics(object? sender, DrawGraphicsEventArgs e)
        {
            var gfx = e.Graphics;
            gfx.ClearScene();
            gfx.BeginScene();

            string textToDraw;
            lock (_textLock) { textToDraw = _displayText; }

            var bounds = ComputeTextBounds(gfx, textToDraw);
            float padding = 5;
            gfx.FillRectangle(_backgroundBrush,
                _startX - padding, _startY - padding,
                _startX + bounds.Width + padding, _startY + bounds.Height + padding);

            ParseAndDrawText(gfx, textToDraw);

            gfx.EndScene();
        }

        private (float Width, float Height) ComputeTextBounds(Graphics gfx, string text)
        {
            if (string.IsNullOrEmpty(text)) return (0, 0);
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            float maxWidth = 0, totalHeight = 0;
            foreach (string line in lines)
            {
                float lineFontSize = GetLineFontSize(line, out string cleanLine);
                Font font = GetFontForSize(gfx, lineFontSize);
                float lineHeight = gfx.MeasureString(font, "Ay").Y + 4;
                string plainText = Regex.Replace(cleanLine, "<[^>]+>", "");
                float lineWidth = gfx.MeasureString(font, plainText).X;
                if (lineWidth > maxWidth) maxWidth = lineWidth;
                totalHeight += lineHeight;
            }
            return (maxWidth, totalHeight);
        }

        private void ParseAndDrawText(Graphics gfx, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            float currentY = _startY;
            var colorRegex = new Regex("<color=#?(?<hex>[A-Fa-f0-9]{6})>(?<inner>.*?)</color>", RegexOptions.Compiled);

            foreach (string rawLine in lines)
            {
                float lineFontSize = GetLineFontSize(rawLine, out string line);
                Font lineFont = GetFontForSize(gfx, lineFontSize);
                float lineHeight = gfx.MeasureString(lineFont, "Ay").Y + 4;
                float currentX = _startX;
                int lastIndex = 0;
                var matches = colorRegex.Matches(line);

                foreach (Match match in matches)
                {
                    if (match.Index > lastIndex)
                    {
                        string plain = line.Substring(lastIndex, match.Index - lastIndex);
                        DrawTextSegment(gfx, plain, _whiteBrush, lineFont, ref currentX, currentY);
                    }
                    string hex = match.Groups["hex"].Value;
                    string coloredText = match.Groups["inner"].Value;
                    SolidBrush brush = GetBrushForHex(gfx, hex);
                    DrawTextSegment(gfx, coloredText, brush, lineFont, ref currentX, currentY);
                    lastIndex = match.Index + match.Length;
                }
                if (lastIndex < line.Length)
                {
                    string remaining = line.Substring(lastIndex);
                    DrawTextSegment(gfx, remaining, _whiteBrush, lineFont, ref currentX, currentY);
                }
                currentY += lineHeight;
            }
        }

        private void DrawTextSegment(Graphics gfx, string text, SolidBrush brush, Font font, ref float x, float y)
        {
            if (string.IsNullOrEmpty(text)) return;
            gfx.DrawText(font, brush, x, y, text);
            x += gfx.MeasureString(font, text).X;
        }



        private float GetLineFontSize(string line, out string cleanLine)
        {
            var match = Regex.Match(line, @"^\s*\{(\d+(?:\.\d+)?)\}\s*(.*)$");
            if (match.Success)
            {
                float size = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                cleanLine = match.Groups[2].Value;
                return size;
            }
            cleanLine = line;
            return _globalFontSize;
        }

        private SolidBrush GetBrushForHex(Graphics gfx, string hexColor)
        {
            if (_customBrushes.TryGetValue(hexColor, out SolidBrush? brush) && brush != null)
                return brush;
            if (byte.TryParse(hexColor.Substring(0, 2), NumberStyles.HexNumber, null, out byte r) &&
                byte.TryParse(hexColor.Substring(2, 2), NumberStyles.HexNumber, null, out byte g) &&
                byte.TryParse(hexColor.Substring(4, 2), NumberStyles.HexNumber, null, out byte b))
            {
                brush = gfx.CreateSolidBrush(r, g, b);
                _customBrushes[hexColor] = brush;
                return brush;
            }
            return _whiteBrush;
        }

        private Font GetFontForSize(Graphics gfx, float size)
        {
            if (_fontsBySize.TryGetValue(size, out Font? font) && font != null)
                return font;
            font = gfx.CreateFont(_fontName, size);
            _fontsBySize[size] = font;
            return font;
        }

        // Wird vom Hauptthread aufgerufen, um den anzuzeigenden Text zu ändern
        public void UpdateText(string newText)
        {
            lock (_textLock)
            {
                _displayText = newText;
            }
        }

        // Beendet das Overlay und den Thread
        public void Stop()
        {
            if (!_running) return;
            _running = false;
            try
            {
                _window?.Dispose();
            }
            catch
            {
                // ignore disposal errors
            }
            _windowThread?.Join(2000);
        }

        public void Dispose()
        {
            Stop();
        }

    }
}