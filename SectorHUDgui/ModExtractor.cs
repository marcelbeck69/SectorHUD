using SharpDX.DXGI;
using System.Diagnostics;
using SectorHUDgui.Properties;

namespace SectorHUDgui
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

        public static List<string> RunExtractor(string extractorPath, string arguments)
        {
            List<string> outputLines = new List<string>();
            string tempFile = Path.GetTempFileName();
            try
            {
                string cmdArguments = $@"/c """"{extractorPath}"" {arguments} > ""{tempFile}"" 2>&1""";

                using (var process = new Process())
                {
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.Arguments = cmdArguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = false;
                    process.StartInfo.RedirectStandardError = false;
                    process.Start();
                    process.WaitForExit();
                }
                outputLines = File.ReadAllLines(tempFile).ToList();
            }
            catch (Exception ex)
            {
                outputLines.Add($"Error running extractor: {ex.Message}");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
            return outputLines;
        }

    }
}