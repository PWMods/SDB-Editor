using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SDBEditor.Models;
using SDBEditor.ViewModels;

namespace SDBEditor.Handlers
{
    /// <summary>
    /// Singleton class for managing comprehensive SDB caching at multiple levels
    /// </summary>
    public class SDBCacheManager
    {
        #region Singleton Implementation

        private static SDBCacheManager _instance;
        private static readonly object _lockObject = new object();

        public static SDBCacheManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new SDBCacheManager();
                        }
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Cache Containers

        // Raw file caching
        private Dictionary<string, byte[]> _rawFileCache = new Dictionary<string, byte[]>();
        private Dictionary<string, DateTime> _fileTimestamps = new Dictionary<string, DateTime>();

        // Parsed entries caching 
        private Dictionary<string, List<StringEntry>> _parsedEntriesCache = new Dictionary<string, List<StringEntry>>();

        // Search results caching with LRU
        private Dictionary<string, List<StringEntry>> _searchCache = new Dictionary<string, List<StringEntry>>();
        private List<string> _searchCacheKeys = new List<string>(); // For LRU tracking
        private const int MAX_SEARCH_CACHE_SIZE = 100; // Increased from 50 for better caching

        // Decoded text caching
        private Dictionary<uint, string> _decodedTextCache = new Dictionary<uint, string>();

        // View model caching
        private Dictionary<uint, StringEntryViewModel> _viewModelCache = new Dictionary<uint, StringEntryViewModel>();

        #endregion

        #region Cache Statistics

        public int RawFileCacheHits { get; private set; } = 0;
        public int ParsedEntriesCacheHits { get; private set; } = 0;
        public int SearchCacheHits { get; private set; } = 0;
        public int DecodedTextCacheHits { get; private set; } = 0;
        public int ViewModelCacheHits { get; private set; } = 0;
        public int CacheMisses { get; private set; } = 0;

        #endregion

        private SDBCacheManager()
        {
            // Private constructor for singleton
            Console.WriteLine("SDBCacheManager initialized");
        }

        #region Raw File Caching

        /// <summary>
        /// Cache the raw file data
        /// </summary>
        public void CacheRawFile(string filePath, byte[] fileData)
        {
            if (string.IsNullOrEmpty(filePath) || fileData == null || fileData.Length == 0)
                return;

            lock (_lockObject)
            {
                _rawFileCache[filePath] = fileData;
                _fileTimestamps[filePath] = File.GetLastWriteTime(filePath);
                Console.WriteLine($"Cached raw file: {filePath}, size: {fileData.Length} bytes");
            }
        }

