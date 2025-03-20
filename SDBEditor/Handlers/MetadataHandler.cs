using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json; // Added JSON serialization
using SDBEditor.Models;

namespace SDBEditor.Handlers
{
    /// <summary>
    /// Handles metadata for game languages and other information
    /// </summary>
    public class MetadataHandler
    {
        private string _baseDir;
        private Dictionary<string, object> _metadata;

        public MetadataHandler(string baseDir = "Metadata")
        {
            _baseDir = baseDir;
            _metadata = new Dictionary<string, object>();

            // Ensure directory exists
            Directory.CreateDirectory(baseDir);
        }

        /// <summary>
        /// Detect game name and language from file content and path
        /// </summary>
        public Tuple<string, string> DetectGameInfo(string filePath, List<StringEntry> strings)
        {
            string gameName = "Unknown Game";
            string language = "Unknown Language";

            try
            {
                // Game identifiers
                Dictionary<string, List<string>> gameMarkers = new Dictionary<string, List<string>>
                {
                    { "WWE2K25", new List<string> { "WWE 2K25", "2K25", "WWE2K25" } },  // Added WWE 2K25 support
                    { "WWE2K24", new List<string> { "WWE 2K24", "2K24", "WWE2K24" } },
                    { "WWE2K23", new List<string> { "WWE 2K23", "2K23", "WWE2K23" } },
                    { "WWE2K22", new List<string> { "WWE 2K22", "2K22", "WWE2K22" } },
                    { "WWE2K20", new List<string> { "WWE 2K20", "2K20", "WWE2K20" } },
                    { "WWE2K19", new List<string> { "WWE 2K19", "2K19", "WWE2K19" } }
                };

                // Language markers
                Dictionary<string, List<string>> languageMap = new Dictionary<string, List<string>>
                {
                    { "ARA", new List<string> { "ال", "في", "من", "على" } },  // Arabic
                    { "ENG", new List<string> { "the", "and", "match", "win" } },  // English
                    { "FRA", new List<string> { "le", "la", "les", "des" } },  // French
                    { "GER", new List<string> { "der", "die", "das", "und" } },  // German
                    { "ITA", new List<string> { "il", "la", "gli", "nel" } },  // Italian
                    { "SPA", new List<string> { "el", "la", "los", "las" } }   // Spanish
                };

                // Collect sample texts
                List<string> sampleTexts = new List<string>();
                foreach (var entry in strings.Take(100))
                {
                    sampleTexts.Add(entry.Text.ToLower());
                }

                // Game detection - check file path first
                string pathLower = filePath.ToLower();
                foreach (var game in gameMarkers)
                {
                    if (game.Value.Any(marker => pathLower.Contains(marker.ToLower())))
                    {
                        gameName = game.Key;
                        break;
                    }
                }

                // If path didn't give a match, check string content
                if (gameName == "Unknown Game")
                {
                    string contentText = string.Join(" ", sampleTexts);
                    foreach (var game in gameMarkers)
                    {
                        if (game.Value.Any(marker => contentText.Contains(marker.ToLower())))
                        {
                            gameName = game.Key;
                            break;
                        }
                    }
                }

                // Language detection
                Dictionary<string, int> languageScores = languageMap.ToDictionary(kv => kv.Key, kv => 0);
                foreach (string text in sampleTexts)
                {
                    foreach (var lang in languageMap)
                    {
                        if (lang.Value.Any(marker => text.Contains(marker)))
                        {
                            languageScores[lang.Key]++;
                        }
                    }
                }

                // Set language if we have a clear winner
                int maxScore = languageScores.Values.Max();
                if (maxScore > 5)
                {
                    language = languageScores.First(kv => kv.Value == maxScore).Key;
                }

                return new Tuple<string, string>(gameName, language);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in game/language detection: {ex.Message}");
                return new Tuple<string, string>(gameName, language);
            }
        }

        /// <summary>
        /// Load metadata for a specific game
        /// </summary>
        public Dictionary<uint, string> LoadMetadata(string language)
        {
            try
            {
                string metadataFile = Path.Combine(_baseDir, language, "metadata.json");
                if (File.Exists(metadataFile))
                {
                    string jsonContent = File.ReadAllText(metadataFile);
                    var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                    // Convert string keys to uint keys
                    return metadata.ToDictionary(
                        kv => uint.Parse(kv.Key),
                        kv => kv.Value
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load metadata: {ex.Message}");
            }

            return new Dictionary<uint, string>();
        }

        /// <summary>
        /// Load language strings for a specific game and language
        /// </summary>
        public Dictionary<uint, string> LoadLanguageStrings(string gameName, string language)
        {
            try
            {
                string metadataFile = Path.Combine(_baseDir, gameName, language, "strings.json");
                if (File.Exists(metadataFile))
                {
                    string jsonContent = File.ReadAllText(metadataFile);
                    var stringData = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonContent);

                    // Convert string keys to uint keys
                    return stringData.ToDictionary(
                        kv => uint.Parse(kv.Key),
                        kv => kv.Value
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load language strings: {ex.Message}");
            }

            return new Dictionary<uint, string>();
        }

        /// <summary>
        /// Save metadata for a specific game
        /// </summary>
        public bool SaveMetadata(string language, Dictionary<uint, string> metadata)
        {
            try
            {
                string metadataDir = Path.Combine(_baseDir, language);
                Directory.CreateDirectory(metadataDir);

                // Convert uint keys to string keys for JSON serialization
                var stringKeyMetadata = metadata.ToDictionary(
                    kv => kv.Key.ToString(),
                    kv => kv.Value
                );

                string metadataFile = Path.Combine(metadataDir, "metadata.json");
                string jsonContent = JsonSerializer.Serialize(stringKeyMetadata, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(metadataFile, jsonContent);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save metadata: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get list of available languages for a game
        /// </summary>
        public List<string> GetAvailableLanguages(string gameName)
        {
            List<string> languages = new List<string>();

            string gameDir = Path.Combine(_baseDir, gameName);
            if (Directory.Exists(gameDir))
            {
                foreach (string dir in Directory.GetDirectories(gameDir))
                {
                    string lang = Path.GetFileName(dir);
                    languages.Add(lang.ToUpper());
                }

                // Also check for SDB files directly in the game directory
                foreach (string file in Directory.GetFiles(gameDir, "*.sdb"))
                {
                    string lang = Path.GetFileNameWithoutExtension(file);
                    if (!languages.Contains(lang.ToUpper()))
                    {
                        languages.Add(lang.ToUpper());
                    }
                }
            }

            // Add default languages if none found
            if (languages.Count == 0)
            {
                languages.Add("ENG");
                languages.Add("FRA");
                languages.Add("GER");
                languages.Add("ITA");
                languages.Add("SPA");
            }

            return languages;
        }
    }
}