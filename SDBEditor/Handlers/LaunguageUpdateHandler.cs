using System;
using System.Collections.Generic;
using System.Linq;
using SDBEditor.Models;

namespace SDBEditor.Handlers
{
    /// <summary>
    /// Extension methods and utilities for handling language updates
    /// </summary>
    public static class LanguageUpdateHandler
    {
        /// <summary>
        /// Update language detection for SDBHandler with proper UI refresh
        /// </summary>
        public static void UpdateLanguageDetection(this SDBHandler sdbHandler, string filePath, MetadataHandler metadataHandler)
        {
            try
            {
                Console.WriteLine($"\n[LanguageUpdateHandler] Updating language detection for: {filePath}");

                // Get current state
                string oldLanguage = sdbHandler.Language;
                string oldGame = sdbHandler.GameName;

                // Get strings for metadata generation
                var strings = sdbHandler.GetAllStrings();

                // Use MetadataHandler for detection (filename-based)
                var (detectedGame, detectedLanguage) = metadataHandler.DetectGameInfo(filePath, strings);

                // Update SDBHandler if needed
                bool updated = false;

                if (sdbHandler.Language != detectedLanguage && detectedLanguage != "Unknown Language")
                {
                    sdbHandler.Language = detectedLanguage;
                    updated = true;
                    Console.WriteLine($"[LanguageUpdateHandler] Language updated: {oldLanguage} -> {detectedLanguage}");
                }

                if (sdbHandler.GameName != detectedGame && detectedGame != "Unknown Game")
                {
                    sdbHandler.GameName = detectedGame;
                    updated = true;
                    Console.WriteLine($"[LanguageUpdateHandler] Game updated: {oldGame} -> {detectedGame}");
                }

                // If still unknown, try to detect from filename pattern
                if (sdbHandler.Language == "Unknown Language")
                {
                    string fallbackLanguage = DetectLanguageFromFilenameFallback(filePath);
                    if (!string.IsNullOrEmpty(fallbackLanguage))
                    {
                        sdbHandler.Language = fallbackLanguage;
                        updated = true;
                        Console.WriteLine($"[LanguageUpdateHandler] Language set from fallback: {fallbackLanguage}");
                    }
                }

                // Trigger UI update if needed
                if (updated)
                {
                    OnLanguageUpdated?.Invoke(sdbHandler, new LanguageUpdateEventArgs
                    {
                        OldLanguage = oldLanguage,
                        NewLanguage = sdbHandler.Language,
                        OldGame = oldGame,
                        NewGame = sdbHandler.GameName,
                        FilePath = filePath
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LanguageUpdateHandler] Error updating language: {ex.Message}");
            }
        }

        /// <summary>
        /// Force update language without content detection
        /// </summary>
        public static void ForceUpdateLanguage(this SDBHandler sdbHandler, string language, string gameName = null)
        {
            string oldLanguage = sdbHandler.Language;
            string oldGame = sdbHandler.GameName;

            sdbHandler.Language = language;
            if (!string.IsNullOrEmpty(gameName))
            {
                sdbHandler.GameName = gameName;
            }

            Console.WriteLine($"[LanguageUpdateHandler] Forced update: Language={language}, Game={gameName ?? oldGame}");

            // Trigger UI update
            OnLanguageUpdated?.Invoke(sdbHandler, new LanguageUpdateEventArgs
            {
                OldLanguage = oldLanguage,
                NewLanguage = language,
                OldGame = oldGame,
                NewGame = sdbHandler.GameName,
                FilePath = sdbHandler.CurrentFile
            });
        }

        /// <summary>
        /// Fallback language detection from filename patterns
        /// </summary>
        private static string DetectLanguageFromFilenameFallback(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            string filename = System.IO.Path.GetFileNameWithoutExtension(filePath).ToUpper();

            // Extended language list with common variations
            var languagePatterns = new Dictionary<string, string[]>
            {
                { "ENG", new[] { "ENG", "ENGLISH", "EN", "US", "UK" } },
                { "FRA", new[] { "FRA", "FRENCH", "FR", "FRANCAIS" } },
                { "GER", new[] { "GER", "GERMAN", "DE", "DEU", "DEUTSCH" } },
                { "ITA", new[] { "ITA", "ITALIAN", "IT", "ITALIANO" } },
                { "SPA", new[] { "SPA", "SPANISH", "ES", "ESP", "ESPANOL" } },
                { "ARA", new[] { "ARA", "ARABIC", "AR" } },
                { "JPN", new[] { "JPN", "JAPANESE", "JP", "JA" } },
                { "KOR", new[] { "KOR", "KOREAN", "KO", "KR" } },
                { "CHN", new[] { "CHN", "CHINESE", "ZH", "CN" } },
                { "RUS", new[] { "RUS", "RUSSIAN", "RU" } },
                { "POR", new[] { "POR", "PORTUGUESE", "PT", "BR" } },
                { "DUT", new[] { "DUT", "DUTCH", "NL", "NED" } }
            };

            foreach (var lang in languagePatterns)
            {
                if (lang.Value.Any(pattern => filename.Contains(pattern)))
                {
                    return lang.Key;
                }
            }

            return null;
        }

        /// <summary>
        /// Event raised when language is updated
        /// </summary>
        public static event EventHandler<LanguageUpdateEventArgs> OnLanguageUpdated;
    }

    /// <summary>
    /// Event arguments for language updates
    /// </summary>
    public class LanguageUpdateEventArgs : EventArgs
    {
        public string OldLanguage { get; set; }
        public string NewLanguage { get; set; }
        public string OldGame { get; set; }
        public string NewGame { get; set; }
        public string FilePath { get; set; }
    }

    /// <summary>
    /// Add these methods to your MainForm or UI class
    /// </summary>
    public static class UIUpdateExtensions
    {
        /// <summary>
        /// Subscribe to language updates and refresh UI
        /// Call this in your form's constructor
        /// </summary>
        public static void SetupLanguageUpdateHandling(this System.Windows.Forms.Form mainForm,
            System.Windows.Forms.Label gameLabel,
            System.Windows.Forms.Label languageLabel,
            System.Windows.Forms.Label titleLabel = null)
        {
            LanguageUpdateHandler.OnLanguageUpdated += (sender, e) =>
            {
                // Update UI on the main thread
                mainForm.BeginInvoke(new Action(() =>
                {
                    if (gameLabel != null)
                    {
                        gameLabel.Text = $"Game: {e.NewGame}";
                    }

                    if (languageLabel != null)
                    {
                        languageLabel.Text = $"Language: {e.NewLanguage}";
                    }

                    if (titleLabel != null)
                    {
                        titleLabel.Text = $"SDB Editor - {e.NewGame} - {e.NewLanguage}";
                    }

                    // Force UI refresh
                    mainForm.Refresh();

                    Console.WriteLine($"[UI] Updated display: Game={e.NewGame}, Language={e.NewLanguage}");
                }));
            };
        }

        /// <summary>
        /// Manually trigger a language re-detection
        /// </summary>
        public static void RefreshLanguageDetection(this System.Windows.Forms.Form mainForm,
            SDBHandler sdbHandler,
            MetadataHandler metadataHandler)
        {
            if (sdbHandler?.CurrentFile != null)
            {
                sdbHandler.UpdateLanguageDetection(sdbHandler.CurrentFile, metadataHandler);
            }
        }
    }
}