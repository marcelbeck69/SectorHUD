using System.Diagnostics;

namespace SectorHUD
{
    public static class ModExtractor
    {
        // Mapping für DLC-Namen
        public static readonly Dictionary<string, string> dlcMappings = new()
        {
            { "east", "DLC Going East!" },
            { "north", "DLC Scandinavia" },
            { "fr", "DLC Vive la France!" },
            { "it", "DLC Italia" },
            { "balt", "DLC Beyond the Baltic Sea" },
            { "balkan_e", "DLC Road to the Black Sea" },
            { "iberia", "DLC Iberia" },
            { "balkan_w", "DLC West Balkans" },
            { "greece", "DLC Greece" },
            { "polar", "DLC Nordic Horizons" },
            { "nevada", "DLC Nevada" },
            { "arizona", "DLC Arizona" },
            { "nm", "DLC New Mexico" },
            { "or", "DLC Oregon" },
            { "wa", "DLC Washington" },
            { "ut", "DLC Utah" },
            { "id", "DLC Idaho" },
            { "co", "DLC Colorado" },
            { "wy", "DLC Wyoming" },
            { "tx", "DLC Texas" },
            { "mt", "DLC Montana" },
            { "ok", "DLC Oklahoma" },
            { "ks", "DLC Kansas" },
            { "ne", "DLC Nebraska" },
            { "ar", "DLC Arkansas" },
            { "mo", "DLC Missouri" },
            { "ia", "DLC Iowa" },
            { "la", "DLC Louisiana" }
        };

        // Führt den SCS-Extractor aus und gibt die Konsolenausgabe zurück.
        public static string RunExtractor(string extractorPath, string arguments)
        {
            // Temporäre Datei erstellen
            string tempFile = Path.GetTempFileName();

            try
            {
                // Starte cmd.exe mit Umleitung der Ausgaben in die temporäre Datei
                // 2>&1 leitet auch stderr nach stdout um, damit alles in einer Datei landet
                string cmdArguments = $@"/c """"{extractorPath}"" {arguments} > ""{tempFile}"" 2>&1""";

                using (var process = new Process())
                {
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = cmdArguments;
                    process.StartInfo.UseShellExecute = false;    // Kein separates Fenster
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = false;
                    process.StartInfo.RedirectStandardError = false;
                    process.Start();
                    process.WaitForExit();   // Wartet, bis der gesamte Prozess inkl. Umleitung fertig ist
                }

                // Komplette Ausgabe aus der Datei lesen
                string output = File.ReadAllText(tempFile);
                return output;
            }
            finally
            {
                // Aufräumen
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
    }
}