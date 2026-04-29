using System.Globalization;
using System.Text;
using SectorHUDgui.Properties;

namespace SectorHUDgui
{
    public static class ConfigManager
    {
        private static Dictionary<string, Dictionary<string, string>> _config = new();

        public static void LoadOrCreateConfig()
        {
            if (!File.Exists(AppPaths.IniFilePath))
            {
                Directory.CreateDirectory(AppPaths.AppDataRoamingPath);
                CreateDefaultIni(AppPaths.IniFilePath);
                Helpers.UpdateStatus(Strings.NewConfigurationFileCreated);
            }
            _config = ParseIniFile(AppPaths.IniFilePath);
        }
        private static Dictionary<string, Dictionary<string, string>> ParseIniFile(string path)
        {
            var ini = new Dictionary<string, Dictionary<string, string>>();
            string currentSection = "NO_SECTION";
            ini[currentSection] = new Dictionary<string, string>();

            foreach (string line in File.ReadLines(path))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    if (!ini.ContainsKey(currentSection))
                        ini[currentSection] = new Dictionary<string, string>();
                }
                else if (trimmed.Contains('='))
                {
                    int eqPos = trimmed.IndexOf('=');
                    string key = trimmed.Substring(0, eqPos).Trim();
                    string value = trimmed.Substring(eqPos + 1).Trim();

                    // Umgebungsvariablen expandieren
                    value = Environment.ExpandEnvironmentVariables(value).Replace("\"", "");

                    // Pfad-Fallback bei Pipe
                    if (!string.IsNullOrEmpty(value) && value.Contains('|'))
                    {
                        string[] paths = value.Split('|');
                        string existingPath = paths.FirstOrDefault(p => File.Exists(p.Trim()) || Directory.Exists(p.Trim())) ?? "";
                        value = existingPath != "" ? existingPath.Trim() : paths[0].Trim();
                    }

                    ini[currentSection][key] = value;
                }
            }
            return ini;
        }
        private static void CreateDefaultIni(string path)
        {
            var sb = new StringBuilder();
            string gameFolder, userFolder, serverPath, extractorPath;
            sb.AppendLine("; SectorHUD configuration file");
            sb.AppendLine("; Edit the paths according to your system");
            sb.AppendLine();
            sb.AppendLine("[General]");
            sb.AppendLine("TempPath = %TEMP%");
            sb.AppendLine("Autostart = False");
            sb.AppendLine();
            serverPath = AppPaths.GetTelemetryServerPath();
            extractorPath = AppPaths.GetExtractorPath();
            sb.AppendLine("[Tools]");
            sb.AppendLine("TelemetryURL = http://localhost:25555/api/ets2/telemetry");
            sb.AppendLine($"TelemetryPath = {serverPath}");
            sb.AppendLine("TelemetryDebug = False");
            sb.AppendLine($"SKZKPath = {extractorPath}");
            sb.AppendLine();
            gameFolder = AppPaths.GetGameInstallFolder(AppPaths.GameType.EuroTruckSimulator2);
            userFolder = AppPaths.GetUserDataFolder(AppPaths.GameType.EuroTruckSimulator2);
            sb.AppendLine("[ETS2]");
            sb.AppendLine($"GamePath = {gameFolder}");
            sb.AppendLine($"GameDocPath = {userFolder}");
            sb.AppendLine("Map = europe");
            sb.AppendLine();
            gameFolder = AppPaths.GetGameInstallFolder(AppPaths.GameType.AmericanTruckSimulator);
            userFolder = AppPaths.GetUserDataFolder(AppPaths.GameType.AmericanTruckSimulator);      
            sb.AppendLine("[ATS]");
            sb.AppendLine($"GamePath = {gameFolder}");
            sb.AppendLine($"GameDocPath = {userFolder}");
            sb.AppendLine("Map = usa");
            sb.AppendLine();
            sb.AppendLine("[InGame]");
            sb.AppendLine("Enabled = True");
            sb.AppendLine("Font = Arial");
            sb.AppendLine("FontSize = 16");
            sb.AppendLine("Transparency = 75");
            sb.AppendLine("PositionX = 20");
            sb.AppendLine("PositionY = 38");
            sb.AppendLine("ShowSector = True");
            sb.AppendLine("ShowJob = True");
            sb.AppendLine("ShowClock = True");
            sb.AppendLine("FormatStringSector = {12}<color=E8A52A>[MOD]</color>\\n");
            sb.AppendLine("FormatStringJobCity = {12}From: <color=FF0000> [SOURCE]</color>\\n{12}To: <color=00FF00> [DESTINATION]</color>\\n");
            sb.AppendLine("FormatStringJobTime = {12}Arrival in<color=FFFF00> [ETARELRT] min</color> at<color=FFFF00> [ETART]</color>\\n");
            sb.AppendLine("FormatStringClock = {20}[CLOCK]");
            sb.AppendLine("DisplayIndex = 0");
            sb.AppendLine("; Colors: <color=RRGGBB>Text</color>, {size} at the beginning of the line");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        public static void ResetToDefaults()
        {
            CreateDefaultIni(AppPaths.IniFilePath);
            _config = ParseIniFile(AppPaths.IniFilePath);
        }
        public static string GetValue(string section, string key, string defaultValue = "")
        {
            if (_config.TryGetValue(section, out var sectionDict) && sectionDict.TryGetValue(key, out var value))
                return value;
            return defaultValue;
        }
        public static float GetFloat(string section, string key, float defaultValue)
        {
            string val = GetValue(section, key, "");
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;
            return defaultValue;
        }
        public static bool GetBool(string section, string key, bool defaultValue)
        {
            string val = GetValue(section, key, defaultValue ? "True" : "False");
            return val == "True";
        }
        public static int GetInt(string section, string key, int defaultValue)
        {
            string val = GetValue(section, key, defaultValue.ToString(CultureInfo.InvariantCulture));
            if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
                return result;
            return defaultValue;
        }
        public static void SetValue(string section, string key, string value)
        {
            if (!_config.ContainsKey(section))
                _config[section] = new Dictionary<string, string>();
            _config[section][key] = value;
        }
        public static void Save()
        {
            using var writer = new StreamWriter(AppPaths.IniFilePath, false, Encoding.UTF8);
            foreach (var section in _config)
            {
                writer.WriteLine($"[{section.Key}]");
                foreach (var kv in section.Value)
                    writer.WriteLine($"{kv.Key}={kv.Value}");
                writer.WriteLine();
            }
        }
        public static void Reload()
        {
            _config = ParseIniFile(AppPaths.IniFilePath);
        }

    }
}