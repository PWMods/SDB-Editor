using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SDBEditor.Models;

namespace SDBEditor.Handlers
{
    public class SharelistEntry : INotifyPropertyChanged
    {
        private uint _hashId;
        private string _text;
        private string _description;
        private DateTime _dateAdded;
        private bool _isNewAddition;

        public uint HashId
        {
            get => _hashId;
            set
            {
                if (_hashId != value)
                {
                    _hashId = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HexValue));
                }
            }
        }

        public string HexValue => HashId.ToString("X");

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime DateAdded
        {
            get => _dateAdded;
            set
            {
                if (_dateAdded != value)
                {
                    _dateAdded = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsNewAddition
        {
            get => _isNewAddition;
            set
            {
                if (_isNewAddition != value)
                {
                    _isNewAddition = value;
                    OnPropertyChanged();
                }
            }
        }

        public SharelistEntry(uint hashId, string text, string description = null, DateTime? dateAdded = null, bool isNewAddition = false)
        {
            HashId = hashId;
            Text = text ?? string.Empty;
            Description = description ?? text ?? string.Empty;
            DateAdded = dateAdded ?? DateTime.Now;
            IsNewAddition = isNewAddition;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SharelistMetadata
    {
        public string Author { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string TwitterHandle { get; set; }
        public string PatreonUrl { get; set; }
        public string DiscordUrl { get; set; }
        public string WebsiteUrl { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public List<SharelistUpdate> Updates { get; set; } = new List<SharelistUpdate>();

        public SharelistMetadata()
        {
            CreatedDate = DateTime.Now;
            ModifiedDate = DateTime.Now;
            Updates = new List<SharelistUpdate>();
        }
    }

    public class SharelistUpdate
    {
        public DateTime UpdateDate { get; set; }
        public string VersionTag { get; set; }
        public int EntriesAdded { get; set; }
        public int EntriesModified { get; set; }
        public int EntriesRemoved { get; set; }
        public string UpdateNotes { get; set; }

        public SharelistUpdate()
        {
            UpdateDate = DateTime.Now;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Update {VersionTag} - {UpdateDate:yyyy-MM-dd}");

            if (EntriesAdded > 0)
                sb.AppendLine($"- Added {EntriesAdded} entries");

            if (EntriesModified > 0)
                sb.AppendLine($"- Modified {EntriesModified} entries");

            if (EntriesRemoved > 0)
                sb.AppendLine($"- Removed {EntriesRemoved} entries");

            if (!string.IsNullOrEmpty(UpdateNotes))
                sb.AppendLine(UpdateNotes);

            return sb.ToString();
        }
    }

    /// <summary>
    /// Custom collection that prevents duplicate entries based on HashId
    /// </summary>
    public class UniqueSharelistCollection : ObservableCollection<SharelistEntry>
    {
        private HashSet<uint> _hashIds = new HashSet<uint>();

        protected override void InsertItem(int index, SharelistEntry item)
        {
            if (item != null && !_hashIds.Contains(item.HashId))
            {
                _hashIds.Add(item.HashId);
                base.InsertItem(index, item);
            }
            else
            {
                Console.WriteLine($"Prevented duplicate entry with HashId: {item?.HashId}");
            }
        }

        protected override void RemoveItem(int index)
        {
            if (index >= 0 && index < Count)
            {
                _hashIds.Remove(this[index].HashId);
            }
            base.RemoveItem(index);
        }

        protected override void ClearItems()
        {
            _hashIds.Clear();
            base.ClearItems();
        }

        protected override void SetItem(int index, SharelistEntry item)
        {
            if (index >= 0 && index < Count)
            {
                _hashIds.Remove(this[index].HashId);
            }

            if (item != null && !_hashIds.Contains(item.HashId))
            {
                _hashIds.Add(item.HashId);
                base.SetItem(index, item);
            }
        }

        /// <summary>
        /// Check if the collection contains an entry with the specified HashId
        /// </summary>
        public bool ContainsHashId(uint hashId)
        {
            return _hashIds.Contains(hashId);
        }

        /// <summary>
        /// Get entry by HashId
        /// </summary>
        public SharelistEntry GetByHashId(uint hashId)
        {
            return this.FirstOrDefault(e => e.HashId == hashId);
        }
    }

    public class SharelistManager
    {
        private static SharelistManager _instance;
        public static SharelistManager Instance => _instance ??= new SharelistManager();

        public UniqueSharelistCollection Entries { get; }
        public SharelistMetadata CurrentMetadata { get; set; }

        // Collection to track original entries when updating an existing sharelist
        private List<uint> _originalEntryHashIds = new List<uint>();

        private const string FILE_SIGNATURE = "SHARESDB_V2.0"; // Updated version for metadata support
        private const string LEGACY_SIGNATURE = "SHARESDB_V1.0";

        private SharelistManager()
        {
            Entries = new UniqueSharelistCollection();
            CurrentMetadata = new SharelistMetadata();
        }

        public void AddEntry(uint hashId, string text, string description = null, bool updateIfExists = false)
        {
            // Check if already exists
            var existingEntry = Entries.GetByHashId(hashId);
            if (existingEntry != null)
            {
                if (updateIfExists)
                {
                    // Update the existing entry
                    existingEntry.Text = text;
                    existingEntry.Description = description ?? text;
                    Console.WriteLine($"Updated existing entry with HashId: {hashId}");
                }
                else
                {
                    Console.WriteLine($"Skipped duplicate entry with HashId: {hashId}");
                }
                return;
            }

            // Mark as new addition if we're updating an existing sharelist
            bool isNewAddition = _originalEntryHashIds.Count > 0 && !_originalEntryHashIds.Contains(hashId);

            Entries.Add(new SharelistEntry(hashId, text, description, DateTime.Now, isNewAddition));
        }

        public void AddEntries(IEnumerable<StringEntry> entries)
        {
            foreach (var entry in entries)
            {
                AddEntry(entry.HashId, entry.Text);
            }
        }

        public void RemoveEntry(SharelistEntry entry)
        {
            Entries.Remove(entry);
        }

        public void Clear()
        {
            Entries.Clear();
            _originalEntryHashIds.Clear();
            CurrentMetadata = new SharelistMetadata();
        }

        /// <summary>
        /// Merge entries with duplicate prevention
        /// </summary>
        public (int added, int updated, int skipped) MergeEntries(IEnumerable<SharelistEntry> entriesToMerge, bool updateExisting = false)
        {
            int added = 0;
            int updated = 0;
            int skipped = 0;

            foreach (var entry in entriesToMerge)
            {
                var existingEntry = Entries.GetByHashId(entry.HashId);

                if (existingEntry != null)
                {
                    if (updateExisting)
                    {
                        existingEntry.Text = entry.Text;
                        existingEntry.Description = entry.Description;
                        existingEntry.DateAdded = entry.DateAdded;
                        updated++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                else
                {
                    Entries.Add(entry);
                    added++;
                }
            }

            return (added, updated, skipped);
        }

        // Method to prepare for updating an existing sharelist
        public void PrepareForUpdate()
        {
            // Store all current hash IDs to track what's original vs new
            _originalEntryHashIds = Entries.Select(e => e.HashId).ToList();

            // Reset IsNewAddition flag on all existing entries
            foreach (var entry in Entries)
            {
                entry.IsNewAddition = false;
            }
        }

        // Method to finalize an update and generate changelog
        public SharelistUpdate FinalizeUpdate(string versionTag, string updateNotes = "")
        {
            var update = new SharelistUpdate
            {
                VersionTag = versionTag,
                UpdateNotes = updateNotes,
                EntriesAdded = Entries.Count(e => e.IsNewAddition),
                // Could also track modifications but we don't have that info right now
                EntriesModified = 0,
                EntriesRemoved = 0
            };

            // Update version
            if (!string.IsNullOrEmpty(versionTag))
            {
                CurrentMetadata.Version = versionTag;
            }

            // Update the modified date
            CurrentMetadata.ModifiedDate = DateTime.Now;

            // Add the update to metadata
            CurrentMetadata.Updates.Add(update);

            // Generate update information for description
            string updateInfo = GenerateUpdateInfo();

            // Append to description if it exists, otherwise create new
            if (!string.IsNullOrEmpty(CurrentMetadata.Description))
            {
                CurrentMetadata.Description += Environment.NewLine + Environment.NewLine + updateInfo;
            }
            else
            {
                CurrentMetadata.Description = updateInfo;
            }

            return update;
        }

        // Helper method to generate update information for the description
        private string GenerateUpdateInfo()
        {
            StringBuilder sb = new StringBuilder();

            if (CurrentMetadata.Updates.Count > 0)
            {
                var latestUpdate = CurrentMetadata.Updates.Last();

                sb.AppendLine("--- LATEST UPDATE ---");
                sb.AppendLine(latestUpdate.ToString());
            }

            return sb.ToString();
        }

        // Synchronous version - kept for backward compatibility
        public bool ExportToFile(string filePath, SharelistMetadata metadata = null)
        {
            try
            {
                if (metadata == null)
                    metadata = CurrentMetadata;

                using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // File signature and version
                    writer.WriteLine(FILE_SIGNATURE);
                    writer.WriteLine($"# WWE 2K SDB Sharelist");
                    writer.WriteLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                    // Metadata section
                    writer.WriteLine();
                    writer.WriteLine("## METADATA ##");
                    writer.WriteLine($"Author: {metadata.Author ?? "Anonymous"}");
                    writer.WriteLine($"Version: {metadata.Version ?? "1.0"}");

                    // Handle multi-line descriptions
                    if (!string.IsNullOrEmpty(metadata.Description))
                    {
                        // Split description into lines
                        string[] descLines = metadata.Description.Split(new[] { Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.None);
                        foreach (string descLine in descLines)
                        {
                            writer.WriteLine($"Description: {descLine}");
                        }
                    }
                    else
                    {
                        writer.WriteLine($"Description: No description provided");
                    }

                    writer.WriteLine($"Created: {metadata.CreatedDate:yyyy-MM-dd}");
                    writer.WriteLine($"Modified: {DateTime.Now:yyyy-MM-dd}");

                    // Social/Support Links
                    if (!string.IsNullOrWhiteSpace(metadata.TwitterHandle))
                        writer.WriteLine($"Twitter: {metadata.TwitterHandle}");
                    if (!string.IsNullOrWhiteSpace(metadata.PatreonUrl))
                        writer.WriteLine($"Patreon: {metadata.PatreonUrl}");
                    if (!string.IsNullOrWhiteSpace(metadata.DiscordUrl))
                        writer.WriteLine($"Discord: {metadata.DiscordUrl}");
                    if (!string.IsNullOrWhiteSpace(metadata.WebsiteUrl))
                        writer.WriteLine($"Website: {metadata.WebsiteUrl}");

                    // New: Write update history if available
                    if (metadata.Updates != null && metadata.Updates.Count > 0)
                    {
                        writer.WriteLine("## UPDATES ##");
                        foreach (var update in metadata.Updates)
                        {
                            writer.WriteLine($"UpdateDate: {update.UpdateDate:yyyy-MM-dd}");
                            writer.WriteLine($"VersionTag: {update.VersionTag}");
                            writer.WriteLine($"EntriesAdded: {update.EntriesAdded}");
                            writer.WriteLine($"EntriesModified: {update.EntriesModified}");
                            writer.WriteLine($"EntriesRemoved: {update.EntriesRemoved}");

                            if (!string.IsNullOrEmpty(update.UpdateNotes))
                            {
                                string[] noteLines = update.UpdateNotes.Split(new[] { Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.None);
                                foreach (string noteLine in noteLines)
                                {
                                    writer.WriteLine($"UpdateNotes: {noteLine}");
                                }
                            }
                        }
                        writer.WriteLine("## END UPDATES ##");
                    }

                    writer.WriteLine("## END METADATA ##");
                    writer.WriteLine();

                    // Entry count
                    writer.WriteLine($"# Total Entries: {Entries.Count}");
                    writer.WriteLine();

                    // Entries - now with date added information as a comment
                    foreach (var entry in Entries.OrderBy(e => e.HashId))
                    {
                        string newFlag = entry.IsNewAddition ? " [NEW]" : "";
                        writer.WriteLine($"HashID: {entry.HashId}, {entry.Description}{newFlag} # Added {entry.DateAdded:yyyy-MM-dd}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export sharelist: {ex.Message}");
                return false;
            }
        }

        // Async version - for better UI responsiveness
        public async Task<bool> ExportToFileAsync(string filePath, SharelistMetadata metadata = null)
        {
            try
            {
                if (metadata == null)
                    metadata = CurrentMetadata;

                await Task.Run(() => {
                    using (StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8))
                    {
                        // File signature and version
                        writer.WriteLine(FILE_SIGNATURE);
                        writer.WriteLine($"# WWE 2K SDB Sharelist");
                        writer.WriteLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                        // Metadata section
                        writer.WriteLine();
                        writer.WriteLine("## METADATA ##");
                        writer.WriteLine($"Author: {metadata.Author ?? "Anonymous"}");
                        writer.WriteLine($"Version: {metadata.Version ?? "1.0"}");

                        // Handle multi-line descriptions
                        if (!string.IsNullOrEmpty(metadata.Description))
                        {
                            // Split description into lines
                            string[] descLines = metadata.Description.Split(new[] { Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.None);
                            foreach (string descLine in descLines)
                            {
                                writer.WriteLine($"Description: {descLine}");
                            }
                        }
                        else
                        {
                            writer.WriteLine($"Description: No description provided");
                        }

                        writer.WriteLine($"Created: {metadata.CreatedDate:yyyy-MM-dd}");
                        writer.WriteLine($"Modified: {DateTime.Now:yyyy-MM-dd}");

                        // Social/Support Links
                        if (!string.IsNullOrWhiteSpace(metadata.TwitterHandle))
                            writer.WriteLine($"Twitter: {metadata.TwitterHandle}");
                        if (!string.IsNullOrWhiteSpace(metadata.PatreonUrl))
                            writer.WriteLine($"Patreon: {metadata.PatreonUrl}");
                        if (!string.IsNullOrWhiteSpace(metadata.DiscordUrl))
                            writer.WriteLine($"Discord: {metadata.DiscordUrl}");
                        if (!string.IsNullOrWhiteSpace(metadata.WebsiteUrl))
                            writer.WriteLine($"Website: {metadata.WebsiteUrl}");

                        // New: Write update history if available
                        if (metadata.Updates != null && metadata.Updates.Count > 0)
                        {
                            writer.WriteLine("## UPDATES ##");
                            foreach (var update in metadata.Updates)
                            {
                                writer.WriteLine($"UpdateDate: {update.UpdateDate:yyyy-MM-dd}");
                                writer.WriteLine($"VersionTag: {update.VersionTag}");
                                writer.WriteLine($"EntriesAdded: {update.EntriesAdded}");
                                writer.WriteLine($"EntriesModified: {update.EntriesModified}");
                                writer.WriteLine($"EntriesRemoved: {update.EntriesRemoved}");

                                if (!string.IsNullOrEmpty(update.UpdateNotes))
                                {
                                    string[] noteLines = update.UpdateNotes.Split(new[] { Environment.NewLine, "\n", "\r\n" }, StringSplitOptions.None);
                                    foreach (string noteLine in noteLines)
                                    {
                                        writer.WriteLine($"UpdateNotes: {noteLine}");
                                    }
                                }
                            }
                            writer.WriteLine("## END UPDATES ##");
                        }

                        writer.WriteLine("## END METADATA ##");
                        writer.WriteLine();

                        // Entry count
                        writer.WriteLine($"# Total Entries: {Entries.Count}");
                        writer.WriteLine();

                        // Entries - now with date added information as a comment
                        foreach (var entry in Entries.OrderBy(e => e.HashId))
                        {
                            string newFlag = entry.IsNewAddition ? " [NEW]" : "";
                            writer.WriteLine($"HashID: {entry.HashId}, {entry.Description}{newFlag} # Added {entry.DateAdded:yyyy-MM-dd}");
                        }
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export sharelist: {ex.Message}");
                return false;
            }
        }

        // Synchronous version with duplicate checking
        public (int imported, SharelistMetadata metadata) ImportFromFile(string filePath, bool clearBeforeImport = false)
        {
            int imported = 0;
            int skipped = 0;
            SharelistMetadata metadata = new SharelistMetadata();
            bool isValidFormat = false;
            bool inMetadataSection = false;
            bool inUpdatesSection = false;
            SharelistUpdate currentUpdate = null;
            string currentUpdateNotes = null;

            try
            {
                if (clearBeforeImport)
                {
                    Clear();
                }

                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

                if (lines.Length == 0)
                    return (imported: 0, metadata: metadata);

                // Check format version
                string firstLine = lines[0].Trim();
                if (firstLine == FILE_SIGNATURE || firstLine == LEGACY_SIGNATURE)
                {
                    isValidFormat = true;
                    Console.WriteLine($"Detected {firstLine} format file");
                }
                else
                {
                    // Check for legacy format
                    if (lines[0].TrimStart().StartsWith("#") || lines[0].Contains("HashID:"))
                    {
                        Console.WriteLine("Warning: File doesn't have SHARESDB signature, attempting legacy import");
                        isValidFormat = true;
                    }
                }

                if (!isValidFormat)
                {
                    throw new InvalidOperationException("Invalid file format. Expected .sharesdb or compatible text format.");
                }

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    // Skip empty lines and signature
                    if (string.IsNullOrWhiteSpace(trimmedLine) ||
                        trimmedLine == FILE_SIGNATURE ||
                        trimmedLine == LEGACY_SIGNATURE)
                        continue;

                    // Handle metadata section
                    if (trimmedLine == "## METADATA ##")
                    {
                        inMetadataSection = true;
                        continue;
                    }
                    else if (trimmedLine == "## END METADATA ##")
                    {
                        inMetadataSection = false;
                        continue;
                    }

                    // Handle updates section
                    if (trimmedLine == "## UPDATES ##")
                    {
                        inUpdatesSection = true;
                        inMetadataSection = false;
                        continue;
                    }
                    else if (trimmedLine == "## END UPDATES ##")
                    {
                        inUpdatesSection = false;

                        // Add the last update if we have one in progress
                        if (currentUpdate != null && !metadata.Updates.Contains(currentUpdate))
                        {
                            if (!string.IsNullOrEmpty(currentUpdateNotes))
                            {
                                currentUpdate.UpdateNotes = currentUpdateNotes;
                            }
                            metadata.Updates.Add(currentUpdate);
                            currentUpdate = null;
                            currentUpdateNotes = null;
                        }
                        continue;
                    }

                    if (inMetadataSection)
                    {
                        ParseMetadataLine(trimmedLine, metadata);
                        continue;
                    }

                    if (inUpdatesSection)
                    {
                        if (trimmedLine.StartsWith("UpdateDate:"))
                        {
                            // If we encounter a new update date, store the previous update if exists
                            if (currentUpdate != null && !metadata.Updates.Contains(currentUpdate))
                            {
                                if (!string.IsNullOrEmpty(currentUpdateNotes))
                                {
                                    currentUpdate.UpdateNotes = currentUpdateNotes;
                                }
                                metadata.Updates.Add(currentUpdate);
                            }

                            // Start a new update
                            currentUpdate = new SharelistUpdate();
                            currentUpdateNotes = null;

                            // Parse the date
                            string dateValue = trimmedLine.Substring("UpdateDate:".Length).Trim();
                            if (DateTime.TryParse(dateValue, out DateTime updateDate))
                            {
                                currentUpdate.UpdateDate = updateDate;
                            }
                        }
                        else if (currentUpdate != null)
                        {
                            // Parse other update properties
                            if (trimmedLine.StartsWith("VersionTag:"))
                            {
                                currentUpdate.VersionTag = trimmedLine.Substring("VersionTag:".Length).Trim();
                            }
                            else if (trimmedLine.StartsWith("EntriesAdded:"))
                            {
                                string value = trimmedLine.Substring("EntriesAdded:".Length).Trim();
                                if (int.TryParse(value, out int entriesAdded))
                                {
                                    currentUpdate.EntriesAdded = entriesAdded;
                                }
                            }
                            else if (trimmedLine.StartsWith("EntriesModified:"))
                            {
                                string value = trimmedLine.Substring("EntriesModified:".Length).Trim();
                                if (int.TryParse(value, out int entriesModified))
                                {
                                    currentUpdate.EntriesModified = entriesModified;
                                }
                            }
                            else if (trimmedLine.StartsWith("EntriesRemoved:"))
                            {
                                string value = trimmedLine.Substring("EntriesRemoved:".Length).Trim();
                                if (int.TryParse(value, out int entriesRemoved))
                                {
                                    currentUpdate.EntriesRemoved = entriesRemoved;
                                }
                            }
                            else if (trimmedLine.StartsWith("UpdateNotes:"))
                            {
                                string noteLine = trimmedLine.Substring("UpdateNotes:".Length).Trim();
                                if (string.IsNullOrEmpty(currentUpdateNotes))
                                {
                                    currentUpdateNotes = noteLine;
                                }
                                else
                                {
                                    currentUpdateNotes += Environment.NewLine + noteLine;
                                }
                            }
                        }

                        continue;
                    }

                    // Skip comments
                    if (trimmedLine.StartsWith("#"))
                        continue;

                    // Parse entries
                    if (line.Contains("HashID:") && line.Contains(","))
                    {
                        try
                        {
                            // Handle the [NEW] flag if present
                            bool isNewAddition = line.Contains("[NEW]");

                            // Remove the [NEW] flag before parsing
                            string parsableLine = line.Replace("[NEW]", "");

                            // Check for date added comment
                            DateTime dateAdded = DateTime.Now;
                            if (parsableLine.Contains("# Added "))
                            {
                                int commentIndex = parsableLine.IndexOf("# Added ");
                                string dateStr = parsableLine.Substring(commentIndex + 8).Trim();
                                if (DateTime.TryParse(dateStr, out DateTime parsedDate))
                                {
                                    dateAdded = parsedDate;
                                }
                                parsableLine = parsableLine.Substring(0, commentIndex);
                            }

                            string[] parts = parsableLine.Split(new[] { "HashID:", "," }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 1)
                            {
                                string hashIdStr = parts[0].Trim();
                                string description = parts.Length > 1 ? parts[1].Trim() : "";

                                if (uint.TryParse(hashIdStr, out uint hashId))
                                {
                                    // CHECK FOR DUPLICATES HERE
                                    if (!Entries.ContainsHashId(hashId))
                                    {
                                        Entries.Add(new SharelistEntry(hashId, description, description, dateAdded, isNewAddition));
                                        imported++;
                                    }
                                    else
                                    {
                                        skipped++;
                                        Console.WriteLine($"Skipped duplicate entry with HashId: {hashId}");
                                    }
                                }
                            }
                        }
                        catch
                        {
                            Console.WriteLine($"Skipped malformed line: {line}");
                        }
                    }
                }

                // Add the last update if there's one in progress
                if (currentUpdate != null && !metadata.Updates.Contains(currentUpdate))
                {
                    if (!string.IsNullOrEmpty(currentUpdateNotes))
                    {
                        currentUpdate.UpdateNotes = currentUpdateNotes;
                    }
                    metadata.Updates.Add(currentUpdate);
                }

                // Log import results
                Console.WriteLine($"Import complete: {imported} entries imported, {skipped} duplicates skipped");

                // After loading all entries, store the hash IDs for tracking updates
                _originalEntryHashIds = Entries.Select(e => e.HashId).ToList();

                // Store the imported metadata
                CurrentMetadata = metadata;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to import sharelist: {ex.Message}");
                throw;
            }

            return (imported: imported, metadata: metadata);
        }

        // Async version - for better UI responsiveness
        public async Task<(int imported, SharelistMetadata metadata)> ImportFromFileAsync(string filePath, bool clearBeforeImport = false)
        {
            return await Task.Run(() => ImportFromFile(filePath, clearBeforeImport));
        }

        private void ParseMetadataLine(string line, SharelistMetadata metadata)
        {
            if (line.Contains(":"))
            {
                int colonIndex = line.IndexOf(':');
                string key = line.Substring(0, colonIndex).Trim();
                string value = line.Substring(colonIndex + 1).Trim();

                switch (key.ToLower())
                {
                    case "author":
                        metadata.Author = value;
                        break;
                    case "version":
                        metadata.Version = value;
                        break;
                    case "description":
                        // Check if this is an additional line for an existing description
                        if (!string.IsNullOrEmpty(metadata.Description))
                            metadata.Description += Environment.NewLine + value;
                        else
                            metadata.Description = value;
                        break;
                    case "twitter":
                        metadata.TwitterHandle = value;
                        break;
                    case "patreon":
                        metadata.PatreonUrl = value;
                        break;
                    case "discord":
                        metadata.DiscordUrl = value;
                        break;
                    case "website":
                        metadata.WebsiteUrl = value;
                        break;
                    case "created":
                        if (DateTime.TryParse(value, out DateTime created))
                            metadata.CreatedDate = created;
                        break;
                    case "modified":
                        if (DateTime.TryParse(value, out DateTime modified))
                            metadata.ModifiedDate = modified;
                        break;
                }
            }
        }

        public static SharelistMetadata GetFileMetadata(string filePath)
        {
            var metadata = new SharelistMetadata();
            bool inMetadataSection = false;
            bool inUpdatesSection = false;
            SharelistUpdate currentUpdate = null;
            string currentUpdateNotes = null;

            try
            {
                var lines = File.ReadAllLines(filePath);

                foreach (var line in lines)
                {
                    string trimmedLine = line.Trim();

                    if (trimmedLine == "## METADATA ##")
                    {
                        inMetadataSection = true;
                        continue;
                    }
                    else if (trimmedLine == "## END METADATA ##")
                    {
                        inMetadataSection = false;
                        continue;
                    }

                    // Handle updates section
                    if (trimmedLine == "## UPDATES ##")
                    {
                        inUpdatesSection = true;
                        inMetadataSection = false;
                        continue;
                    }
                    else if (trimmedLine == "## END UPDATES ##")
                    {
                        inUpdatesSection = false;

                        // Add the last update if we have one in progress
                        if (currentUpdate != null && !metadata.Updates.Contains(currentUpdate))
                        {
                            if (!string.IsNullOrEmpty(currentUpdateNotes))
                            {
                                currentUpdate.UpdateNotes = currentUpdateNotes;
                            }
                            metadata.Updates.Add(currentUpdate);
                            currentUpdate = null;
                            currentUpdateNotes = null;
                        }

                        break; // We have all metadata, no need to read further
                    }

                    if (inMetadataSection)
                    {
                        Instance.ParseMetadataLine(trimmedLine, metadata);
                    }

                    if (inUpdatesSection)
                    {
                        if (trimmedLine.StartsWith("UpdateDate:"))
                        {
                            // If we encounter a new update date, store the previous update if exists
                            if (currentUpdate != null && !metadata.Updates.Contains(currentUpdate))
                            {
                                if (!string.IsNullOrEmpty(currentUpdateNotes))
                                {
                                    currentUpdate.UpdateNotes = currentUpdateNotes;
                                }
                                metadata.Updates.Add(currentUpdate);
                            }

                            // Start a new update
                            currentUpdate = new SharelistUpdate();
                            currentUpdateNotes = null;

                            // Parse the date
                            string dateValue = trimmedLine.Substring("UpdateDate:".Length).Trim();
                            if (DateTime.TryParse(dateValue, out DateTime updateDate))
                            {
                                currentUpdate.UpdateDate = updateDate;
                            }
                        }
                        else if (currentUpdate != null)
                        {
                            // Parse other update properties
                            if (trimmedLine.StartsWith("VersionTag:"))
                            {
                                currentUpdate.VersionTag = trimmedLine.Substring("VersionTag:".Length).Trim();
                            }
                            else if (trimmedLine.StartsWith("EntriesAdded:"))
                            {
                                string value = trimmedLine.Substring("EntriesAdded:".Length).Trim();
                                if (int.TryParse(value, out int entriesAdded))
                                {
                                    currentUpdate.EntriesAdded = entriesAdded;
                                }
                            }
                            else if (trimmedLine.StartsWith("EntriesModified:"))
                            {
                                string value = trimmedLine.Substring("EntriesModified:".Length).Trim();
                                if (int.TryParse(value, out int entriesModified))
                                {
                                    currentUpdate.EntriesModified = entriesModified;
                                }
                            }
                            else if (trimmedLine.StartsWith("EntriesRemoved:"))
                            {
                                string value = trimmedLine.Substring("EntriesRemoved:".Length).Trim();
                                if (int.TryParse(value, out int entriesRemoved))
                                {
                                    currentUpdate.EntriesRemoved = entriesRemoved;
                                }
                            }
                            else if (trimmedLine.StartsWith("UpdateNotes:"))
                            {
                                string noteLine = trimmedLine.Substring("UpdateNotes:".Length).Trim();
                                if (string.IsNullOrEmpty(currentUpdateNotes))
                                {
                                    currentUpdateNotes = noteLine;
                                }
                                else
                                {
                                    currentUpdateNotes += Environment.NewLine + noteLine;
                                }
                            }
                        }
                    }
                }

                // Add the last update if there's one in progress
                if (currentUpdate != null && !metadata.Updates.Contains(currentUpdate))
                {
                    if (!string.IsNullOrEmpty(currentUpdateNotes))
                    {
                        currentUpdate.UpdateNotes = currentUpdateNotes;
                    }
                    metadata.Updates.Add(currentUpdate);
                }
            }
            catch
            {
                // Return default metadata on error
            }

            return metadata;
        }

        // Async version of GetFileMetadata
        public static async Task<SharelistMetadata> GetFileMetadataAsync(string filePath)
        {
            return await Task.Run(() => GetFileMetadata(filePath));
        }

        public bool CreateBackup(string backupDirectory = null)
        {
            try
            {
                if (string.IsNullOrEmpty(backupDirectory))
                {
                    backupDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "SDBEditor",
                        "SharelistBackups"
                    );
                }

                Directory.CreateDirectory(backupDirectory);

                string backupFile = Path.Combine(
                    backupDirectory,
                    $"sharelist_backup_{DateTime.Now:yyyyMMdd_HHmmss}.sharesdb"
                );

                return ExportToFile(backupFile, CurrentMetadata);
            }
            catch
            {
                return false;
            }
        }

        // Async version of CreateBackup
        public async Task<bool> CreateBackupAsync(string backupDirectory = null)
        {
            try
            {
                if (string.IsNullOrEmpty(backupDirectory))
                {
                    backupDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "SDBEditor",
                        "SharelistBackups"
                    );
                }

                await Task.Run(() => Directory.CreateDirectory(backupDirectory));

                string backupFile = Path.Combine(
                    backupDirectory,
                    $"sharelist_backup_{DateTime.Now:yyyyMMdd_HHmmss}.sharesdb"
                );

                return await ExportToFileAsync(backupFile, CurrentMetadata);
            }
            catch
            {
                return false;
            }
        }

        // Method to generate a version suggestion based on the current version
        public string SuggestNextVersion()
        {
            string currentVersion = CurrentMetadata.Version ?? "1.0";

            // Try to parse the version
            if (Version.TryParse(currentVersion, out Version version))
            {
                // Increment the minor version
                return $"{version.Major}.{version.Minor + 1}";
            }

            // If we can't parse as a standard version, look for simple patterns
            if (currentVersion.Contains("."))
            {
                string[] parts = currentVersion.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1], out int minorVersion))
                {
                    parts[parts.Length - 1] = (minorVersion + 1).ToString();
                    return string.Join(".", parts);
                }
            }

            // If all else fails, just append .1
            return currentVersion + ".1";
        }
    }
}