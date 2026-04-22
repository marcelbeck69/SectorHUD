using System.Reflection;

namespace SectorHUDgui
{
    public partial class MainForm : Form
    {
        private System.Windows.Forms.Timer? _telemetryTimer;
        private TelemetryData? _currentTelemetry;
        private OverlayRenderer? _overlay;
        private bool _monitorActive;
        private string? _currentModTable, _currentSectorTable;

        // UI-Elemente (im Designer erstellt oder manuell)
        private MenuStrip? mainMenu;
        private ToolStripMenuItem? fileMenu, databaseMenu, monitorMenu, settingsMenu, helpMenu;
        private ToolStripMenuItem? exitMenuItem;
        private ToolStripMenuItem? queryETS2MenuItem, queryATSMenuItem;
        private ToolStripMenuItem? updateETS2MenuItem, updateATSMenuItem, recreateETS2MenuItem, recreateATSMenuItem;
        private ToolStripMenuItem? startMonitorMenuItem, stopMonitorMenuItem;
        private ToolStripMenuItem? configMenuItem;
        private StatusStrip? statusStrip;
        public static ToolStripStatusLabel? statusLabel;
        private RichTextBox? rtbOutput;

        public MainForm()
        {
            InitializeComponent();
            InitializeTelemetryTimer();
            _monitorActive = false;
            UpdateMonitorMenuState();
        }

        private void InitializeComponent()
        {
            // ========== Menüleiste ==========
            mainMenu = new MenuStrip();
            mainMenu.Dock = DockStyle.Top;
            fileMenu = new ToolStripMenuItem("File");
            exitMenuItem = new ToolStripMenuItem("Exit", null, (s, e) => Close());
            fileMenu.DropDownItems.Add(exitMenuItem);

            databaseMenu = new ToolStripMenuItem("Database");
            queryETS2MenuItem = new ToolStripMenuItem("Query ETS2", null, QueryETS2_Click);
            queryATSMenuItem = new ToolStripMenuItem("Query ATS", null, QueryATS_Click);
            updateETS2MenuItem = new ToolStripMenuItem("Update ETS2", null, UpdateETS2_Click);
            updateATSMenuItem = new ToolStripMenuItem("Update ATS", null, UpdateATS_Click);
            recreateETS2MenuItem = new ToolStripMenuItem("Recreate ETS2", null, RecreateETS2_Click);
            recreateATSMenuItem = new ToolStripMenuItem("Recreate ATS", null, RecreateATS_Click);
            databaseMenu.DropDownItems.AddRange(new ToolStripItem[] {
                queryETS2MenuItem, queryATSMenuItem,
                new ToolStripSeparator(), updateETS2MenuItem, updateATSMenuItem,
                new ToolStripSeparator(), recreateETS2MenuItem, recreateATSMenuItem });

            monitorMenu = new ToolStripMenuItem("Monitor");
            startMonitorMenuItem = new ToolStripMenuItem("Start", null, StartMonitor_Click);
            stopMonitorMenuItem = new ToolStripMenuItem("Stop", null, StopMonitor_Click) { Enabled = false };
            monitorMenu.DropDownItems.AddRange(new ToolStripItem[] { startMonitorMenuItem, stopMonitorMenuItem });

            settingsMenu = new ToolStripMenuItem("Settings");
            configMenuItem = new ToolStripMenuItem("Edit Configuration", null, EditConfig_Click);
            settingsMenu.DropDownItems.Add(configMenuItem);

            helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDownItems.Add("Info", null, (s, e) => ShowInfoDialog());

            mainMenu.Items.AddRange(new ToolStripItem[] { fileMenu, databaseMenu, monitorMenu, settingsMenu, helpMenu });

            // ========== Statusleiste ==========
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("Ready");
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
            this.Size = new System.Drawing.Size(600, 400);
            this.FormClosing += MainForm_FormClosing;
        }

        private void ShowInfoDialog()
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            string infoText = $"{AppPaths.AppName}\n";
            if (version != null) { infoText += $"Version: {version.Major}.{version.Minor}.{version.Build}\n"; }
            infoText += $"Copyright by {AppPaths.CompanyName}\n\nDatabase: {AppPaths.DatabaseFilePath}\nSettings: {AppPaths.IniFilePath}";
            MessageBox.Show(infoText, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            _currentTelemetry = telemetry;

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
                        overlayText = "SectorHUD<color=00FF00> ready</color>";
                    _overlay.UpdateText(overlayText);
                }
                statusLabel?.Text = "Connected with Telemetry server";

            }
            else
            {
                _overlay?.UpdateText("SectorHUD<color=FFFF00> waiting for game...</color>");
                statusLabel?.Text = "No connection - Is the game running?";
            }

