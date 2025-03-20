using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using SDBEditor.Models;

namespace SDBEditor.Handlers
{
    /// <summary>
    /// Handles the loading, saving, and manipulation of SDB files
    /// </summary>
    public class SDBHandler
    {
        private List<StringEntry> _strings;
        private Dictionary<string, object> _metadata;
        private Dictionary<uint, string> _decodedTextCache; // Cache for decoded string text

        public bool IsMangled { get; private set; }
        public string GameName { get; set; }
        public string Language { get; set; }
        public string CurrentFile { get; private set; }

        public SDBHandler()
        {
            _strings = new List<StringEntry>();
            _metadata = new Dictionary<string, object>();
            _decodedTextCache = new Dictionary<uint, string>(); // Initialize decoded text cache
            IsMangled = false;
            GameName = "Unknown Game";
            Language = "Unknown Language";
        }

        /// <summary>
        /// Load and parse an SDB file
        /// </summary>
        public bool LoadSDB(string filePath)
        {
            try
            {
                // Store the current file path
                CurrentFile = filePath;

                // Check if mangled - must happen before trying to load from cache!
                IsMangled = CheckIfMangled(filePath);
                Console.WriteLine($"LoadSDB: File {filePath} IsMangled = {IsMangled}");

                // Check if we already have this file cached in SDBCacheManager
                var cachedEntries = SDBCacheManager.Instance.GetCachedParsedEntries(filePath);
                if (cachedEntries != null)
                {
                    // Use the cached data instead of parsing the file again
                    _strings = cachedEntries.Select(e => new StringEntry(e.HashId, e.Text, e.Mangled)).ToList();

                    // Make sure we set IsMangled correctly based on the file, not the cached data
                    // The cached data's mangled status should be ignored

                    // Detect game and language
                    DetectGameAndLanguage(filePath);

                    Console.WriteLine($"Loaded SDB from cache: {filePath}, {_strings.Count} entries, IsMangled = {IsMangled}");
                    return true;
                }

                // Read file bytes
                byte[] fileBytes = File.ReadAllBytes(filePath);

                // Parse header
                uint headerTag = BitConverter.ToUInt32(fileBytes, 0);
                uint numStrings = BitConverter.ToUInt32(fileBytes, 4);

                // Double-check mangled status from actual file header
                IsMangled = (headerTag == 0x100);
                Console.WriteLine($"LoadSDB from bytes: Header tag: 0x{headerTag:X}, IsMangled = {IsMangled}");

                // Clear existing strings and caches
                _strings.Clear();
                _decodedTextCache.Clear();

                // Parse string entries
                int offset = 8; // Start after header

                // Create a list to hold entries for parallel processing
                var entries = new List<(uint address, uint size, uint guid)>();

                // First pass: collect all entries
                for (int i = 0; i < numStrings; i++)
                {
                    uint address = BitConverter.ToUInt32(fileBytes, offset);
                    offset += 4;

                    uint size = BitConverter.ToUInt32(fileBytes, offset);
                    offset += 4;

                    uint guid = BitConverter.ToUInt32(fileBytes, offset);
                    offset += 4;

                    entries.Add((address, size, guid));
                }

                // Second pass: process entries (potentially in parallel for large files)
                if (entries.Count > 5000)
                {
                    // Use parallel processing for large datasets
                    var stringList = new StringEntry[entries.Count];

                    Parallel.For(0, entries.Count, i =>
                    {
                        var (address, size, guid) = entries[i];

                        // Read string data
                        byte[] stringData = new byte[size];
                        Array.Copy(fileBytes, address, stringData, 0, size);

                        // Demangle if needed
                        if (IsMangled)
                        {
                            stringData = DemangleString(stringData, (int)address);
                        }

                        // Create string entry with proper UTF-8 handling
                        var entry = new StringEntry(guid, Encoding.UTF8.GetString(stringData).TrimEnd('\0'), IsMangled);
                        stringList[i] = entry;
                    });

                    // Add all entries to the main list
                    _strings.AddRange(stringList);
                }
                else
                {
                    // Sequential processing for smaller datasets
                    foreach (var (address, size, guid) in entries)
                    {
                        // Read string data
                        byte[] stringData = new byte[size];
                        Array.Copy(fileBytes, address, stringData, 0, size);

                        // Demangle if needed
                        if (IsMangled)
                        {
                            stringData = DemangleString(stringData, (int)address);
                        }

                        // Convert to text with proper UTF-8 handling
                        string text = Encoding.UTF8.GetString(stringData).TrimEnd('\0');

                        // Add to string list
                        _strings.Add(new StringEntry(guid, text, IsMangled));
                    }
                }

                // Cache the parsed entries for future use
                SDBCacheManager.Instance.CacheParsedEntries(filePath, _strings);

                // Detect game and language
                DetectGameAndLanguage(filePath);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load SDB: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load SDB from memory stream (for cached data)
        /// </summary>
        public bool LoadSDBFromStream(MemoryStream stream)
        {
            try
            {
                // Reset state
                _strings = new List<StringEntry>();
                _decodedTextCache.Clear();

                // Position the stream at the beginning
                stream.Position = 0;

                // Read all bytes from the stream
                byte[] fileBytes = new byte[stream.Length];
                stream.Read(fileBytes, 0, fileBytes.Length);

                // Parse header
                uint headerTag = BitConverter.ToUInt32(fileBytes, 0);

                // Set mangled status based on header
                IsMangled = (headerTag == 0x100);
                Console.WriteLine($"LoadSDBFromStream: Header tag: 0x{headerTag:X}, IsMangled = {IsMangled}");

                uint numStrings = BitConverter.ToUInt32(fileBytes, 4);

                // Parse string entries
                int offset = 8; // Start after header

                // Create a list to hold entries for parallel processing
                var entries = new List<(uint address, uint size, uint guid)>();

                // First pass: collect all entries
                for (int i = 0; i < numStrings; i++)
                {
                    uint address = BitConverter.ToUInt32(fileBytes, offset);
                    offset += 4;

                    uint size = BitConverter.ToUInt32(fileBytes, offset);
                    offset += 4;

                    uint guid = BitConverter.ToUInt32(fileBytes, offset);
                    offset += 4;

                    entries.Add((address, size, guid));
                }

                // Second pass: process entries
                foreach (var (address, size, guid) in entries)
                {
                    // Read string data
                    byte[] stringData = new byte[size];
                    Array.Copy(fileBytes, address, stringData, 0, size);

                    // Demangle if needed
                    if (IsMangled)
                    {
                        stringData = DemangleString(stringData, (int)address);
                    }

                    // Convert to text with proper UTF-8 handling
                    string text = Encoding.UTF8.GetString(stringData).TrimEnd('\0');

                    // Add to string list
                    _strings.Add(new StringEntry(guid, text, IsMangled));
                }

                // Cache the parsed entries if we have a current file path
                if (!string.IsNullOrEmpty(CurrentFile))
                {
                    SDBCacheManager.Instance.CacheParsedEntries(CurrentFile, _strings);
                }

                // Detect game and language
                DetectGameAndLanguage(Path.GetFileName(CurrentFile ?? ""));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load SDB from stream: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save the current strings to an SDB file
        /// </summary>
        public bool SaveSDB(string filePath)
        {
            try
            {
                // Ensure strings collection is initialized
                if (_strings == null)
                {
                    _strings = new List<StringEntry>();
                }

                using (FileStream fs = new FileStream(filePath, FileMode.Create))
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    // Write header
                    writer.Write((uint)0);  // Header tag (0 for unmangled)
                    writer.Write((uint)_strings.Count);  // Number of strings

                    // Calculate table pointer
                    uint tablePtr = 8 + (12 * (uint)_strings.Count);

                    // Write string address table
                    List<byte[]> stringDataList = new List<byte[]>();
                    foreach (var entry in _strings)
                    {
                        if (entry == null) continue; // Skip null entries

                        // Convert text to bytes with proper UTF-8 encoding
                        byte[] textBytes = Encoding.UTF8.GetBytes(entry.Text ?? string.Empty);
                        stringDataList.Add(textBytes);

                        // Write entry information
                        writer.Write(tablePtr);  // Address
                        writer.Write((uint)textBytes.Length);  // Size
                        writer.Write(entry.HashId);  // Hash ID/GUID

                        // Update table pointer for next entry
                        tablePtr += (uint)textBytes.Length + 1;  // +1 for null terminator
                    }

                    // Write string data
                    foreach (var textBytes in stringDataList)
                    {
                        writer.Write(textBytes);
                        writer.Write((byte)0);  // Null terminator
                    }
                }

                // Update the cache after saving
                if (_strings != null && _strings.Count > 0)
                {
                    // Cache the raw file data and parsed entries
                    byte[] fileData = File.ReadAllBytes(filePath);
                    SDBCacheManager.Instance.CacheRawFile(filePath, fileData);
                    SDBCacheManager.Instance.CacheParsedEntries(filePath, _strings);
                }

                // Update current file
                CurrentFile = filePath;

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save SDB: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if the SDB file is mangled/encrypted with improved detection
        /// </summary>
        private bool CheckIfMangled(string filePath)
        {
            try
            {
                // Always use try-with-resources pattern to ensure file is properly closed
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    if (fs.Length < 4)
                        return false; // File too small to have a header

                    uint headerTag = reader.ReadUInt32();

                    // The proper header tag for mangled files is 0x100
                    bool isMangled = (headerTag == 0x100);

                    Console.WriteLine($"CheckIfMangled: File {filePath} has header {headerTag:X}, IsMangled = {isMangled}");

                    return isMangled;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking if mangled: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Demangle/decrypt a string
        /// </summary>
        private byte[] DemangleString(byte[] data, int address)
        {
            byte key = (byte)((address & 0xFF) ^ 0xCD);
            byte[] decrypted = new byte[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                decrypted[i] = (byte)(data[i] ^ key);
                key = data[i];
            }

            return decrypted;
        }

        /// <summary>
        /// Mangle/encrypt a string
        /// </summary>
        private byte[] MangleString(byte[] data, int address)
        {
            byte key = (byte)((address & 0xFF) ^ 0xCD);
            byte[] encrypted = new byte[data.Length];

            for (int i = 0; i < data.Length; i++)
            {
                encrypted[i] = (byte)(data[i] ^ key);
                key = encrypted[i];
            }

            return encrypted;
        }

        /// <summary>
        /// Detect game name and language from file content and path
        /// </summary>
        private void DetectGameAndLanguage(string filePath)
        {
            // Default values
            GameName = "Unknown Game";
            Language = "Unknown Language";

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

                // Ensure strings collection is initialized
                if (_strings == null)
                {
                    _strings = new List<StringEntry>();
                    return;
                }

                // Game detection - check file path first
                string pathLower = filePath.ToLower();
                foreach (var game in gameMarkers)
                {
                    if (game.Value.Any(marker => pathLower.Contains(marker.ToLower())))
                    {
                        GameName = game.Key;
                        break;
                    }
                }

                // If path didn't give a match, check string content
                if (GameName == "Unknown Game")
                {
                    string contentText = string.Join(" ", _strings.Where(s => s != null).Select(s => (s.Text ?? string.Empty).ToLower()));
                    foreach (var game in gameMarkers)
                    {
                        if (game.Value.Any(marker => contentText.Contains(marker.ToLower())))
                        {
                            GameName = game.Key;
                            break;
                        }
                    }
                }

                // Language detection
                Dictionary<string, int> languageScores = languageMap.ToDictionary(kv => kv.Key, kv => 0);
                foreach (var entry in _strings.Where(s => s != null).Take(100))
                {
                    string text = (entry.Text ?? string.Empty).ToLower();
                    foreach (var lang in languageMap)
                    {
                        if (lang.Value.Any(marker => text.Contains(marker)))
                        {
                            languageScores[lang.Key]++;
                        }
                    }
                }

                // Set language if we have a clear winner
                if (languageScores.Values.Any())
                {
                    int maxScore = languageScores.Values.Max();
                    if (maxScore > 5)
                    {
                        Language = languageScores.First(kv => kv.Value == maxScore).Key;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in game/language detection: {ex.Message}");
            }
        }

        /// <summary>
        /// Set strings directly (for cached data)
        /// </summary>
        public void SetStrings(List<StringEntry> strings)
        {
            // Deep copy the strings
            _strings = strings.Select(e => new StringEntry(e.HashId, e.Text, e.Mangled)).ToList();

            // Update cache if we have a current file
            if (!string.IsNullOrEmpty(CurrentFile))
            {
                SDBCacheManager.Instance.CacheParsedEntries(CurrentFile, _strings);
            }

            // Clear decoded text cache to maintain consistency
            _decodedTextCache.Clear();
        }

        /// <summary>
        /// Get all string entries with caching
        /// </summary>
        public List<StringEntry> GetAllStrings()
        {
            // Check the cache first if we have a current file
            if (!string.IsNullOrEmpty(CurrentFile))
            {
                var cachedEntries = SDBCacheManager.Instance.GetCachedParsedEntries(CurrentFile);
                if (cachedEntries != null)
                {
                    // Use the cached entries and update our internal collection
                    _strings = cachedEntries.Select(e => new StringEntry(e.HashId, e.Text, e.Mangled)).ToList();
                    return cachedEntries;
                }
            }

            // Return a copy to avoid external modifications to underlying collection
            return _strings?.ToList() ?? new List<StringEntry>();
        }

        /// <summary>
        /// Get a chunk of strings (for virtualization)
        /// </summary>
        public List<StringEntry> GetStringChunk(int startIndex, int count)
        {
            // Get strings from cache first
            var allStrings = GetAllStrings();

            // Return a copy of the requested window
            if (allStrings == null || allStrings.Count == 0)
                return new List<StringEntry>();

            return allStrings
                .Skip(startIndex)
                .Take(Math.Min(count, allStrings.Count - startIndex))
                .ToList();
        }

        /// <summary>
        /// Add a new string
        /// </summary>
        public bool AddString(string text, uint? hashId = null)
        {
            try
            {
                // Initialize the collection if null
                if (_strings == null)
                {
                    _strings = new List<StringEntry>();
                }

                // Ensure text is not null
                text = text ?? string.Empty;

                if (hashId == null)
                {
                    hashId = GenerateUniqueHashId();
                }

                _strings.Add(new StringEntry(hashId.Value, text));

                // Update cache if we have a current file
                if (!string.IsNullOrEmpty(CurrentFile))
                {
                    SDBCacheManager.Instance.CacheParsedEntries(CurrentFile, _strings);
                    SDBCacheManager.Instance.ClearSearchCache(); // Clear search cache since data has changed
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to add string: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generate a unique hash ID for a new string
        /// </summary>
        public uint GenerateUniqueHashId()
        {
            // Initialize the collection if null
            if (_strings == null)
            {
                _strings = new List<StringEntry>();
            }

            if (_strings.Count == 0)
            {
                return 0x10000000;
            }
            return _strings.Max(entry => entry.HashId) + 1;
        }

        /// <summary>
        /// Get a string entry by its hash ID
        /// </summary>
        public StringEntry GetStringByHash(uint hashId)
        {
            // Initialize the collection if null
            if (_strings == null)
            {
                _strings = new List<StringEntry>();
                return null;
            }

            return _strings.FirstOrDefault(entry => entry != null && entry.HashId == hashId);
        }

        /// <summary>
        /// Update an existing string's text
        /// </summary>
        public bool UpdateString(uint hashId, string newText)
        {
            try
            {
                // Initialize the collection if null
                if (_strings == null)
                {
                    _strings = new List<StringEntry>();
                    return false;
                }

                // Ensure text is not null
                newText = newText ?? string.Empty;

                var entry = _strings.FirstOrDefault(e => e != null && e.HashId == hashId);
                if (entry != null)
                {
                    entry.Text = newText;

                    // Update the decoded text cache
                    if (_decodedTextCache.ContainsKey(hashId))
                    {
                        _decodedTextCache[hashId] = newText;
                    }

                    // Update cache if we have a current file
                    if (!string.IsNullOrEmpty(CurrentFile))
                    {
                        SDBCacheManager.Instance.CacheParsedEntries(CurrentFile, _strings);
                        SDBCacheManager.Instance.ClearSearchCache(); // Clear search cache since data has changed
                    }

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update string: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete a string by its hash ID
        /// </summary>
        public bool DeleteString(uint hashId)
        {
            try
            {
                // Initialize the collection if null
                if (_strings == null)
                {
                    _strings = new List<StringEntry>();
                    return false;
                }

                int index = _strings.FindIndex(entry => entry != null && entry.HashId == hashId);
                if (index >= 0)
                {
                    _strings.RemoveAt(index);

                    // Remove from decoded text cache
                    _decodedTextCache.Remove(hashId);

                    // Update cache if we have a current file
                    if (!string.IsNullOrEmpty(CurrentFile))
                    {
                        SDBCacheManager.Instance.CacheParsedEntries(CurrentFile, _strings);
                        SDBCacheManager.Instance.ClearSearchCache(); // Clear search cache since data has changed
                    }

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete string: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Search strings based on different criteria with efficient caching
        /// </summary>
        public List<StringEntry> SearchStrings(string query, string searchBy = "Text")
        {
            // Check the cache in SDBCacheManager first
            if (!string.IsNullOrEmpty(CurrentFile))
            {
                var cachedResults = SDBCacheManager.Instance.GetCachedSearchResults(query, searchBy);
                if (cachedResults != null)
                {
                    return cachedResults;
                }
            }

            // Initialize the collection if null
            if (_strings == null)
            {
                _strings = new List<StringEntry>();
                return new List<StringEntry>();
            }

            query = query?.ToLower() ?? string.Empty;
            var results = new List<StringEntry>();

            // Use LINQ for more efficient filtering
            if (searchBy.Equals("Text", StringComparison.OrdinalIgnoreCase))
            {
                results = _strings
                    .Where(entry => entry != null && (entry.Text ?? string.Empty).ToLower().Contains(query))
                    .ToList();
            }
            else if (searchBy.Equals("String Hash ID", StringComparison.OrdinalIgnoreCase))
            {
                results = _strings
                    .Where(entry => entry != null && entry.HashId.ToString().Contains(query))
                    .ToList();
            }
            else if (searchBy.Equals("Index", StringComparison.OrdinalIgnoreCase))
            {
                results = _strings
                    .Where((entry, index) => entry != null && index.ToString().Contains(query))
                    .ToList();
            }

            // Cache the results in SDBCacheManager
            if (!string.IsNullOrEmpty(CurrentFile) && results.Count > 0 && results.Count < 5000)
            {
                SDBCacheManager.Instance.CacheSearchResults(query, searchBy, results);
            }

            return results;
        }

        /// <summary>
        /// Import strings from a file (Excel or CSV)
        /// </summary>
        public bool ImportStrings(string filePath, out int importedCount, out int skippedCount, out int errorCount)
        {
            importedCount = 0;
            skippedCount = 0;
            errorCount = 0;

            try
            {
                // Initialize the collection if null
                if (_strings == null)
                {
                    _strings = new List<StringEntry>();
                }

                // Create backup before import if current file exists
                if (!string.IsNullOrEmpty(CurrentFile))
                {
                    var backupHandler = new BackupHandler();
                    backupHandler.CreateBackup(CurrentFile, "pre_import");
                }

                // Read file based on extension
                List<string> importTexts = new List<string>();

                if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    // Read CSV
                    string[] lines = File.ReadAllLines(filePath, Encoding.UTF8); // Specify UTF-8 encoding
                    if (lines.Length > 1) // Assuming first line is header
                    {
                        // Parse header to find text column
                        string[] headers = lines[0].Split(',');
                        int textColumnIndex = 2; // Default to third column

                        // Find column named "Text" or similar
                        for (int i = 0; i < headers.Length; i++)
                        {
                            if (headers[i].Contains("Text", StringComparison.OrdinalIgnoreCase))
                            {
                                textColumnIndex = i;
                                break;
                            }
                        }

                        // Read data rows
                        for (int i = 1; i < lines.Length; i++)
                        {
                            string[] fields = lines[i].Split(',');
                            if (fields.Length > textColumnIndex)
                            {
                                importTexts.Add(fields[textColumnIndex].Trim('"'));
                            }
                        }
                    }
                }
                else if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                         filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    // Read Excel using our new ExcelHandler
                    importTexts = ExcelHandler.ReadExcelFile(filePath);
                }
                else
                {
                    throw new Exception("Unsupported file format");
                }

                // Process the imported texts
                uint lastHashId = GenerateUniqueHashId();

                // Get existing texts to avoid duplicates
                HashSet<string> existingTexts = new HashSet<string>(
                    _strings.Where(e => e != null).Select(e => (e.Text ?? string.Empty).ToLower()));

                foreach (string text in importTexts)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(text) || text.ToLower() == "nan")
                        {
                            skippedCount++;
                            continue;
                        }

                        // Check for duplicates
                        if (!existingTexts.Contains(text.ToLower()))
                        {
                            uint newHashId = lastHashId + (uint)importedCount;
                            if (AddString(text, newHashId))
                            {
                                importedCount++;
                                existingTexts.Add(text.ToLower());
                            }
                            else
                            {
                                errorCount++;
                            }
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    catch
                    {
                        errorCount++;
                    }
                }

                // Update cache if we have a current file
                if (!string.IsNullOrEmpty(CurrentFile))
                {
                    SDBCacheManager.Instance.CacheParsedEntries(CurrentFile, _strings);
                    SDBCacheManager.Instance.ClearSearchCache(); // Clear search cache since data has changed
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Import failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Export strings to a file (Excel, CSV, or TXT)
        /// </summary>
        public bool ExportStrings(string filePath)
        {
            try
            {
                // Initialize the collection if null
                if (_strings == null)
                {
                    _strings = new List<StringEntry>();
                }

                // Export based on file extension
                if (filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    // Create a list of string data
                    List<string[]> rows = new List<string[]>();

                    // Add header
                    rows.Add(new[] { "Index", "String Hash ID", "Hex", "Text" });

                    // Add data rows
                    for (int i = 0; i < _strings.Count; i++)
                    {
                        StringEntry entry = _strings[i];
                        if (entry == null) continue;

                        rows.Add(new[]
                        {
                            i.ToString(),
                            entry.HashId.ToString(),
                            entry.HashId.ToString("X"),
                            entry.Text ?? string.Empty
                        });
                    }

                    // Write CSV with UTF-8 encoding and BOM
                    using (StreamWriter writer = new StreamWriter(filePath, false, new UTF8Encoding(true)))
                    {
                        foreach (string[] row in rows)
                        {
                            writer.WriteLine(string.Join(",", row.Select(field => $"\"{(field ?? string.Empty).Replace("\"", "\"\"")}\"")));
                        }
                    }
                }
                else if (filePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                         filePath.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    // Use our new ExcelHandler for export
                    return ExcelHandler.ExportToExcel(filePath, _strings);
                }
                else if (filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    // Write TXT (tab-separated) with UTF-8 encoding and BOM
                    using (StreamWriter writer = new StreamWriter(filePath, false, new UTF8Encoding(true)))
                    {
                        // Add header
                        writer.WriteLine("Index\tString Hash ID\tHex\tText");

                        // Add data rows
                        for (int i = 0; i < _strings.Count; i++)
                        {
                            StringEntry entry = _strings[i];
                            if (entry == null) continue;

                            writer.WriteLine($"{i}\t{entry.HashId}\t{entry.HashId.ToString("X")}\t{entry.Text ?? string.Empty}");
                        }
                    }
                }
                else
                {
                    throw new Exception("Unsupported file format");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Export failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Import strings from Excel with advanced duplicate detection
        /// </summary>
        public bool ImportStringsFromExcel(
            string filePath,
            out int importedCount,
            out int skippedCount,
            out int errorCount,
            int startRow = 2,
            int endRow = 0,
            int columnIndex = -1)
        {
            importedCount = 0;
            skippedCount = 0;
            errorCount = 0;

            try
            {
                // Initialize the collection if null
                if (_strings == null)
                {
                    _strings = new List<StringEntry>();
                }

                // Create backup before import if current file exists
                if (!string.IsNullOrEmpty(CurrentFile))
                {
                    var backupHandler = new BackupHandler();
                    backupHandler.CreateBackup(CurrentFile, "pre_excel_import");
                }

                // Get existing texts to avoid duplicates
                HashSet<string> existingTexts = new HashSet<string>(
                    _strings.Where(e => e != null).Select(e => (e.Text ?? string.Empty).ToLower()));

                // Process Excel file with advanced duplicate detection
                var result = ExcelHandler.ProcessExcelFileWithDuplicateDetection(
                    filePath, existingTexts, startRow, endRow, columnIndex);

                var newEntries = result.Item1;
                var duplicateEntries = result.Item2;
                var errorMessages = result.Item3;

                // Add the new strings
                uint lastHashId = GenerateUniqueHashId();
                foreach (string text in newEntries)
                {
                    if (AddString(text, lastHashId + (uint)importedCount))
                    {
                        importedCount++;
                    }
                    else
                    {
                        errorCount++;
                    }
                }

                // Update statistics
                skippedCount = duplicateEntries.Count;
                errorCount += errorMessages.Count;

                // Update cache if we have a current file
                if (!string.IsNullOrEmpty(CurrentFile))
                {
                    SDBCacheManager.Instance.CacheParsedEntries(CurrentFile, _strings);
                    SDBCacheManager.Instance.ClearSearchCache(); // Clear search cache since data has changed
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excel import failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get cached decoded text for a hash ID
        /// </summary>
        public string GetCachedDecodedText(uint hashId)
        {
            if (_decodedTextCache.TryGetValue(hashId, out string cachedText))
            {
                return cachedText;
            }

            // Cache miss, find and cache the text
            var entry = GetStringByHash(hashId);
            if (entry != null)
            {
                string text = entry.Text;
                _decodedTextCache[hashId] = text;
                return text;
            }

            return null;
        }

        /// <summary>
        /// Clear all caches
        /// </summary>
        public void ClearCaches()
        {
            _decodedTextCache.Clear();

            // Clear SDBCacheManager caches as well if we have a current file
            if (!string.IsNullOrEmpty(CurrentFile))
            {
                SDBCacheManager.Instance.ClearCache(CurrentFile);
            }
        }
    }
}