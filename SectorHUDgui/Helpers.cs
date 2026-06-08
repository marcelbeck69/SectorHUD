namespace SectorHUDgui
{
    public static class Helpers
    {
        public static string FormatTelemetryString(string format, TelemetryData data)
        {
            var replacements = new Dictionary<string, string>
            {
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
                { "[DISTANCE]", $"{data.Distance} {data.DistanceUnit}" },
                { "\\n", "\n" }
            };
            string result = format;
            foreach (var kv in replacements)
                result = result.Replace(kv.Key, kv.Value);
            return result;
        }
        public static void UpdateStatus(string text)
        {
            MainForm.statusLabel?.Text = text;
        }
    }
}