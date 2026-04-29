using Microsoft.Data.Sqlite;
using SectorHUDgui.Properties;
using System.Data;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using static System.ComponentModel.Design.ObjectSelectorEditor;

namespace SectorHUDgui
{
    public static class DatabaseManager
    {
        private static SqliteConnection _connection = null!;
        private static string _logPath = string.Empty;

        private static void Log(string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_logPath))
                {
                    string? logDir = Path.GetDirectoryName(AppPaths.DatabaseFilePath);
                    if (string.IsNullOrEmpty(logDir)) return;
                    _logPath = Path.Combine(logDir, "SectorHUD_scan.log");
                }
                using (var writer = new StreamWriter(_logPath, true))
                {
                    writer.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
                }
            }
            catch { }
        }

        public static void Initialize(string dbFolder)
        {
            Directory.CreateDirectory(AppPaths.AppDataLocalPath);
            _connection = new SqliteConnection($"Data Source={AppPaths.DatabaseFilePath};Pooling=True;");
            _connection.Open();
            CreateTables();
        }

        private static void CreateTables()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ets_mods (id INTEGER PRIMARY KEY AUTOINCREMENT, file TEXT NOT NULL, name TEXT NOT NULL, pri INTEGER, filedate INTEGER);
                    CREATE TABLE IF NOT EXISTS ets_sectors (mod INTEGER, x INTEGER, y INTEGER, UNIQUE(mod, x, y));
                    CREATE TABLE IF NOT EXISTS ats_mods (id INTEGER PRIMARY KEY AUTOINCREMENT, file TEXT NOT NULL, name TEXT NOT NULL, pri INTEGER, filedate INTEGER);
                    CREATE TABLE IF NOT EXISTS ats_sectors (mod INTEGER, x INTEGER, y INTEGER, UNIQUE(mod, x, y));
                ";
                cmd.ExecuteNonQuery();
            }
        }

        public static void Close() => _connection?.Close();

        public static string GetModName(string modTable, string sectorTable, int x, int y, bool topmostOnly)
        {
            string query = $@"
                    SELECT COALESCE(m.name, '???') AS ModName, m.file AS ModFile 
                    FROM {sectorTable} s 
                    LEFT JOIN {modTable} m ON s.mod = m.id 
                    WHERE s.x = @X AND s.y = @Y AND m.pri > 0 
                    ORDER BY m.pri DESC";
            if (topmostOnly)
                query += " LIMIT 1";
            using (var cmd = new SqliteCommand(query, _connection))
            {
                cmd.Parameters.AddWithValue("@X", x);
                cmd.Parameters.AddWithValue("@Y", y);

                using (var reader = cmd.ExecuteReader())
                {
                    var mods = new List<string>();
                    while (reader.Read())
                        mods.Add($"{reader["ModName"]}");
                    string modtext = string.Join(" > ", mods);
                    // MessageBox.Show(modtext + modTable + sectorTable, $"Mods at sector ({x}, {y})", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return modtext;
                }
            }
        }

        public static bool UpdateDatabase(string game, string modTable, string sectorTable, bool recreate)
        {
            Log($"=== Scan started for {game} (recreate={recreate}) ===");

            string map = ConfigManager.GetValue(game, "Map");
            string modPath = Path.Combine(ConfigManager.GetValue(game, "GameDocPath"), "mod");
            string gameLogPath = Path.Combine(ConfigManager.GetValue(game, "GameDocPath"), "game.log.txt");
            string tempPath = ConfigManager.GetValue("General", "TempPath", "%TEMP%");
            string extractorPath = ConfigManager.GetValue("Tools", "SKZKPath", ".\\extractor.exe");
            string extractPath = Path.Combine(tempPath, "SectorHUD");

            if (!File.Exists(gameLogPath))
            {
                string msg = string.Format(Strings.GamelogNotFound, gameLogPath);
                Log(msg);
                MessageBox.Show(msg, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (!File.Exists(extractorPath))
            {
                string msg = string.Format(Strings.ExtractorNotFound, extractorPath);
                Log(msg);
                MessageBox.Show(msg, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            Helpers.UpdateStatus(string.Format(Strings.UpdatingGameDatabase, game));

            using (var cmd = _connection.CreateCommand())
            {
                if (recreate)
                {
                    cmd.CommandText = $"DELETE FROM {modTable}; DELETE FROM {sectorTable}; DELETE FROM SQLITE_SEQUENCE WHERE name='{modTable}';";
                    cmd.ExecuteNonQuery();
                    Log($"Tables {modTable} and {sectorTable} were cleared (recreate=true).");
                }
                else
                {
                    cmd.CommandText = $"UPDATE {modTable} SET pri = 0";
                    cmd.ExecuteNonQuery();
                    Log($"Priority reset for all entries in {modTable}.");
                }
            }

            var mods = new List<(string file, string path, string name)>();

            // Base Game
            string baseMapPath = Path.Combine(ConfigManager.GetValue(game, "GamePath"), "base_map.scs");
            if (File.Exists(baseMapPath))
                mods.Add(("base_map.scs", baseMapPath, "Base Game"));

            // DLCs aus game.log.txt extrahieren
            string logContent = File.ReadAllText(gameLogPath);
            var dlcMatches = Regex.Matches(logContent, @"dlc_(.+?)\.scs mounted");
            var dlcNames = new List<string>();
            foreach (Match m in dlcMatches)
            {
                string dlcBase = m.Groups[1].Value.Trim();
                string dlcFile = $"dlc_{dlcBase}.scs";
                string dlcFullPath = Path.Combine(ConfigManager.GetValue(game, "GamePath"), dlcFile);
                if (!File.Exists(dlcFullPath))
                {
                    string warn = string.Format(Strings.DlcFileNotFound, dlcFile);
                    Log($"WARNING: {warn}");
                    MessageBox.Show(warn, Strings.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }
                string dlcName = ModExtractor.dlcMappings.ContainsKey(dlcBase) ? ModExtractor.dlcMappings[dlcBase] :
                    $"DLC {string.Join(" ", dlcBase.Split('_').Select(p => char.ToUpper(p[0]) + p.Substring(1).ToLower()))}";
                mods.Add((dlcFile, dlcFullPath, dlcName));
                dlcNames.Add(dlcName);
            }
            Log($"Found DLCs: {(dlcNames.Count > 0 ? string.Join(", ", dlcNames) : "none")}");

            // Lokale Mods aus game.log.txt
            var modMatches = Regex.Matches(logContent, @"Active local mod (.+?) \(name: (.+?), version");
            var seenMods = new HashSet<string>();
            var localModNames = new List<string>();
            foreach (Match m in modMatches)
            {
                string modFileBase = m.Groups[1].Value.Trim();
                if (seenMods.Contains(modFileBase)) continue;
                string modName = m.Groups[2].Value.Trim();
                string modFile = modFileBase + ".zip";
                string modFullPath = Path.Combine(modPath, modFile);
                if (!File.Exists(modFullPath))
                {
                    modFile = modFileBase + ".scs";
                    modFullPath = Path.Combine(modPath, modFile);
                    if (!File.Exists(modFullPath))
                    {
                        MessageBox.Show(string.Format(Strings.ModFileNotFound, modFile), Strings.Warning, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }
                }
                seenMods.Add(modFileBase);
                mods.Add((modFile, modFullPath, modName));
                localModNames.Add(modName);
            }
            Log($"Found local mods: {(localModNames.Count > 0 ? string.Join(", ", localModNames) : "none")}");

            int pri = 0, sectors = 0;
            foreach (var mod in mods)
            {
                pri++;
                int progress = pri * 100 / mods.Count;
                Helpers.UpdateStatus(string.Format(Strings.ScanningMod, progress, mod.name));
                Log($"Processing [{pri}/{mods.Count}]: {mod.name} ({mod.file})");
                DateTime lastWrite = File.GetLastWriteTimeUtc(mod.path);
                long fileDate = new DateTimeOffset(lastWrite).ToUnixTimeSeconds();
                try
                {
                    using (var transaction = _connection.BeginTransaction())
                    {
                        // Existiert bereits?
                        long modId;
                        bool scanIt = false;
                        using (var checkCmd = _connection.CreateCommand())
                        {
                            checkCmd.Transaction = transaction;
                            checkCmd.CommandText = $"SELECT id, filedate FROM {modTable} WHERE file = @File";
                            checkCmd.Parameters.AddWithValue("@File", mod.file);
                            using (var reader = checkCmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    modId = reader.GetInt64(0);
                                    long dbFileDate = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);                                    
                                    if (fileDate > dbFileDate)
                                    {
                                        scanIt = true;
                                        using (var updateCmd = new SqliteCommand($"DELETE FROM {modTable} WHERE id = @Id; DELETE FROM {sectorTable} WHERE mod = @Id;", _connection))
                                        {
                                            updateCmd.Transaction = transaction;
                                            updateCmd.Parameters.AddWithValue("@Id", modId);
                                            updateCmd.ExecuteNonQuery();
                                        }
                                        Log($"  Recreating record due to newer filedate (ID={modId})");
                                    }
                                    else
                                    {
                                        scanIt = false;
                                        using (var updateCmd = new SqliteCommand($"UPDATE {modTable} SET pri = @Pri WHERE id = @Id", _connection))
                                        {
                                            updateCmd.Transaction = transaction;
                                            updateCmd.Parameters.AddWithValue("@Id", modId);
                                            updateCmd.Parameters.AddWithValue("@Pri", pri);
                                            updateCmd.ExecuteNonQuery();
                                        }
                                        Log($"  Updated priority of existing record (ID={modId}) to {pri}");
                                    }
                                }
                                else 
                                { 
                                    scanIt = true; 
                                }
                            }
                        }
                        if (scanIt)
                        {
                            using (var insertCmd = new SqliteCommand($"INSERT INTO {modTable} (name, file, pri, filedate) VALUES (@Name, @File, @Pri, @FileDate); SELECT last_insert_rowid();", _connection))
                            {
                                insertCmd.Transaction = transaction;
                                insertCmd.Parameters.AddWithValue("@Name", mod.name);
                                insertCmd.Parameters.AddWithValue("@File", mod.file);
                                insertCmd.Parameters.AddWithValue("@Pri", pri);
                                insertCmd.Parameters.AddWithValue("@FileDate", fileDate);
                                object? insertScalar = insertCmd.ExecuteScalar();
                                if (insertScalar == null || insertScalar == DBNull.Value)
                                    throw new InvalidOperationException("INSERT did not return a new id.");
                                modId = Convert.ToInt64(insertScalar);
                            }
                            Log($"  Inserted new record (ID={modId})");

                            // Extractor ausführen
                            string args = $"--list \"{mod.path}\"";
                            List<string> output = ModExtractor.RunExtractor(extractorPath, args);

                            // Prüfen, ob genügend Zeilen gefunden wurden
                            int lineCount = output.Count();
                            if (lineCount < 5)
                            {
                                Helpers.UpdateStatus(string.Format(Strings.ScanningModDeep, progress, mod.name));
                                Log($"  Shallow scan gave only {lineCount} lines, switching to deep scan...");
                                if (!Directory.Exists(extractPath))
                                    Directory.CreateDirectory(extractPath);
                                args = $"\"{mod.path}\" --deep --dry-run --partial=/map --dest=\"{extractPath}\"";
                                output = ModExtractor.RunExtractor(extractorPath, args);
                            }

                            // Sektoren parsen
                            List<string> sectorLines = output.Where(line => line.Contains(map) && line.Contains("sec") && line.Contains(".base")).ToList();
                            int sectorsAdded = 0;
                            if (sectorLines.Any())
                            {
                                using (var insertSector = new SqliteCommand($"INSERT OR IGNORE INTO {sectorTable} (x, y, mod) VALUES (@X, @Y, @Mod)", _connection))
                                {
                                    insertSector.Transaction = transaction;
                                    var paramX = insertSector.Parameters.Add("@X", Microsoft.Data.Sqlite.SqliteType.Integer);
                                    var paramY = insertSector.Parameters.Add("@Y", Microsoft.Data.Sqlite.SqliteType.Integer);
                                    insertSector.Parameters.AddWithValue("@Mod", modId);
                                    foreach (var line in sectorLines)
                                    {
                                        var match = Regex.Match(line, @"sec(.\d{4})(.\d{4})\.base");
                                        if (match.Success)
                                        {
                                            paramX.Value = int.Parse(match.Groups[1].Value);
                                            paramY.Value = int.Parse(match.Groups[2].Value);
                                            insertSector.ExecuteNonQuery();
                                            sectorsAdded++;
                                        }
                                    }
                                }
                                sectors += sectorsAdded;
                                Log($"  {sectorsAdded} sectors added {mod.name} (map filter: {map})");
                            }
                            else
                            {
                                Log($"  No sectors found for {mod.name}.");
                            }

                            // Temporäre Extraktionsverzeichnisse löschen
                            if (Directory.Exists(extractPath))
                                Directory.Delete(extractPath, true);
                        }
                        transaction.Commit();
                    }
                }
                catch (Exception ex)
                {
                    Log($"  ERROR while processing {mod.name}: {ex.Message}");
                }
            }
            string finalMsg = string.Format(Strings.DoneGameDatabaseUpdated, game, sectors);
            Helpers.UpdateStatus(finalMsg);
            Log($"=== Scan finished. Total processed: {pri} mods/DLCs, total sectors: {sectors} ===");
            return true;
        }

        public static string QueryDatabaseSingle(string modTable, string sectorTable, string input)
        {
            string result = "";
            if (string.IsNullOrWhiteSpace(input)) return "";
            int x = 0, y = 0;
            var sectorMatch = Regex.Match(input, @"^sec([+-]\d{4})([+-]\d{4})$");
            if (sectorMatch.Success)
            {
                x = int.Parse(sectorMatch.Groups[1].Value);
                y = int.Parse(sectorMatch.Groups[2].Value);
            }
            else
            {
                var coordMatch = Regex.Match(input, @"^(-?\d+),(-?\d+)$");
                if (coordMatch.Success)
                {
                    x = int.Parse(coordMatch.Groups[1].Value);
                    y = int.Parse(coordMatch.Groups[2].Value);
                    x = (int)Math.Floor(x / 4000.0);
                    y = (int)Math.Floor(y / 4000.0);
                }
                else
                {
                    MessageBox.Show(Strings.InvalidFormat, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return "";
                }
            }
            Helpers.UpdateStatus(string.Format(Strings.QueryingDatabaseSectortable, sectorTable, x, y));
            using (var cmd = new SqliteCommand($@"
                    SELECT m.name AS ModName, m.file AS ModFile 
                    FROM {sectorTable} s 
                    LEFT JOIN {modTable} m ON s.mod = m.id 
                    WHERE s.x = @X AND s.y = @Y AND m.pri > 0 
                    ORDER BY m.pri DESC", _connection))
            {
                cmd.Parameters.AddWithValue("@X", x);
                cmd.Parameters.AddWithValue("@Y", y);
                using (var reader = cmd.ExecuteReader())
                {
                    var mods = new List<string>();
                    while (reader.Read())
                        mods.Add($"{reader["ModName"]} [{reader["ModFile"]}]");
                    if (mods.Any())
                    {
                        int p = 0;
                        foreach (var mod in mods)
                        {
                            p++;
                            result += $"{p} {mod}\n";
                        }
                    }
                    else
                    {
                        result = Strings.NoModsFoundAtThisLocation;
                    }
                }
            }
            return result;
        }

        public static int CountMods(string modTable)
        {
            using (var cmd = new SqliteCommand($@"SELECT COUNT(*) FROM {modTable}", _connection))
            {
                object? result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
                else
                    return 0;
            }
        }
        public static int CountSectors(string sectorTable)
        {
            using (var cmd = new SqliteCommand($@"SELECT COUNT(DISTINCT x || ',' || y) FROM {sectorTable}", _connection))
            {
                object? result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
                else
                    return 0;
            }
        }
    }
}