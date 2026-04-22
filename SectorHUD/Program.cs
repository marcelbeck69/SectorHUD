using System.Text;

namespace SectorHUD
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Konfiguration laden
            ConfigManager.LoadOrCreateConfig();

            // Datenbank initialisieren
            DatabaseManager.Initialize(ConfigManager.GetConfigValue("General", "DatabasePath", "."));

            // Hauptmenü
            while (true)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("======= Sector HUD =======");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" 1  Start Monitor");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(" 2  Query ETS2 Database");
                Console.WriteLine(" 3  Query ATS Database");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(" 4  Update ETS2 Database");
                Console.WriteLine(" 5  Update ATS Database");
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" 6  Recreate ETS2 Database");
                Console.WriteLine(" 7  Recreate ATS Database");
                Console.ResetColor();
                Console.WriteLine(" 0  Quit");
                Console.WriteLine();
                string choice = Helpers.ReadChoice("Enter your choice", 0, 7);
                if (choice == "0") break;

                bool isEts = (choice == "1" || choice == "2" || choice == "4" || choice == "6");
                string modTable = isEts ? "ets_mods" : "ats_mods";
                string sectorTable = isEts ? "ets_sectors" : "ats_sectors";
                string gameName = isEts ? "ETS2" : "ATS";

                if (choice == "4" || choice == "5" || choice == "6" || choice == "7")
                {
                    DatabaseManager.UpdateDatabase(gameName, modTable, sectorTable, choice == "6" || choice == "7");
                }
                else if (choice == "2" || choice == "3")
                {
                    DatabaseManager.QueryDatabase(modTable, sectorTable);
                }
                else if (choice == "1")
                {
                    RunMonitor(modTable, sectorTable, gameName);
                }
            }

            DatabaseManager.Close();
        }

        static void RunMonitor(string modTable, string sectorTable, string gameName)
        {
            // Telemetry-Server starten (falls nötig)
            TelemetryClient.EnsureServerRunning();

            OverlayRenderer? overlay = null;
            if (ConfigManager.GetConfigBool("InGame", "Enabled", true))
            {
                float posX = ConfigManager.GetConfigFloat("InGame", "PositionX", 20);
                float posY = ConfigManager.GetConfigFloat("InGame", "PositionY", 38);
                string fontName = ConfigManager.GetConfigValue("InGame", "Font", "Arial");
                float fontSize = ConfigManager.GetConfigFloat("InGame", "FontSize", 16);
                overlay = new OverlayRenderer(fontName, fontSize, posX, posY);
                overlay.Start();
            }

            Console.WriteLine("\nMonitor running. Press Q to return to the menu.\n");
            bool lastConnected = true;
            while (true)
            {
                Thread.Sleep(5000);
                var telemetry = TelemetryClient.GetTelemetryInfo().GetAwaiter().GetResult();

                if (telemetry.Connected)
                {
                    telemetry.AllMods = DatabaseManager.GetModName(modTable, sectorTable, telemetry.X, telemetry.Y, false);
                    telemetry.TopMod = DatabaseManager.GetModName(modTable, sectorTable, telemetry.X, telemetry.Y, true);

                    if (ConfigManager.GetConfigBool("Console", "Enabled", true))
                    {
                        if (ConfigManager.GetConfigBool("Console", "ClearScreen", false)) Console.Clear();
                        string consoleText = Helpers.FormatTelemetryString(
                            ConfigManager.GetConfigValue("Console", "FormatString", "[TIMESTAMP] [SECTOR] [MODS]"),
                            telemetry);
                        Console.WriteLine(consoleText);
                    }

                    if (ConfigManager.GetConfigBool("InGame", "Enabled", true) && overlay != null)
                    {
                        string formatString = BuildOverlayFormatString(telemetry.JobActive);
                        string overlayText = Helpers.FormatTelemetryString(formatString, telemetry);
                        if (string.IsNullOrWhiteSpace(overlayText))
                            overlayText = "SectorHUD<color=00FF00> ready</color>";
                        overlay.UpdateText(overlayText);
                    }
                }
                else if (lastConnected)
                {
                    overlay?.UpdateText("SectorHUD<color=FFFF00> waiting for game...</color>");
                    Helpers.ShowWarning("Warning: Game is not running...");
                }
                lastConnected = telemetry.Connected;

                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q) break;
            }
            overlay?.Stop();
        }

        static string BuildOverlayFormatString(bool jobActive)
        {
            string format = "";
            if (ConfigManager.GetConfigBool("InGame", "ShowSector", true))
                format += ConfigManager.GetConfigValue("InGame", "FormatStringSector", "{12}<color=E8A52A>[MOD]</color>\\n");
            if (ConfigManager.GetConfigBool("InGame", "ShowJob", true) && jobActive)
            {
                if (ConfigManager.GetConfigBool("InGame", "ShowJobCity", true))
                    format += ConfigManager.GetConfigValue("InGame", "FormatStringJobCity", "{12}From: <color=FF0000> [SOURCE]</color>\\n{12}To: <color=00FF00> [DESTINATION]</color>\\n");
                format += ConfigManager.GetConfigValue("InGame", "FormatStringJobTime", "{12}Arrival in<color=FFFF00> [ETARELRT] min</color> at<color=FFFF00> [ETART]</color>\\n");
            }
            if (ConfigManager.GetConfigBool("InGame", "ShowClock", true))
                format += ConfigManager.GetConfigValue("InGame", "FormatStringClock", "{20}[CLOCK]");
            return format;
        }
    }
}

