namespace SectorHUDgui
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // Konfiguration laden (erstellt INI falls nötig)
            ConfigManager.LoadOrCreateConfig();
            // Datenbank initialisieren
            DatabaseManager.Initialize(ConfigManager.GetValue("General", "DatabasePath", "."));
            Application.Run(new MainForm());
            // Aufräumen
            DatabaseManager.Close();
        }
    }
}