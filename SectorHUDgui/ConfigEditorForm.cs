using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;

namespace SectorHUDgui
{
    public partial class ConfigEditorForm : Form
    {
        private PropertyGrid propertyGrid = null!;
        private Button btnSave = null!, btnCancel = null!;
        private ConfigData _configData = null!;

        public ConfigEditorForm()
        {
            InitializeComponent();
            LoadConfig();
        }
        private void InitializeComponent()
        {
            propertyGrid = new PropertyGrid { Dock = DockStyle.Fill };

            // Panel für die Button-Leiste
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(5)
            };

            // FlowLayoutPanel für dynamische Anordnung
            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = false
            };

            btnSave = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Width = 100,
                Margin = new Padding(5, 0, 0, 0)
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Width = 100,
                Margin = new Padding(5, 0, 0, 0)
            };

            flowPanel.Controls.Add(btnCancel);
            flowPanel.Controls.Add(btnSave);

            buttonPanel.Controls.Add(flowPanel);

            this.Controls.Add(propertyGrid);
            this.Controls.Add(buttonPanel);

            this.Size = new System.Drawing.Size(600, 500);
            this.Text = "Edit Configuration";
        }

        private void LoadConfig()
        {
            _configData = new ConfigData();
            // Werte aus ConfigManager in _configData übernehmen
            _configData.General_TempPath = ConfigManager.GetValue("General", "TempPath");
            _configData.General_Autostart = ConfigManager.GetBool("General", "Autostart", false);
            _configData.Tools_TelemetryURL = ConfigManager.GetValue("Tools", "TelemetryURL");
            _configData.Tools_TelemetryPath = ConfigManager.GetValue("Tools", "TelemetryPath");
            _configData.Tools_TelemetryDebug = ConfigManager.GetBool("Tools", "TelemetryDebug", false);
            _configData.Tools_SKZKPath = ConfigManager.GetValue("Tools", "SKZKPath");
            _configData.ETS2_GamePath = ConfigManager.GetValue("ETS2", "GamePath");
            _configData.ETS2_GameDocPath = ConfigManager.GetValue("ETS2", "GameDocPath");
            _configData.ETS2_Map = ConfigManager.GetValue("ETS2", "Map");
            _configData.ATS_GamePath = ConfigManager.GetValue("ATS", "GamePath");
            _configData.ATS_GameDocPath = ConfigManager.GetValue("ATS", "GameDocPath");
            _configData.ATS_Map = ConfigManager.GetValue("ATS", "Map");
            _configData.InGame_Enabled = ConfigManager.GetBool("InGame", "Enabled", true);
            _configData.InGame_Font = ConfigManager.GetValue("InGame", "Font");
            _configData.InGame_FontSize = ConfigManager.GetFloat("InGame", "FontSize", 16);
            _configData.InGame_PositionX = ConfigManager.GetFloat("InGame", "PositionX", 20);
            _configData.InGame_PositionY = ConfigManager.GetFloat("InGame", "PositionY", 38);
            _configData.InGame_ShowSector = ConfigManager.GetBool("InGame", "ShowSector", true);
            _configData.InGame_ShowJob = ConfigManager.GetBool("InGame", "ShowJob", true);
            _configData.InGame_ShowClock = ConfigManager.GetBool("InGame", "ShowClock", true);
            _configData.InGame_FormatStringSector = ConfigManager.GetValue("InGame", "FormatStringSector");
            _configData.InGame_FormatStringJobCity = ConfigManager.GetValue("InGame", "FormatStringJobCity");
            _configData.InGame_FormatStringJobTime = ConfigManager.GetValue("InGame", "FormatStringJobTime");
            _configData.InGame_FormatStringClock = ConfigManager.GetValue("InGame", "FormatStringClock");
            _configData.InGame_DisplayIndex = ConfigManager.GetInt("InGame", "DisplayIndex", 0);
            propertyGrid.SelectedObject = _configData;
        }

        private void SaveConfig()
        {
            // _configData in INI schreiben (ConfigManager.UpdateValue)
            // Nullwerte werden hier zu leeren Strings konvertiert, damit SetValue kein null erhält.
            ConfigManager.SetValue("General", "TempPath", _configData.General_TempPath ?? string.Empty);
            ConfigManager.SetValue("General", "Autostart", _configData.General_Autostart.ToString());

            ConfigManager.SetValue("Tools", "TelemetryURL", _configData.Tools_TelemetryURL ?? string.Empty);
            ConfigManager.SetValue("Tools", "TelemetryPath", _configData.Tools_TelemetryPath ?? string.Empty);
            ConfigManager.SetValue("Tools", "TelemetryDebug", _configData.Tools_TelemetryDebug.ToString());
            ConfigManager.SetValue("Tools", "SKZKPath", _configData.Tools_SKZKPath ?? string.Empty);

            ConfigManager.SetValue("ETS2", "GamePath", _configData.ETS2_GamePath ?? string.Empty);
            ConfigManager.SetValue("ETS2", "GameDocPath", _configData.ETS2_GameDocPath ?? string.Empty);
            ConfigManager.SetValue("ETS2", "Map", _configData.ETS2_Map ?? string.Empty);

            ConfigManager.SetValue("ATS", "GamePath", _configData.ATS_GamePath ?? string.Empty);
            ConfigManager.SetValue("ATS", "GameDocPath", _configData.ATS_GameDocPath ?? string.Empty);
            ConfigManager.SetValue("ATS", "Map", _configData.ATS_Map ?? string.Empty);

            ConfigManager.SetValue("InGame", "Enabled", _configData.InGame_Enabled.ToString());
            ConfigManager.SetValue("InGame", "Font", _configData.InGame_Font ?? string.Empty);
            ConfigManager.SetValue("InGame", "FontSize", _configData.InGame_FontSize.ToString(CultureInfo.InvariantCulture));
            ConfigManager.SetValue("InGame", "PositionX", _configData.InGame_PositionX.ToString(CultureInfo.InvariantCulture));
            ConfigManager.SetValue("InGame", "PositionY", _configData.InGame_PositionY.ToString(CultureInfo.InvariantCulture));
            ConfigManager.SetValue("InGame", "ShowSector", _configData.InGame_ShowSector.ToString());
            ConfigManager.SetValue("InGame", "ShowJob", _configData.InGame_ShowJob.ToString());
            ConfigManager.SetValue("InGame", "ShowClock", _configData.InGame_ShowClock.ToString());
            ConfigManager.SetValue("InGame", "FormatStringSector", _configData.InGame_FormatStringSector ?? string.Empty);
            ConfigManager.SetValue("InGame", "FormatStringJobCity", _configData.InGame_FormatStringJobCity ?? string.Empty);
            ConfigManager.SetValue("InGame", "FormatStringJobTime", _configData.InGame_FormatStringJobTime ?? string.Empty);
            ConfigManager.SetValue("InGame", "FormatStringClock", _configData.InGame_FormatStringClock ?? string.Empty);
            ConfigManager.SetValue("InGame", "DisplayIndex", _configData.InGame_DisplayIndex.ToString(CultureInfo.InvariantCulture));

            ConfigManager.Save();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
                SaveConfig();
            base.OnFormClosing(e);
        }

        // Hilfsklasse für PropertyGrid
        public class ConfigData
        {
            [Category("General")] public string? General_TempPath { get; set; }
            [Category("General")] public bool General_Autostart { get; set; }
            [Category("Tools")] public string? Tools_TelemetryURL { get; set; }
            [Category("Tools")][Editor(typeof(FileNameEditor), typeof(UITypeEditor))] public string? Tools_TelemetryPath { get; set; }
            [Category("Tools")] public bool Tools_TelemetryDebug { get; set; }
            [Category("Tools")][Editor(typeof(FileNameEditor), typeof(UITypeEditor))] public string? Tools_SKZKPath { get; set; }
            [Category("ETS2")][Editor(typeof(FolderNameEditor), typeof(UITypeEditor))] public string? ETS2_GamePath { get; set; }
            [Category("ETS2")][Editor(typeof(FolderNameEditor), typeof(UITypeEditor))] public string? ETS2_GameDocPath { get; set; }
            [Category("ETS2")] public string? ETS2_Map { get; set; }
            [Category("ATS")][Editor(typeof(FolderNameEditor), typeof(UITypeEditor))] public string? ATS_GamePath { get; set; }
            [Category("ATS")][Editor(typeof(FolderNameEditor), typeof(UITypeEditor))] public string? ATS_GameDocPath { get; set; }
            [Category("ATS")] public string? ATS_Map { get; set; }
            [Category("InGame")] public bool InGame_Enabled { get; set; }
            [Category("InGame")] public string? InGame_Font { get; set; }
            [Category("InGame")] public float InGame_FontSize { get; set; }
            [Category("InGame")] public float InGame_PositionX { get; set; }
            [Category("InGame")] public float InGame_PositionY { get; set; }
            [Category("InGame")] public bool InGame_ShowSector { get; set; }
            [Category("InGame")] public bool InGame_ShowJob { get; set; }
            [Category("InGame")] public bool InGame_ShowClock { get; set; }
            [Category("InGame")] public string? InGame_FormatStringSector { get; set; }
            [Category("InGame")] public string? InGame_FormatStringJobCity { get; set; }
            [Category("InGame")] public string? InGame_FormatStringJobTime { get; set; }
            [Category("InGame")] public string? InGame_FormatStringClock { get; set; }
            [Category("InGame")] public int InGame_DisplayIndex { get; set; }
        }
    }

    public class FileNameEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
            => UITypeEditorEditStyle.Modal;
        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider? provider, object? value)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select file";
                dialog.FileName = value as string;
                if (dialog.ShowDialog() == DialogResult.OK)
                    return dialog.FileName;
            }
            return value;
        }
    }
    public class FolderNameEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
            => UITypeEditorEditStyle.Modal;
        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider? provider, object? value)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = value as string;
                if (dialog.ShowDialog() == DialogResult.OK)
                    return dialog.SelectedPath;
            }
            return value;
        }
    }
}