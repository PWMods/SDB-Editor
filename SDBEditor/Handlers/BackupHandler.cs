using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SDBEditor.Handlers
{
    /// <summary>
    /// Represents a single backup entry
    /// </summary>
    public class BackupEntry
    {
        public string FilePath { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }

        public BackupEntry(string filePath, DateTime timestamp, string description)
        {
            FilePath = filePath;
            Timestamp = timestamp;
            Description = description;
        }
    }

    /// <summary>
    /// Handles the creation and management of backups
    /// </summary>
    public class BackupHandler
    {
        private string _backupDir;
        private int _maxBackups;
        private List<BackupEntry> _backupList;

        public BackupHandler(string backupDir = "Rollback", int maxBackups = 5)
        {
            _backupDir = backupDir;
            _maxBackups = maxBackups;
            _backupList = new List<BackupEntry>();

            // Ensure backup directory exists
            Directory.CreateDirectory(_backupDir);

            // Load backup list
            LoadBackupList();
        }

        /// <summary>
        /// Load the list of available backups
        /// </summary>
        private void LoadBackupList()
        {
            _backupList.Clear();

            if (Directory.Exists(_backupDir))
            {
                foreach (string filePath in Directory.GetFiles(_backupDir, "*.backup"))
                {
                    string fileName = Path.GetFileName(filePath);
                    DateTime timestamp = File.GetCreationTime(filePath);
                    string description = GetBackupDescription(fileName);

                    _backupList.Add(new BackupEntry(filePath, timestamp, description));
                }
            }

            // Sort backups by timestamp (newest first)
            _backupList = _backupList.OrderByDescending(b => b.Timestamp).ToList();
        }

        /// <summary>
        /// Extract description from backup filename
        /// </summary>
        private string GetBackupDescription(string fileName)
        {
            try
            {
                string[] parts = fileName.Split('.');
                if (parts.Length >= 3)
                {
                    return parts[1];
                }
                return "Automatic backup";
            }
            catch
            {
                return "Unknown backup";
            }
        }

        /// <summary>
        /// Create a new backup of the given file
        /// </summary>
        public bool CreateBackup(string filePath, string description = "")
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"File does not exist: {filePath}");
                    return false;
                }

                // Generate backup filename
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = Path.GetFileName(filePath);
                string backupName = $"{filename}.{description ?? "backup"}.{timestamp}.backup";
                string backupPath = Path.Combine(_backupDir, backupName);

                // Create backup
                File.Copy(filePath, backupPath, true);

                // Update backup list
                LoadBackupList();

                // Enforce maximum number of backups
                CleanupOldBackups();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create backup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restore a file from a backup
        /// </summary>
        public bool RestoreBackup(string backupPath, string targetPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                {
                    Console.WriteLine($"Backup file does not exist: {backupPath}");
                    return false;
                }

                // Create backup of current state before restoring
                CreateBackup(targetPath, "pre_restore");

                // Restore the backup
                File.Copy(backupPath, targetPath, true);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to restore backup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the most recent backup
        /// </summary>
        public BackupEntry GetLatestBackup()
        {
            return _backupList.FirstOrDefault();
        }

        /// <summary>
        /// Get a list of all available backups
        /// </summary>
        public List<BackupEntry> GetBackupList()
        {
            return _backupList;
        }

        /// <summary>
        /// Find a backup by date
        /// </summary>
        public BackupEntry GetBackupByDate(DateTime date)
        {
            return _backupList.FirstOrDefault(b => b.Timestamp.Date == date.Date);
        }

        /// <summary>
        /// Remove old backups exceeding the maximum limit
        /// </summary>
        private void CleanupOldBackups()
        {
            while (_backupList.Count > _maxBackups)
            {
                BackupEntry oldestBackup = _backupList.Last();
                try
                {
                    File.Delete(oldestBackup.FilePath);
                    _backupList.Remove(oldestBackup);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to remove old backup {oldestBackup.FilePath}: {ex.Message}");
                    break;
                }
            }
        }

        /// <summary>
        /// Delete a specific backup
        /// </summary>
        public bool DeleteBackup(string backupPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                    LoadBackupList();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to delete backup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete all backups
        /// </summary>
        public bool ClearAllBackups()
        {
            try
            {
                foreach (var backup in _backupList)
                {
                    File.Delete(backup.FilePath);
                }
                _backupList.Clear();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to clear backups: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Export a backup to another location
        /// </summary>
        public bool ExportBackup(string backupPath, string exportPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, exportPath, true);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to export backup: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Import a backup from another location
        /// </summary>
        public bool ImportBackup(string importPath, string description = "")
        {
            try
            {
                if (!File.Exists(importPath))
                {
                    return false;
                }

                // Generate new backup name
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = Path.GetFileName(importPath);
                string backupName = $"{filename}.{description ?? "imported"}.{timestamp}.backup";
                string backupPath = Path.Combine(_backupDir, backupName);

                // Import the backup
                File.Copy(importPath, backupPath, true);
                LoadBackupList();
                CleanupOldBackups();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to import backup: {ex.Message}");
                return false;
            }
        }
    }
}