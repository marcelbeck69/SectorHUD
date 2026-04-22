using System.Globalization;
using System.Text;

namespace SectorHUD
{
    public static class ConfigManager
    {
        private static Dictionary<string, Dictionary<string, string>> _config = new();

        public static void LoadOrCreateConfig()
        {
            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SectorHUD.ini");
            if (!File.Exists(iniPath))
            {
                CreateDefaultIni(iniPath);
                Helpers.ShowWarning("New configuration file created. Please adjust paths and restart.");
                Console.ReadKey();
                Environment.Exit(0);
            }
            _config = ParseIniFile(iniPath);
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
            sb.AppendLine("; SectorHUD configuration file");
            sb.AppendLine("; Edit the paths according to your system");
            sb.AppendLine();
            sb.AppendLine("[General]");
            sb.AppendLine("DatabasePath = .");
            sb.AppendLine("TempPath = %TEMP%");
            sb.AppendLine();
            sb.AppendLine("[Tools]");
            sb.AppendLine("TelemetryURL = http://localhost:25555/api/ets2/telemetry");
            sb.AppendLine("TelemetryPath = .\\ets2-telemetry-server-master\\server\\Ets2Telemetry.exe");
            sb.AppendLine("TelemetryDebug = 0");
            sb.AppendLine("SKZKPath = .\\extractor.exe");
            sb.AppendLine();
            sb.AppendLine("[ETS2]");
            sb.AppendLine("GamePath = C:\\Program Files (x86)\\Steam\\steamapps\\common\\Euro Truck Simulator 2");
            sb.AppendLine("GameDocPath = %USERPROFILE%\\Documents\\Euro Truck Simulator 2");
            sb.AppendLine("Map = europe");
            sb.AppendLine();
            sb.AppendLine("[ATS]");
            sb.AppendLine("GamePath = C:\\Program Files (x86)\\Steam\\steamapps\\common\\American Truck Simulator");
            sb.AppendLine("GameDocPath = %USERPROFILE%\\Documents\\American Truck Simulator");
            sb.AppendLine("Map = usa");
            sb.AppendLine();
            sb.AppendLine("[Console]");
            sb.AppendLine("Enabled = 1");
            sb.AppendLine("ClearScreen = 0");
            sb.AppendLine("FormatString = [TIMESTAMP] [SECTOR] [MODS]");
            sb.AppendLine();
            sb.AppendLine("[InGame]");
            sb.AppendLine("Enabled = 1");
            sb.AppendLine("Font = Arial");
            sb.AppendLine("FontSize = 16");
            sb.AppendLine("PositionX = 20");
            sb.AppendLine("PositionY = 38");
            sb.AppendLine("ShowSector = 1");
            sb.AppendLine("ShowJob = 1");
            sb.AppendLine("ShowClock = 1");
            sb.AppendLine("FormatStringSector = {12}<color=E8A52A>[MOD]</color>\\n");
            sb.AppendLine("FormatStringJobCity = {12}From: <color=FF0000> [SOURCE]</color>\\n{12}To: <color=00FF00> [DESTINATION]</color>\\n");
            sb.AppendLine("FormatStringJobTime = {12}Arrival in<color=FFFF00> [ETARELRT] min</color> at<color=FFFF00> [ETART]</color>\\n");
            sb.AppendLine("FormatStringClock = {20}[CLOCK]");
            sb.AppendLine("; Colors: <color=RRGGBB>Text</color>, {size} at the beginning of the line");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
        public static string GetConfigValue(string section, string key, string defaultValue = "")
        {
            if (_config.TryGetValue(section, out var sectionDict) && sectionDict.TryGetValue(key, out var value))
                return value;
            return defaultValue;
        }
        public static float GetConfigFloat(string section, string key, float defaultValue)
        {
            string val = GetConfigValue(section, key, "");
            if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;
            return defaultValue;
        }
        public static bool GetConfigBool(string section, string key, bool defaultValue)
        {
            string val = GetConfigValue(section, key, defaultValue ? "1" : "0");
            return val == "1";
        }
    }
}