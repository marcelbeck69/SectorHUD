namespace SectorHUDgui
{
    public static class AppPaths
    {
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
    }
}