            UpdateTelemetryDisplay(telemetry);
        }

        private void UpdateTelemetryDisplay(TelemetryData data)
        {
            if (InvokeRequired) { Invoke(new Action(() => UpdateTelemetryDisplay(data))); return; }

            rtbOutput?.Clear();
            if (!data.Connected)
            {
                rtbOutput?.AppendText("Waiting for game...\n");
                return;
            }

            // ========== Allgemeine Informationen ==========
            AppendFormattedLine("General Information", FontStyle.Bold, Color.DarkBlue);
            AppendFormattedLine($"Game:              {data.Game}", FontStyle.Regular, Color.Black);
            AppendFormattedLine($"Sector:            {data.Sector}", FontStyle.Regular, Color.Black);
            AppendFormattedLine($"Distance:          {data.Distance} km", FontStyle.Regular, Color.Black);
            AppendFormattedLine($"Time (real):       {data.Clock}", FontStyle.Regular, Color.Black);
            rtbOutput?.AppendText("\n");

            // ========== Job-Informationen (nur wenn aktiv) ==========
            if (data.JobActive)
            {
                AppendFormattedLine("Job Information", FontStyle.Bold, Color.DarkBlue);
                AppendFormattedLine($"From:              {data.Source}", FontStyle.Regular, Color.Black);
                AppendFormattedLine($"To:                {data.Destination}", FontStyle.Regular, Color.Black);
                AppendFormattedLine($"Remaining (game):  {data.RemRel}", FontStyle.Regular, Color.Black);
                AppendFormattedLine($"Remaining (real):  {data.RemRelRT}", FontStyle.Regular, Color.Black);
                AppendFormattedLine($"ETA (real):        {data.ETART}", FontStyle.Bold, Color.Red);
                rtbOutput?.AppendText("\n");
            }
            else
            {
                AppendFormattedLine("No active job", FontStyle.Italic, Color.Gray);
                rtbOutput?.AppendText("\n");
            }

            // ========== Mods ==========
            AppendFormattedLine("Active Mods", FontStyle.Bold, Color.DarkBlue);
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
                AppendFormattedLine("  No mods found", FontStyle.Italic, Color.Gray);
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

        private void StartMonitor_Click(object? sender, EventArgs e)
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
            statusLabel?.Text = "Monitor is running";
        }

        private void StopMonitor_Click(object? sender, EventArgs e)
        {
            _monitorActive = false;
            _telemetryTimer?.Stop();
            _overlay?.Stop();
            _overlay = null;
            UpdateMonitorMenuState();
            statusLabel?.Text = "Monitor stopped";
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
            string input = Microsoft.VisualBasic.Interaction.InputBox("Enter sector name (e.g., sec+0004-0002)\nor coordinates (e.g., 8520,-12650):", $"Query {game}");
            if (string.IsNullOrWhiteSpace(input)) return;
            var result = DatabaseManager.QueryDatabaseSingle(modTable, sectorTable, input);
            MessageBox.Show(result, "Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateETS2_Click(object? sender, EventArgs e) => RunDatabaseUpdate("ETS2", "ets_mods", "ets_sectors", false);
        private void UpdateATS_Click(object? sender, EventArgs e) => RunDatabaseUpdate("ATS", "ats_mods", "ats_sectors", false);
        private void RecreateETS2_Click(object? sender, EventArgs e) => RunDatabaseUpdate("ETS2", "ets_mods", "ets_sectors", true);
        private void RecreateATS_Click(object? sender, EventArgs e) => RunDatabaseUpdate("ATS", "ats_mods", "ats_sectors", true);

        private async void RunDatabaseUpdate(string game, string modTable, string sectorTable, bool recreate)
        {
            startMonitorMenuItem?.SetEnabled(false);
            bool success = await Task.Run(() => DatabaseManager.UpdateDatabase(game, modTable, sectorTable, recreate));
            startMonitorMenuItem?.SetEnabled(true);
            if (success) MessageBox.Show($"{game} database has been updated.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    MessageBox.Show("Configuration has been updated. Start the monitor again if necessary.", "Notice");
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