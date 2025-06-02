using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace SDBEditor.Handlers
{
    public static class SdbFunctionEntryMapper
    {
        // Now using uint instead of string hex
        private static readonly Dictionary<uint, string> _hashToLabel = new();

        public static void Initialize(string jsonPath)
        {
            _hashToLabel.Clear();

            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"[SDB Mapper] File not found: {jsonPath}");
                return;
            }

            try
            {
                var text = File.ReadAllText(jsonPath);
                var root = JsonNode.Parse(text);

                foreach (var entry in root.AsArray())
                {
                    if (entry is not JsonObject obj)
                        continue;

                    foreach (var kvp in obj)
                    {
                        var key = kvp.Key;

                        if (!key.EndsWith("_sdb", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (kvp.Value == null)
                            continue;

                        if (uint.TryParse(kvp.Value.ToString(), out var hash))
                        {
                            string label = ConvertKeyToLabel(key);

                            if (!_hashToLabel.ContainsKey(hash))
                            {
                                _hashToLabel[hash] = label;
                                // Optional: Console.WriteLine($"Mapped {hash} → {label}");
                            }
                        }
                    }
                }

                Console.WriteLine($"[SDB Mapper] Loaded {_hashToLabel.Count} SDB label entries.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SDB Mapper] Error parsing {jsonPath}: {ex.Message}");
            }
        }

        public static string Lookup(uint hash)
        {
            return _hashToLabel.TryGetValue(hash, out var label) ? label : string.Empty;
        }

        private static string ConvertKeyToLabel(string sdbKey)
        {
            var baseKey = sdbKey.Replace("_sdb", "", StringComparison.OrdinalIgnoreCase)
                                .Replace("_", " ");

            return baseKey switch
            {
                "fullname" => "Full Name",
                "fullname 2" => "Full Name 2",
                "nickname" => "Nickname",
                "ringname" => "Ring Name",
                "entrance name" => "Entrance Name",
                _ => CapitalizeWords(baseKey)
            };
        }

        private static string CapitalizeWords(string input)
        {
            var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..];

            return string.Join(" ", words);
        }
    }
}
