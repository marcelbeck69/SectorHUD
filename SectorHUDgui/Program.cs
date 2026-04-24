namespace SectorHUDgui
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Global\\SectorHUD_12345678-1234-1234-1234-123456789012", out createdNew))
            {
                if (createdNew)
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
                else
                {
                    MessageBox.Show("Program is already running.", $"{AppPaths.AppName}", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}