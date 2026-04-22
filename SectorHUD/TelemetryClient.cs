using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Globalization;

namespace SectorHUD
{
    public class TelemetryData
    {
        public bool Connected { get; set; }
        public string? Game { get; set; }
        public string? Source { get; set; }
        public string? Destination { get; set; }
        public string? RemRel { get; set; }
        public string? RemRelRT { get; set; }
        public string? RemRT { get; set; }
        public string? ETARel { get; set; }
        public string? ETARelRT { get; set; }
        public string? ETART { get; set; }
        public int Distance { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string? Sector { get; set; }
        public string? TimeStamp { get; set; }
        public string? Clock { get; set; }
        public bool JobActive { get; set; }
        public string? AllMods { get; set; }
        public string? TopMod { get; set; }
    }

    public static class TelemetryClient
    {
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static double _tsAvg = 15.0; // gleitender Durchschnitt

        public static async Task<TelemetryData> GetTelemetryInfo()
        {
            bool debug = ConfigManager.GetConfigBool("Tools", "TelemetryDebug", false);
            string url = ConfigManager.GetConfigValue("Tools", "TelemetryURL", "http://localhost:25555/api/ets2/telemetry");
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    if (debug) Helpers.ShowErrorMessage($"Error: Telemetry server returned status code {response.StatusCode}");
                    return new TelemetryData { Connected = false };
                }

                string json = await response.Content.ReadAsStringAsync();
                JObject data = JObject.Parse(json);

                if (!(data["game"]?["connected"]?.Value<bool>() ?? false))
                {
                    if (debug) Helpers.ShowErrorMessage("Info: Game is not connected to telemetry.");
                    return new TelemetryData { Connected = false };
                }

                // Gleitenden Durchschnitt der Zeitskala aktualisieren
                double timeScale = data["game"]?["timeScale"]?.Value<double>() ?? 19;
                _tsAvg = 0.005 * timeScale + 0.995 * _tsAvg;

                // Zeiten parsen
                string remainingTimeStr = data["job"]?["remainingTime"]?.Value<string>() ?? "";
                string etaTimeStr = data["navigation"]?["estimatedTime"]?.Value<string>() ?? "";
                TimeSpan remainingTime = ParseTimeString(remainingTimeStr);
                TimeSpan etaTime = ParseTimeString(etaTimeStr);

                int remMinsGame = (int)remainingTime.TotalMinutes;
                int remMinsReal = (int)(remainingTime.TotalMinutes / _tsAvg);
                int etaMinsGame = (int)etaTime.TotalMinutes;
                int etaMinsReal = (int)(etaTime.TotalMinutes / _tsAvg);

                string remReal = DateTime.Now.AddMinutes(remMinsReal).ToString("HH:mm");
                string etaReal = DateTime.Now.AddMinutes(etaMinsReal).ToString("HH:mm");

                // Sektor berechnen
                float truckX = data["truck"]?["placement"]?["x"]?.Value<float>() ?? 0;
                float truckZ = data["truck"]?["placement"]?["z"]?.Value<float>() ?? 0;
                int sectorX = (int)Math.Floor(truckX / 4000);
                int sectorZ = (int)Math.Floor(truckZ / 4000);
                string signX = sectorX >= 0 ? "+" : "-";
                string signZ = sectorZ >= 0 ? "+" : "-";
                string absX = Math.Abs(sectorX).ToString("0000");
                string absZ = Math.Abs(sectorZ).ToString("0000");

                string sourceCity = data["job"]?["sourceCity"]?.Value<string>() ?? "";
                string sourceCompany = data["job"]?["sourceCompany"]?.Value<string>() ?? "";
                string source = string.IsNullOrWhiteSpace(sourceCity) ? "" : $"{sourceCity} ({sourceCompany})";
                string destCity = data["job"]?["destinationCity"]?.Value<string>() ?? "";
                string destCompany = data["job"]?["destinationCompany"]?.Value<string>() ?? "";
                string destination = string.IsNullOrWhiteSpace(destCity) ? "" : $"{destCity} ({destCompany})";

