using SectorHUDgui.Properties;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace SectorHUDgui
{
    public partial class MainForm : Form
    {
        private System.Windows.Forms.Timer? _telemetryTimer;
        private TelemetryData? _lastTelemetry = null;
        private OverlayRenderer? _overlay;
        private bool _monitorActive;
        private string? _currentModTable, _currentSectorTable;

        // UI-Elemente (im Designer erstellt oder manuell)
        private MenuStrip? mainMenu;
        private ToolStripMenuItem? fileMenu, databaseMenu, monitorMenu, settingsMenu, helpMenu;
        private ToolStripMenuItem? exitMenuItem;
        private ToolStripMenuItem? queryETS2MenuItem, queryATSMenuItem;
        private ToolStripMenuItem? updateETS2MenuItem, updateATSMenuItem, recreateETS2MenuItem, recreateATSMenuItem;
        private ToolStripMenuItem? startMonitorMenuItem, stopMonitorMenuItem, demoMonitorMenuItem;
        private ToolStripMenuItem? configMenuItem;
        private ToolStripMenuItem? infoMenuItem, manualMenuItem;
        private StatusStrip? statusStrip;
        public static ToolStripStatusLabel? statusLabel;
        private RichTextBox? rtbOutput;

        public MainForm()
        {
            InitializeComponent();
            this.Icon = Properties.Resources.AppIcon;
            InitializeTelemetryTimer();
            _monitorActive = false;
            UpdateMonitorMenuState();
            if (ConfigManager.GetBool("General", "Autostart", false))
                this.Shown += (s, e) => StartMonitor();
        }

        private void InitializeComponent()
        {
            // ========== Menüleiste ==========
            mainMenu = new MenuStrip();
            mainMenu.Dock = DockStyle.Top;
            fileMenu = new ToolStripMenuItem(Strings.File);
            exitMenuItem = new ToolStripMenuItem(Strings.Exit, null, (s, e) => Close());
            fileMenu.DropDownItems.Add(exitMenuItem);

            databaseMenu = new ToolStripMenuItem(Strings.Database);
            queryETS2MenuItem = new ToolStripMenuItem(Strings.QueryETS2, null, QueryETS2_Click);
            queryATSMenuItem = new ToolStripMenuItem(Strings.QueryATS, null, QueryATS_Click);
            updateETS2MenuItem = new ToolStripMenuItem(Strings.UpdateETS2, null, UpdateETS2_Click);
            updateATSMenuItem = new ToolStripMenuItem(Strings.UpdateATS, null, UpdateATS_Click);
            recreateETS2MenuItem = new ToolStripMenuItem(Strings.RecreateETS2, null, RecreateETS2_Click);
            recreateATSMenuItem = new ToolStripMenuItem(Strings.RecreateATS, null, RecreateATS_Click);
            databaseMenu.DropDownItems.AddRange(new ToolStripItem[] {
                queryETS2MenuItem, queryATSMenuItem,
                new ToolStripSeparator(), updateETS2MenuItem, updateATSMenuItem,
                new ToolStripSeparator(), recreateETS2MenuItem, recreateATSMenuItem });

            monitorMenu = new ToolStripMenuItem(Strings.Monitor);
            startMonitorMenuItem = new ToolStripMenuItem(Strings.Start, null, StartMonitor_Click);
            stopMonitorMenuItem = new ToolStripMenuItem(Strings.Stop, null, StopMonitor_Click) { Enabled = false };
            demoMonitorMenuItem = new ToolStripMenuItem(Strings.Demo, null, DemoMonitor_Click);
            monitorMenu.DropDownItems.AddRange(new ToolStripItem[] { startMonitorMenuItem, stopMonitorMenuItem, new ToolStripSeparator(), demoMonitorMenuItem });

            settingsMenu = new ToolStripMenuItem(Strings.Settings);
            configMenuItem = new ToolStripMenuItem(Strings.EditConfiguration, null, EditConfig_Click);
            settingsMenu.DropDownItems.Add(configMenuItem);

            helpMenu = new ToolStripMenuItem(Strings.Help);
            infoMenuItem = new ToolStripMenuItem(Strings.Info, null, (s, e) => ShowInfoDialog());
            manualMenuItem = new ToolStripMenuItem(Strings.Instructions, null, (s, e) => ShowManual());
            helpMenu.DropDownItems.AddRange(new ToolStripItem[] { infoMenuItem, manualMenuItem });

            mainMenu.Items.AddRange(new ToolStripItem[] { fileMenu, databaseMenu, monitorMenu, settingsMenu, helpMenu });

            // ========== Statusleiste ==========
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel(Strings.Ready);
            statusStrip.Items.Add(statusLabel);

            // ========== Hauptpanel ==========
            rtbOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = true,
                BackColor = SystemColors.Window,
                Font = new Font("Consolas", 10), // Monospaced für bessere Ausrichtung
            };
            var container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 24, 4, 4) };
            container.Controls.Add(rtbOutput);
            // Form
            this.Controls.Add(mainMenu);
            this.Controls.Add(container);
            this.Controls.Add(statusStrip);
            this.MainMenuStrip = mainMenu;
            this.Text = AppPaths.AppName;
            this.Size = new System.Drawing.Size(600, 460);
            this.FormClosing += MainForm_FormClosing;
        }
        private void ShowInfoDialog()
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;

            // Build-Datum aus der Versionsnummer berechnen (nur wenn Version vorhanden)
            DateTime buildDate = DateTime.MinValue;
            if (version != null && version.Build > 0)
                buildDate = new DateTime(2000, 1, 1).AddDays(version.Build);

            string infoText = $"{AppPaths.AppName}\n";
            if (version != null) infoText += string.Format(Strings.InfoTextVersion, version.Major, version.Minor, version.Build);
            if (buildDate != DateTime.MinValue) infoText += string.Format(Strings.InfoTextBuildDate, buildDate);
            infoText += string.Format(Strings.InfoTextCopyrightPaths, AppPaths.CompanyName, AppPaths.DatabaseFilePath, AppPaths.IniFilePath);
            MessageBox.Show(infoText, Strings.Info, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void ShowManual()
        {
            string manualPath = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? Path.Combine(Application.StartupPath, "SectorHUD Manual DE.pdf") : Path.Combine(Application.StartupPath, "SectorHUD Manual.pdf");
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo(manualPath) { UseShellExecute = true };
                    process.Start();
                }
            }
            catch (Exception ex)    
            {
                MessageBox.Show(string.Format(Strings.ErrorWhileOpeningPdf, ex.Message), Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void InitializeTelemetryTimer()
        {
            _telemetryTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _telemetryTimer.Tick += TelemetryTimer_Tick;
        }

        private async void TelemetryTimer_Tick(object? sender, EventArgs e)
        {
            if (!_monitorActive) return;
            var telemetry = await TelemetryClient.GetTelemetryInfo();
            if (_lastTelemetry != null &&
                telemetry.Sector == _lastTelemetry.Sector && telemetry.Source == _lastTelemetry.Source && telemetry.Clock == _lastTelemetry.Clock &&
                telemetry.RemRel == _lastTelemetry.RemRel && telemetry.RemRT == _lastTelemetry.RemRT && telemetry.ETARel == _lastTelemetry.ETARel &&
                telemetry.ETART == _lastTelemetry.ETART && telemetry.Distance == _lastTelemetry.Distance) return;
            _lastTelemetry = telemetry;

            if (telemetry.Connected)
            {
                // Mods nachladen 
                _currentModTable = telemetry.Game == "ETS2" ? "ets_mods" : "ats_mods";
                _currentSectorTable = telemetry.Game == "ETS2" ? "ets_sectors" : "ats_sectors";
                telemetry.AllMods = DatabaseManager.GetModName(_currentModTable, _currentSectorTable, telemetry.X, telemetry.Y, false);
                telemetry.TopMod = DatabaseManager.GetModName(_currentModTable, _currentSectorTable, telemetry.X, telemetry.Y, true);

                // Overlay aktualisieren
                if (_overlay != null && ConfigManager.GetBool("InGame", "Enabled", true))
                {
                    string formatString = BuildOverlayFormatString(telemetry.JobActive);
                    string overlayText = Helpers.FormatTelemetryString(formatString, telemetry);
                    if (string.IsNullOrWhiteSpace(overlayText))
                        overlayText = "SectorHUD<color=00FF00> " + Strings.Ready + "</color>";
                    _overlay.UpdateText(overlayText);
                }
                statusLabel?.Text = Strings.ConnectedWithTelemetryServer;

            }
            else
            {
                _overlay?.UpdateText("SectorHUD<color=FFFF00> " + Strings.WaitingForGame + "</color>");
                statusLabel?.Text = Strings.NoConnectionIsTheGameRunning;
            }

            UpdateTelemetryDisplay(telemetry);
        }

        private void UpdateTelemetryDisplay(TelemetryData data)
        {
            if (InvokeRequired) { Invoke(new Action(() => UpdateTelemetryDisplay(data))); return; }

            rtbOutput?.Clear();
            if (!data.Connected)
            {
                rtbOutput?.AppendText(Strings.WaitingForGame);
                return;
            }

            // ========== Allgemeine Informationen ==========
            AppendFormattedLine(Strings.DisplayGeneralInformation, FontStyle.Bold, Color.DarkBlue);
            AppendFormattedLine(string.Format(Strings.DisplayGame, data.Game), FontStyle.Regular, Color.Black);
            AppendFormattedLine(string.Format(Strings.DisplaySector, data.Sector), FontStyle.Regular, Color.Black);
            AppendFormattedLine(string.Format(Strings.DisplayDistance, data.Distance), FontStyle.Regular, Color.Black);
            AppendFormattedLine(string.Format(Strings.DisplayClock, data.Clock), FontStyle.Regular, Color.Black);
            rtbOutput?.AppendText("\n");

            // ========== Job-Informationen (nur wenn aktiv) ==========
            if (data.JobActive)
            {
                AppendFormattedLine(Strings.DisplayJobInformation, FontStyle.Bold, Color.DarkBlue);
                AppendFormattedLine(string.Format(Strings.DisplaySource, data.Source), FontStyle.Regular, Color.Black);
                AppendFormattedLine(string.Format(Strings.DisplayDestination, data.Destination), FontStyle.Regular, Color.Black);
                AppendFormattedLine(string.Format(Strings.DisplayRemRel, data.RemRel), FontStyle.Regular, Color.Black);
                AppendFormattedLine(string.Format(Strings.DisplayRemRelRT, data.RemRelRT), FontStyle.Regular, Color.Black);
                AppendFormattedLine(string.Format(Strings.DisplayRemRT, data.RemRT), FontStyle.Regular, Color.Black);
                AppendFormattedLine(string.Format(Strings.DisplayETARel, data.ETARel), FontStyle.Regular, Color.Black);
                AppendFormattedLine(string.Format(Strings.DisplayETARelRT, data.ETARelRT), FontStyle.Regular, Color.Black);
                AppendFormattedLine(string.Format(Strings.DisplayETART, data.ETART), FontStyle.Bold, Color.Red);
                rtbOutput?.AppendText("\n");
            }
            else
            {
                AppendFormattedLine(Strings.NoActiveJob, FontStyle.Italic, Color.Gray);
                rtbOutput?.AppendText("\n");
            }

            // ========== Mods ==========
            AppendFormattedLine(Strings.DisplayActiveMods, FontStyle.Bold, Color.DarkBlue);
            if (!string.IsNullOrEmpty(data.AllMods))
            {
                var mods = data.AllMods.Split(new[] { " > " }, StringSplitOptions.None);
                for (int i = 0; i < mods.Length; i++)
                {
                    FontStyle style = (i == 0) ? FontStyle.Bold : FontStyle.Regular;
                    AppendFormattedLine($"{mods[i]}", style, (i == 0) ? Color.Red : Color.Black);
                }
            }
            else
                AppendFormattedLine("  " + Strings.NoModsFound, FontStyle.Italic, Color.Gray);
        }

        // Hilfsmethode zum Anhängen von formatiertem Text
        private void AppendFormattedLine(string text, FontStyle style, Color color)
        {
            if (rtbOutput == null) return;
            int start = rtbOutput.TextLength;
            rtbOutput.AppendText(text + "\n");
            rtbOutput.Select(start, text.Length);
            rtbOutput.SelectionFont = new Font(rtbOutput.Font, style);
            rtbOutput.SelectionColor = color;
            rtbOutput.Select(rtbOutput.TextLength, 0); // Cursor ans Ende
        }

        private string BuildOverlayFormatString(bool jobActive)
        {
            string format = "";
            if (ConfigManager.GetBool("InGame", "ShowSector", true))
                format += ConfigManager.GetValue("InGame", "FormatStringSector", "{12}<color=E8A52A>[MOD]</color>\\n");
            if (ConfigManager.GetBool("InGame", "ShowJob", true) && jobActive)
            {
                if (ConfigManager.GetBool("InGame", "ShowJobCity", true))
                    format += ConfigManager.GetValue("InGame", "FormatStringJobCity", "{12}From: <color=FF0000> [SOURCE]</color>\\n{12}To: <color=00FF00> [DESTINATION]</color>\\n");
                format += ConfigManager.GetValue("InGame", "FormatStringJobTime", "{12}Arrival in<color=FFFF00> [ETARELRT] min</color> at<color=FFFF00> [ETART]</color>\\n");
            }
            if (ConfigManager.GetBool("InGame", "ShowClock", true))
                format += ConfigManager.GetValue("InGame", "FormatStringClock", "{20}[CLOCK]");
            return format;
        }
        private void StartMonitor()
        {
            if (_monitorActive) return;
            _monitorActive = true;
            _currentModTable = "ets_mods";
            _currentSectorTable = "ets_sectors";

            // Telemetry-Server sicherstellen
            TelemetryClient.EnsureServerRunning();

            // Overlay starten
            if (ConfigManager.GetBool("InGame", "Enabled", true))
            {
                float posX = ConfigManager.GetFloat("InGame", "PositionX", 20);
                float posY = ConfigManager.GetFloat("InGame", "PositionY", 38);
                string fontName = ConfigManager.GetValue("InGame", "Font", "Arial");
                float fontSize = ConfigManager.GetFloat("InGame", "FontSize", 16);
                int displayIndex = ConfigManager.GetInt("InGame", "DisplayIndex", 0);
                _overlay = new OverlayRenderer(fontName, fontSize, posX, posY, displayIndex);
                _overlay.Start();
            }

            _telemetryTimer?.Start();
            UpdateMonitorMenuState();
            statusLabel?.Text = Strings.MonitorIsRunning;
        }
        private void StopMonitor()
        {
            if (!_monitorActive) return;
            _monitorActive = false;
            _telemetryTimer?.Stop();
            _overlay?.Stop();
            _overlay = null;
            UpdateMonitorMenuState();
            rtbOutput?.Clear();
            statusLabel?.Text = Strings.MonitorStopped;
        }
        private void StartMonitor_Click(object? sender, EventArgs e) => StartMonitor();
        private void StopMonitor_Click(object? sender, EventArgs e) => StopMonitor();
        private void DemoMonitor_Click(object? sender, EventArgs e)
        {
            if (_monitorActive) return;
            // Beispiel-Telemetrie-Daten
            var demoData = new TelemetryData
            {
                Connected = true,
                Game = "ETS2",
                Sector = "sec+0002-0001",
                Source = "Berlin (EuroGoodies)",
                Destination = "Zürich (LKW Log)",
                Distance = 480,
                Clock = DateTime.Now.ToString("t"),
                JobActive = true,
                RemRel = "23:07",
                RemRelRT = "1:13",
                RemRT = DateTime.Now.AddMinutes(73).ToString("t"),
                ETARel = "7:55",
                ETARelRT = "25",
                ETART = DateTime.Now.AddMinutes(25).ToString("t"),
                AllMods = "ProMods Europe > Base Game",
                TopMod = "ProMods Europe"
            };
            UpdateTelemetryDisplay(demoData);
            MessageBox.Show(Strings.DemoDataDisplayed, Strings.Demo, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateMonitorMenuState()
        {
            startMonitorMenuItem?.SetEnabled(!_monitorActive);
            stopMonitorMenuItem?.SetEnabled(_monitorActive);
        }

        private void QueryETS2_Click(object? sender, EventArgs e) => ShowQueryDialog("ets_mods", "ets_sectors", "ETS2");
        private void QueryATS_Click(object? sender, EventArgs e) => ShowQueryDialog("ats_mods", "ats_sectors", "ATS");
        private void ShowQueryDialog(string modTable, string sectorTable, string game)
        {
            // Einfaches Eingabeformular für Sektor/Koordinaten
            string input = Microsoft.VisualBasic.Interaction.InputBox(Strings.EnterSectorName, string.Format(Strings.QueryGame, game));
            if (string.IsNullOrWhiteSpace(input)) return;
            var result = DatabaseManager.QueryDatabaseSingle(modTable, sectorTable, input);
            MessageBox.Show(result, Strings.Result, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateETS2_Click(object? sender, EventArgs e) => RunDatabaseUpdate("ETS2", "ets_mods", "ets_sectors", false);
        private void UpdateATS_Click(object? sender, EventArgs e) => RunDatabaseUpdate("ATS", "ats_mods", "ats_sectors", false);
        private void RecreateETS2_Click(object? sender, EventArgs e) => RunDatabaseUpdate("ETS2", "ets_mods", "ets_sectors", true);
        private void RecreateATS_Click(object? sender, EventArgs e) => RunDatabaseUpdate("ATS", "ats_mods", "ats_sectors", true);

        private async void RunDatabaseUpdate(string game, string modTable, string sectorTable, bool recreate)
        {
            StopMonitor();
            startMonitorMenuItem?.SetEnabled(false);
            bool success = await Task.Run(() => DatabaseManager.UpdateDatabase(game, modTable, sectorTable, recreate));
            startMonitorMenuItem?.SetEnabled(true);
            if (success) MessageBox.Show(string.Format(Strings.GameDatabaseHasBeenUpdated, game), Strings.Done, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditConfig_Click(object? sender, EventArgs e)
        {
            string iniPath = Path.Combine(AppPaths.AppDataRoamingPath, "SectorHUD.ini");
            using (var editor = new ConfigEditorForm())
            {
                if (editor.ShowDialog() == DialogResult.OK)
                {
                    // Konfiguration neu laden
                    ConfigManager.Reload();
                    MessageBox.Show(Strings.ConfigurationHasBeenUpdated, Strings.Info);
                }
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            StopMonitor_Click(sender, e);
        }
    }

    // Erweiterungsmethoden, damit Aufrufe konsistent null-sicher sind
    internal static class ToolStripExtensions
    {
        public static void SetEnabled(this ToolStripItem? item, bool enabled)
        {
            if (item is null) return;
            item.Enabled = enabled;
        }
    }
}