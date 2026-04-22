namespace SectorHUD
{
    public static class Helpers
    {
        public static string ReadChoice(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write($"{prompt}: ");
                string input = Console.ReadLine() ?? "";
                if (int.TryParse(input, out int val) && val >= min && val <= max)
                    return input;
                ShowWarning($"Invalid input. Please enter a number between {min} and {max}.");
            }
        }
        public static void ShowErrorMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public static void ShowWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        public static string FormatTelemetryString(string format, TelemetryData data)
        {
            var replacements = new Dictionary<string, string>
            {
                { "[TIMESTAMP]", data.TimeStamp ?? ""},
                { "[CLOCK]", data.Clock ?? ""},
                { "[SECTOR]", data.Sector ?? ""},
                { "[MODS]", data.AllMods ?? "" },
                { "[MOD]", data.TopMod ?? "" },
                { "[SOURCE]", data.Source ?? ""},
                { "[DESTINATION]", data.Destination ?? ""},
                { "[REMREL]", data.RemRel ?? ""},
                { "[REMRELRT]", data.RemRelRT ?? ""},
                { "[REMRT]", data.RemRT ?? ""},
                { "[ETAREL]", data.ETARel ?? ""},
                { "[ETARELRT]", data.ETARelRT ?? ""},
                { "[ETART]", data.ETART ?? ""},
                { "[DISTANCE]", data.Distance.ToString() },
                { "\\n", "\n" }
            };
            string result = format;
            foreach (var kv in replacements)
                result = result.Replace(kv.Key, kv.Value);
            return result;
        }
    }
}