                int distance = (int)Math.Floor(data["navigation"]?["estimatedDistance"]?.Value<float>() / 1000 ?? 0);

                return new TelemetryData
                {
                    Connected = true,
                    Game = data["game"]?["gameName"]?.Value<string>() ?? "",
                    Source = source,
                    Destination = destination,
                    RemRel = FormatMinutesToHoursMinutes(remMinsGame),
                    RemRelRT = FormatMinutesToHoursMinutes(remMinsReal),
                    RemRT = remReal,
                    ETARel = FormatMinutesToHoursMinutes(etaMinsGame),
                    ETARelRT = FormatMinutesToHoursMinutes(etaMinsReal),
                    ETART = etaReal,
                    Distance = distance,
                    X = sectorX,
                    Y = sectorZ,
                    Sector = $"sec{signX}{absX}{signZ}{absZ}",
                    TimeStamp = DateTime.Now.ToString("HH:mm:ss"),
                    Clock = DateTime.Now.ToString("HH:mm"),
                    JobActive = etaTime.TotalHours > 0
                };
            }
            catch (TaskCanceledException tex) when (!tex.CancellationToken.IsCancellationRequested)
            {
                if (debug) Helpers.ShowErrorMessage($"Timeout while accessing Telemetry server ({url}): {tex.Message}");
                return new TelemetryData { Connected = false };
            }
            catch (HttpRequestException hre)
            {
                if (debug) Helpers.ShowErrorMessage($"HTTP error while accessing Telemetry server ({url}): {hre.Message}\n{hre.InnerException}");
                return new TelemetryData { Connected = false };
            }
            catch (Exception ex)
            {
                if (debug) Helpers.ShowErrorMessage($"Unexpected error while accessing Telemetry server ({url}): {ex}");
                return new TelemetryData { Connected = false };
            }
        }

        public static void EnsureServerRunning()
        {
            string telemetryExe = ConfigManager.GetConfigValue("Tools", "TelemetryPath", ".\\Ets2Telemetry.exe");
            try { /* Test mit kleinem HttpClient */ }
            catch { Process.Start(telemetryExe); Thread.Sleep(5000); }
        }

        private static TimeSpan ParseTimeString(string timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr))
                return TimeSpan.Zero;

            timeStr = timeStr.Trim();
            if (timeStr.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
                timeStr = timeStr[..^1];

            // 1) Direktes TimeSpan parsen (invariant)
            if (TimeSpan.TryParse(timeStr, CultureInfo.InvariantCulture, out TimeSpan ts))
                return ts;

            // 2) TimeSpan-Exact-Formate prüfen
            var spanFormats = new[] { "c", @"hh\:mm\:ss", @"h\:mm\:ss" };
            foreach (var fmt in spanFormats)
                if (TimeSpan.TryParseExact(timeStr, fmt, CultureInfo.InvariantCulture, out ts))
                    return ts;

            // 3) DateTime versuchen (InvariantCulture und CurrentCulture)
            if (DateTime.TryParse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime dt) ||
                DateTime.TryParse(timeStr, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
            {
                return dt - DateTime.MinValue;
            }

            // 4) Einige häufige DateTime-Formate als Fallback
            var dtFormats = new[]
            {
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-dd HH:mm:ss",
                "dd/MM/yyyy HH:mm:ss",
                "d/M/yyyy H:mm:ss",
                "dd.MM.yyyy HH:mm:ss"
            };
            foreach (var fmt in dtFormats)
            {
                if (DateTime.TryParseExact(timeStr, fmt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt) ||
                    DateTime.TryParseExact(timeStr, fmt, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
                {
                    return dt - DateTime.MinValue;
                }
            }

            // Letzte Option: 0
            return TimeSpan.Zero;
        }
        private static string FormatMinutesToHoursMinutes(int minutes)
        {
            int hours = minutes / 60;
            int mins = minutes % 60;
            return $"{hours}:{mins:D2}";
        }
    }
}