        /// <summary>
        /// Get cached raw file data if valid
        /// </summary>
        public byte[] GetCachedRawFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            lock (_lockObject)
            {
                if (_rawFileCache.TryGetValue(filePath, out byte[] cachedData))
                {
                    if (File.Exists(filePath))
                    {
                        DateTime currentTimestamp = File.GetLastWriteTime(filePath);
                        if (_fileTimestamps.TryGetValue(filePath, out DateTime cachedTimestamp)
                            && currentTimestamp <= cachedTimestamp)
                        {
                            RawFileCacheHits++;
                            return cachedData;
                        }
                    }
                    else
                    {
                        // File doesn't exist anymore, but we have cached data
                        // Return it anyway and let the caller decide what to do
                        RawFileCacheHits++;
                        return cachedData;
                    }
                }

                CacheMisses++;
                return null;
            }
        }

        #endregion

        #region Parsed Entries Caching

        /// <summary>
        /// Cache the parsed string entries
        /// </summary>
        public void CacheParsedEntries(string filePath, List<StringEntry> entries)
        {
            if (string.IsNullOrEmpty(filePath) || entries == null)
                return;

            lock (_lockObject)
            {
                // Create a deep copy to avoid reference issues
                _parsedEntriesCache[filePath] = entries.Select(e => new StringEntry(e.HashId, e.Text, e.Mangled)).ToList();

                // Update timestamp if needed
                if (!_fileTimestamps.ContainsKey(filePath) && File.Exists(filePath))
                {
                    _fileTimestamps[filePath] = File.GetLastWriteTime(filePath);
                }

                Console.WriteLine($"Cached parsed entries: {filePath}, count: {entries.Count}");
            }
        }

        /// <summary>
        /// Get cached parsed entries if valid, with optimized timestamp checking
        /// </summary>
        public List<StringEntry> GetCachedParsedEntries(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            lock (_lockObject)
            {
                if (_parsedEntriesCache.TryGetValue(filePath, out List<StringEntry> entries))
                {
                    // Only check file timestamp if file exists
                    if (File.Exists(filePath))
                    {
                        if (_fileTimestamps.TryGetValue(filePath, out DateTime cachedTimestamp))
                        {
                            // Only check actual file timestamp if needed
                            DateTime currentTimestamp = File.GetLastWriteTime(filePath);
                            if (currentTimestamp <= cachedTimestamp)
                            {
                                ParsedEntriesCacheHits++;
                                // Return a deep copy to prevent modification of cached data
                                return entries.Select(e => new StringEntry(e.HashId, e.Text, e.Mangled)).ToList();
                            }
                        }
                    }
                    else
                    {
                        // File doesn't exist anymore but we have cached entries
                        // Return them anyway and let the caller decide what to do
                        ParsedEntriesCacheHits++;
                        return entries.Select(e => new StringEntry(e.HashId, e.Text, e.Mangled)).ToList();
                    }
                }

                CacheMisses++;
                return null;
            }
        }

        #endregion

        #region Search Results Caching

        /// <summary>
        /// Cache search results with LRU tracking and improved key normalization
        /// </summary>
        public void CacheSearchResults(string query, string searchBy, List<StringEntry> results)
        {
            // Don't cache empty results or very large result sets
            if (results == null || results.Count == 0 || results.Count > 5000)
                return;

            lock (_lockObject)
            {
                // Normalize the cache key to handle null or empty values
                string normalizedQuery = query?.ToLower() ?? "";
                string normalizedSearchBy = searchBy?.ToLower() ?? "text";
                string cacheKey = $"{normalizedQuery}|{normalizedSearchBy}";

                // Cache a copy of the results
                _searchCache[cacheKey] = results.Select(e => new StringEntry(e.HashId, e.Text, e.Mangled)).ToList();

                // Update LRU tracking
                if (_searchCacheKeys.Contains(cacheKey))
                    _searchCacheKeys.Remove(cacheKey);

                _searchCacheKeys.Add(cacheKey);

                // Enforce cache size limit with LRU eviction
                while (_searchCacheKeys.Count > MAX_SEARCH_CACHE_SIZE)
                {
                    string oldestKey = _searchCacheKeys[0];
                    _searchCacheKeys.RemoveAt(0);
                    _searchCache.Remove(oldestKey);
                }

                Console.WriteLine($"Cached search results: {cacheKey}, count: {results.Count}");
            }
        }

        /// <summary>
        /// Get cached search results if available, with improved key normalization
        /// </summary>
        public List<StringEntry> GetCachedSearchResults(string query, string searchBy)
        {
            lock (_lockObject)
            {
                // Normalize the cache key to handle null or empty values
                string normalizedQuery = query?.ToLower() ?? "";
                string normalizedSearchBy = searchBy?.ToLower() ?? "text";
                string cacheKey = $"{normalizedQuery}|{normalizedSearchBy}";

                if (_searchCache.TryGetValue(cacheKey, out List<StringEntry> results))
                {
                    // Update LRU tracking
                    _searchCacheKeys.Remove(cacheKey);
                    _searchCacheKeys.Add(cacheKey);

                    SearchCacheHits++;
                    return results.Select(e => new StringEntry(e.HashId, e.Text, e.Mangled)).ToList();
                }

                CacheMisses++;
                return null;
            }
        }

        #endregion

        #region Decoded Text Caching

        /// <summary>
        /// Cache decoded text string
        /// </summary>
        public void CacheDecodedText(uint hashId, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            lock (_lockObject)
            {
                _decodedTextCache[hashId] = text;
            }
        }

        /// <summary>
        /// Get cached decoded text if available
        /// </summary>
        public string GetCachedDecodedText(uint hashId)
        {
            lock (_lockObject)
            {
                if (_decodedTextCache.TryGetValue(hashId, out string text))
                {
                    DecodedTextCacheHits++;
                    return text;
                }

                return null;
            }
        }

        #endregion

        #region View Model Caching

        /// <summary>
        /// Cache view model
        /// </summary>
        public void CacheViewModel(uint hashId, StringEntryViewModel viewModel)
        {
            if (viewModel == null)
                return;

            lock (_lockObject)
            {
                _viewModelCache[hashId] = viewModel;
            }
        }

        /// <summary>
        /// Get cached view model if available
        /// </summary>
        public StringEntryViewModel GetCachedViewModel(uint hashId)
        {
            lock (_lockObject)
            {
                if (_viewModelCache.TryGetValue(hashId, out StringEntryViewModel viewModel))
                {
                    ViewModelCacheHits++;
                    return viewModel;
                }

                return null;
            }
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Clear raw file cache for a specific file
        /// </summary>
        public void ClearRawCache(string filePath = null)
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    _rawFileCache.Clear();
                    Console.WriteLine("Cleared all raw file caches");
                }
                else if (_rawFileCache.ContainsKey(filePath))
                {
                    _rawFileCache.Remove(filePath);
                    Console.WriteLine($"Cleared raw file cache for: {filePath}");
                }
            }
        }

        /// <summary>
        /// Clear parsed entries cache for a specific file
        /// </summary>
        public void ClearParsedEntriesCache(string filePath = null)
        {
            lock (_lockObject)
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    _parsedEntriesCache.Clear();
                    Console.WriteLine("Cleared all parsed entries caches");
                }
                else if (_parsedEntriesCache.ContainsKey(filePath))
                {
                    _parsedEntriesCache.Remove(filePath);
                    Console.WriteLine($"Cleared parsed entries cache for: {filePath}");
                }
            }
        }

        /// <summary>
        /// Clear search cache
        /// </summary>
        public void ClearSearchCache()
        {
            lock (_lockObject)
            {
                _searchCache.Clear();
                _searchCacheKeys.Clear();
                Console.WriteLine("Cleared search cache");
            }
        }

        /// <summary>
        /// Clear decoded text cache
        /// </summary>
        public void ClearDecodedTextCache()
        {
            lock (_lockObject)
            {
                _decodedTextCache.Clear();
                Console.WriteLine("Cleared decoded text cache");
            }
        }

        /// <summary>
        /// Clear view model cache
        /// </summary>
        public void ClearViewModelCache()
        {
            lock (_lockObject)
            {
                _viewModelCache.Clear();
                Console.WriteLine("Cleared view model cache");
            }
        }

        /// <summary>
        /// Clear all caches
        /// </summary>
        public void ClearAllCaches()
        {
            lock (_lockObject)
            {
                _rawFileCache.Clear();
                _parsedEntriesCache.Clear();
                _searchCache.Clear();
                _searchCacheKeys.Clear();
                _decodedTextCache.Clear();
                _viewModelCache.Clear();
                _fileTimestamps.Clear();

                // Reset statistics
                RawFileCacheHits = 0;
                ParsedEntriesCacheHits = 0;
                SearchCacheHits = 0;
                DecodedTextCacheHits = 0;
                ViewModelCacheHits = 0;
                CacheMisses = 0;

                Console.WriteLine("Cleared all caches");
            }
        }

        /// <summary>
        /// Clear cache for a specific file
        /// </summary>
        public void ClearCache(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            lock (_lockObject)
            {
                ClearRawCache(filePath);
                ClearParsedEntriesCache(filePath);
                ClearSearchCache(); // Search cache is per-query, so clear all
                ClearViewModelCache(); // View models could be from any file

                // Remove from timestamps
                if (_fileTimestamps.ContainsKey(filePath))
                    _fileTimestamps.Remove(filePath);

                Console.WriteLine($"Cleared all caches for file: {filePath}");
            }
        }

        /// <summary>
        /// Get cache statistics as string
        /// </summary>
        public string GetCacheStats()
        {
            lock (_lockObject)
            {
                return $"Cache Statistics:\n" +
                       $"- Raw File Cache Hits: {RawFileCacheHits}\n" +
                       $"- Parsed Entries Cache Hits: {ParsedEntriesCacheHits}\n" +
                       $"- Search Cache Hits: {SearchCacheHits}\n" +
                       $"- Decoded Text Cache Hits: {DecodedTextCacheHits}\n" +
                       $"- View Model Cache Hits: {ViewModelCacheHits}\n" +
                       $"- Cache Misses: {CacheMisses}\n" +
                       $"- Raw File Cache Size: {_rawFileCache.Count} files\n" +
                       $"- Parsed Entries Cache Size: {_parsedEntriesCache.Count} files\n" +
                       $"- Search Cache Size: {_searchCache.Count} queries\n" +
                       $"- Decoded Text Cache Size: {_decodedTextCache.Count} strings\n" +
                       $"- View Model Cache Size: {_viewModelCache.Count} view models";
            }
        }

        #endregion
    }
}