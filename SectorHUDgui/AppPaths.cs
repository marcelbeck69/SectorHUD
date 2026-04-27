using Microsoft.Win32;
using System.Diagnostics;
using static SectorHUDgui.AppPaths;

namespace SectorHUDgui
{
    public static class AppPaths
    {
        public enum GameType
        {
            EuroTruckSimulator2,
            AmericanTruckSimulator
        }
        public static string CompanyName { get; set; } = "MarsTheChemist";
        public static string AppName { get; set; } = "SectorHUD";

        // 1. Pfad für die INI-Datei (Roaming)
        public static string AppDataRoamingPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), CompanyName, AppName);
        public static string IniFilePath =>
            Path.Combine(AppDataRoamingPath, "SectorHUD.ini");

        // 2. Pfad für die SQLite-Datenbank (Lokal)
        public static string AppDataLocalPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CompanyName, AppName);
        public static string DatabaseFilePath =>
            Path.Combine(AppDataLocalPath, "SectorHUD.db");

        public static string GetUserDataFolder(GameType game)
        {
            string gameFolderName = game == GameType.EuroTruckSimulator2 ? "Euro Truck Simulator 2" : "American Truck Simulator";
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string standardPath = Path.Combine(documents, gameFolderName);
            if (!Directory.Exists(standardPath))
            {
                string? registryDocuments = GetDocumentsFolderFromRegistry();
                if (registryDocuments != null)
                {
                    string registryPath = Path.Combine(registryDocuments, gameFolderName);
                    if (Directory.Exists(registryPath)) return registryPath;
                }
            }
            return standardPath;
        }
        private static string? GetDocumentsFolderFromRegistry()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"))
                {
                    string? personal = key?.GetValue("Personal")?.ToString();
                    if (!string.IsNullOrEmpty(personal) && Directory.Exists(personal)) return personal;
                }
            }
            catch { }
            return null;
        }
        public static string GetGameInstallFolder(GameType game)
        {
            string? installFolder = TryGetSteamGameFolder(game);
            if (installFolder != null) return installFolder;
            installFolder = TryGetNonSteamGameFolder(game);
            if (installFolder != null) return installFolder;
            // Fallback: typischer Pfad für DVD/Standalone-Version
            string defaultPath = game == GameType.EuroTruckSimulator2 ? @"C:\Program Files (x86)\Euro Truck Simulator 2" : @"C:\Program Files (x86)\American Truck Simulator";
            return defaultPath;
        }
        private static string? TryGetSteamGameFolder(GameType game)
        {
            string? steamInstallPath = GetSteamInstallPath();
            if (steamInstallPath == null) return null;
            List<string> libraryPaths = GetSteamLibraryPaths(steamInstallPath);
            string gameSubPath = game == GameType.EuroTruckSimulator2 ? @"steamapps\common\Euro Truck Simulator 2" : @"steamapps\common\American Truck Simulator";
            foreach (string lib in libraryPaths)
            {
                string candidate = Path.Combine(lib, gameSubPath);
                if (Directory.Exists(candidate)) return candidate;
            }
            return null;
        }
        private static string? GetSteamInstallPath()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    string? path = key?.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        path = path.Replace('/', '\\');
                        if (Directory.Exists(path)) return path;
                    }
                }
            }
            catch { }
            return null;
        }
        private static List<string> GetSteamLibraryPaths(string steamInstallPath)
        {
            var libraries = new List<string>();
            libraries.Add(steamInstallPath);
            string libraryFoldersVdf = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(libraryFoldersVdf))
            {
                try
                {
                    string content = File.ReadAllText(libraryFoldersVdf);
                    var matches = System.Text.RegularExpressions.Regex.Matches(content,
                        @"""path""\s+""([^""]+)""");
                    foreach (System.Text.RegularExpressions.Match match in matches)
                    {
                        if (match.Success && match.Groups.Count > 1)
                        {
                            string libPath = match.Groups[1].Value.Replace("\\\\", "\\");
                            if (Directory.Exists(libPath) && !libraries.Contains(libPath)) libraries.Add(libPath);
                        }
                    }
                }
                catch { }
            }
            return libraries;
        }
        private static string? TryGetNonSteamGameFolder(GameType game)
        {

            List<string> commonPaths = new List<string>
            {
                game == GameType.EuroTruckSimulator2 ? @"C:\Program Files\Euro Truck Simulator 2" : @"C:\Program Files\American Truck Simulator",
                game == GameType.EuroTruckSimulator2 ? @"C:\Program Files (x86)\Euro Truck Simulator 2" : @"C:\Program Files (x86)\American Truck Simulator",
                @"C:\Program Files (x86)\Steam\steamapps\common\" + (game == GameType.EuroTruckSimulator2 ? "Euro Truck Simulator 2" : "American Truck Simulator")
            };
            foreach (string path in commonPaths)
            {
                if (Directory.Exists(path) && Directory.GetFiles(path, "*.scs").Length > 0) return path;
            }
            return null;
        }
        
        public static string GetTelemetryServerPath()
        {
            string? pathFromRunningProcess = GetPathFromRunningProcess();
            if (!string.IsNullOrEmpty(pathFromRunningProcess) && File.Exists(pathFromRunningProcess)) return pathFromRunningProcess;
            string? foundPath = SearchInCommonDirectories();
            if (!string.IsNullOrEmpty(foundPath) && File.Exists(foundPath)) return foundPath;
            return ".\\ets2-telemetry-server-master\\server\\Ets2Telemetry.exe";
        }
        private static string? GetPathFromRunningProcess()
        {
            try
            {
                var p = Process.GetProcessesByName("Ets2Telemetry").FirstOrDefault();
                return p?.MainModule?.FileName;
            }
            catch (System.ComponentModel.Win32Exception) { return null; }
            catch (System.UnauthorizedAccessException) { return null; }
        }
        private static string? SearchInCommonDirectories()
        {
            List<string> searchDirectories = new List<string>();
            searchDirectories.Add(Directory.GetCurrentDirectory());
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            searchDirectories.Add(programFilesX86);
            searchDirectories.Add(Path.Combine(programFilesX86, "Ets2TelemetryServer"));
            searchDirectories.Add(Path.Combine(programFilesX86, "Funbits Telemetry Server"));
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            searchDirectories.Add(programFiles);
            searchDirectories.Add(Path.Combine(programFiles, "Ets2TelemetryServer"));
            searchDirectories.Add(Path.Combine(programFiles, "Funbits Telemetry Server"));
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            searchDirectories.Add(Path.Combine(userProfile, "Desktop"));
            searchDirectories.Add(Path.Combine(userProfile, "Documents"));
            searchDirectories.Add(Path.Combine(userProfile, "Downloads"));
            string ets2Install = GetGameInstallFolder(GameType.EuroTruckSimulator2);
            if (!string.IsNullOrEmpty(ets2Install))
            {
                searchDirectories.Add(ets2Install);
                searchDirectories.Add(Path.Combine(ets2Install, "server"));
                searchDirectories.Add(Path.Combine(ets2Install, "telemetry"));
            }
            string atsInstall = GetGameInstallFolder(GameType.AmericanTruckSimulator);
            if (!string.IsNullOrEmpty(atsInstall))
            {
                searchDirectories.Add(atsInstall);
                searchDirectories.Add(Path.Combine(atsInstall, "server"));
                searchDirectories.Add(Path.Combine(atsInstall, "telemetry"));
            }
            searchDirectories.Add(Path.Combine(ets2Install ?? "", "tools", "TelemetryServer"));
            searchDirectories.Add(Path.Combine(atsInstall ?? "", "tools", "TelemetryServer"));

            foreach (string dir in searchDirectories)
            {
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    string exePath = Path.Combine(dir, "Ets2Telemetry.exe");
                    if (File.Exists(exePath)) return exePath;
                    string serverSubPath = Path.Combine(dir, "server", "Ets2Telemetry.exe");
                    if (File.Exists(serverSubPath)) return serverSubPath;
                    try
                    {
                        foreach (string subDir in Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly))
                        {
                            exePath = Path.Combine(subDir, "Ets2Telemetry.exe");
                            if (File.Exists(exePath)) return exePath;
                            exePath = Path.Combine(subDir, "server", "Ets2Telemetry.exe");
                            if (File.Exists(exePath)) return exePath;
                        }
                    }
                    catch { }
                }
            }
            return null;
        }
        public static string GetExtractorPath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string extractorPath = Path.Combine(baseDirectory, "extractor.exe");
            return File.Exists(extractorPath) ? extractorPath : "extractor.exe";
        }
    }
}

