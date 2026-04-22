using Microsoft.Data.Sqlite;
using System.Data;
using System.Text.RegularExpressions;

namespace SectorHUD
{
    public static class DatabaseManager
    {
        private static SqliteConnection _connection = null!;

        public static void Initialize(string dbFolder)
        {
            string dbPath = Path.Combine(dbFolder, "SectorHUD.db");
            _connection = new SqliteConnection($"Data Source={dbPath};Pooling=True;");
            _connection.Open();
            CreateTables();
        }

        private static void CreateTables()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ets_mods (id INTEGER PRIMARY KEY AUTOINCREMENT, file TEXT NOT NULL, name TEXT NOT NULL, pri INTEGER);
                    CREATE TABLE IF NOT EXISTS ets_sectors (mod INTEGER, x INTEGER, y INTEGER, UNIQUE(mod, x, y));
                    CREATE TABLE IF NOT EXISTS ats_mods (id INTEGER PRIMARY KEY AUTOINCREMENT, file TEXT NOT NULL, name TEXT NOT NULL, pri INTEGER);
                    CREATE TABLE IF NOT EXISTS ats_sectors (mod INTEGER, x INTEGER, y INTEGER, UNIQUE(mod, x, y));
                ";
                cmd.ExecuteNonQuery();
            }
        }

        public static void Close() => _connection?.Close();

        public static string GetModName(string modTable, string sectorTable, int x, int y, bool topmostOnly)
        {
            string query = $@"
                SELECT COALESCE(m.name, '???') AS ModName 
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
                var names = new List<string>();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        names.Add(reader["ModName"]?.ToString() ?? "???");
                }
                return string.Join(" > ", names);
            }
        }

        public static void UpdateDatabase(string game, string modTable, string sectorTable, bool recreate)
        {

            Console.WriteLine($"\nUpdating {game} database...");

            string map = ConfigManager.GetConfigValue(game, "Map");
            string modPath = Path.Combine(ConfigManager.GetConfigValue(game, "GameDocPath"), "mod");
            string gameLogPath = Path.Combine(ConfigManager.GetConfigValue(game, "GameDocPath"), "game.log.txt");
            string tempPath = ConfigManager.GetConfigValue("General", "TempPath", "%TEMP%");
            string extractorPath = ConfigManager.GetConfigValue("Tools", "SKZKPath", ".\\extractor.exe");
            string sectorsFilePath = Path.Combine(tempPath, "SectorHUD_modmap.txt");
            string extractPath = Path.Combine(tempPath, "SectorHUD");

            if (!File.Exists(gameLogPath))
            {
                Helpers.ShowErrorMessage($"Error: game.log.txt not found in {gameLogPath}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }
            if (!File.Exists(extractorPath))
            {
                Helpers.ShowErrorMessage($"Error: SKZH Extractor not found in {extractorPath}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
                return;
            }

            using (var cmd = _connection.CreateCommand())
            {
                if (recreate)
                {
                    cmd.CommandText = $"DELETE FROM {modTable}; DELETE FROM {sectorTable}; DELETE FROM SQLITE_SEQUENCE WHERE name='{modTable}';";
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    cmd.CommandText = $"UPDATE {modTable} SET pri = 0";
                    cmd.ExecuteNonQuery();
                }
            }

            var mods = new List<(string file, string path, string name)>();

            // Base Game
            string baseMapPath = Path.Combine(ConfigManager.GetConfigValue(game, "GamePath"), "base_map.scs");
            if (File.Exists(baseMapPath))
                mods.Add(("base_map.scs", baseMapPath, "Base Game"));

            // DLCs aus game.log.txt extrahieren
            string logContent = File.ReadAllText(gameLogPath);
            var dlcMatches = Regex.Matches(logContent, @"dlc_(.+?)\\.scs mounted");
            foreach (Match m in dlcMatches)
            {
                string dlcBase = m.Groups[1].Value.Trim();
                string dlcFile = $"dlc_{dlcBase}.scs";
                string dlcFullPath = Path.Combine(ConfigManager.GetConfigValue(game, "GamePath"), dlcFile);
                if (!File.Exists(dlcFullPath))
                {
                    Helpers.ShowWarning($"Warning: DLC file not found: {dlcFile} (skipped)");
                    continue;
                }
                string dlcName = ModExtractor.dlcMappings.ContainsKey(dlcBase) ? ModExtractor.dlcMappings[dlcBase] :
                    $"DLC {string.Join(" ", dlcBase.Split('_').Select(p => char.ToUpper(p[0]) + p.Substring(1).ToLower()))}";
                mods.Add((dlcFile, dlcFullPath, dlcName));
            }

            // Lokale Mods aus game.log.txt
            var modMatches = Regex.Matches(logContent, @"Active local mod (.+?) \(name: (.+?), version");
            var seenMods = new HashSet<string>();
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
                        Helpers.ShowWarning($"Warning: Mod file not found: {modFileBase} (skipped)");
                        continue;
                    }
                }
                seenMods.Add(modFileBase);
                mods.Add((modFile, modFullPath, modName));
            }

            int pri = 0;
            foreach (var mod in mods)
            {
                pri++;
                Console.Write($"Processing mod: {mod.name} ... ");

                using (var transaction = _connection.BeginTransaction())
                {
                    // Existiert bereits?
                    long modId;
                    using (var checkCmd = _connection.CreateCommand())
                    {
                        checkCmd.Transaction = transaction;
                        checkCmd.CommandText = $"SELECT id FROM {modTable} WHERE file = @File";
                        checkCmd.Parameters.AddWithValue("@File", mod.file);
                        object? result = checkCmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            modId = Convert.ToInt64(result);
                            Console.Write("updating priority... ");
                            using (var updateCmd = new SqliteCommand($"UPDATE {modTable} SET pri = @Pri WHERE id = @Id", _connection))
                            {
                                updateCmd.Transaction = transaction;
                                updateCmd.Parameters.AddWithValue("@Id", modId);
                                updateCmd.Parameters.AddWithValue("@Pri", pri);
                                updateCmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // Neue Mod einfügen
                            using (var insertCmd = new SqliteCommand($"INSERT INTO {modTable} (name, file, pri) VALUES (@Name, @File, @Pri); SELECT last_insert_rowid();", _connection))
                            {
                                insertCmd.Transaction = transaction;
                                insertCmd.Parameters.AddWithValue("@Name", mod.name);
                                insertCmd.Parameters.AddWithValue("@File", mod.file);
                                insertCmd.Parameters.AddWithValue("@Pri", pri);
                                object? insertScalar = insertCmd.ExecuteScalar();
                                if (insertScalar == null || insertScalar == DBNull.Value)
                                    throw new InvalidOperationException("INSERT did not return a new id.");
                                modId = Convert.ToInt64(insertScalar);
                            }

                            // Extractor ausführen
                            Console.Write("extracting sectors... ");
                            string args = $"--list \"{mod.path}\"";
                            string output = ModExtractor.RunExtractor(extractorPath, args);
                            File.WriteAllText(sectorsFilePath, output);

                            // Prüfen, ob genügend Zeilen gefunden wurden
                            int lineCount = File.ReadLines(sectorsFilePath).Count();
                            if (lineCount < 5)
                            {
                                Console.Write("(deep scanning, please be patient)... ");
                                if (!Directory.Exists(extractPath))
                                    Directory.CreateDirectory(extractPath);
                                args = $"\"{mod.path}\" --deep --partial=/map --dest=\"{extractPath}\"";
                                output = ModExtractor.RunExtractor(extractorPath, args);
                                File.WriteAllText(sectorsFilePath, output);
                            }

                            // Sektoren parsen
                            var sectorLines = File.ReadLines(sectorsFilePath)
                                .Where(line => line.Contains(map) && line.Contains("sec") && line.Contains(".base"))
                                .ToList();
                            if (sectorLines.Any())
                            {
                                Console.Write($"{sectorLines.Count} sectors saved... ");
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
                                        }
                                    }
                                }
                            }

                            // Temporäre Extraktionsverzeichnisse löschen
                            if (Directory.Exists(extractPath))
                                Directory.Delete(extractPath, true);
                        }
                    }
                    transaction.Commit();
                }
                Console.WriteLine("done.");
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Database processing finished.\n");
            Console.ResetColor();
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }



        public static void QueryDatabase(string modTable, string sectorTable)
        {
            Console.WriteLine();
            while (true)
            {
                Console.Write("Enter sector name (e.g., sec+0004-0002) or coordinates (e.g., 8520,-12650) or leave empty to exit: ");
                string input = Console.ReadLine() ?? "";
                if (string.IsNullOrWhiteSpace(input))
                    break;

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
                        Helpers.ShowErrorMessage("Invalid format.");
                        continue;
                    }
                }

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
                                Console.ForegroundColor = (p == 1) ? ConsoleColor.Yellow : ConsoleColor.Gray;
                                Console.WriteLine($"{p} {mod}");
                            }
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("No mods found at this location.");
                            Console.ResetColor();
                        }
                    }
                }
                Console.WriteLine();
            }
        }
    }
}