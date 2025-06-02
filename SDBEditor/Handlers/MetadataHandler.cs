using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SDBEditor.Models;

namespace SDBEditor.Handlers
{
    /// <summary>
    /// Handles metadata for game languages - builds metadata from actual loaded content
    /// </summary>
    public class MetadataHandler
    {
        private readonly string _baseDir;
        private readonly Dictionary<string, object> _metadata;
        private bool _enableLogging = true;

        // Track loaded languages to avoid duplicate processing
        private readonly HashSet<string> _processedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Language code to full name mapping
        private readonly Dictionary<string, string> _languageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ARA", "Arabic" },
            { "ENG", "English" },
            { "FRA", "French" },
            { "GER", "German" },
            { "ITA", "Italian" },
            { "SPA", "Spanish" },
            { "JPN", "Japanese" },
            { "KOR", "Korean" },
            { "CHN", "Chinese" },
            { "RUS", "Russian" },
            { "POR", "Portuguese" },
            { "DUT", "Dutch" }
        };

        // Game identifiers
        private readonly Dictionary<string, List<string>> _gameIdentifiers = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "WWE2K25", new List<string> { "WWE 2K25", "2K25", "WWE2K25" } },
            { "WWE2K24", new List<string> { "WWE 2K24", "2K24", "WWE2K24" } },
            { "WWE2K23", new List<string> { "WWE 2K23", "2K23", "WWE2K23" } },
            { "WWE2K22", new List<string> { "WWE 2K22", "2K22", "WWE2K22" } },
            { "WWE2K20", new List<string> { "WWE 2K20", "2K20", "WWE2K20" } },
            { "WWE2K19", new List<string> { "WWE 2K19", "2K19", "WWE2K19" } }
        };

        // Common string indices that appear across WWE games
        // Since we don't know the exact property name, we'll work with indices
        private readonly HashSet<uint> _commonIndices = new HashSet<uint>
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
            100, 101, 102, 103, 104, 105, 106, 107, 108, 109, 110,
            200, 201, 202, 203, 204, 205,
            300, 301, 302, 303, 304, 305,
            400, 401, 402, 403, 404, 405,
            500, 501, 502, 503, 504, 505
        };

        public bool EnableLogging
        {
            get => _enableLogging;
            set => _enableLogging = value;
        }

        public int MinMetadataEntries { get; set; } = 30;
        public int MaxMetadataEntries { get; set; } = 40;

        public MetadataHandler(string baseDir = "Metadata")
        {
            _baseDir = baseDir;
            _metadata = new Dictionary<string, object>();

            // Ensure directory exists
            Directory.CreateDirectory(baseDir);

            LogMessage("MetadataHandler initialized - Dynamic metadata generation enabled");
        }

        /// <summary>
        /// Detect game name and language from file - FILENAME ONLY
        /// </summary>
        public Tuple<string, string> DetectGameInfo(string filePath, List<StringEntry> strings = null)
        {
            LogMessage($"\n============================================================");
            LogMessage($"LANGUAGE DETECTION - Dynamic Metadata Mode");
            LogMessage($"File: {filePath}");
            LogMessage($"============================================================");

            string gameName = "Unknown Game";
            string language = "Unknown Language";

            try
            {
                // STEP 1: Extract filename
                string fullFilename = Path.GetFileName(filePath);
                string filename = Path.GetFileNameWithoutExtension(filePath);

                LogMessage($"Full Filename: '{fullFilename}'");
                LogMessage($"Without Extension: '{filename}'");

                if (string.IsNullOrEmpty(filename))
                {
                    LogMessage("ERROR: Could not extract filename");
                    return new Tuple<string, string>(gameName, language);
                }

                // STEP 2: Detect language from filename
                string upperFilename = filename.ToUpper().Trim();
                LogMessage($"Checking: '{upperFilename}'");

                // Direct match check - EXPLICIT
                if (upperFilename == "ENG")
                {
                    language = "ENG";
                    LogMessage("✓ MATCHED: ENG (exact)");
                }
                else if (upperFilename == "FRA")
                {
                    language = "FRA";
                    LogMessage("✓ MATCHED: FRA (exact)");
                }
                else if (upperFilename == "GER")
                {
                    language = "GER";
                    LogMessage("✓ MATCHED: GER (exact)");
                }
                else if (upperFilename == "ITA")
                {
                    language = "ITA";
                    LogMessage("✓ MATCHED: ITA (exact)");
                }
                else if (upperFilename == "SPA")
                {
                    language = "SPA";
                    LogMessage("✓ MATCHED: SPA (exact)");
                }
                else if (upperFilename == "ARA")
                {
                    language = "ARA";
                    LogMessage("✓ MATCHED: ARA (exact)");
                }
                else
                {
                    // Check for language codes in filename
                    foreach (var lang in _languageNames.Keys)
                    {
                        if (upperFilename.Contains(lang))
                        {
                            language = lang;
                            LogMessage($"✓ MATCHED: {lang} (contains)");
                            break;
                        }
                    }
                }

                LogMessage($"Language Detection Result: {language}");

                // STEP 3: Detect game name
                gameName = DetectGameName(filePath);
                LogMessage($"Game Detection Result: {gameName}");

                // STEP 4: Generate metadata from strings if provided
                if (language != "Unknown Language" && strings != null && strings.Count > 0)
                {
                    LogMessage($"\nChecking if metadata generation needed for {language}...");
                    GenerateMetadataFromStrings(language, strings);
                }

                LogMessage($"\nFINAL RESULT: Game={gameName}, Language={language}");
                return new Tuple<string, string>(gameName, language);
            }
            catch (Exception ex)
            {
                LogMessage($"ERROR: {ex.Message}");
                LogMessage($"Stack Trace: {ex.StackTrace}");
                return new Tuple<string, string>(gameName, language);
            }
        }

        /// <summary>
        /// Generate metadata from actual string entries
        /// </summary>
        private void GenerateMetadataFromStrings(string language, List<StringEntry> strings)
        {
            if (strings == null || strings.Count == 0)
            {
                LogMessage("No strings provided for metadata generation");
                return;
            }

            try
            {
                string metadataFile = Path.Combine(_baseDir, language, "metadata.json");
                Dictionary<uint, string> existingMetadata = new Dictionary<uint, string>();

                // Load existing metadata if it exists
                if (File.Exists(metadataFile))
                {
                    LogMessage($"Loading existing metadata for {language}...");
                    existingMetadata = LoadMetadata(language);
                    LogMessage($"Existing entries: {existingMetadata.Count}");
                }
                else
                {
                    LogMessage($"No existing metadata for {language}, creating new...");
                }

                // Only proceed if we need more entries
                if (existingMetadata.Count >= MinMetadataEntries)
                {
                    LogMessage($"Metadata for {language} already has {existingMetadata.Count} entries (minimum: {MinMetadataEntries})");
                    return;
                }

                LogMessage($"Building metadata from {strings.Count} string entries...");

                // Add language-specific default entries first
                existingMetadata[0] = _languageNames.ContainsKey(language) ? _languageNames[language] : language;
                existingMetadata[1] = $"{language} Language File";

                // Add some language-specific entries to ensure uniqueness
                switch (language)
                {
                    case "ENG":
                        existingMetadata[2] = "English";
                        existingMetadata[3] = "United States";
                        existingMetadata[4] = "EN-US";
                        break;
                    case "FRA":
                        existingMetadata[2] = "Français";
                        existingMetadata[3] = "France";
                        existingMetadata[4] = "FR-FR";
                        break;
                    case "GER":
                        existingMetadata[2] = "Deutsch";
                        existingMetadata[3] = "Deutschland";
                        existingMetadata[4] = "DE-DE";
                        break;
                    case "ITA":
                        existingMetadata[2] = "Italiano";
                        existingMetadata[3] = "Italia";
                        existingMetadata[4] = "IT-IT";
                        break;
                    case "SPA":
                        existingMetadata[2] = "Español";
                        existingMetadata[3] = "España";
                        existingMetadata[4] = "ES-ES";
                        break;
                    case "ARA":
                        existingMetadata[2] = "العربية";
                        existingMetadata[3] = "عربي";
                        existingMetadata[4] = "AR-SA";
                        break;
                }

                // Now add entries from the actual string data using HashId property
                var candidateStrings = strings
                    .Where(s => s != null &&
                           !string.IsNullOrWhiteSpace(s.Text) &&
                           s.Text.Length >= 3 &&
                           s.Text.Length <= 100)
                    .OrderBy(s => s.HashId) // Use HashId property
                    .Take(100)
                    .ToList();

                foreach (var entry in candidateStrings)
                {
                    if (existingMetadata.Count >= MaxMetadataEntries)
                        break;

                    // Use the HashId property directly
                    uint entryId = entry.HashId;

                    // Skip if we already have this ID
                    if (!existingMetadata.ContainsKey(entryId))
                    {
                        existingMetadata[entryId] = entry.Text;
                        LogMessage($"  Added entry: [{entryId}] = \"{TruncateText(entry.Text, 50)}\"");
                    }
                }

                LogMessage($"Total metadata entries for {language}: {existingMetadata.Count}");

                // Save the metadata
                SaveMetadata(language, existingMetadata);

                // Mark this language as processed
                _processedLanguages.Add(language);
            }
            catch (Exception ex)
            {
                LogMessage($"Error generating metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Truncate text for logging
        /// </summary>
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Detect game name from file path
        /// </summary>
        private string DetectGameName(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return "Unknown Game";

            string pathUpper = filePath.ToUpper();

            foreach (var game in _gameIdentifiers)
            {
                if (game.Value.Any(identifier => pathUpper.Contains(identifier.ToUpper())))
                {
                    return game.Key;
                }
            }

            // Check parent directories
            try
            {
                DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(filePath));
                while (dir != null && dir.Parent != null)
                {
                    foreach (var game in _gameIdentifiers)
                    {
                        if (game.Value.Any(identifier =>
                            dir.Name.IndexOf(identifier, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            return game.Key;
                        }
                    }
                    dir = dir.Parent;
                }
            }
            catch { }

            return "Unknown Game";
        }

        /// <summary>
        /// Load metadata for a specific language
        /// </summary>
        public Dictionary<uint, string> LoadMetadata(string language)
        {
            try
            {
                string metadataFile = Path.Combine(_baseDir, language, "metadata.json");
                LogMessage($"\nLoading metadata for {language}");
                LogMessage($"  Path: {metadataFile}");
                LogMessage($"  Exists: {File.Exists(metadataFile)}");

                if (File.Exists(metadataFile))
                {
                    string jsonContent = File.ReadAllText(metadataFile);
                    var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                    if (metadata != null)
                    {
                        var result = metadata.ToDictionary(
                            kv => uint.Parse(kv.Key),
                            kv => kv.Value
                        );
                        LogMessage($"  Loaded {result.Count} entries");

                        // Log first few entries for debugging
                        int count = 0;
                        foreach (var entry in result.Take(5))
                        {
                            LogMessage($"    [{entry.Key}] = \"{TruncateText(entry.Value, 40)}\"");
                            count++;
                        }
                        if (result.Count > 5)
                        {
                            LogMessage($"    ... and {result.Count - 5} more entries");
                        }

                        return result;
                    }
                }
                else
                {
                    LogMessage($"  Metadata file not found for {language}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"  Error loading metadata: {ex.Message}");
            }

            return new Dictionary<uint, string>();
        }

        /// <summary>
        /// Save metadata for a specific language
        /// </summary>
        public bool SaveMetadata(string language, Dictionary<uint, string> metadata)
        {
            try
            {
                string metadataDir = Path.Combine(_baseDir, language);
                Directory.CreateDirectory(metadataDir);

                var stringKeyMetadata = metadata.ToDictionary(
                    kv => kv.Key.ToString(),
                    kv => kv.Value
                );

                string metadataFile = Path.Combine(metadataDir, "metadata.json");
                string jsonContent = JsonSerializer.Serialize(stringKeyMetadata, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(metadataFile, jsonContent, Encoding.UTF8);
                LogMessage($"Saved {metadata.Count} entries to {metadataFile}");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error saving metadata: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get metadata statistics
        /// </summary>
        public void ShowMetadataStats()
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("METADATA STATISTICS");
            Console.WriteLine(new string('=', 60));

            if (Directory.Exists(_baseDir))
            {
                foreach (string dir in Directory.GetDirectories(_baseDir))
                {
                    string lang = Path.GetFileName(dir);
                    var metadata = LoadMetadata(lang);
                    Console.WriteLine($"{lang}: {metadata.Count} entries");
                }
            }
            else
            {
                Console.WriteLine("No metadata directory found");
            }

            Console.WriteLine(new string('=', 60));
        }

        /// <summary>
        /// Clear metadata for a specific language
        /// </summary>
        public void ClearLanguageMetadata(string language)
        {
            try
            {
                string metadataDir = Path.Combine(_baseDir, language);
                if (Directory.Exists(metadataDir))
                {
                    Directory.Delete(metadataDir, true);
                    _processedLanguages.Remove(language);
                    LogMessage($"Cleared metadata for {language}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error clearing metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Force regenerate metadata for a language from strings
        /// </summary>
        public void ForceRegenerateMetadata(string language, List<StringEntry> strings)
        {
            LogMessage($"\nForce regenerating metadata for {language}...");
            ClearLanguageMetadata(language);
            GenerateMetadataFromStrings(language, strings);
        }

        /// <summary>
        /// Diagnostic method to show StringEntry structure
        /// </summary>
        public void DiagnoseStringEntryStructure(List<StringEntry> strings)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("STRINGENTRY STRUCTURE DIAGNOSTIC");
            Console.WriteLine(new string('=', 60));

            if (strings == null || strings.Count == 0)
            {
                Console.WriteLine("No strings provided");
                return;
            }

            var firstEntry = strings.FirstOrDefault();
            if (firstEntry != null)
            {
                var type = firstEntry.GetType();
                Console.WriteLine($"Type: {type.FullName}");
                Console.WriteLine("\nProperties:");

                foreach (var prop in type.GetProperties())
                {
                    try
                    {
                        var value = prop.GetValue(firstEntry);
                        Console.WriteLine($"  {prop.Name} ({prop.PropertyType.Name}): {value?.ToString() ?? "null"}");
                    }
                    catch
                    {
                        Console.WriteLine($"  {prop.Name} ({prop.PropertyType.Name}): [Error reading value]");
                    }
                }
            }

            Console.WriteLine(new string('=', 60));
        }

        // Standard methods for compatibility
        public List<string> GetAvailableLanguages(string gameName)
        {
            var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(_baseDir))
            {
                foreach (string dir in Directory.GetDirectories(_baseDir))
                {
                    string dirName = Path.GetFileName(dir).ToUpper();
                    if (_languageNames.ContainsKey(dirName))
                    {
                        languages.Add(dirName);
                    }
                }
            }

            if (languages.Count == 0)
            {
                return _languageNames.Keys.OrderBy(k => k).ToList();
            }

            return languages.OrderBy(l => l).ToList();
        }

        private void LogMessage(string message)
        {
            if (_enableLogging)
            {
                Console.WriteLine($"[MetadataHandler] {message}");
            }
        }

        // Stub methods for compatibility
        public Dictionary<uint, string> LoadLanguageStrings(string gameName, string language)
        {
            return new Dictionary<uint, string>();
        }

        public bool SaveLanguageStrings(string gameName, string language, Dictionary<uint, string> strings)
        {
            return true;
        }

        public void AddSupportedLanguage(string languageCode)
        {
            if (languageCode?.Length == 3)
            {
                _languageNames[languageCode.ToUpper()] = languageCode.ToUpper();
            }
        }

        public void RegisterGameIdentifier(string gameName, params string[] identifiers)
        {
            if (!_gameIdentifiers.ContainsKey(gameName))
            {
                _gameIdentifiers[gameName] = new List<string>();
            }
            _gameIdentifiers[gameName].AddRange(identifiers);
        }

        public List<string> GetSupportedLanguages()
        {
            return _languageNames.Keys.OrderBy(k => k).ToList();
        }

        public string GetMetadataPath() => _baseDir;

        // Deprecated methods (kept for compatibility)
        private void InitializeLanguageMarkers() { }
        private string DetectLanguageFromContent(List<StringEntry> strings) => "Unknown";
        private string DetectLanguageFromFilename(string filePath)
        {
            var result = DetectGameInfo(filePath);
            return result.Item2;
        }
    }
}