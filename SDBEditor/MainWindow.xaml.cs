using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SDBEditor.Handlers;
using SDBEditor.Models;
using SDBEditor.ViewModels;
using SDBEditor.Views;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using static SDBEditor.Views.RollbackDialog.NewSdbDialog;
using static SDBEditor.Views.RollbackDialog.NewSdbDialog.EnhancedMergeSdbDialog;
using SharelistEntry = SDBEditor.Handlers.SharelistEntry;
using SDBEditor.Handlers;
using static SDBEditor.Views.RollbackDialog;

namespace SDBEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SDBHandler _sdbHandler;
        private MetadataHandler _metadataHandler;
        private BackupHandler _backupHandler;
        private ObservableCollection<StringEntryViewModel> _stringsCollection;
        private string _currentFile;

        // Fields for performance improvements
        private bool _initialAnimationPlayed = false;
        private string _lastSearchQuery = string.Empty;
        private DispatcherTimer _searchTimer;
        private CancellationTokenSource _searchCancellationTokenSource; // Added for search cancellation

        // UI virtualization fields
        private const int VIRTUALIZATION_CHUNK_SIZE = 100;
        private int _lastFirstVisibleItem = 0;
        private int _lastLastVisibleItem = 0;
        private DispatcherTimer _scrollTimer;
        private bool _isDraggingScrollbar = false;

        // Status bar animation fields
        private Storyboard _statusBarFadeIn;
        private bool _statusBarVisible = true;

        // Store the path of the dropped file
        private string _droppedFilePath;
        private string _droppedFileType;

        public MainWindow()
        {
            InitializeComponent();

            // ✅ Initialize your SDB label mapping BEFORE anything UI binds to it
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "metadata", "CharProfileTable.json");
            SdbFunctionEntryMapper.Initialize(jsonPath);

            // Configure UI for better Unicode display
            ConfigureUnicodeSupport();

            // Initialize handlers
            _sdbHandler = new SDBHandler();
            _metadataHandler = new MetadataHandler();
            _backupHandler = new BackupHandler();

            // ADD THIS: Setup automatic language detection updates
            SetupLanguageUpdateHandling();

            // Initialize observable collection for DataGrid
            _stringsCollection = new ObservableCollection<StringEntryViewModel>();
            StringsDataGrid.ItemsSource = _stringsCollection;

            InitializeSharelist();
            // Initialize flag for scrollbar dragging
            _isDraggingScrollbar = false;

            // Hide game info panel initially - will be shown when file is loaded
            GameInfoPanel.Visibility = Visibility.Collapsed;

            // Load the SDB logo
            LoadSdbLogo();

            // Connect selection changed event for animation with the new extended handler
            StringsDataGrid.SelectionChanged += StringsDataGrid_SelectionChanged_Extended;

            // Initialize search timer for debouncing
            _searchTimer = new DispatcherTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(300); // 300ms delay
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                PerformSearch();
            };

            // Initialize search cancellation token source
            _searchCancellationTokenSource = new CancellationTokenSource();

            // Initialize scroll timer for debouncing scroll events
            _scrollTimer = new DispatcherTimer();
            _scrollTimer.Interval = TimeSpan.FromMilliseconds(5); // Reduced to 5ms for more responsive scrolling
            _scrollTimer.Tick += (s, e) =>
            {
                _scrollTimer.Stop();
                UpdateVisibleRows(false);
            };

            // DataGrid direct events to improve scrolling
            StringsDataGrid.LoadingRow += (s, e) =>
            {
                if (_isDraggingScrollbar)
                {
                    // Force immediate row loading during scrollbar dragging
                    var row = e.Row;
                    row.InvalidateVisual();
                    row.UpdateLayout();
                }
            };

            // Optimize DataGrid for large datasets
            OptimizeDataGridForLargeLists();

            // Initialize improved scrolling
            InitializeImprovedScrolling();

            // Load last file if available
            LoadLastFileIfAvailable();

            // Setup status bar animations
            SetupStatusBarAnimations();

            // Set default search type to Text (instead of Index)
            if (SearchTypeComboBox != null && SearchTypeComboBox.Items.Count > 0)
            {
                SearchTypeComboBox.SelectedIndex = 0; // Text is now the first option
            }
        }


        /// <summary>
        /// Fixes "Unknown Language" by detecting from filename
        /// Add this method to your MainWindow class
        /// </summary>
        private void FixUnknownLanguage()
        {
            if (_sdbHandler != null && _sdbHandler.Language == "Unknown Language" && !string.IsNullOrEmpty(_currentFile))
            {
                // Get filename without extension
                string filename = Path.GetFileNameWithoutExtension(_currentFile).ToUpper();

                Console.WriteLine($"[FixUnknownLanguage] Checking filename: '{filename}'");

                // List of supported languages
                string[] languages = { "ENG", "FRA", "GER", "ITA", "SPA", "ARA", "JPN", "KOR", "CHN", "RUS", "POR", "DUT" };

                // Check for exact match first
                if (languages.Contains(filename))
                {
                    _sdbHandler.Language = filename;
                    UpdateLanguageUI(filename);
                    Console.WriteLine($"[FixUnknownLanguage] Exact match found: {filename}");
                    return;
                }

                // Check if filename contains language code
                foreach (string lang in languages)
                {
                    if (filename.Contains(lang))
                    {
                        _sdbHandler.Language = lang;
                        UpdateLanguageUI(lang);
                        Console.WriteLine($"[FixUnknownLanguage] Language found in filename: {lang}");
                        return;
                    }
                }

                Console.WriteLine("[FixUnknownLanguage] No language found in filename");
            }
        }

        /// <summary>
        /// Updates the UI with the detected language
        /// </summary>
        private void UpdateLanguageUI(string language)
        {
            // Update the game info text
            if (GameExtraInfoText != null)
            {
                GameExtraInfoText.Text = $", Language: {language}";
            }

            // Update window title
            if (_sdbHandler.GameName != "Unknown Game")
            {
                this.Title = $"SDB Editor - {_sdbHandler.GameName} - {language}";
            }

            // Update status
            UpdateStatus($"Language detected: {language}");

            // Force UI refresh
            this.UpdateLayout();
        }

        private void CopyMultipleHashMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (StringsDataGrid?.SelectedItems != null && StringsDataGrid.SelectedItems.Count > 0)
                {
                    // Collect all selected hash IDs
                    var hashIds = new List<string>();

                    foreach (var item in StringsDataGrid.SelectedItems)
                    {
                        if (item is StringEntryViewModel selectedItem)
                        {
                            hashIds.Add(selectedItem.HashId.ToString());
                        }
                    }

                    if (hashIds.Count > 0)
                    {
                        // Join with commas
                        string hashIdList = string.Join(", ", hashIds);

                        bool success = ClipboardManager.SafeCopy(hashIdList);

                        if (success)
                        {
                            UpdateStatus($"Copied {hashIds.Count} hash IDs to clipboard");

                            // Optional: Show the first few IDs in the status
                            if (hashIds.Count <= 3)
                            {
                                UpdateStatus($"Copied hash IDs: {hashIdList}");
                            }
                            else
                            {
                                UpdateStatus($"Copied {hashIds.Count} hash IDs: {string.Join(", ", hashIds.Take(3))}...");
                            }
                        }
                        else
                        {
                            // Retry with a small delay
                            Dispatcher.BeginInvoke(new Action(async () =>
                            {
                                await Task.Delay(100);
                                bool retrySuccess = await ClipboardManager.SafeCopyAsync(hashIdList);
                                UpdateStatus(retrySuccess
                                    ? $"Copied {hashIds.Count} hash IDs to clipboard"
                                    : "Failed to copy hash IDs - clipboard may be in use");
                            }));
                        }
                    }
                }
                else
                {
                    UpdateStatus("No items selected to copy hash IDs");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in copy multiple hash IDs operation: {ex.Message}");
                UpdateStatus("Error during copy operation");
            }
        }

        #region File Drag & Drop Functionality

        /// <summary>
        /// Handles the DragEnter event for file drag and drop
        /// </summary>
        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Get the files being dragged
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Check if any of the files is a supported type
                if (files.Any(f => IsValidDropFile(f)))
                {
                    // Allow drop
                    e.Effects = DragDropEffects.Copy;

                    // Show the drag drop overlay
                    DragDropOverlay.Visibility = Visibility.Visible;

                    // Update overlay title based on first supported file type
                    string firstValidFile = files.First(f => IsValidDropFile(f));
                    string extension = Path.GetExtension(firstValidFile).ToLower();

                    DragDropTitleTextBlock.Text = GetDragDropTitleFromExtension(extension);
                    DragDropDescriptionTextBlock.Text = GetDragDropDescriptionFromExtension(extension);

                    // Show/hide appropriate controls based on file type
                    UpdateDragDropOverlayForFileType(extension);
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        /// <summary>
        /// Handles the DragOver event for file drag and drop
        /// </summary>
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            // Just reuse the DragEnter logic
            Window_DragEnter(sender, e);
        }

        /// <summary>
        /// Checks if a file is valid for drag and drop
        /// </summary>
        private bool IsValidDropFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension == ".sdb" || extension == ".csv" || extension == ".xlsx" || extension == ".xls" || extension == ".sharesdb";
        }

        /// <summary>
        /// Gets appropriate title for the drag drop overlay based on file extension
        /// </summary>
        private string GetDragDropTitleFromExtension(string extension)
        {
            switch (extension)
            {
                case ".sdb":
                    return "SDB File Drop Detected";
                case ".csv":
                    return "CSV Import Options";
                case ".xlsx":
                case ".xls":
                    return "Excel Import Options";
                case ".sharesdb":
                    return "Sharelist File Detected";
                default:
                    return "File Drop Detected";
            }
        }

        /// <summary>
        /// Gets appropriate description for the drag drop overlay based on file extension
        /// </summary>
        private string GetDragDropDescriptionFromExtension(string extension)
        {
            switch (extension)
            {
                case ".sdb":
                    return "Drop to open the SDB file";
                case ".csv":
                case ".xlsx":
                case ".xls":
                    return "Drop the file to import strings";
                case ".sharesdb":
                    return "Drop to import sharelist";
                default:
                    return "Drop to process file";
            }
        }

        /// <summary>
        /// Updates the drag drop overlay UI based on the file type
        /// </summary>
        private void UpdateDragDropOverlayForFileType(string extension)
        {
            // Hide all optional panels first
            ImportOptionsPanel.Visibility = Visibility.Collapsed;
            RangeSelectionGrid.Visibility = Visibility.Collapsed;

            // Show appropriate panels based on file type
            if (extension == ".csv" || extension == ".xlsx" || extension == ".xls")
            {
                ImportOptionsPanel.Visibility = Visibility.Visible;
                if (LimitRangeCheckbox.IsChecked == true)
                {
                    RangeSelectionGrid.Visibility = Visibility.Visible;
                }
            }
            else if (extension == ".sharesdb")
            {
                // For .sharesdb files, we don't need the import options panel
                // The merge will happen directly after user confirmation
                ImportOptionsPanel.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Handles the Drop event for file drag and drop
        /// </summary>
        private void Window_Drop(object sender, DragEventArgs e)
        {
            // Hide the overlay initially - we may show it again for import files
            DragDropOverlay.Visibility = Visibility.Collapsed;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Get the first valid file in the drop
                string validFile = files.FirstOrDefault(f => IsValidDropFile(f));

                if (!string.IsNullOrEmpty(validFile))
                {
                    string extension = Path.GetExtension(validFile).ToLower();
                    _droppedFilePath = validFile;
                    _droppedFileType = extension;

                    // Process based on file type
                    switch (extension)
                    {
                        case ".sdb":
                            LoadSdbFile(validFile);
                            break;

                        case ".sharesdb":
                            // Check if an SDB file is loaded first
                            if (string.IsNullOrEmpty(_currentFile))
                            {
                                MessageBox.Show("Please open an SDB file first before merging a sharelist.",
                                               "No File Loaded",
                                               MessageBoxButton.OK,
                                               MessageBoxImage.Information);
                                return;
                            }

                            // Go directly to the sharelist info dialog
                            MergeFromSharelistFile(validFile);
                            break;

                        case ".csv":
                            // Check if an SDB file is loaded first
                            if (string.IsNullOrEmpty(_currentFile))
                            {
                                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }

                            // Show the overlay with import options
                            DragDropOverlay.Visibility = Visibility.Visible;
                            DragDropTitleTextBlock.Text = GetDragDropTitleFromExtension(extension);
                            DragDropDescriptionTextBlock.Text = GetDragDropDescriptionFromExtension(extension);
                            FileNameTextBlock.Text = $"File: {Path.GetFileName(validFile)}";

                            // Show appropriate controls
                            ImportOptionsPanel.Visibility = Visibility.Visible;
                            break;

                        case ".xlsx":
                        case ".xls":
                            // Check if an SDB file is loaded first
                            if (string.IsNullOrEmpty(_currentFile))
                            {
                                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }

                            // Use our new method to handle Excel files
                            HandleExcelFileDrop(validFile);
                            break;
                    }
                }
            }

            e.Handled = true;
        }

        /// <summary>
        /// Event handler for the range checkbox state changed
        /// </summary>
        private void LimitRangeCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (LimitRangeCheckbox.IsChecked == true)
            {
                RangeSelectionGrid.Visibility = Visibility.Visible;

                // If we have an Excel file, update UI with its info
                if (!string.IsNullOrEmpty(_droppedFilePath) &&
                    (_droppedFileType == ".xlsx" || _droppedFileType == ".xls"))
                {
                    UpdateUIForExcelFile(_droppedFilePath);
                }
            }
            else
            {
                RangeSelectionGrid.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Event handler for the cancel button
        /// </summary>
        private void CancelImportButton_Click(object sender, RoutedEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;
            _droppedFilePath = null;
            _droppedFileType = null;
        }

        /// <summary>
        /// Event handler for the import button
        /// </summary>
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_droppedFilePath))
            {
                MessageBox.Show("No file available for import.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (_droppedFileType == ".csv")
                {
                    // Process CSV file
                    if (LimitRangeCheckbox.IsChecked == true)
                    {
                        ProcessCsvWithRange(_droppedFilePath);
                    }
                    else
                    {
                        ProcessCsvFile(_droppedFilePath);
                    }
                }
                else if (_droppedFileType == ".xlsx" || _droppedFileType == ".xls")
                {
                    // Process Excel file with same options as CSV
                    if (LimitRangeCheckbox.IsChecked == true)
                    {
                        ProcessExcelWithRange(_droppedFilePath);
                    }
                    else
                    {
                        ProcessExcelFile(_droppedFilePath);
                    }
                }

                // Hide the overlay after processing
                DragDropOverlay.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during import: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupLanguageUpdateHandling()
        {
            // Subscribe to language update events
            LanguageUpdateHandler.OnLanguageUpdated += (sender, e) =>
            {
                // Update UI on the main thread
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Update the game info text
                    if (GameExtraInfoText != null)
                    {
                        GameExtraInfoText.Text = $", Language: {e.NewLanguage}";
                    }

                    // Update window title if needed
                    if (e.NewGame != "Unknown Game" && e.NewLanguage != "Unknown Language")
                    {
                        this.Title = $"SDB Editor - {e.NewGame} - {e.NewLanguage}";
                    }

                    // Force UI refresh
                    this.UpdateLayout();

                    Console.WriteLine($"[UI] Language updated from {e.OldLanguage} to {e.NewLanguage}");
                }));
            };
        }

        /// <summary>
        /// Process a CSV file with full import
        /// </summary>
        private void ProcessCsvFile(string filePath)
        {
            try
            {
                // Create backup before import
                if (!string.IsNullOrEmpty(_currentFile))
                {
                    _backupHandler.CreateBackup(_currentFile, "pre_csv_import");
                }

                Mouse.OverrideCursor = Cursors.Wait;

                // Read the CSV file
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

                if (lines.Length < 2) // Need at least a header and one data row
                {
                    MessageBox.Show("CSV file contains no data or is in an invalid format.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Find the text column (look for a column named "Text" or similar)
                string[] headers = ParseCsvLine(lines[0]);
                int textColumnIndex = -1;

                for (int i = 0; i < headers.Length; i++)
                {
                    if (headers[i].Contains("Text", StringComparison.OrdinalIgnoreCase))
                    {
                        textColumnIndex = i;
                        break;
                    }
                }

                // If no column with "Text" in the name is found, use the 3rd column (index 2) if available
                if (textColumnIndex == -1 && headers.Length > 2)
                {
                    textColumnIndex = 2;
                }
                // Otherwise use the last column
                else if (textColumnIndex == -1 && headers.Length > 0)
                {
                    textColumnIndex = headers.Length - 1;
                }

                if (textColumnIndex == -1)
                {
                    MessageBox.Show("Could not determine which column contains the text data.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Process each data row
                int importedCount = 0;
                int skippedCount = 0;
                int errorCount = 0;
                uint nextHashId = _sdbHandler.GenerateUniqueHashId();

                // Get existing texts to avoid duplicates
                HashSet<string> existingTexts = new HashSet<string>(
                    _sdbHandler.GetAllStrings()
                        .Where(e => e != null)
                        .Select(e => (e.Text ?? string.Empty).ToLower()));

                for (int i = 1; i < lines.Length; i++) // Skip header row
                {
                    try
                    {
                        string[] fields = ParseCsvLine(lines[i]);

                        if (fields.Length <= textColumnIndex)
                        {
                            skippedCount++;
                            continue;
                        }

                        string text = fields[textColumnIndex].Trim();

                        if (string.IsNullOrWhiteSpace(text) || text.ToLower() == "nan")
                        {
                            skippedCount++;
                            continue;
                        }

                        // Check for duplicates
                        if (!existingTexts.Contains(text.ToLower()))
                        {
                            if (_sdbHandler.AddString(text, nextHashId + (uint)importedCount))
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
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing CSV line {i}: {ex.Message}");
                        errorCount++;
                    }
                }

                // Refresh display
                DisplayStrings();

                UpdateStatus($"CSV Import completed: {importedCount} added, {skippedCount} skipped, {errorCount} errors");

                MessageBox.Show(
                    $"CSV Import completed successfully.\nNew entries: {importedCount}\nSkipped: {skippedCount}\nErrors: {errorCount}",
                    "Import Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process CSV file: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Process a CSV file with range constraints
        /// </summary>
        private void ProcessCsvWithRange(string filePath)
        {
            try
            {
                // Create backup before import
                if (!string.IsNullOrEmpty(_currentFile))
                {
                    _backupHandler.CreateBackup(_currentFile, "pre_csv_import_range");
                }

                Mouse.OverrideCursor = Cursors.Wait;

                // Read the CSV file
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

                if (lines.Length < 2) // Need at least a header and one data row
                {
                    MessageBox.Show("CSV file contains no data or is in an invalid format.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Parse the start and end row indices
                int startRow = 1; // Default to start after header
                int endRow = lines.Length - 1; // Default to last row

                if (!string.IsNullOrEmpty(StartRowTextBox.Text) && int.TryParse(StartRowTextBox.Text, out int parsedStartRow))
                {
                    startRow = parsedStartRow;
                }

                if (!string.IsNullOrEmpty(EndRowTextBox.Text) && EndRowTextBox.Text.ToLower() != "all")
                {
                    if (int.TryParse(EndRowTextBox.Text, out int parsedEndRow))
                    {
                        endRow = parsedEndRow;
                    }
                }

                // Ensure valid range
                startRow = Math.Max(1, startRow); // Don't start before the first data row
                endRow = Math.Min(endRow, lines.Length - 1); // Don't go beyond the last row

                if (startRow > endRow)
                {
                    MessageBox.Show("Invalid range: Start row is after end row.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Determine column to use based on selection
                int textColumnIndex = -1;
                string[] headers = ParseCsvLine(lines[0]);

                if (ColumnSelectionComboBox.SelectedIndex == 0) // Auto-detect
                {
                    // Find a column named "Text" or similar
                    for (int i = 0; i < headers.Length; i++)
                    {
                        if (headers[i].Contains("Text", StringComparison.OrdinalIgnoreCase))
                        {
                            textColumnIndex = i;
                            break;
                        }
                    }

                    // If no column with "Text" found, use the 3rd column (index 2) if available
                    if (textColumnIndex == -1 && headers.Length > 2)
                    {
                        textColumnIndex = 2;
                    }
                    // Otherwise use the last column
                    else if (textColumnIndex == -1 && headers.Length > 0)
                    {
                        textColumnIndex = headers.Length - 1;
                    }
                }
                else
                {
                    // User selected a specific column (indices 1-4 for columns 0-3)
                    textColumnIndex = ColumnSelectionComboBox.SelectedIndex - 1;

                    // Ensure the index is valid
                    if (textColumnIndex < 0 || textColumnIndex >= headers.Length)
                    {
                        MessageBox.Show("Selected column is not available in the CSV file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (textColumnIndex == -1)
                {
                    MessageBox.Show("Could not determine which column contains the text data.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Process data rows within the specified range
                int importedCount = 0;
                int skippedCount = 0;
                int errorCount = 0;
                uint nextHashId = _sdbHandler.GenerateUniqueHashId();

                // Get existing texts to avoid duplicates
                HashSet<string> existingTexts = new HashSet<string>(
                    _sdbHandler.GetAllStrings()
                        .Where(e => e != null)
                        .Select(e => (e.Text ?? string.Empty).ToLower()));

                for (int i = startRow; i <= endRow; i++)
                {
                    try
                    {
                        if (i >= lines.Length)
                        {
                            break; // Beyond the end of the file
                        }

                        string[] fields = ParseCsvLine(lines[i]);

                        if (fields.Length <= textColumnIndex)
                        {
                            skippedCount++;
                            continue;
                        }

                        string text = fields[textColumnIndex].Trim();

                        if (string.IsNullOrWhiteSpace(text) || text.ToLower() == "nan")
                        {
                            skippedCount++;
                            continue;
                        }

                        // Check for duplicates
                        if (!existingTexts.Contains(text.ToLower()))
                        {
                            if (_sdbHandler.AddString(text, nextHashId + (uint)importedCount))
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
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing CSV line {i}: {ex.Message}");
                        errorCount++;
                    }
                }

                // Refresh display
                DisplayStrings();

                UpdateStatus($"CSV Import (ranged) completed: {importedCount} added, {skippedCount} skipped, {errorCount} errors");

                MessageBox.Show(
                    $"CSV Import completed successfully.\nRange: {startRow}-{endRow}\nNew entries: {importedCount}\nSkipped: {skippedCount}\nErrors: {errorCount}",
                    "Import Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process CSV file: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Process an Excel file with full import
        /// </summary>
        private void ProcessExcelFile(string filePath)
        {
            try
            {
                // Create backup before import
                if (!string.IsNullOrEmpty(_currentFile))
                {
                    _backupHandler.CreateBackup(_currentFile, "pre_excel_import");
                }

                Mouse.OverrideCursor = Cursors.Wait;

                // Use the new method in SDBHandler to process Excel files
                int importedCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                if (_sdbHandler.ImportStringsFromExcel(filePath,
                    out importedCount, out skippedCount, out errorCount))
                {
                    // Refresh display
                    DisplayStrings();

                    UpdateStatus($"Excel Import completed: {importedCount} added, {skippedCount} skipped, {errorCount} errors");

                    MessageBox.Show(
                        $"Excel Import completed successfully.\nNew entries: {importedCount}\nSkipped: {skippedCount}\nErrors: {errorCount}",
                        "Import Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to import Excel file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process Excel file: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Process an Excel file with range constraints
        /// </summary>
        private void ProcessExcelWithRange(string filePath)
        {
            try
            {
                // Create backup before import
                if (!string.IsNullOrEmpty(_currentFile))
                {
                    _backupHandler.CreateBackup(_currentFile, "pre_excel_import_range");
                }

                Mouse.OverrideCursor = Cursors.Wait;

                // Parse the start and end row indices
                int startRow = 2; // Default to start after header (row 1)
                int endRow = 0; // Default to all rows

                if (!string.IsNullOrEmpty(StartRowTextBox.Text) && int.TryParse(StartRowTextBox.Text, out int parsedStartRow))
                {
                    startRow = parsedStartRow;
                }

                if (!string.IsNullOrEmpty(EndRowTextBox.Text) && EndRowTextBox.Text.ToLower() != "all")
                {
                    if (int.TryParse(EndRowTextBox.Text, out int parsedEndRow))
                    {
                        endRow = parsedEndRow;
                    }
                }

                // Get total row count to validate endRow
                int totalRows = ExcelHandler.GetExcelRowCount(filePath);
                if (endRow <= 0 || endRow > totalRows)
                {
                    endRow = totalRows;
                }

                // Ensure valid range
                startRow = Math.Max(2, startRow); // Don't start before the first data row

                if (startRow > endRow)
                {
                    MessageBox.Show("Invalid range: Start row is after end row.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Determine column to use based on selection
                int columnIndex = -1; // Default to auto-detect

                if (ColumnSelectionComboBox.SelectedIndex > 0) // First item is "Auto-detect"
                {
                    // Extract column index from selected item (format: "1: Column Name")
                    string selectedItem = ColumnSelectionComboBox.SelectedItem.ToString();
                    if (selectedItem.Contains(":"))
                    {
                        string indexPart = selectedItem.Split(':')[0].Trim();
                        if (int.TryParse(indexPart, out int index))
                        {
                            columnIndex = index - 1; // Convert from 1-based to 0-based
                        }
                    }
                    else
                    {
                        // Fall back to using selection index directly
                        columnIndex = ColumnSelectionComboBox.SelectedIndex - 1;
                    }
                }

                // Import with the specified range
                int importedCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                if (_sdbHandler.ImportStringsFromExcel(
                    filePath,
                    out importedCount, out skippedCount, out errorCount,
                    startRow, endRow, columnIndex))
                {
                    // Refresh display
                    DisplayStrings();

                    UpdateStatus($"Excel Import (ranged) completed: {importedCount} added, {skippedCount} skipped, {errorCount} errors");

                    MessageBox.Show(
                        $"Excel Import completed successfully.\nRange: {startRow}-{endRow}\nNew entries: {importedCount}\nSkipped: {skippedCount}\nErrors: {errorCount}",
                        "Import Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to import Excel file with range constraints.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to process Excel file: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Helper method to read an Excel file - Updated to use the ExcelHandler
        /// </summary>
        private List<List<object>> ReadExcelFile(string filePath)
        {
            try
            {
                // Use the new ExcelHandler to read the file
                return ExcelHandler.ReadExcelFileAsTable(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading Excel file: {ex.Message}");

                // Fallback to CSV reading for backward compatibility
                try
                {
                    var result = new List<List<object>>();
                    string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

                    foreach (var line in lines)
                    {
                        var row = new List<object>();
                        foreach (var field in ParseCsvLine(line))
                        {
                            row.Add(field);
                        }
                        result.Add(row);
                    }

                    return result;
                }
                catch
                {
                    throw; // Rethrow if even the fallback fails
                }
            }
        }

        /// <summary>
        /// Update ColumnSelectionComboBox with actual column names from Excel file
        /// </summary>
        private void UpdateColumnSelectionFromExcel(string filePath)
        {
            try
            {
                // Save current selection
                object currentSelection = ColumnSelectionComboBox.SelectedItem;

                // Clear existing items
                ColumnSelectionComboBox.Items.Clear();

                // Always add "Auto-detect" as the first option
                ColumnSelectionComboBox.Items.Add("Auto-detect");

                // Get column names from Excel
                var columnNames = ExcelHandler.GetExcelColumnNames(filePath);

                if (columnNames != null && columnNames.Count > 0)
                {
                    // Add actual column names with indices
                    for (int i = 0; i < columnNames.Count; i++)
                    {
                        ColumnSelectionComboBox.Items.Add($"{i + 1}: {columnNames[i]}");
                    }
                }
                else
                {
                    // Fall back to generic column names
                    ColumnSelectionComboBox.Items.Add("First Column");
                    ColumnSelectionComboBox.Items.Add("Second Column");
                    ColumnSelectionComboBox.Items.Add("Third Column");
                    ColumnSelectionComboBox.Items.Add("Fourth Column");
                }

                // Restore selection or default to first item
                if (currentSelection != null && ColumnSelectionComboBox.Items.Contains(currentSelection))
                {
                    ColumnSelectionComboBox.SelectedItem = currentSelection;
                }
                else
                {
                    ColumnSelectionComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update column selection: {ex.Message}");

                // Ensure the combo box has default items
                if (ColumnSelectionComboBox.Items.Count == 0)
                {
                    ColumnSelectionComboBox.Items.Add("Auto-detect");
                    ColumnSelectionComboBox.Items.Add("First Column");
                    ColumnSelectionComboBox.Items.Add("Second Column");
                    ColumnSelectionComboBox.Items.Add("Third Column");
                    ColumnSelectionComboBox.Items.Add("Fourth Column");
                    ColumnSelectionComboBox.SelectedIndex = 0;
                }
            }
        }

        /// <summary>
        /// Update the UI when an Excel file is dropped or selected
        /// </summary>
        private void UpdateUIForExcelFile(string filePath)
        {
            try
            {
                // Get row count to display in the UI
                int rowCount = ExcelHandler.GetExcelRowCount(filePath);

                // Set end row text to reflect the total rows
                if (rowCount > 0)
                {
                    EndRowTextBox.Text = rowCount.ToString();
                }
                else
                {
                    EndRowTextBox.Text = "All";
                }

                // Update the file name display
                FileNameTextBlock.Text = $"File: {Path.GetFileName(filePath)} ({rowCount} rows)";

                // Update column selection dropdown
                UpdateColumnSelectionFromExcel(filePath);

                // Default start row to 2 (after header)
                StartRowTextBox.Text = "2";

                // Show the import options panel
                ImportOptionsPanel.Visibility = Visibility.Visible;

                // Show range selection grid if checkbox is checked
                if (LimitRangeCheckbox.IsChecked == true)
                {
                    RangeSelectionGrid.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update UI for Excel file: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle Excel file dropping directly
        /// </summary>
        private void HandleExcelFileDrop(string filePath)
        {
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Store the file path for later processing
            _droppedFilePath = filePath;
            _droppedFileType = Path.GetExtension(filePath).ToLower();

            // Show the overlay with import options
            DragDropOverlay.Visibility = Visibility.Visible;
            DragDropTitleTextBlock.Text = GetDragDropTitleFromExtension(_droppedFileType);
            DragDropDescriptionTextBlock.Text = GetDragDropDescriptionFromExtension(_droppedFileType);

            // Update UI with Excel file information
            UpdateUIForExcelFile(filePath);
        }

        /// <summary>
        /// Helper method to parse CSV line with proper handling of quoted fields
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return new string[0];

            List<string> fields = new List<string>();
            bool inQuotes = false;
            StringBuilder field = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Escaped quote
                        field.Append('"');
                        i++; // Skip the next quote
                    }
                    else
                    {
                        // Toggle quote state
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // End of field
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else
                {
                    field.Append(c);
                }
            }

            // Add the last field
            fields.Add(field.ToString());

            return fields.ToArray();
        }

        #endregion

        /// <summary>
        /// Performs a complete reset of DataGrid state and all caches when switching files
        /// </summary>
        private void CompleteUIReset()
        {
            try
            {
                // Cancel any pending operations
                _searchCancellationTokenSource?.Cancel();
                _searchTimer?.Stop();
                _scrollTimer?.Stop();

                // Reset animation flags and search state
                _initialAnimationPlayed = false;
                _lastSearchQuery = string.Empty;

                // Reset virtualization state
                _lastFirstVisibleItem = 0;
                _lastLastVisibleItem = 0;
                _isDraggingScrollbar = false;

                // Reset selection
                StringsDataGrid.SelectedItem = null;

                // Force clear collection
                if (_stringsCollection != null)
                {
                    StringsDataGrid.ItemsSource = null;
                    _stringsCollection.Clear();
                    StringsDataGrid.ItemsSource = _stringsCollection;
                }

                // Clear search box
                if (SearchBox != null)
                {
                    SearchBox.Clear();
                }

                // Force UI update and layout refresh
                StringsDataGrid.UpdateLayout();

                // Force garbage collection to clear memory
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CompleteUIReset: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset handlers to ensure clean state when switching between files
        /// </summary>
        private void ResetHandlers()
        {
            // Create a new SDBHandler instance instead of trying to reset the existing one
            _sdbHandler = new SDBHandler();
        }

        /// <summary>
        /// Resets and reinitializes the DataGrid's scrolling behavior
        /// </summary>
        private void ResetScrollViewer()
        {
            try
            {
                var scrollViewer = GetScrollViewer(StringsDataGrid);
                if (scrollViewer != null)
                {
                    // Reset scroll position
                    scrollViewer.ScrollToVerticalOffset(0);
                    scrollViewer.ScrollToHorizontalOffset(0);

                    // Disable and re-enable virtualization to force reset
                    var panel = FindVisualChild<VirtualizingStackPanel>(scrollViewer);
                    if (panel != null)
                    {
                        bool wasVirtualizing = VirtualizingPanel.GetIsVirtualizing(panel);
                        VirtualizingPanel.SetIsVirtualizing(panel, false);

                        // Force layout update
                        panel.UpdateLayout();

                        // Restore virtualization
                        VirtualizingPanel.SetIsVirtualizing(panel, wasVirtualizing);
                    }

                    // Very important - this prevents recycled row issues
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // Force the DataGrid to completely re-measure rows
                        StringsDataGrid.Items.Refresh();
                        StringsDataGrid.UpdateLayout();
                    }), DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting ScrollViewer: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to adjust row heights for multi-line content
        /// </summary>
        private void AdjustRowHeightsForMultilineContent()
        {
            if (StringsDataGrid == null || StringsDataGrid.Items.Count == 0)
                return;

            try
            {
                // Get the visible range
                var visibleRange = GetVisibleRowRange();
                int firstVisibleIndex = visibleRange.Item1;
                int lastVisibleIndex = visibleRange.Item2;

                // Update row heights for visible items
                for (int i = firstVisibleIndex; i <= lastVisibleIndex; i++)
                {
                    if (i >= 0 && i < StringsDataGrid.Items.Count)
                    {
                        var item = StringsDataGrid.Items[i];
                        var row = (DataGridRow)StringsDataGrid.ItemContainerGenerator.ContainerFromItem(item);

                        if (row != null)
                        {
                            // Ensure row doesn't exceed maximum height
                            if (row.ActualHeight > 150)
                            {
                                row.Height = 150;
                            }

                            // Force the row to measure itself again
                            row.InvalidateMeasure();
                            row.UpdateLayout();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adjusting row heights: {ex.Message}");
            }
        }

        /// <summary>
        /// Attach event handlers directly to the DataGrid
        /// </summary>
        private void InitializeImprovedScrolling()
        {
            // Set up direct scrollbar tracking for improved dragging
            StringsDataGrid.Loaded += (s, e) =>
            {
                // Wait for UI to fully load before attaching events
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    // Setup both our custom handlers and built-in handlers
                    SetupScrollbarDragHandling();

                    // Force full initialization of all virtualization panels
                    ForceDataGridRefresh();

                    // Add direct event handler for row creation
                    var itemsPresenter = FindVisualChild<ItemsPresenter>(StringsDataGrid);
                    if (itemsPresenter != null)
                    {
                        var panel = VisualTreeHelper.GetChild(itemsPresenter, 0) as VirtualizingStackPanel;
                        if (panel != null)
                        {
                            // Force the panel into clean state
                            panel.InvalidateMeasure();
                            panel.UpdateLayout();
                        }
                    }
                }), DispatcherPriority.Loaded);
            };
        }

        /// <summary>
        /// Forces the DataGrid to completely refresh its display
        /// </summary>
        private void ForceDataGridRefresh()
        {
            try
            {
                // Save current selection
                var selectedItem = StringsDataGrid.SelectedItem;

                // Force ItemsSource refresh - this is the nuclear option
                var currentSource = StringsDataGrid.ItemsSource;
                StringsDataGrid.ItemsSource = null;
                StringsDataGrid.ItemsSource = currentSource;

                // Restore selection
                if (selectedItem != null)
                {
                    StringsDataGrid.SelectedItem = selectedItem;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ForceDataGridRefresh: {ex.Message}");
            }
        }

        /// <summary>
        /// Configure application for proper Unicode text rendering and display
        /// </summary>
        private void ConfigureUnicodeSupport()
        {
            try
            {
                // Register encoding provider to support more text encodings
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                // Set DataGrid to use fonts with excellent Unicode support
                StringsDataGrid.FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic");

                // Configure text rendering for better clarity with special characters
                TextOptions.SetTextFormattingMode(StringsDataGrid, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(StringsDataGrid, TextRenderingMode.ClearType);
                TextOptions.SetTextHintingMode(StringsDataGrid, TextHintingMode.Auto);

                // Ensure the text column has correct rendering options
                if (StringsDataGrid.Columns.Count >= 4)
                {
                    var textColumn = StringsDataGrid.Columns[3] as DataGridTextColumn;
                    if (textColumn != null && textColumn.ElementStyle == null)
                    {
                        var style = new Style(typeof(TextBlock));
                        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
                        style.Setters.Add(new Setter(TextBlock.FontFamilyProperty,
                            new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic")));
                        style.Setters.Add(new Setter(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal));
                        textColumn.ElementStyle = style;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error configuring Unicode support: {ex.Message}");
                // Continue even if Unicode configuration fails
            }
        }

        /// <summary>
        /// Formats a file path for display in the status bar
        /// </summary>
        private string FormatPathForDisplay(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return string.Empty;

            try
            {
                // Get the root directory name (last folder in path)
                string rootFolder = Path.GetFileName(Path.GetDirectoryName(filePath));

                // Get the filename
                string fileName = Path.GetFileName(filePath);

                // Format as "Root Folder: Filename.sdb"
                return $"Root Folder: {rootFolder} | File: {fileName}";
            }
            catch
            {
                // In case of any path parsing errors, just return the filename
                return Path.GetFileName(filePath);
            }
        }

        /// <summary>
        /// Setup the animations for the status bar (without hiding)
        /// </summary>
        private void SetupStatusBarAnimations()
        {
            try
            {
                // Get fade-in storyboard from resources, but we don't use fade-out anymore
                _statusBarFadeIn = (Storyboard)FindResource("StatusBarFadeIn");

                // Set the target
                Storyboard.SetTarget(_statusBarFadeIn, AnimatedStatusBar);

                // Start with visible status bar
                AnimatedStatusBar.Opacity = 1.0;
                _statusBarVisible = true;

                // No auto-hide timer anymore, as we want the status bar to always stay visible
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up status bar animations: {ex.Message}");
            }
        }

        /// <summary>
        /// Method to update status text (without hiding animation)
        /// </summary>
        public void UpdateStatus(string statusMessage)
        {
            try
            {
                // Update the text
                StatusText.Text = statusMessage;

                // Show status bar with animation if it's not visible
                if (!_statusBarVisible)
                {
                    ShowStatusBar();
                }

                // No scheduling of hiding - we want to keep it visible
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating status: {ex.Message}");
                StatusText.Text = statusMessage; // Fallback
            }
        }

        /// <summary>
        /// Show status bar with animation
        /// </summary>
        private void ShowStatusBar()
        {
            if (!_statusBarVisible)
            {
                try
                {
                    AnimatedStatusBar.BeginStoryboard(_statusBarFadeIn);
                    _statusBarVisible = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error showing status bar: {ex.Message}");
                    AnimatedStatusBar.Opacity = 1.0; // Fallback
                }
            }
        }

        /// <summary>
        /// Show cache statistics
        /// </summary>
        private void CacheStats_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                SDBCacheManager.Instance.GetCacheStats(),
                "Cache Statistics",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        #region Sharelist Management

        private void InitializeSharelist()
        {
            // Bind the sharelist to the UI
            SharelistItemsControl.ItemsSource = SharelistManager.Instance.Entries;

            // Update count when collection changes
            SharelistManager.Instance.Entries.CollectionChanged += (s, e) =>
            {
                UpdateSharelistCount();
            };

            UpdateSharelistCount();
        }

        private void UpdateSharelistCount()

        {

            int count = SharelistManager.Instance.Entries.Count;

            int newCount = SharelistManager.Instance.Entries.Count(e => e.IsNewAddition);

            SharelistCountText.Text = count.ToString();

            SharelistButtonBadgeText.Text = count.ToString();

            SharelistButtonBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Update the title to show new entries if any

            if (newCount > 0)

            {

                // Add a badge or indicator in the title

                var titlePanel = SharelistPanel.FindName("SharelistTitlePanel") as StackPanel;

                if (titlePanel != null)

                {

                    // Check if the new entries badge already exists

                    var existingBadge = titlePanel.Children.OfType<Border>()

                        .FirstOrDefault(b => b.Name == "NewEntriesBadge");

                    if (existingBadge == null)

                    {

                        // Create a new badge

                        Border newBadge = new Border

                        {

                            Name = "NewEntriesBadge",

                            Background = new SolidColorBrush(Color.FromRgb(255, 80, 80)),

                            CornerRadius = new CornerRadius(10),

                            Margin = new Thickness(10, 0, 0, 0),

                            Padding = new Thickness(8, 2, 8, 2),

                            VerticalAlignment = VerticalAlignment.Center

                        };

                        TextBlock badgeText = new TextBlock

                        {

                            Text = $"{newCount} new",

                            Foreground = Brushes.White,

                            FontWeight = FontWeights.Bold,

                            FontSize = 11

                        };

                        newBadge.Child = badgeText;

                        titlePanel.Children.Add(newBadge);

                    }

                    else

                    {

                        // Update existing badge

                        var badgeText = existingBadge.Child as TextBlock;

                        if (badgeText != null)

                        {

                            badgeText.Text = $"{newCount} new";

                        }

                        // Show/hide based on count

                        existingBadge.Visibility = newCount > 0 ? Visibility.Visible : Visibility.Collapsed;

                    }

                }

            }

        }

        private void ToggleSharelist_Click(object sender, RoutedEventArgs e)
        {
            if (SharelistPanel.Visibility == Visibility.Visible)
            {
                // Hide with animation
                var slideOut = new DoubleAnimation
                {
                    From = 0,
                    To = 350,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };

                var transform = new TranslateTransform();
                SharelistPanel.RenderTransform = transform;

                slideOut.Completed += (s, args) => SharelistPanel.Visibility = Visibility.Collapsed;
                transform.BeginAnimation(TranslateTransform.XProperty, slideOut);
            }
            else
            {
                // Show with animation
                SharelistPanel.Visibility = Visibility.Visible;

                var slideIn = new DoubleAnimation
                {
                    From = 350,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };

                var transform = new TranslateTransform();
                SharelistPanel.RenderTransform = transform;
                transform.BeginAnimation(TranslateTransform.XProperty, slideIn);
            }
        }

        private void CloseSharelist_Click(object sender, RoutedEventArgs e)
        {
            ToggleSharelist_Click(sender, e);
        }

        private void UpdateSharelist_Click(object sender, RoutedEventArgs e)
        {
            // Check if we have an existing sharelist loaded
            if (SharelistManager.Instance.Entries.Count == 0)
            {
                MessageBox.Show("No sharelist loaded. Please import a sharelist first.",
                               "No Sharelist",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
                return;
            }

            // Count new entries (those marked with IsNewAddition = true)
            int newEntriesCount = SharelistManager.Instance.Entries.Count(entry => entry.IsNewAddition);

            // Open the update dialog
            var updateDialog = new SharelistUpdateDialog(SharelistManager.Instance.CurrentMetadata, newEntriesCount)
            {
                Owner = this
            };

            if (updateDialog.ShowDialog() == true && updateDialog.UpdateAccepted)
            {
                // Create the update record
                var update = SharelistManager.Instance.FinalizeUpdate(updateDialog.NewVersion, updateDialog.UpdateNotes);

                // Show update confirmation
                MessageBox.Show(
                    $"Sharelist updated to version {updateDialog.NewVersion}.\n" +
                    $"Added {update.EntriesAdded} new entries.\n\n" +
                    $"Save the sharelist to keep these changes.",
                    "Update Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void AddToSharelist_Click(object sender, RoutedEventArgs e)
        {
            if (StringsDataGrid.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more strings to add to the sharelist.",
                               "No Selection",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
                return;
            }

            int addedCount = 0;
            foreach (var item in StringsDataGrid.SelectedItems)
            {
                if (item is StringEntryViewModel viewModel)
                {
                    SharelistManager.Instance.AddEntry(viewModel.HashId, viewModel.Text);
                    addedCount++;
                }
            }

            UpdateStatus($"Added {addedCount} items to sharelist");

            // Flash the sharelist button to draw attention
            var flashAnimation = new ColorAnimation
            {
                From = Colors.Green,
                To = Colors.White,
                Duration = TimeSpan.FromMilliseconds(300),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2)
            };

            var brush = new SolidColorBrush(Colors.White);
            SharelistButtonBadge.Background = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, flashAnimation);
        }

        private void RemoveFromSharelist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SharelistEntry entry)
            {
                SharelistManager.Instance.RemoveEntry(entry);
                UpdateStatus("Removed from sharelist");
            }
        }

        private void ExportSharelist_Click(object sender, RoutedEventArgs e)
        {
            if (SharelistManager.Instance.Entries.Count == 0)
            {
                MessageBox.Show("Sharelist is empty. Add some entries first.",
                               "Empty Sharelist",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
                return;
            }

            // Check if we have any new entries that need to be finalized in an update
            int newEntriesCount = SharelistManager.Instance.Entries.Count(entry => entry.IsNewAddition);
            if (newEntriesCount > 0)
            {
                bool promptResult = ThemedMessageBox.Show(
                    "New Entries Detected",
                    $"You have {newEntriesCount} new entries that need to be finalized. " +
                    "Do you want to create an update now?",
                    this); // Pass 'this' as the owner

                if (promptResult)
                {
                    // Open the update dialog
                    var updateDialog = new SharelistUpdateDialog(SharelistManager.Instance.CurrentMetadata, newEntriesCount)
                    {
                        Owner = this
                    };
                    if (updateDialog.ShowDialog() == true && updateDialog.UpdateAccepted)
                    {
                        // Create the update record
                        SharelistManager.Instance.FinalizeUpdate(updateDialog.NewVersion, updateDialog.UpdateNotes);
                    }
                    else
                    {
                        // User cancelled update, ask if they still want to export
                        bool continueResult = ThemedMessageBox.Show(
                            "Continue Export",
                            "Do you still want to export without finalizing the update?",
                            this);

                        if (!continueResult)
                        {
                            return;
                        }
                    }
                }
            }

            // Show metadata dialog
            var metadataDialog = new SDBEditor.Views.RollbackDialog.SharelistMetadataDialog(SharelistManager.Instance.CurrentMetadata)
            {
                Owner = this
            };

            if (metadataDialog.ShowDialog() == true)
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Sharelist Files (*.sharesdb)|*.sharesdb|All Files (*.*)|*.*",
                    FileName = $"sharelist_{metadataDialog.Metadata.Author}_{metadataDialog.Metadata.Version.Replace(".", "_")}_{DateTime.Now:yyyyMMdd}.sharesdb",
                    Title = "Export Sharelist"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    if (SharelistManager.Instance.ExportToFile(saveDialog.FileName, metadataDialog.Metadata))
                    {
                        UpdateStatus($"Exported {SharelistManager.Instance.Entries.Count} entries to {System.IO.Path.GetFileName(saveDialog.FileName)}");
                        MessageBox.Show($"Successfully exported {SharelistManager.Instance.Entries.Count} entries.",
                                       "Export Complete",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to export sharelist.",
                                       "Export Error",
                                       MessageBoxButton.OK,
                                       MessageBoxImage.Error);
                    }
                }
            }
        }

        private void PrepareSharelistUpdate_Click(object sender, RoutedEventArgs e)
        {
            // This method prepares the sharelist for adding new entries
            // It marks all current entries as "original" so new ones can be tracked

            if (SharelistManager.Instance.Entries.Count == 0)
            {
                MessageBox.Show("No sharelist loaded. Please import a sharelist first.",
                              "No Sharelist",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
                return;
            }

            SharelistManager.Instance.PrepareForUpdate();

            // Update UI to reflect state change
            UpdateStatus("Sharelist prepared for updates. Add new entries and they will be tracked.");

            // Update the UI to make it clear we're in "update mode"
            var updateButton = SharelistPanel.FindName("UpdateSharelistButton") as Button;
            if (updateButton != null)
            {
                updateButton.IsEnabled = true;

                // Optional: add a visual indicator that we're in update mode
                var updateIcon = updateButton.Content as StackPanel;
                if (updateIcon != null)
                {
                    foreach (var child in updateIcon.Children)
                    {
                        if (child is TextBlock textBlock && textBlock.Text == "Update")
                        {
                            textBlock.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 100));
                            textBlock.FontWeight = FontWeights.Bold;
                            break;
                        }
                    }
                }
            }
        }

        // Override the ImportSharelist_Click method to add update capability
        private void ImportSharelist_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Sharelist Files (*.sharesdb)|*.sharesdb|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                Title = "Import Sharelist"
            };

            if (openDialog.ShowDialog() == true)
            {
                // Clear existing sharelist first
                SharelistManager.Instance.Clear();

                // Show info dialog
                var result = SharelistManager.Instance.ImportFromFile(openDialog.FileName);
                int imported = result.imported;
                SharelistMetadata fullMetadata = result.metadata;

                if (imported > 0)
                {
                    var infoDialog = new SDBEditor.Views.RollbackDialog.SharelistInfoDialog(fullMetadata, imported)
                    {
                        Owner = this
                    };

                    if (infoDialog.ShowDialog() == true)
                    {
                        UpdateStatus($"Imported {imported} entries from {System.IO.Path.GetFileName(openDialog.FileName)}");

                        // Handle the selected action
                        switch (infoDialog.SelectedAction)
                        {
                            case SharelistInfoDialog.ImportAction.PrepareForUpdate:
                                // Prepare for updating
                                SharelistManager.Instance.PrepareForUpdate();
                                UpdateStatus("Sharelist prepared for updates. Add new entries and they will be tracked.");

                                // Enable the update button
                                var updateButton = SharelistPanel.FindName("UpdateSharelistButton") as Button;
                                if (updateButton != null)
                                {
                                    updateButton.IsEnabled = true;
                                }

                                // Show version info if available
                                if (fullMetadata != null && !string.IsNullOrEmpty(fullMetadata.Version))
                                {
                                    var versionInfoPanel = SharelistPanel.FindName("VersionInfoPanel") as StackPanel;
                                    var versionText = SharelistPanel.FindName("VersionText") as TextBlock;

                                    if (versionInfoPanel != null && versionText != null)
                                    {
                                        versionText.Text = fullMetadata.Version;
                                        versionInfoPanel.Visibility = Visibility.Visible;
                                    }
                                }
                                break;

                            case SharelistInfoDialog.ImportAction.MergeImmediately:
                                if (!string.IsNullOrEmpty(_currentFile))
                                {
                                    // Check for conflicts first
                                    var conflicts = new List<ConflictEntry>();
                                    var noConflicts = new List<SharelistEntry>();

                                    foreach (var entry in SharelistManager.Instance.Entries)
                                    {
                                        var existingEntry = _sdbHandler.GetStringByHash(entry.HashId);
                                        if (existingEntry != null)
                                        {
                                            conflicts.Add(new ConflictEntry
                                            {
                                                NewEntry = entry,
                                                ExistingEntry = existingEntry,
                                                Resolution = ConflictResolution.Skip // Default to skip
                                            });
                                        }
                                        else
                                        {
                                            noConflicts.Add(entry);
                                        }
                                    }

                                    // Handle conflicts if any exist
                                    if (conflicts.Count > 0)
                                    {
                                        var conflictDialog = new ConflictResolutionDialog(conflicts)
                                        {
                                            Owner = this
                                        };

                                        if (conflictDialog.ShowDialog() != true)
                                        {
                                            // User cancelled conflict resolution
                                            UpdateStatus("Merge cancelled due to conflicts");
                                            return;
                                        }

                                        // Process resolved conflicts
                                        foreach (var conflict in conflicts)
                                        {
                                            switch (conflict.Resolution)
                                            {
                                                case ConflictResolution.UseNewHashId:
                                                    var existingEntry = conflict.NewEntry;
                                                    noConflicts.Add(new SharelistEntry(
                                                        conflict.NewHashId,
                                                        conflict.NewEntry.Text,
                                                        existingEntry.ToString() // Use string representation as third parameter
                                                    ));
                                                    break;
                                                case ConflictResolution.Replace:
                                                    // Update existing entry with new text
                                                    _sdbHandler.UpdateString(conflict.NewEntry.HashId, conflict.NewEntry.Text);
                                                    break;
                                                case ConflictResolution.Skip:
                                                    // Do nothing - skip this entry
                                                    break;
                                            }
                                        }
                                    }

                                    // Proceed with merge if we have entries to add
                                    if (noConflicts.Count > 0 || conflicts.Any(c => c.Resolution == ConflictResolution.Replace))
                                    {
                                        // Create backup before merge
                                        _backupHandler.CreateBackup(_currentFile, "pre_import_merge");

                                        int addedCount = 0;
                                        int replacedCount = conflicts.Count(c => c.Resolution == ConflictResolution.Replace);

                                        // Add non-conflicted entries
                                        foreach (var entry in noConflicts)
                                        {
                                            if (_sdbHandler.AddString(entry.Text, entry.HashId))
                                            {
                                                addedCount++;
                                            }
                                        }

                                        // Refresh display to show changes
                                        DisplayStrings();

                                        // Calculate final statistics
                                        int skippedCount = conflicts.Count(c => c.Resolution == ConflictResolution.Skip);
                                        int newHashCount = conflicts.Count(c => c.Resolution == ConflictResolution.UseNewHashId);

                                        UpdateStatus($"Import & Merge completed: {imported} imported, {addedCount + newHashCount} merged, {replacedCount} replaced, {skippedCount} skipped");

                                        var successDialog = new ImportSuccessDialog(
                                            "Import & Merge Complete",
                                            $"Successfully imported and merged {imported} entries from {fullMetadata.Author}'s sharelist.",
                                            addedCount + newHashCount,  // new entries
                                            replacedCount,              // replaced entries  
                                            skippedCount               // skipped entries
                                        )
                                        {
                                            Owner = this
                                        };
                                        successDialog.ShowDialog();
                                    }
                                    else
                                    {
                                        MessageBox.Show("No entries were merged.", "Merge Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                }
                                else
                                {
                                    // No SDB file loaded, just show import success
                                    MessageBox.Show(
                                        $"Successfully imported {imported} entries from {fullMetadata.Author}'s sharelist.\n\n" +
                                        "Note: Open an SDB file to merge these entries into it.",
                                        "Import Complete",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                                }
                                break;

                            case SharelistInfoDialog.ImportAction.JustImport:
                            default:
                                // Do nothing - just keep the sharelist as is
                                UpdateStatus($"Sharelist loaded with {imported} entries");
                                break;
                        }
                    }
                    else
                    {
                        // User cancelled info dialog, remove imported entries
                        SharelistManager.Instance.Clear();
                        UpdateStatus("Import cancelled");
                    }
                }
                else
                {
                    MessageBox.Show("No valid entries found in the file.",
                                   "Import Failed",
                                   MessageBoxButton.OK,
                                   MessageBoxImage.Warning);
                }
            }
        }

        // Supporting classes for conflict resolution
        public class ConflictEntry
        {
            public SharelistEntry NewEntry { get; set; }
            public StringEntry ExistingEntry { get; set; }
            public ConflictResolution Resolution { get; set; }
            public uint NewHashId { get; set; }
        }

        public enum ConflictResolution
        {
            Skip,           // Keep existing, ignore new
            Replace,        // Replace existing with new text
            UseNewHashId    // Use new hash ID for the new entry
        }

        // Conflict Resolution Dialog
        public class ConflictResolutionDialog : Window
        {
            private List<ConflictEntry> _conflicts;
            private StackPanel _conflictsPanel;

            public ConflictResolutionDialog(List<ConflictEntry> conflicts)
            {
                _conflicts = conflicts;

                Title = "Resolve Hash ID Conflicts";
                Width = 800;
                Height = 600;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

                InitializeComponent();
            }

            private void InitializeComponent()
            {
                Grid mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

                // Header
                Border headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                    Padding = new Thickness(20, 15, 20, 15)
                };

                StackPanel headerPanel = new StackPanel();
                headerPanel.Children.Add(new TextBlock
                {
                    Text = "Hash ID Conflicts Detected",
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 0)),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold
                });
                headerPanel.Children.Add(new TextBlock
                {
                    Text = $"{_conflicts.Count} entries have Hash IDs that already exist in the current SDB.",
                    Foreground = Brushes.White,
                    FontSize = 14,
                    Margin = new Thickness(0, 5, 0, 0)
                });
                headerPanel.Children.Add(new TextBlock
                {
                    Text = "Choose how to resolve each conflict:",
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                    FontSize = 12,
                    Margin = new Thickness(0, 5, 0, 0)
                });

                headerBorder.Child = headerPanel;
                Grid.SetRow(headerBorder, 0);
                mainGrid.Children.Add(headerBorder);

                // Scrollable content
                ScrollViewer scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(20, 20, 20, 10)
                };

                _conflictsPanel = new StackPanel();
                CreateConflictItems();
                scrollViewer.Content = _conflictsPanel;
                Grid.SetRow(scrollViewer, 1);
                mainGrid.Children.Add(scrollViewer);

                // Buttons
                StackPanel buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(20)
                };

                Button applyAllButton = CreateButton("Apply to All", false);
                applyAllButton.Margin = new Thickness(0, 0, 10, 0);
                applyAllButton.Click += ApplyToAll_Click;
                buttonPanel.Children.Add(applyAllButton);

                Button cancelButton = CreateButton("Cancel", false);
                cancelButton.Margin = new Thickness(0, 0, 10, 0);
                cancelButton.Click += (s, e) => DialogResult = false;
                buttonPanel.Children.Add(cancelButton);

                Button continueButton = CreateButton("Continue", true);
                continueButton.Click += (s, e) => DialogResult = true;
                buttonPanel.Children.Add(continueButton);

                Grid.SetRow(buttonPanel, 2);
                mainGrid.Children.Add(buttonPanel);

                Content = mainGrid;
            }

            private void CreateConflictItems()
            {
                for (int i = 0; i < _conflicts.Count; i++)
                {
                    var conflict = _conflicts[i];

                    Border itemBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
                        CornerRadius = new CornerRadius(4),
                        Margin = new Thickness(0, 0, 0, 10),
                        Padding = new Thickness(15)
                    };

                    StackPanel itemPanel = new StackPanel();

                    // Hash ID info
                    StackPanel hashPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    hashPanel.Children.Add(new TextBlock
                    {
                        Text = "Hash ID: ",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold
                    });
                    hashPanel.Children.Add(new TextBlock
                    {
                        Text = conflict.NewEntry.HashId.ToString(),
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 0)),
                        FontFamily = new FontFamily("Consolas"),
                        FontWeight = FontWeights.SemiBold
                    });
                    hashPanel.Children.Add(new TextBlock
                    {
                        Text = $" (0x{conflict.NewEntry.HashId:X})",
                        Foreground = new SolidColorBrush(Color.FromRgb(150, 255, 150)),
                        FontFamily = new FontFamily("Consolas"),
                        Margin = new Thickness(5, 0, 0, 0)
                    });
                    itemPanel.Children.Add(hashPanel);

                    // Existing vs New text
                    Grid textGrid = new Grid { Margin = new Thickness(0, 10, 0, 10) };
                    textGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    textGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    // Existing text
                    StackPanel existingPanel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
                    existingPanel.Children.Add(new TextBlock
                    {
                        Text = "Existing Text:",
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100)),
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 5)
                    });
                    existingPanel.Children.Add(new TextBox
                    {
                        Text = conflict.ExistingEntry.Text,
                        Background = new SolidColorBrush(Color.FromRgb(50, 30, 30)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(255, 100, 100)),
                        Padding = new Thickness(8),
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        MaxHeight = 80
                    });
                    Grid.SetColumn(existingPanel, 0);
                    textGrid.Children.Add(existingPanel);

                    // New text
                    StackPanel newPanel = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
                    newPanel.Children.Add(new TextBlock
                    {
                        Text = "New Text:",
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 255, 100)),
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 5)
                    });
                    newPanel.Children.Add(new TextBox
                    {
                        Text = conflict.NewEntry.Text,
                        Background = new SolidColorBrush(Color.FromRgb(30, 50, 30)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(100, 255, 100)),
                        Padding = new Thickness(8),
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        MaxHeight = 80
                    });
                    Grid.SetColumn(newPanel, 1);
                    textGrid.Children.Add(newPanel);

                    itemPanel.Children.Add(textGrid);

                    // Resolution options
                    StackPanel resolutionPanel = new StackPanel();
                    resolutionPanel.Children.Add(new TextBlock
                    {
                        Text = "Resolution:",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 5)
                    });

                    // Create radio buttons for resolution options
                    var skipRadio = new RadioButton
                    {
                        Content = "Skip (keep existing, ignore new)",
                        GroupName = $"conflict_{i}",
                        Foreground = Brushes.White,
                        IsChecked = true,
                        Margin = new Thickness(0, 2, 0, 2)
                    };
                    skipRadio.Checked += (s, e) => conflict.Resolution = ConflictResolution.Skip;

                    var replaceRadio = new RadioButton
                    {
                        Content = "Replace (update existing with new text)",
                        GroupName = $"conflict_{i}",
                        Foreground = Brushes.White,
                        Margin = new Thickness(0, 2, 0, 2)
                    };
                    replaceRadio.Checked += (s, e) => conflict.Resolution = ConflictResolution.Replace;

                    var newHashRadio = new RadioButton
                    {
                        Content = "Use new Hash ID for imported entry:",
                        GroupName = $"conflict_{i}",
                        Foreground = Brushes.White,
                        Margin = new Thickness(0, 2, 0, 2)
                    };

                    // New Hash ID input
                    var newHashTextBox = new TextBox
                    {
                        Width = 150,
                        Height = 30,
                        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                        Padding = new Thickness(8, 5, 8, 5),
                        Margin = new Thickness(20, 5, 0, 2),
                        Text = (conflict.NewEntry.HashId + 1000000).ToString(), // Suggest a new ID
                        IsEnabled = false
                    };

                    newHashRadio.Checked += (s, e) => {
                        conflict.Resolution = ConflictResolution.UseNewHashId;
                        newHashTextBox.IsEnabled = true;
                        newHashTextBox.Focus();
                    };

                    skipRadio.Checked += (s, e) => newHashTextBox.IsEnabled = false;
                    replaceRadio.Checked += (s, e) => newHashTextBox.IsEnabled = false;

                    newHashTextBox.TextChanged += (s, e) => {
                        if (uint.TryParse(newHashTextBox.Text, out uint newHash))
                        {
                            conflict.NewHashId = newHash;
                            newHashTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                        }
                        else
                        {
                            newHashTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                        }
                    };

                    // Initialize the new hash ID
                    conflict.NewHashId = conflict.NewEntry.HashId + 1000000;

                    resolutionPanel.Children.Add(skipRadio);
                    resolutionPanel.Children.Add(replaceRadio);
                    resolutionPanel.Children.Add(newHashRadio);
                    resolutionPanel.Children.Add(newHashTextBox);

                    itemPanel.Children.Add(resolutionPanel);
                    itemBorder.Child = itemPanel;
                    _conflictsPanel.Children.Add(itemBorder);
                }
            }

            private void ApplyToAll_Click(object sender, RoutedEventArgs e)
            {
                var dialog = new ApplyToAllDialog();
                if (dialog.ShowDialog() == true)
                {
                    foreach (var conflict in _conflicts)
                    {
                        conflict.Resolution = dialog.SelectedResolution;
                        if (dialog.SelectedResolution == ConflictResolution.UseNewHashId)
                        {
                            conflict.NewHashId = conflict.NewEntry.HashId + dialog.HashIdOffset;
                        }
                    }

                    // Refresh the display
                    _conflictsPanel.Children.Clear();
                    CreateConflictItems();
                }
            }

            private Button CreateButton(string content, bool isPrimary)
            {
                var button = new Button
                {
                    Content = content,
                    Width = 100,
                    Height = 36,
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    BorderThickness = new Thickness(0)
                };

                Color bgColor = isPrimary ? Color.FromRgb(0, 170, 70) : Color.FromRgb(60, 60, 65);
                button.Background = new SolidColorBrush(bgColor);
                button.Foreground = Brushes.White;

                return button;
            }
        }

        /// <summary>
        /// Success dialog that matches the existing theme of AddStringDialog and others
        /// </summary>
        public class ImportSuccessDialog : Window
        {
            public ImportSuccessDialog(string title, string message, int newEntries, int replacedEntries, int skippedEntries)
            {
                // Configure window to match existing dialogs
                Title = title;
                Width = 600;
                Height = 350;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

                // Create main container with border radius - matching AddStringDialog
                Border mainBorder = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                    Margin = new Thickness(0)
                };

                // Create layout
                Grid mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title bar
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Stats panel
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Message
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

                // Create custom title bar - matching existing dialogs
                Border titleBar = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                    Height = 40,
                    Padding = new Thickness(15, 0, 10, 0)
                };

                Grid titleGrid = new Grid();
                titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
                titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title

                // Success icon with glowing effect - matching existing dialogs
                Border iconRect = new Border
                {
                    Width = 16,
                    Height = 16,
                    Background = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Add glow effect to icon - matching existing dialogs
                iconRect.Effect = new DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Color = Color.FromRgb(0, 255, 80),
                    Opacity = 0.7
                };

                Grid.SetColumn(iconRect, 0);
                titleGrid.Children.Add(iconRect);

                // Title text - matching existing dialogs
                TextBlock titleText = new TextBlock
                {
                    Text = title,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.Medium,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(titleText, 1);
                titleGrid.Children.Add(titleText);

                titleBar.Child = titleGrid;
                Grid.SetRow(titleBar, 0);
                mainGrid.Children.Add(titleBar);

                // Make title bar draggable - matching existing dialogs
                titleBar.MouseLeftButtonDown += (s, e) => DragMove();

                // Stats panel with badges layout - matching AddStringDialog style
                Border statsPanelBackground = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
                    Padding = new Thickness(20, 15, 20, 15)
                };

                // Horizontal stack panel for badges - matching AddStringDialog
                StackPanel badgesPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 0)
                };

                // New entries badge with green background
                if (newEntries > 0)
                {
                    Border newBadge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(150, 0, 50, 25)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10, 6, 10, 6),
                        Margin = new Thickness(0, 0, 15, 0)
                    };
                    StackPanel newPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    newPanel.Children.Add(new TextBlock
                    {
                        Text = "Added: ",
                        Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    newPanel.Children.Add(new TextBlock
                    {
                        Text = newEntries.ToString(),
                        Foreground = new SolidColorBrush(Color.FromRgb(150, 255, 150)),
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    newBadge.Child = newPanel;
                    badgesPanel.Children.Add(newBadge);
                }

                // Replaced entries badge with blue background
                if (replacedEntries > 0)
                {
                    Border replacedBadge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(150, 0, 25, 50)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10, 6, 10, 6),
                        Margin = new Thickness(0, 0, 15, 0)
                    };
                    StackPanel replacedPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    replacedPanel.Children.Add(new TextBlock
                    {
                        Text = "Replaced: ",
                        Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    replacedPanel.Children.Add(new TextBlock
                    {
                        Text = replacedEntries.ToString(),
                        Foreground = new SolidColorBrush(Color.FromRgb(150, 200, 255)),
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    replacedBadge.Child = replacedPanel;
                    badgesPanel.Children.Add(replacedBadge);
                }

                // Skipped entries badge with orange background
                if (skippedEntries > 0)
                {
                    Border skippedBadge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(150, 75, 50, 0)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(10, 6, 10, 6),
                        Margin = new Thickness(0, 0, 15, 0)
                    };
                    StackPanel skippedPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    skippedPanel.Children.Add(new TextBlock
                    {
                        Text = "Skipped: ",
                        Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    skippedPanel.Children.Add(new TextBlock
                    {
                        Text = skippedEntries.ToString(),
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    skippedBadge.Child = skippedPanel;
                    badgesPanel.Children.Add(skippedBadge);
                }

                statsPanelBackground.Child = badgesPanel;
                Grid.SetRow(statsPanelBackground, 1);
                mainGrid.Children.Add(statsPanelBackground);

                // Message content area - matching existing dialogs
                Border messageContainer = new Border
                {
                    Margin = new Thickness(20, 20, 20, 0),
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 33)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(15),
                    Effect = new DropShadowEffect
                    {
                        BlurRadius = 10,
                        ShadowDepth = 2,
                        Direction = 315,
                        Color = Colors.Black,
                        Opacity = 0.4
                    }
                };

                TextBlock messageText = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 20,
                    FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic")
                };

                // Apply optimal text rendering settings - matching existing dialogs
                TextOptions.SetTextFormattingMode(messageText, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(messageText, TextRenderingMode.ClearType);
                TextOptions.SetTextHintingMode(messageText, TextHintingMode.Auto);

                messageContainer.Child = messageText;
                Grid.SetRow(messageContainer, 2);
                mainGrid.Children.Add(messageContainer);

                // Button panel with modern design - matching existing dialogs
                Grid buttonPanel = new Grid
                {
                    Margin = new Thickness(20, 20, 20, 20)
                };

                buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Create OK button - matching existing dialogs
                Button okButton = CreateButton("OK", true);
                okButton.Click += (s, e) => DialogResult = true;
                Grid.SetColumn(okButton, 1);
                buttonPanel.Children.Add(okButton);

                Grid.SetRow(buttonPanel, 3);
                mainGrid.Children.Add(buttonPanel);

                mainBorder.Child = mainGrid;
                Content = mainBorder;
            }

            /// <summary>
            /// Creates a modern button with hover and pressed states - SAME as existing dialogs
            /// </summary>
            private Button CreateButton(string content, bool isPrimary)
            {
                var button = new Button
                {
                    Content = content,
                    Width = 100,
                    Height = 36,
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    BorderThickness = new Thickness(0)
                };

                Color bgColor = isPrimary ? Color.FromRgb(0, 170, 70) : Color.FromRgb(60, 60, 65);
                button.Background = new SolidColorBrush(bgColor);
                button.Foreground = Brushes.White;

                return button;
            }

            /// <summary>
            /// Creates a modern button template - SAME as existing dialogs
            /// </summary>
            private ControlTemplate CreateButtonTemplate()
            {
                ControlTemplate template = new ControlTemplate(typeof(Button));

                FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
                borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

                // Add subtle shadow to button - SAME as existing dialogs
                DropShadowEffect buttonShadow = new DropShadowEffect
                {
                    BlurRadius = 5,
                    ShadowDepth = 1,
                    Direction = 315,
                    Color = Colors.Black,
                    Opacity = 0.3
                };
                borderFactory.SetValue(Border.EffectProperty, buttonShadow);

                FrameworkElementFactory contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
                contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                contentPresenterFactory.SetValue(ContentPresenter.MarginProperty, new Thickness(2));

                borderFactory.AppendChild(contentPresenterFactory);
                template.VisualTree = borderFactory;

                return template;
            }
        }

        // Dialog for applying resolution to all conflicts
        public class ApplyToAllDialog : Window
        {
            public ConflictResolution SelectedResolution { get; private set; }
            public uint HashIdOffset { get; private set; } = 1000000;

            public ApplyToAllDialog()
            {
                Title = "Apply to All Conflicts";
                Width = 400;
                Height = 300;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

                var stackPanel = new StackPanel { Margin = new Thickness(20) };

                stackPanel.Children.Add(new TextBlock
                {
                    Text = "Choose resolution for all conflicts:",
                    Foreground = Brushes.White,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 15)
                });

                var skipRadio = new RadioButton
                {
                    Content = "Skip all conflicts",
                    GroupName = "applyAll",
                    Foreground = Brushes.White,
                    IsChecked = true,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                var replaceRadio = new RadioButton
                {
                    Content = "Replace all existing entries",
                    GroupName = "applyAll",
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                var newHashRadio = new RadioButton
                {
                    Content = "Generate new Hash IDs with offset:",
                    GroupName = "applyAll",
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                var offsetTextBox = new TextBox
                {
                    Text = HashIdOffset.ToString(),
                    Width = 150,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                    Margin = new Thickness(20, 5, 0, 15),
                    IsEnabled = false
                };

                newHashRadio.Checked += (s, e) => offsetTextBox.IsEnabled = true;
                skipRadio.Checked += (s, e) => offsetTextBox.IsEnabled = false;
                replaceRadio.Checked += (s, e) => offsetTextBox.IsEnabled = false;

                offsetTextBox.TextChanged += (s, e) => {
                    if (uint.TryParse(offsetTextBox.Text, out uint offset))
                    {
                        HashIdOffset = offset;
                    }
                };

                stackPanel.Children.Add(skipRadio);
                stackPanel.Children.Add(replaceRadio);
                stackPanel.Children.Add(newHashRadio);
                stackPanel.Children.Add(offsetTextBox);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 20, 0, 0)
                };

                var cancelButton = new Button
                {
                    Content = "Cancel",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0)
                };
                cancelButton.Click += (s, e) => DialogResult = false;

                var okButton = new Button
                {
                    Content = "Apply",
                    Width = 80,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(0, 170, 70)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0)
                };
                okButton.Click += (s, e) => {
                    if (skipRadio.IsChecked == true)
                        SelectedResolution = ConflictResolution.Skip;
                    else if (replaceRadio.IsChecked == true)
                        SelectedResolution = ConflictResolution.Replace;
                    else if (newHashRadio.IsChecked == true)
                        SelectedResolution = ConflictResolution.UseNewHashId;

                    DialogResult = true;
                };

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(okButton);
                stackPanel.Children.Add(buttonPanel);

                Content = stackPanel;
            }
        }

        private void ClearSharelist_Click(object sender, RoutedEventArgs e)
        {
            if (SharelistManager.Instance.Entries.Count == 0)
                return;

            var result = MessageBox.Show($"Are you sure you want to clear all {SharelistManager.Instance.Entries.Count} entries from the sharelist?",
                                        "Clear Sharelist",
                                        MessageBoxButton.YesNo,
                                        MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SharelistManager.Instance.Clear();
                UpdateStatus("Sharelist cleared");
            }
        }

        #endregion


        /// <summary>
        /// Enhanced virtualization for DataGrid with better compatibility
        /// </summary>
        private void OptimizeDataGridForLargeLists()
        {
            try
            {
                // Basic virtualization settings that work in most WPF versions
                StringsDataGrid.EnableRowVirtualization = true;
                StringsDataGrid.EnableColumnVirtualization = true;

                // Applying virtualization through attached property
                System.Windows.Controls.VirtualizingPanel.SetIsVirtualizing(StringsDataGrid, true);

                // CRITICAL: Use item scrolling instead of pixel scrolling for smoother operation
                System.Windows.Controls.VirtualizingPanel.SetScrollUnit(StringsDataGrid, ScrollUnit.Item);

                // Set smaller cache length for better performance
                System.Windows.Controls.VirtualizingPanel.SetCacheLength(StringsDataGrid, new VirtualizationCacheLength(2));
                System.Windows.Controls.VirtualizingPanel.SetCacheLengthUnit(StringsDataGrid, VirtualizationCacheLengthUnit.Page);

                // These are standard DataGrid properties that should work in most versions
                StringsDataGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;
                StringsDataGrid.SelectionMode = DataGridSelectionMode.Extended;
                StringsDataGrid.SelectionUnit = DataGridSelectionUnit.FullRow;

                // Set default row height for performance
                StringsDataGrid.MinRowHeight = 24;
                StringsDataGrid.ColumnHeaderHeight = 32;

                // Reduce redraws
                StringsDataGrid.AutoGenerateColumns = false;
                StringsDataGrid.CanUserResizeRows = false;

                // CRITICAL: Disable deferred scrolling for immediate visual updates
                var scrollViewer = GetScrollViewer(StringsDataGrid);
                if (scrollViewer != null)
                {
                    scrollViewer.IsDeferredScrollingEnabled = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in DataGrid optimization: {ex.Message}");
                // Continue even if optimization fails
            }
        }

        /// <summary>
        /// Helper to find ScrollViewer inside DataGrid
        /// </summary>
        private ScrollViewer GetScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer scrollViewer)
                return scrollViewer;

            // Search children
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Add direct scrollbar thumb tracking to ensure updates during dragging
        /// </summary>
        private void SetupScrollbarDragHandling()
        {
            try
            {
                // Step 1: Get access to the DataGrid's ScrollViewer
                var scrollViewer = GetScrollViewer(StringsDataGrid);
                if (scrollViewer != null)
                {
                    // CRITICAL: Disable UI virtualization during scrollbar drag
                    scrollViewer.IsDeferredScrollingEnabled = false;

                    // Step 2: Find the ScrollBar within the ScrollViewer
                    var scrollBar = FindVisualChild<ScrollBar>(scrollViewer, sb => sb.Orientation == Orientation.Vertical);
                    if (scrollBar != null)
                    {
                        // Step 3: Find the Thumb within the ScrollBar
                        var thumb = FindVisualChild<Thumb>(scrollBar);
                        if (thumb != null)
                        {
                            // Setup drag event handlers with double dispatching for UI updates
                            thumb.DragStarted += (s, e) =>
                            {
                                Console.WriteLine("Drag started");
                                // Set a flag to indicate we're in drag mode
                                _isDraggingScrollbar = true;
                            };

                            // This event fires continuously during dragging - need to update UI aggressively
                            thumb.DragDelta += (s, e) =>
                            {
                                // CRITICAL: We need to dispatch this at a higher priority than Render
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    UpdateVisibleRows(true); // Force update
                                }), DispatcherPriority.Render);
                            };

                            thumb.DragCompleted += (s, e) =>
                            {
                                Console.WriteLine("Drag completed");
                                _isDraggingScrollbar = false;

                                // Force a full refresh after drag completes
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    ForceDataGridRefresh();
                                }), DispatcherPriority.Render);
                            };

                            // Also need to handle regular scroll events
                            scrollViewer.ScrollChanged += (s, e) =>
                            {
                                if (e.VerticalChange != 0)
                                {
                                    UpdateVisibleRows(_isDraggingScrollbar);
                                }
                            };

                            Console.WriteLine("Successfully attached scrollbar event handlers");
                        }
                        else
                        {
                            Console.WriteLine("Unable to find Thumb in ScrollBar");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unable to find ScrollBar in ScrollViewer");
                    }
                }
                else
                {
                    Console.WriteLine("Unable to find ScrollViewer in DataGrid");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up scrollbar handling: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to find a specific visual child
        /// </summary>
        private T FindVisualChild<T>(DependencyObject parent, Func<T, bool> condition = null) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild && (condition == null || condition(typedChild)))
                    return typedChild;

                T result = FindVisualChild<T>(child, condition);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Get visible rows in a version-compatible way
        /// </summary>
        private Tuple<int, int> GetVisibleRowRange()
        {
            try
            {
                var scrollViewer = GetScrollViewer(StringsDataGrid);
                if (scrollViewer != null)
                {
                    double rowHeight = StringsDataGrid.RowHeight > 0 ? StringsDataGrid.RowHeight : 24;

                    // Calculate visible range
                    int firstVisibleIndex = (int)(scrollViewer.VerticalOffset / rowHeight);
                    int visibleRowsCount = (int)(scrollViewer.ViewportHeight / rowHeight) + 1;
                    int lastVisibleIndex = Math.Min(firstVisibleIndex + visibleRowsCount,
                                                   StringsDataGrid.Items.Count - 1);

                    return new Tuple<int, int>(firstVisibleIndex, lastVisibleIndex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting visible rows: {ex.Message}");
            }

            // Fallback - use safe default range
            return new Tuple<int, int>(0, Math.Min(100, StringsDataGrid.Items.Count - 1));
        }

        /// <summary>
        /// Optimized version of UpdateVisibleRows with support for multi-line text
        /// </summary>
        private void UpdateVisibleRows(bool forceUpdate = false)
        {
            if (StringsDataGrid?.Items == null || StringsDataGrid.Items.Count == 0)
                return;

            try
            {
                // Get visible range
                var visibleRange = GetVisibleRowRange();
                int firstVisibleIndex = visibleRange.Item1;
                int lastVisibleIndex = visibleRange.Item2;

                // If we're not forcing updates and the visible range hasn't changed significantly, return
                if (!forceUpdate &&
                    Math.Abs(firstVisibleIndex - _lastFirstVisibleItem) < 3 &&
                    Math.Abs(lastVisibleIndex - _lastLastVisibleItem) < 3)
                {
                    return;
                }

                // Create buffer above and below visible area
                int buffer = _isDraggingScrollbar ? 5 : 20; // Smaller buffer during drag for better performance
                int start = Math.Max(0, firstVisibleIndex - buffer);
                int end = Math.Min(StringsDataGrid.Items.Count - 1, lastVisibleIndex + buffer);

                // Update all visible rows immediately
                for (int i = start; i <= end; i++)
                {
                    if (i < _stringsCollection.Count)
                    {
                        var currentViewModel = _stringsCollection[i];
                        if (currentViewModel != null)
                        {
                            // Critical: Force UI to create and update the row container
                            var container = StringsDataGrid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;
                            if (container != null)
                            {
                                // Force a visual refresh of the row
                                container.InvalidateVisual();
                                container.UpdateLayout(); // This is important for immediate updates
                            }

                            // Also refresh the view model's display
                            currentViewModel.RefreshDisplay();
                        }
                    }
                }

                // If we're dragging, force the DataGrid to update its display more aggressively
                if (_isDraggingScrollbar)
                {
                    // Ping the dispatcher to ensure the UI updates
                    Dispatcher.BeginInvoke(new Action(() => { }), DispatcherPriority.Render);
                }

                // Store the current visible range
                _lastFirstVisibleItem = firstVisibleIndex;
                _lastLastVisibleItem = lastVisibleIndex;

                // Adjust row heights for multi-line content
                AdjustRowHeightsForMultilineContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateVisibleRows: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the SDB logo for the sidebar
        /// </summary>
        private void LoadSdbLogo()
        {
            try
            {
                // Try multiple possible filenames and paths to find the logo
                string[] possibleFilenames = new string[]
                {
                    "SDB.png",
                    "sdb.png",
                    "sbd.png",
                    "sbd logo.png",
                    "SDB logo.png"
                };

                // Try different path formats for each filename
                bool imageLoaded = false;
                BitmapImage bitmap = new BitmapImage();

                foreach (string filename in possibleFilenames)
                {
                    string[] pathFormats = new string[]
                    {
                        $"pack://application:,,,/Resources/{filename}",
                        $"pack://application:,,,/SDBEditor;component/Resources/{filename}",
                        $"/SDBEditor;component/Resources/{filename}",
                        $"/Resources/{filename}",
                        $"Resources/{filename}"
                    };

                    foreach (string path in pathFormats)
                    {
                        try
                        {
                            bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();

                            // If we get here without an exception, the path works
                            SdbLogo.Source = bitmap;
                            imageLoaded = true;
                            Console.WriteLine($"Successfully loaded SDB logo from: {path}");
                            break;
                        }
                        catch (Exception pathEx)
                        {
                            Console.WriteLine($"Failed to load SDB logo from path {path}: {pathEx.Message}");
                            // Continue to next path
                        }
                    }

                    if (imageLoaded) break;
                }

                if (!imageLoaded)
                {
                    // If no image loaded, create a text-based logo as fallback
                    TextBlock logoText = new TextBlock
                    {
                        Text = "SDB EDITOR",
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // Replace the Image with the TextBlock in the Grid
                    Grid parentGrid = (Grid)SdbLogo.Parent;
                    int row = Grid.GetRow(SdbLogo);
                    parentGrid.Children.Remove(SdbLogo);
                    Grid.SetRow(logoText, row);
                    parentGrid.Children.Add(logoText);

                    Console.WriteLine("Created text-based SDB logo as fallback");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load or create SDB logo: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the game logo from the Resources folder
        /// </summary>
        private void LoadWWE2KLogo()
        {
            try
            {
                // Try multiple possible paths to find the logo
                string[] possiblePaths = new string[]
                {
                    "pack://application:,,,/Resources/WWE_2K_Logo.png",
                    "pack://application:,,,/SDBEditor;component/Resources/WWE_2K_Logo.png",
                    "/SDBEditor;component/Resources/WWE_2K_Logo.png",
                    "/Resources/WWE_2K_Logo.png",
                    "Resources/WWE_2K_Logo.png"
                };

                bool imageLoaded = false;
                BitmapImage bitmap = new BitmapImage();

                foreach (string path in possiblePaths)
                {
                    try
                    {
                        bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();

                        // If we get here without an exception, the path works
                        WWE2KLogo.Source = bitmap;
                        WWE2KLogo.Visibility = Visibility.Visible;
                        GameVersionText.Visibility = Visibility.Visible;
                        imageLoaded = true;
                        Console.WriteLine($"Successfully loaded game logo from: {path}");
                        break;
                    }
                    catch (Exception pathEx)
                    {
                        Console.WriteLine($"Failed to load from path {path}: {pathEx.Message}");
                        // Continue to next path
                    }
                }

                if (!imageLoaded)
                {
                    throw new Exception("Failed to load image from any path.");
                }
            }
            catch (Exception ex)
            {
                // If loading fails, fall back to text mode
                Console.WriteLine($"Failed to load game logo: {ex.Message}");
                GameVersionText.Text = "Official Game Title: WWE2K24"; // Default text as fallback
                WWE2KLogo.Visibility = Visibility.Collapsed;
            }
        }

        #region File Operations

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "SDB Files (*.sdb)|*.sdb|All Files (*.*)|*.*",
                Title = "Open SDB File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadSdbFile(openFileDialog.FileName);
            }
        }

        private void LoadSdbFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"File not found: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Complete UI reset to fix glitches when switching files
                CompleteUIReset();

                // Reset handlers for clean state
                ResetHandlers();

                // Show loading cursor
                Mouse.OverrideCursor = Cursors.Wait;
                UpdateStatus("Loading file...");

                // IMPORTANT: Check if we're reloading the same file
                bool isReloadingSameFile = !string.IsNullOrEmpty(_currentFile) && _currentFile == filePath;

                // Clear caches - ALWAYS clear if reloading the same file to ensure fresh data
                if (!string.IsNullOrEmpty(_currentFile))
                {
                    SDBCacheManager.Instance.ClearCache(_currentFile);

                    // Also clear search and view model caches when reloading
                    if (isReloadingSameFile)
                    {
                        SDBCacheManager.Instance.ClearSearchCache();
                        SDBCacheManager.Instance.ClearViewModelCache();
                        Console.WriteLine($"Forcing cache refresh for same file: {filePath}");
                    }
                }

                try
                {
                    // For same file reload, always load from disk to get proper order
                    if (isReloadingSameFile)
                    {
                        // Force load from disk to ensure proper ordering
                        byte[] fileData = File.ReadAllBytes(filePath);

                        // Cache the raw file data
                        SDBCacheManager.Instance.CacheRawFile(filePath, fileData);

                        if (_sdbHandler.LoadSDB(filePath))
                        {
                            // Cache the parsed entries after loading
                            SDBCacheManager.Instance.CacheParsedEntries(filePath, _sdbHandler.GetAllStrings());

                            // Update file info
                            _currentFile = filePath;
                            SaveLastFile(filePath);

                            // FIX UNKNOWN LANGUAGE HERE
                            FixUnknownLanguage();

                            // Update UI info
                            UpdateGameInfo();

                            // Display strings - will show properly ordered data
                            DisplayStrings();
                            UpdateStatus($"Reloaded: {FormatPathForDisplay(filePath)}");
                        }
                        else
                        {
                            throw new Exception("Failed to reload SDB file");
                        }
                    }
                    else
                    {
                        // Different file - use normal caching logic
                        // Check if we have cached parsed entries first (fastest)
                        var cachedParsedEntries = SDBCacheManager.Instance.GetCachedParsedEntries(filePath);

                        if (cachedParsedEntries != null)
                        {
                            // Use the cached data directly
                            _sdbHandler.SetStrings(cachedParsedEntries);

                            // Update file info
                            _currentFile = filePath;

                            // FIX UNKNOWN LANGUAGE HERE
                            FixUnknownLanguage();

                            // Update UI info
                            UpdateGameInfo();

                            // Display strings - will use cached data
                            DisplayStrings();
                            UpdateStatus($"Loaded from cache: {FormatPathForDisplay(filePath)}");
                        }
                        else
                        {
                            // Check for cached raw file data
                            var cachedRawData = SDBCacheManager.Instance.GetCachedRawFile(filePath);

                            if (cachedRawData != null)
                            {
                                // Create a memory stream from cached data
                                using (MemoryStream ms = new MemoryStream(cachedRawData))
                                {
                                    if (_sdbHandler.LoadSDBFromStream(ms))
                                    {
                                        // Cache the parsed entries for future use
                                        SDBCacheManager.Instance.CacheParsedEntries(filePath, _sdbHandler.GetAllStrings());

                                        // Update file info
                                        _currentFile = filePath;
                                        SaveLastFile(filePath);

                                        // FIX UNKNOWN LANGUAGE HERE
                                        FixUnknownLanguage();

                                        // Update UI info
                                        UpdateGameInfo();

                                        // Display strings - will use cached data
                                        DisplayStrings();
                                        UpdateStatus($"Loaded from cache: {FormatPathForDisplay(filePath)}");
                                    }
                                    else
                                    {
                                        throw new Exception("Failed to load SDB from cached data");
                                    }
                                }
                            }
                            else
                            {
                                // No cache available, load from disk and cache for future use
                                byte[] fileData = File.ReadAllBytes(filePath);

                                // Cache the raw file data
                                SDBCacheManager.Instance.CacheRawFile(filePath, fileData);

                                if (_sdbHandler.LoadSDB(filePath))
                                {
                                    // Cache the parsed entries after loading
                                    SDBCacheManager.Instance.CacheParsedEntries(filePath, _sdbHandler.GetAllStrings());

                                    // Create backup
                                    _backupHandler.CreateBackup(filePath, "auto_backup");

                                    // Update file info
                                    _currentFile = filePath;
                                    SaveLastFile(filePath);

                                    // FIX UNKNOWN LANGUAGE HERE
                                    FixUnknownLanguage();

                                    // Update UI info
                                    UpdateGameInfo();

                                    // Display strings - will use cached data
                                    DisplayStrings();
                                    UpdateStatus($"Successfully Loaded: {FormatPathForDisplay(filePath)}");
                                }
                                else
                                {
                                    throw new Exception("Failed to load SDB file");
                                }
                            }
                        }
                    }
                }
                finally
                {
                    // Reset cursor
                    Mouse.OverrideCursor = null;
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"Failed to load file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Error loading file");
            }
        }

        /// <summary>
        /// Helper to update game info UI elements with WWE 2K25 support
        /// </summary>
        private void UpdateGameInfo()
        {
            // Now load the game logo if needed
            LoadWWE2KLogo();

            string gameName = _sdbHandler.GameName;
            string gameVersion = "24"; // Default to 24

            // Extract version from game name
            if (!string.IsNullOrEmpty(gameName))
            {
                if (gameName.Contains("2K25")) gameVersion = "25";
                else if (gameName.Contains("2K24")) gameVersion = "24";
                else if (gameName.Contains("2K23")) gameVersion = "23";
                else if (gameName.Contains("2K22")) gameVersion = "22";
                else if (gameName.Contains("2K20")) gameVersion = "20";
                else if (gameName.Contains("2K19")) gameVersion = "19";
                else
                {
                    var versionMatch = System.Text.RegularExpressions.Regex.Match(gameName, @"2K(\d+)");
                    if (versionMatch.Success && versionMatch.Groups.Count > 1)
                    {
                        gameVersion = versionMatch.Groups[1].Value;
                    }
                }
            }

            // Update UI elements with new info
            GameVersionText.Text = $"Official Game Title: WWE 2K{gameVersion}";

            // ADD THIS LINE HERE - FIX UNKNOWN LANGUAGE
            FixUnknownLanguage();

            // Force check mangled status from handler
            bool isActuallyMangled = _sdbHandler.IsMangled;

            // Ensure we display accurate information
            GameExtraInfoText.Text = $", Language: {_sdbHandler.Language}";

            // Log the status for debugging
            Console.WriteLine($"UpdateGameInfo: Mangled status is {isActuallyMangled}, Language is {_sdbHandler.Language}");

            // Make the game info panel visible now that we have real data
            GameInfoPanel.Visibility = Visibility.Visible;
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            // Check if a file is loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "SDB Files (*.sdb)|*.sdb|All Files (*.*)|*.*",
                Title = "Save SDB File"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                SaveSdbFile(saveFileDialog.FileName);
            }
        }

        private void SaveSdbFile(string filePath)
        {
            try
            {
                // Create backup before saving
                if (File.Exists(filePath))
                {
                    _backupHandler.CreateBackup(filePath);
                }

                Mouse.OverrideCursor = Cursors.Wait;

                // Save using SDBHandler
                if (_sdbHandler.SaveSDB(filePath))
                {
                    // Update current file path
                    _currentFile = filePath;
                    SaveLastFile(filePath);

                    // Clear caches to ensure fresh data
                    SDBCacheManager.Instance.ClearCache(filePath);
                    SDBCacheManager.Instance.ClearSearchCache();
                    SDBCacheManager.Instance.ClearViewModelCache();

                    UpdateStatus($"Saved: {FormatPathForDisplay(filePath)}");

                    // AUTOMATIC REFRESH: Reload the file to show proper ordering
                    Mouse.OverrideCursor = null; // Reset cursor before reload

                    // Show refresh status
                    UpdateStatus("Refreshing display...");

                    // Reload the file to get proper string ordering
                    LoadSdbFile(filePath);

                    // Update status to show save and refresh complete
                    UpdateStatus($"Saved and refreshed: {FormatPathForDisplay(filePath)}");
                }
                else
                {
                    Mouse.OverrideCursor = null;
                    MessageBox.Show("Failed to save file.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AutoSeekSdb_Click(object sender, RoutedEventArgs e)
        {
            // Check if a file is loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Create a folder browser dialog
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select Baked Directory",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string directory = folderDialog.SelectedPath;
                    string sdbFolder = null;

                    // Look for Sdb folder
                    foreach (var dir in Directory.GetDirectories(directory, "*", SearchOption.AllDirectories))
                    {
                        if (Path.GetFileName(dir).Equals("Sdb", StringComparison.OrdinalIgnoreCase))
                        {
                            sdbFolder = dir;
                            break;
                        }
                    }

                    // If not found, create it
                    if (sdbFolder == null)
                    {
                        sdbFolder = Path.Combine(directory, "Sdb");
                        Directory.CreateDirectory(sdbFolder);
                    }

                    string sdbFile = Path.Combine(sdbFolder, "ENG.sdb");

                    // Create backup before saving
                    if (File.Exists(sdbFile))
                    {
                        _backupHandler.CreateBackup(sdbFile, "auto_seek_backup");
                    }

                    Mouse.OverrideCursor = Cursors.Wait;

                    // Save using SDBHandler
                    if (_sdbHandler.SaveSDB(sdbFile))
                    {
                        // Clear caches
                        SDBCacheManager.Instance.ClearCache(sdbFile);
                        SDBCacheManager.Instance.ClearSearchCache();
                        SDBCacheManager.Instance.ClearViewModelCache();

                        UpdateStatus($"Auto-saved to: {FormatPathForDisplay(sdbFile)}");

                        // AUTOMATIC REFRESH: If we saved to the current file, reload it
                        if (sdbFile == _currentFile)
                        {
                            Mouse.OverrideCursor = null;
                            UpdateStatus("Refreshing display...");
                            LoadSdbFile(sdbFile);
                            UpdateStatus($"Auto-saved and refreshed: {FormatPathForDisplay(sdbFile)}");
                        }
                        else
                        {
                            Mouse.OverrideCursor = null;
                        }
                    }
                    else
                    {
                        throw new Exception("Failed to save SDB file");
                    }
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"Failed to auto seek SDB: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveLastFile(string filePath)
        {
            try
            {
                string configDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string configFile = Path.Combine(configDir, "last_file.json");

                string jsonContent = $"{{\"last_sdb_file\": \"{filePath.Replace("\\", "\\\\")}\"}}";
                File.WriteAllText(configFile, jsonContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save last file config: {ex.Message}");
            }
        }

        private void LoadLastFileIfAvailable()
        {
            try
            {
                string configDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string configFile = Path.Combine(configDir, "last_file.json");

                if (File.Exists(configFile))
                {
                    string jsonContent = File.ReadAllText(configFile);

                    // Simple JSON parsing
                    if (jsonContent.Contains("last_sdb_file"))
                    {
                        int startIndex = jsonContent.IndexOf("\"last_sdb_file\"") + "\"last_sdb_file\"".Length;
                        int valueStartIndex = jsonContent.IndexOf("\"", startIndex) + 1;
                        int valueEndIndex = jsonContent.IndexOf("\"", valueStartIndex);

                        if (valueStartIndex > 0 && valueEndIndex > valueStartIndex)
                        {
                            string lastFile = jsonContent.Substring(valueStartIndex, valueEndIndex - valueStartIndex);

                            if (!string.IsNullOrEmpty(lastFile) && File.Exists(lastFile))
                            {
                                LoadSdbFile(lastFile);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to auto-load last file: {ex.Message}");
                UpdateStatus("Ready");
            }
        }

        #endregion

        #region Display Strings

        // Modified DisplayStrings method with optimized caching
        private async void DisplayStrings()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            UpdateStatus("Loading strings...");

            try
            {
                // Check if a file has been loaded
                if (string.IsNullOrEmpty(_currentFile))
                {
                    // No file loaded yet, just clear the collection if it exists
                    if (_stringsCollection != null)
                    {
                        _stringsCollection.Clear();
                    }
                    UpdateStatus("No file loaded. Please open an SDB file.");
                    return;
                }

                // Reset animation flag when loading new file
                _initialAnimationPlayed = false;
                _lastSearchQuery = string.Empty;

                // Get strings - use Task.Run to avoid blocking UI
                List<StringEntry> strings = null;

                await Task.Run(() => {
                    // Get from cache if available first
                    strings = SDBCacheManager.Instance.GetCachedParsedEntries(_currentFile);

                    // If not in cache, get from handler (which will also try to use cache)
                    if (strings == null)
                    {
                        strings = _sdbHandler.GetAllStrings() ?? new List<StringEntry>();

                        // Cache for future use if not already cached
                        if (strings.Count > 0)
                        {
                            SDBCacheManager.Instance.CacheParsedEntries(_currentFile, strings);
                        }
                    }
                });

                // Update collection with async implementation
                await Task.Yield(); // Ensure UI responsiveness
                PopulateStringCollection(strings);

                // Reset scrolling behavior to prevent glitches
                ResetScrollViewer();

                UpdateStatus($"Loaded {strings.Count} strings");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error loading strings: {ex.Message}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // Improved PopulateStringCollection method with memory optimization
        private async void PopulateStringCollection(List<StringEntry> strings)
        {
            if (_stringsCollection == null)
            {
                _stringsCollection = new ObservableCollection<StringEntryViewModel>();
                StringsDataGrid.ItemsSource = _stringsCollection;
            }

            // Reset row height to default
            StringsDataGrid.MinRowHeight = 24;

            // Remember current position
            object selectedItem = StringsDataGrid.SelectedItem;
            string selectedHashId = (selectedItem as StringEntryViewModel)?.HashId.ToString();

            // Show loading indicator
            UpdateStatus("Loading strings...");
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                // Load data on background thread to prevent UI freezing
                await Task.Run(() => {
                    // Process data in background
                    var viewModels = new List<StringEntryViewModel>(strings.Count);

                    // Process in batches for large collections
                    const int batchSize = 100;

                    for (int i = 0; i < strings.Count; i += batchSize)
                    {
                        var batch = strings.Skip(i).Take(batchSize);
                        foreach (var entry in batch)
                        {
                            if (entry == null) continue;

                            // Check view model cache first - avoid UI thread access
                            var cachedViewModel = SDBCacheManager.Instance.GetCachedViewModel(entry.HashId);

                            StringEntryViewModel viewModel;
                            if (cachedViewModel != null)
                            {
                                viewModel = cachedViewModel;
                                viewModel.Index = i;
                            }
                            else
                            {
                                viewModel = new StringEntryViewModel(entry);
                                viewModel.Index = i;
                                SDBCacheManager.Instance.CacheViewModel(entry.HashId, viewModel);
                            }

                            viewModels.Add(viewModel);
                        }
                    }

                    // Update UI on the UI thread
                    Dispatcher.Invoke(() => {
                        try
                        {
                            // Disable UI updates during manipulation
                            StringsDataGrid.BeginInit();

                            // Clear collection safely
                            try
                            {
                                _stringsCollection.Clear();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error clearing collection: {ex.Message}");
                                _stringsCollection = new ObservableCollection<StringEntryViewModel>();
                                StringsDataGrid.ItemsSource = _stringsCollection;
                            }

                            // Add items in batches to maintain UI responsiveness
                            for (int i = 0; i < viewModels.Count; i += 500)
                            {
                                var displayBatch = viewModels.Skip(i).Take(500);
                                foreach (var vm in displayBatch)
                                {
                                    _stringsCollection.Add(vm);
                                }

                                // Force UI update every 500 items
                                if (i + 500 < viewModels.Count)
                                {
                                    // Allow UI to process events
                                    Dispatcher.Yield(DispatcherPriority.Background);
                                }
                            }

                            // Restore selection if possible
                            if (!string.IsNullOrEmpty(selectedHashId))
                            {
                                try
                                {
                                    RestoreSelection(selectedHashId);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error restoring selection: {ex.Message}");
                                }
                            }
                        }
                        finally
                        {
                            // Re-enable UI updates
                            StringsDataGrid.EndInit();

                            // Force a complete refresh
                            ForceDataGridRefresh();

                            // Apply animation only on initial load
                            if (!_initialAnimationPlayed)
                            {
                                ApplyLoadAnimation();
                                _initialAnimationPlayed = true;
                            }
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in PopulateStringCollection: {ex.Message}");
                UpdateStatus($"Error loading strings: {ex.Message}");
            }
            finally
            {
                // Always reset cursor
                Mouse.OverrideCursor = null;
                UpdateStatus($"Loaded {_stringsCollection.Count} strings");
            }
        }

        /// <summary>
        /// Load the initial chunk of items to show immediately
        /// </summary>
        private void LoadInitialChunk(List<StringEntry> strings, int chunkSize)
        {
            int itemsToLoad = Math.Min(chunkSize, strings.Count);

            for (int i = 0; i < itemsToLoad; i++)
            {
                try
                {
                    var entry = strings[i];
                    if (entry != null)
                    {
                        // Check view model cache first
                        var cachedViewModel = SDBCacheManager.Instance.GetCachedViewModel(entry.HashId);

                        if (cachedViewModel != null)
                        {
                            // Update the index which might have changed
                            cachedViewModel.Index = i;
                            _stringsCollection.Add(cachedViewModel);
                        }
                        else
                        {
                            // Create new view model
                            var viewModel = new StringEntryViewModel(entry);
                            viewModel.Index = i;

                            // Cache the view model
                            SDBCacheManager.Instance.CacheViewModel(entry.HashId, viewModel);

                            _stringsCollection.Add(viewModel);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading initial chunk item {i}: {ex.Message}");
                    // Continue with next item rather than crashing
                }
            }
        }

        /// <summary>
        /// Schedule loading of remaining items to avoid UI freezing with improved memory management
        /// </summary>
        private void ScheduleRemainingItemsLoad(List<StringEntry> strings, int initialChunkSize)
        {
            if (strings.Count <= initialChunkSize)
                return;

            // Use a lower priority dispatcher to load remaining items
            Dispatcher.BeginInvoke(new Action(() =>
            {
                const int CHUNK_SIZE = 200; // Reduced from 500 to prevent memory pressure
                int totalChunks = (int)Math.Ceiling((double)(strings.Count - initialChunkSize) / CHUNK_SIZE);

                for (int chunk = 0; chunk < totalChunks; chunk++)
                {
                    // Check if UI is still responsive between chunks
                    if (chunk % 5 == 0)
                    {
                        // Yield to UI thread more frequently
                        Dispatcher.Yield(DispatcherPriority.Background);
                    }

                    int startIndex = initialChunkSize + (chunk * CHUNK_SIZE);
                    int endIndex = Math.Min(startIndex + CHUNK_SIZE, strings.Count);

                    // Load this chunk with error handling
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        try
                        {
                            if (i < strings.Count)
                            {
                                var entry = strings[i];
                                if (entry != null)
                                {
                                    // Check view model cache first
                                    var cachedViewModel = SDBCacheManager.Instance.GetCachedViewModel(entry.HashId);

                                    if (cachedViewModel != null)
                                    {
                                        // Update the index which might have changed
                                        cachedViewModel.Index = i;
                                        _stringsCollection.Add(cachedViewModel);
                                    }
                                    else
                                    {
                                        // Create new view model
                                        var viewModel = new StringEntryViewModel(entry);
                                        viewModel.Index = i;

                                        // Cache the view model
                                        SDBCacheManager.Instance.CacheViewModel(entry.HashId, viewModel);

                                        _stringsCollection.Add(viewModel);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading chunk item {i}: {ex.Message}");
                            // Continue with next item
                        }
                    }

                    // Force garbage collection between chunks to reduce memory pressure
                    if (chunk % 10 == 0)
                    {
                        GC.Collect(0, GCCollectionMode.Optimized, false);
                    }
                }

                // Force a full GC after loading all chunks
                GC.Collect();

                // Adjust row heights after all data is loaded
                AdjustRowHeightsForMultilineContent();
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// Restore previous selection if possible
        /// </summary>
        private void RestoreSelection(string hashId)
        {
            try
            {
                var matchingItem = _stringsCollection.FirstOrDefault(vm =>
                    vm.HashId.ToString() == hashId);

                if (matchingItem != null)
                {
                    StringsDataGrid.SelectedItem = matchingItem;
                    StringsDataGrid.ScrollIntoView(matchingItem);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring selection: {ex.Message}");
            }
        }

        // Improved ApplyLoadAnimation method
        private void ApplyLoadAnimation()
        {
            if (StringsDataGrid?.ItemsSource == null) return;

            // This will run animations in sequence for each row
            var delay = TimeSpan.FromMilliseconds(10); // Reduce delay to speed up overall animation
            var maxItems = Math.Min(30, StringsDataGrid.Items.Count); // Only animate visible items
            var currentDelay = TimeSpan.Zero;

            for (int i = 0; i < maxItems; i++)
            {
                try
                {
                    if (i >= StringsDataGrid.Items.Count)
                        break;

                    var item = StringsDataGrid.Items[i];
                    var row = (DataGridRow)StringsDataGrid.ItemContainerGenerator.ContainerFromItem(item);
                    if (row != null)
                    {
                        // Create and configure the animation
                        DoubleAnimation fadeAnimation = new DoubleAnimation
                        {
                            From = 0.0,
                            To = 1.0,
                            Duration = TimeSpan.FromMilliseconds(200), // Shorter duration
                            BeginTime = currentDelay
                        };

                        // Create and configure the translation animation
                        TranslateTransform transform = new TranslateTransform();
                        row.RenderTransform = transform;

                        DoubleAnimation slideAnimation = new DoubleAnimation
                        {
                            From = 15, // Start slightly to the right
                            To = 0,
                            Duration = TimeSpan.FromMilliseconds(200), // Shorter duration
                            BeginTime = currentDelay,
                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                        };

                        // Apply animations
                        row.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
                        transform.BeginAnimation(TranslateTransform.XProperty, slideAnimation);

                        // Increment delay for next row
                        currentDelay += delay;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Animation error on row {i}: {ex.Message}");
                    // Continue with other rows
                }
            }
        }

        /// <summary>
        /// Updated selection animation with green glow effect
        /// </summary>
        private void StringsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StringsDataGrid.SelectedItem == null) return;

            try
            {
                var row = (DataGridRow)StringsDataGrid.ItemContainerGenerator.ContainerFromItem(StringsDataGrid.SelectedItem);
                if (row != null)
                {
                    // Create a pulsing glow effect with green color to match theme
                    DropShadowEffect glowEffect = new DropShadowEffect
                    {
                        Color = (Color)ColorConverter.ConvertFromString("#00FF50"), // Changed to green
                        BlurRadius = 10,
                        ShadowDepth = 0,
                        Opacity = 0.7
                    };

                    row.Effect = glowEffect;

                    // Create continuous pulsing animation
                    DoubleAnimation pulseAnimation = new DoubleAnimation
                    {
                        From = 10,
                        To = 20,
                        Duration = TimeSpan.FromMilliseconds(1000),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever // Make it continuous
                    };

                    // Apply the animation - no completion handler to keep it visible
                    glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, pulseAnimation);

                    // Also animate opacity slightly for more visual interest
                    DoubleAnimation opacityAnimation = new DoubleAnimation
                    {
                        From = 0.5,
                        To = 0.8,
                        Duration = TimeSpan.FromMilliseconds(1200),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };

                    glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, opacityAnimation);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Selection animation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Extended selection handler that manages both new selections and deselections while handling row heights
        /// </summary>
        private void StringsDataGrid_SelectionChanged_Extended(object sender, SelectionChangedEventArgs e)
        {
            // First apply new effect to selected item
            StringsDataGrid_SelectionChanged(sender, e);

            // Then clear effects from deselected items
            if (e.RemovedItems != null && e.RemovedItems.Count > 0)
            {
                foreach (var item in e.RemovedItems)
                {
                    var row = (DataGridRow)StringsDataGrid.ItemContainerGenerator.ContainerFromItem(item);
                    if (row != null)
                    {
                        row.Effect = null;
                    }
                }
            }

            // Adjust row heights for newly selected items
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                foreach (var item in e.AddedItems)
                {
                    var row = (DataGridRow)StringsDataGrid.ItemContainerGenerator.ContainerFromItem(item);
                    if (row != null)
                    {
                        // Force height recalculation
                        row.InvalidateMeasure();
                        row.UpdateLayout();

                        // Limit height if needed
                        if (row.ActualHeight > 150)
                        {
                            row.Height = 150;
                        }
                    }
                }
            }
        }

        private void RefreshDisplay_Click(object sender, RoutedEventArgs e)
        {
            // Check if a file has been loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Force complete reload from disk
            LoadSdbFile(_currentFile);
        }

        private void RefreshDataGridDisplay()
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                // Force the DataGrid to refresh its display
                var temp = StringsDataGrid.ItemsSource;
                StringsDataGrid.ItemsSource = null;
                StringsDataGrid.ItemsSource = temp;

                // Force all items to refresh
                StringsDataGrid.Items.Refresh();

                // Update layout
                StringsDataGrid.UpdateLayout();

                UpdateStatus("Display refreshed");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// Refresh cache if the file has been modified externally
        /// </summary>
        private void RefreshCacheIfNeeded()
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentFile) && File.Exists(_currentFile))
                {
                    // Get the current file timestamp
                    DateTime currentTimestamp = File.GetLastWriteTime(_currentFile);

                    // Compare with cache timestamp by checking if raw data is outdated
                    byte[] cachedData = SDBCacheManager.Instance.GetCachedRawFile(_currentFile);
                    if (cachedData == null)
                    {
                        // No cached data or timestamp mismatch, reload from disk
                        byte[] newData = File.ReadAllBytes(_currentFile);

                        // Update all caches
                        SDBCacheManager.Instance.CacheRawFile(_currentFile, newData);

                        // Reload data from memory stream
                        using (MemoryStream ms = new MemoryStream(newData))
                        {
                            _sdbHandler.LoadSDBFromStream(ms);
                        }

                        // Update parsed entries cache
                        SDBCacheManager.Instance.CacheParsedEntries(_currentFile, _sdbHandler.GetAllStrings());

                        // Clear search cache since data has changed
                        SDBCacheManager.Instance.ClearSearchCache();

                        // Update status
                        UpdateStatus($"Refreshed from disk: {FormatPathForDisplay(_currentFile)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing cache: {ex.Message}");
            }
        }

        #endregion

        #region Search

        // Improved SearchBox_TextChanged handler with cancellation support
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Cancel any ongoing search
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();

            // Reset and restart the timer for each keystroke
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        // Async PerformSearch with cancellation support
        private async void PerformSearch()
        {
            string searchQuery = SearchBox?.Text?.ToLower() ?? string.Empty;

            // If query hasn't changed, do nothing
            if (searchQuery == _lastSearchQuery)
                return;

            _lastSearchQuery = searchQuery;

            // Create local copy of the token for this specific search
            var cancellationToken = _searchCancellationTokenSource.Token;

            try
            {
                // Perform search on a background thread to avoid UI freezing
                await Task.Run(() =>
                {
                    // Check for cancellation before returning to UI thread
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    // Call search on UI thread but with the ability to cancel
                    Dispatcher.Invoke(() => SearchStrings(), DispatcherPriority.Background);
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Search was canceled, do nothing
                Console.WriteLine("Search operation was canceled");
            }
            catch (Exception ex)
            {
                // Log the exception but don't crash
                Console.WriteLine($"Search error: {ex.Message}");
                UpdateStatus($"Search error: {ex.Message}");
            }
        }

        // Improved SearchStrings method with error handling
        private void SearchStrings()
        {
            // Check if a file has been loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                return; // Don't search if no file is loaded
            }

            // Set wait cursor
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                // Get selected search type safely with null checks
                string searchBy = "Text"; // Default value
                if (SearchTypeComboBox?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Content != null)
                {
                    searchBy = selectedItem.Content.ToString();
                }

                string searchQuery = SearchBox?.Text?.ToLower() ?? string.Empty;

                // Get filtered strings - from cache if available
                List<StringEntry> filteredStrings = null;

                try
                {
                    if (string.IsNullOrEmpty(searchQuery))
                    {
                        // For empty searches, get cached parsed entries
                        filteredStrings = SDBCacheManager.Instance.GetCachedParsedEntries(_currentFile);

                        // If cache miss, get from handler (which will now check cache)
                        if (filteredStrings == null)
                        {
                            filteredStrings = _sdbHandler.GetAllStrings() ?? new List<StringEntry>();

                            // Cache the entries for future searches
                            if (filteredStrings.Count > 0)
                            {
                                SDBCacheManager.Instance.CacheParsedEntries(_currentFile, filteredStrings);
                            }
                        }
                    }
                    else
                    {
                        // Check search cache first
                        filteredStrings = SDBCacheManager.Instance.GetCachedSearchResults(searchQuery, searchBy);

                        if (filteredStrings == null)
                        {
                            // Cache miss - perform the search (using null-safe calls)
                            filteredStrings = _sdbHandler?.SearchStrings(searchQuery, searchBy) ?? new List<StringEntry>();

                            // Cache the results for future searches
                            if (filteredStrings.Count > 0 && filteredStrings.Count < 5000)
                            {
                                SDBCacheManager.Instance.CacheSearchResults(searchQuery, searchBy, filteredStrings);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Recover from search errors with an empty result set
                    Console.WriteLine($"Search error: {ex.Message}");
                    filteredStrings = new List<StringEntry>();
                }

                // Make sure we have a valid list even if there was an error
                if (filteredStrings == null)
                {
                    filteredStrings = new List<StringEntry>();
                }

                // Update collection efficiently with error handling
                try
                {
                    PopulateStringCollection(filteredStrings);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error populating string collection: {ex.Message}");

                    // Fallback - reset to empty collection
                    _stringsCollection.Clear();
                }

                // Update status message
                UpdateStatus(string.IsNullOrEmpty(searchQuery)
                    ? $"Loaded {filteredStrings.Count} strings from cache"
                    : $"Found {filteredStrings.Count} matching strings");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Search error: {ex.Message}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void SearchTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Check if initialized properly
            if (SearchTypeComboBox?.SelectedItem == null)
            {
                return;
            }

            SearchStrings();
        }

        #endregion

        #region Context Menu Operations

        private void StringsDataGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Enable or disable menu items based on selection
            bool hasSelection = StringsDataGrid?.SelectedItem != null;
            bool hasMultipleSelection = StringsDataGrid?.SelectedItems?.Count > 1;

            // Check if context menu exists
            if (StringsDataGrid?.ContextMenu == null)
            {
                return;
            }

            // Get the menu items from the context menu
            foreach (var item in StringsDataGrid.ContextMenu.Items)
            {
                if (item is MenuItem menuItem)
                {
                    menuItem.IsEnabled = hasSelection;

                    // Update the Copy Hash ID menu item
                    if (menuItem.Name == "CopyHashMenuItem")
                    {
                        if (hasMultipleSelection)
                        {
                            menuItem.Header = $"Copy {StringsDataGrid.SelectedItems.Count} Hash IDs";
                            // Try to update icon if possible
                            if (menuItem.Icon is TextBlock iconTextBlock)
                            {
                                iconTextBlock.Text = "\uE8F4"; // Multiple pages icon
                            }
                        }
                        else
                        {
                            menuItem.Header = "Copy Hash ID";
                            // Reset icon
                            if (menuItem.Icon is TextBlock iconTextBlock)
                            {
                                iconTextBlock.Text = "\uE8C8"; // Single page icon
                            }
                        }
                    }
                }
            }
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedString();
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DeleteSelectedString();
        }

        private void CopyHashMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (StringsDataGrid?.SelectedItems != null && StringsDataGrid.SelectedItems.Count > 1)
                {
                    // Multiple selection - copy all with commas
                    var hashIds = new List<string>();

                    foreach (var item in StringsDataGrid.SelectedItems)
                    {
                        if (item is StringEntryViewModel selectedItem)
                        {
                            hashIds.Add(selectedItem.HashId.ToString());
                        }
                    }

                    if (hashIds.Count > 0)
                    {
                        string hashIdList = string.Join(", ", hashIds);
                        bool success = ClipboardManager.SafeCopy(hashIdList);

                        if (success)
                        {
                            UpdateStatus($"Copied {hashIds.Count} hash IDs to clipboard");
                        }
                        else
                        {
                            Dispatcher.BeginInvoke(new Action(async () =>
                            {
                                await Task.Delay(100);
                                bool retrySuccess = await ClipboardManager.SafeCopyAsync(hashIdList);
                                UpdateStatus(retrySuccess
                                    ? $"Copied {hashIds.Count} hash IDs to clipboard"
                                    : "Failed to copy hash IDs - clipboard may be in use");
                            }));
                        }
                    }
                }
                else if (StringsDataGrid?.SelectedItem is StringEntryViewModel selectedItem)
                {
                    // Single selection - existing behavior
                    string hashText = selectedItem.HashId.ToString();
                    bool success = ClipboardManager.SafeCopy(hashText);

                    if (success)
                    {
                        UpdateStatus($"Hash ID {hashText} copied to clipboard");
                        FlashCopiedText(selectedItem);
                    }
                    else
                    {
                        Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            await Task.Delay(100);
                            bool retrySuccess = await ClipboardManager.SafeCopyAsync(hashText);
                            UpdateStatus(retrySuccess
                                ? $"Hash ID {hashText} copied to clipboard"
                                : "Failed to copy hash ID - clipboard may be in use");
                        }));
                    }
                }
                else
                {
                    UpdateStatus("No item selected to copy hash ID");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in copy hash operation: {ex.Message}");
                UpdateStatus("Error during copy operation");
            }
        }



        private void CopyHexMenuItem_Click(object sender, RoutedEventArgs e)
{
    try
    {
        if (StringsDataGrid?.SelectedItem is StringEntryViewModel selectedItem)
        {
            string hexValue = selectedItem.HexValue;
            bool success = ClipboardManager.SafeCopy(hexValue);
            
            if (success)
            {
                UpdateStatus($"Hex value {hexValue} copied to clipboard");
                FlashCopiedText(selectedItem);
            }
            else
            {
                // Retry with a small delay
                Dispatcher.BeginInvoke(new Action(async () => 
                {
                    await Task.Delay(100);
                    bool retrySuccess = await ClipboardManager.SafeCopyAsync(hexValue);
                    UpdateStatus(retrySuccess 
                        ? $"Hex value {hexValue} copied to clipboard" 
                        : "Failed to copy hex value - clipboard may be in use");
                }));
            }
        }
        else
        {
            UpdateStatus("No item selected to copy hex value");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in copy hex operation: {ex.Message}");
        UpdateStatus("Error during copy operation");
    }
}

private void CopyReverseHexMenuItem_Click(object sender, RoutedEventArgs e)
{
    try
    {
        if (StringsDataGrid?.SelectedItem is StringEntryViewModel selectedItem)
        {
            string hexValue = selectedItem.HexValue;

            // Ensure even number of characters by padding if needed
            if (hexValue.Length % 2 != 0)
            {
                hexValue = "0" + hexValue;
            }

            // Split into byte pairs and reverse
            List<string> bytes = new List<string>();
            for (int i = 0; i < hexValue.Length; i += 2)
            {
                bytes.Add(hexValue.Substring(i, 2));
            }
            bytes.Reverse();

            // Join back to string
            string reversedHex = string.Join("", bytes);

            bool success = ClipboardManager.SafeCopy(reversedHex);
            
            if (success)
            {
                UpdateStatus($"Reversed hex value {reversedHex} copied to clipboard");
                FlashCopiedText(selectedItem);
            }
            else
            {
                // Retry with a small delay
                Dispatcher.BeginInvoke(new Action(async () => 
                {
                    await Task.Delay(100);
                    bool retrySuccess = await ClipboardManager.SafeCopyAsync(reversedHex);
                    UpdateStatus(retrySuccess 
                        ? $"Reversed hex value {reversedHex} copied to clipboard" 
                        : "Failed to copy reversed hex value - clipboard may be in use");
                }));
            }
        }
        else
        {
            UpdateStatus("No item selected to copy reversed hex value");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in copy reversed hex operation: {ex.Message}");
        UpdateStatus("Error during copy operation");
    }
}

private void CopyIndexMenuItem_Click(object sender, RoutedEventArgs e)
{
    try
    {
        if (StringsDataGrid?.SelectedItem is StringEntryViewModel selectedItem)
        {
            string indexText = selectedItem.Index.ToString();
            bool success = ClipboardManager.SafeCopy(indexText);
            
            if (success)
            {
                UpdateStatus($"Index {indexText} copied to clipboard");
                FlashCopiedText(selectedItem);
            }
            else
            {
                // Retry with a small delay
                Dispatcher.BeginInvoke(new Action(async () => 
                {
                    await Task.Delay(100);
                    bool retrySuccess = await ClipboardManager.SafeCopyAsync(indexText);
                    UpdateStatus(retrySuccess 
                        ? $"Index {indexText} copied to clipboard" 
                        : "Failed to copy index - clipboard may be in use");
                }));
            }
        }
        else
        {
            UpdateStatus("No item selected to copy index");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error in copy index operation: {ex.Message}");
        UpdateStatus("Error during copy operation");
    }
}

// Add visual feedback method
private void FlashCopiedText(StringEntryViewModel item)
{
    try
    {
        var row = (DataGridRow)StringsDataGrid.ItemContainerGenerator.ContainerFromItem(item);
        if (row != null)
        {
            // Create a green flash effect
            var flashAnimation = new System.Windows.Media.Animation.ColorAnimation(
                Colors.Green,
                Colors.Transparent,
                new Duration(TimeSpan.FromMilliseconds(500)));
                
            // Create a brush for the animation
            var animatedBrush = new SolidColorBrush(Colors.Transparent);
            animatedBrush.Opacity = 0.3;
            
            // Apply a temporary overlay to the row
            Border overlay = new Border
            {
                Background = animatedBrush,
                IsHitTestVisible = false
            };
            
            // Add the overlay
            var presenter = FindVisualChild<ContentPresenter>(row);
            if (presenter != null)
            {
                var grid = presenter.Content as Grid;
                if (grid == null)
                {
                    // Create a grid to hold the original content and overlay
                    grid = new Grid();
                    ContentPresenter originalContent = new ContentPresenter
                    {
                        Content = presenter.Content
                    };
                    grid.Children.Add(originalContent);
                    presenter.Content = grid;
                }
                
                grid.Children.Add(overlay);
                
                // Start the animation
                animatedBrush.BeginAnimation(SolidColorBrush.ColorProperty, flashAnimation);
                
                // Remove the overlay after animation completes
                Dispatcher.BeginInvoke(new Action(() => {
                    try { grid.Children.Remove(overlay); } catch { }
                }), System.Windows.Threading.DispatcherPriority.Background, 
                   new object[] { });
            }
        }
    }
    catch (Exception ex)
    {
        // Don't let animation errors affect main functionality
        Console.WriteLine($"Animation error: {ex.Message}");
    }
}

        private void StringsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditSelectedString();
        }

        private void EditSelectedString()
        {
            // Check if a file has been loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (StringsDataGrid?.SelectedItem is StringEntryViewModel selectedItem)
            {
                // Create edit dialog with index and hash ID
                var editDialog = new EditStringDialog(
                    selectedItem.Text,
                    selectedItem.Index,
                    selectedItem.HashId)
                {
                    Owner = this,
                    Title = $"Edit String (ID: {selectedItem.HashId})"
                };

                if (editDialog.ShowDialog() == true)
                {
                    string newText = editDialog.UpdatedText;
                    if (!string.IsNullOrEmpty(newText) && newText != selectedItem.Text)
                    {
                        // Update string in handler
                        if (_sdbHandler != null && _sdbHandler.UpdateString(selectedItem.HashId, newText))
                        {
                            // Create backup after editing
                            if (!string.IsNullOrEmpty(_currentFile))
                            {
                                _backupHandler.CreateBackup(_currentFile, "post_edit");
                            }

                            // Update UI to reflect changes
                            selectedItem.Text = newText;

                            // Update cached view model
                            SDBCacheManager.Instance.CacheViewModel(selectedItem.HashId, selectedItem);

                            UpdateStatus("String updated successfully");
                        }
                        else
                        {
                            MessageBox.Show("Failed to update string.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void DeleteSelectedString()
        {
            // Check if a file has been loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (StringsDataGrid?.SelectedItem is StringEntryViewModel selectedItem)
            {
                // Confirm deletion
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the string with Hash ID: {selectedItem.HashId}?",
                    "Confirm Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Delete string
                    if (_sdbHandler != null && _sdbHandler.DeleteString(selectedItem.HashId))
                    {
                        // Create backup after deletion
                        if (!string.IsNullOrEmpty(_currentFile))
                        {
                            _backupHandler.CreateBackup(_currentFile, "post_delete");
                        }

                        // Refresh display to reflect changes - caches are updated in DeleteString
                        DisplayStrings();
                        UpdateStatus("String deleted successfully");
                    }
                    else
                    {
                        MessageBox.Show("Failed to delete string.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        #endregion

        #region String Operations

        private void AddString_Click(object sender, RoutedEventArgs e)
        {
            // Check if a file has been loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowAddStringDialog();
        }

        private void AddMultiple_Click(object sender, RoutedEventArgs e)
        {
            // Check if a file has been loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowAddMultipleStringsDialog();
        }

        private void ShowAddStringDialog()
        {
            // Get next available hash ID
            uint nextHashId = _sdbHandler.GenerateUniqueHashId();
            int nextIndex = _stringsCollection?.Count ?? 0;

            // Create add dialog
            var addDialog = new AddStringDialog(nextHashId, nextIndex)
            {
                Owner = this,
                Title = "Add New String"
            };

            if (addDialog.ShowDialog() == true)
            {
                // Use correct property name of StringText from fixed AddStringDialog
                string newText = addDialog.StringText;
                uint hashId = addDialog.HashId;

                if (!string.IsNullOrEmpty(newText))
                {
                    // Add new string
                    if (_sdbHandler != null && _sdbHandler.AddString(newText, hashId))
                    {
                        // Create backup after adding
                        if (!string.IsNullOrEmpty(_currentFile))
                        {
                            _backupHandler.CreateBackup(_currentFile, "post_add");
                        }

                        // Add to UI collection to avoid full refresh
                        var newViewModel = new StringEntryViewModel(new StringEntry(hashId, newText));
                        newViewModel.Index = nextIndex;

                        // Cache the new view model
                        SDBCacheManager.Instance.CacheViewModel(hashId, newViewModel);

                        _stringsCollection.Add(newViewModel);

                        UpdateStatus("New string added successfully");
                    }
                    else
                    {
                        MessageBox.Show("Failed to add string.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ShowAddMultipleStringsDialog()
        {
            // Create dialog for adding multiple strings
            var addMultipleDialog = new AddMultipleStringsDialog(_sdbHandler)
            {
                Owner = this,
                Title = "Add Multiple Strings"
            };

            if (addMultipleDialog.ShowDialog() == true)
            {
                // Create backup after adding multiple
                if (!string.IsNullOrEmpty(_currentFile))
                {
                    _backupHandler.CreateBackup(_currentFile, "post_add_multiple");
                }

                // Refresh display to reflect changes - caches are updated in AddString
                DisplayStrings();
                UpdateStatus($"Added {addMultipleDialog.AddedCount} strings successfully");
            }
        }

        #endregion

        #region Import/Export Operations

        private void ImportStrings_Click(object sender, RoutedEventArgs e)
        {
            // Check if a file is loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ImportStringsFromFile();
        }

        private void ExportStrings_Click(object sender, RoutedEventArgs e)
        {
            // Check if a file is loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ExportStringsToFile();
        }

        private void ImportStringsFromFile()
        {
            try
            {
                // Set up dialog
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "CSV Files (*.csv)|*.csv|Excel Files (*.xlsx)|*.xlsx|All Files (*.*)|*.*",
                    Title = "Import Strings"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string filePath = openFileDialog.FileName;
                    string extension = Path.GetExtension(filePath).ToLower();

                    // Store the path and show the import options dialog
                    _droppedFilePath = filePath;
                    _droppedFileType = extension;

                    // Show the overlay with import options
                    DragDropOverlay.Visibility = Visibility.Visible;
                    DragDropTitleTextBlock.Text = GetDragDropTitleFromExtension(extension);
                    DragDropDescriptionTextBlock.Text = GetDragDropDescriptionFromExtension(extension);
                    FileNameTextBlock.Text = $"File: {Path.GetFileName(filePath)}";

                    // Show appropriate controls
                    ImportOptionsPanel.Visibility = Visibility.Visible;

                    // If it's an Excel file, update the UI with Excel-specific information
                    if (extension == ".xlsx" || extension == ".xls")
                    {
                        UpdateUIForExcelFile(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"Failed to import strings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportStringsToFile()
        {
            try
            {
                // Set up dialog
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx|CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    Title = "Export Strings"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;

                    // Check if SDBHandler is initialized
                    if (_sdbHandler == null)
                    {
                        _sdbHandler = new SDBHandler();
                        MessageBox.Show("SDB Handler was not initialized. Export may not work correctly.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    Mouse.OverrideCursor = Cursors.Wait;

                    // Perform export
                    bool success = _sdbHandler.ExportStrings(filePath);

                    if (success)
                    {
                        UpdateStatus($"Export completed: {Path.GetFileName(filePath)}");
                        MessageBox.Show(
                            $"Strings successfully exported to {filePath}",
                            "Export Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to export strings to file.", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    Mouse.OverrideCursor = null;
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"Failed to export strings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Advanced Operations

        private void MergeSdbs_Click(object sender, RoutedEventArgs e)
        {
            // Check if a file has been loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MergeSdbFiles();
        }

        private void ChangeLanguage_Click(object sender, RoutedEventArgs e)
        {
            // Check if a file has been loaded
            if (string.IsNullOrEmpty(_currentFile))
            {
                MessageBox.Show("Please open an SDB file first.", "No File Loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ChangeLanguage();
        }

        private void Rollback_Click(object sender, RoutedEventArgs e)
        {
            RollbackToBackup();
        }

        private void MergeSdbFiles()
        {
            try
            {
                // Check if a file is loaded
                if (string.IsNullOrEmpty(_currentFile))
                {
                    MessageBox.Show("Please load an SDB file first.", "Merge SDBs", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show enhanced merge dialog
                var mergeDialog = new EnhancedMergeSdbDialog
                {
                    Owner = this
                };

                if (mergeDialog.ShowDialog() == true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;

                    switch (mergeDialog.SelectedMode)
                    {
                        case EnhancedMergeSdbDialog.MergeMode.MergeAll:
                            MergeAllFromSdb();
                            break;

                        case EnhancedMergeSdbDialog.MergeMode.MergeFromSharelist:
                            MergeFromSharelist();
                            break;

                        case EnhancedMergeSdbDialog.MergeMode.MergeFromFile:
                            // Reset cursor before handling file import which shows its own dialogs
                            Mouse.OverrideCursor = null;
                            MergeFromSharelistFile(mergeDialog.FilePath);
                            // Don't reset after - MergeFromSharelistFile handles its own cursor state
                            return;

                        case EnhancedMergeSdbDialog.MergeMode.MergeSpecific:
                            MergeSpecificHashIds(mergeDialog.SpecificHashIds);
                            break;
                    }

                    Mouse.OverrideCursor = null;
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"Failed to merge SDBs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MergeAllFromSdb()
        {
            // Select second SDB file to merge
            var openFileDialog = new OpenFileDialog
            {
                Filter = "SDB Files (*.sdb)|*.sdb|All Files (*.*)|*.*",
                Title = "Select SDB File to Merge"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string secondFilePath = openFileDialog.FileName;

                // Create backup before merge
                _backupHandler.CreateBackup(_currentFile, "pre_merge");

                // Create temporary SDBHandler for the second file
                SDBHandler secondHandler = new SDBHandler();
                if (secondHandler.LoadSDB(secondFilePath))
                {
                    // Count before merge
                    int initialCount = _sdbHandler.GetAllStrings().Count;

                    // Add strings from second file to current
                    int addedCount = 0;
                    int duplicateCount = 0;

                    foreach (var entry in secondHandler.GetAllStrings())
                    {
                        if (entry == null) continue;

                        // Check if hash ID already exists in current file
                        if (_sdbHandler.GetStringByHash(entry.HashId) != null)
                        {
                            duplicateCount++;
                            continue; // Skip duplicate
                        }

                        // Add new string
                        if (_sdbHandler.AddString(entry.Text, entry.HashId))
                        {
                            addedCount++;
                        }
                    }

                    // Refresh display
                    DisplayStrings();
                    UpdateStatus($"Merged SDBs: {addedCount} strings added, {duplicateCount} duplicates skipped");

                    var successDialog = new ImportSuccessDialog(
    "SDB Merge Complete",
    $"Successfully merged entries from {Path.GetFileName(secondFilePath)}.",
    addedCount,      // new entries
    0,               // replaced entries (not applicable for SDB merge)
    duplicateCount   // skipped entries
)
                    {
                        Owner = this
                    };
                    successDialog.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Failed to load second SDB file for merging.", "Merge Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MergeFromSharelist()
        {
            if (SharelistManager.Instance.Entries.Count == 0)
            {
                MessageBox.Show("Sharelist is empty.", "Nothing to Merge", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check for conflicts first
            var conflicts = new List<ConflictEntry>();
            var noConflicts = new List<SharelistEntry>();

            foreach (var entry in SharelistManager.Instance.Entries)
            {
                var existingEntry = _sdbHandler.GetStringByHash(entry.HashId);
                if (existingEntry != null)
                {
                    conflicts.Add(new ConflictEntry
                    {
                        NewEntry = entry,
                        ExistingEntry = existingEntry,
                        Resolution = ConflictResolution.Skip // Default to skip
                    });
                }
                else
                {
                    noConflicts.Add(entry);
                }
            }

            // Handle conflicts if any exist
            if (conflicts.Count > 0)
            {
                var conflictDialog = new ConflictResolutionDialog(conflicts)
                {
                    Owner = this
                };

                if (conflictDialog.ShowDialog() != true)
                {
                    // User cancelled conflict resolution
                    UpdateStatus("Merge cancelled due to conflicts");
                    return;
                }

                // Process resolved conflicts
                foreach (var conflict in conflicts)
                {
                    switch (conflict.Resolution)
                    {
                        case ConflictResolution.UseNewHashId:
                            noConflicts.Add(new SharelistEntry(
                            conflict.NewHashId,
                            conflict.NewEntry.Text,
                            conflict.NewEntry.Text // Use the text as the third parameter
                        ));
                            break;
                        case ConflictResolution.Replace:
                            // This will be handled separately below
                            break;
                        case ConflictResolution.Skip:
                            // Do nothing - skip this entry
                            break;
                    }
                }
            }

            // Create backup before merge
            _backupHandler.CreateBackup(_currentFile, "pre_sharelist_merge");

            int addedCount = 0;
            int replacedCount = 0;
            int skippedCount = 0;

            // Handle replacements first
            foreach (var conflict in conflicts.Where(c => c.Resolution == ConflictResolution.Replace))
            {
                if (_sdbHandler.UpdateString(conflict.NewEntry.HashId, conflict.NewEntry.Text))
                {
                    replacedCount++;
                }
            }

            // Add non-conflicted entries
            foreach (var entry in noConflicts)
            {
                if (_sdbHandler.AddString(entry.Text, entry.HashId))
                {
                    addedCount++;
                }
            }

            // Count skipped entries
            skippedCount = conflicts.Count(c => c.Resolution == ConflictResolution.Skip);

            // Refresh display
            DisplayStrings();

            UpdateStatus($"Merged from sharelist: {addedCount} added, {replacedCount} replaced, {skippedCount} skipped");

            var successDialog = new ImportSuccessDialog(
    "Sharelist Merge Complete",
    $"Successfully merged {SharelistManager.Instance.Entries.Count} entries from your sharelist.",
    addedCount,      // new entries
    replacedCount,   // replaced entries  
    skippedCount     // skipped entries
)
            {
                Owner = this
            };
            successDialog.ShowDialog();

        }

        private void MergeFromSharelistFile(string filePath)
        {
            try
            {
                // Create backup before merge
                _backupHandler.CreateBackup(_currentFile, "pre_file_merge");

                // Reset cursor before showing dialogs
                Mouse.OverrideCursor = null;

                // Import entries to sharelist temporarily
                var result = SharelistManager.Instance.ImportFromFile(filePath);
                int imported = result.imported;
                SharelistMetadata fullMetadata = result.metadata;

                if (imported == 0)
                {
                    MessageBox.Show("No valid entries found in the file.", "Import Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show info dialog
                var infoDialog = new SDBEditor.Views.RollbackDialog.SharelistInfoDialog(fullMetadata, imported)
                {
                    Owner = this
                };

                if (infoDialog.ShowDialog() == true)
                {
                    // Set wait cursor for the merge operation
                    Mouse.OverrideCursor = Cursors.Wait;

                    try
                    {
                        // Now merge these entries
                        MergeFromSharelist();
                    }
                    finally
                    {
                        // Always reset cursor when merge completes
                        Mouse.OverrideCursor = null;
                    }
                }
                else
                {
                    // User cancelled, clear the sharelist
                    SharelistManager.Instance.Clear();
                    UpdateStatus("Import cancelled");
                }
            }
            catch (Exception ex)
            {
                // Reset cursor in case of error
                Mouse.OverrideCursor = null;
                MessageBox.Show($"Failed to import sharelist: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Final safety check to ensure cursor is reset
                Mouse.OverrideCursor = null;
            }
        }

        private void MergeSpecificHashIds(List<uint> hashIds)
        {
            // Select source SDB file
            var openFileDialog = new OpenFileDialog
            {
                Filter = "SDB Files (*.sdb)|*.sdb|All Files (*.*)|*.*",
                Title = "Select Source SDB File"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                // Create backup before merge
                _backupHandler.CreateBackup(_currentFile, "pre_specific_merge");

                // Load source SDB
                SDBHandler sourceHandler = new SDBHandler();
                if (sourceHandler.LoadSDB(openFileDialog.FileName))
                {
                    int addedCount = 0;
                    int notFoundCount = 0;
                    int duplicateCount = 0;

                    foreach (uint hashId in hashIds)
                    {
                        var entry = sourceHandler.GetStringByHash(hashId);

                        if (entry == null)
                        {
                            notFoundCount++;
                            continue;
                        }

                        // Check if already exists
                        if (_sdbHandler.GetStringByHash(hashId) != null)
                        {
                            duplicateCount++;
                            continue;
                        }

                        // Add the string
                        if (_sdbHandler.AddString(entry.Text, entry.HashId))
                        {
                            addedCount++;
                        }
                    }

                    // Refresh display
                    DisplayStrings();
                    UpdateStatus($"Specific merge: {addedCount} added, {duplicateCount} skipped, {notFoundCount} not found");

                    var successDialog = new ImportSuccessDialog(
    "Specific Merge Complete",
    $"Successfully merged specific hash IDs from {Path.GetFileName(openFileDialog.FileName)}.\nRequested: {hashIds.Count}, Not found: {notFoundCount}",
    addedCount,      // new entries
    0,               // replaced entries (not applicable)
    duplicateCount   // skipped entries
)
                    {
                        Owner = this
                    };
                    successDialog.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Failed to load source SDB file.", "Load Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ChangeLanguage()
        {
            try
            {
                // Check if a file is loaded
                if (string.IsNullOrEmpty(_currentFile))
                {
                    MessageBox.Show("Please load an SDB file first.", "Change Language", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get available languages from metadata
                var availableLanguages = _metadataHandler.GetAvailableLanguages(_sdbHandler.GameName);

                if (availableLanguages == null || availableLanguages.Count == 0)
                {
                    MessageBox.Show("No language metadata available for this game.", "Change Language", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Show language selection dialog
                var languageDialog = new LanguageSelectionDialog(availableLanguages)
                {
                    Owner = this,
                    Title = "Select Language"
                };

                if (languageDialog.ShowDialog() == true)
                {
                    string selectedLanguage = languageDialog.SelectedLanguage;

                    // Create backup before language change
                    _backupHandler.CreateBackup(_currentFile, $"pre_lang_change_{selectedLanguage}");

                    Mouse.OverrideCursor = Cursors.Wait;

                    // Load language metadata
                    var languageStrings = _metadataHandler.LoadLanguageStrings(_sdbHandler.GameName, selectedLanguage);

                    if (languageStrings != null && languageStrings.Count > 0)
                    {
                        int updatedCount = 0;

                        // Update existing strings
                        foreach (var pair in languageStrings)
                        {
                            uint hashId = pair.Key;
                            string text = pair.Value;

                            if (_sdbHandler.UpdateString(hashId, text))
                            {
                                updatedCount++;
                            }
                        }

                        // Update game language
                        _sdbHandler.Language = selectedLanguage;

                        // Update display
                        GameExtraInfoText.Text = $", Language: {_sdbHandler.Language}, Mangled: {_sdbHandler.IsMangled}";

                        // Refresh display to reflect changes - caches are updated in UpdateString
                        DisplayStrings();

                        UpdateStatus($"Language changed to {selectedLanguage}: {updatedCount} strings updated");

                        MessageBox.Show(
                            $"Language changed to {selectedLanguage}.\n{updatedCount} strings were updated.",
                            "Language Change Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("No strings found for the selected language.", "Language Change", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    Mouse.OverrideCursor = null;
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"Failed to change language: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RollbackToBackup()
        {
            try
            {
                // Get available backups
                var backups = _backupHandler.GetBackupList();

                if (backups == null || backups.Count == 0)
                {
                    MessageBox.Show("No backups available for rollback.", "Rollback", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Show backup selection dialog
                var rollbackDialog = new RollbackDialog(backups)
                {
                    Owner = this,
                    Title = "Select Backup for Rollback"
                };

                if (rollbackDialog.ShowDialog() == true)
                {
                    string selectedBackupPath = rollbackDialog.SelectedBackupPath;

                    // Create backup of current state before rollback
                    if (!string.IsNullOrEmpty(_currentFile))
                    {
                        _backupHandler.CreateBackup(_currentFile, "pre_rollback");
                    }

                    Mouse.OverrideCursor = Cursors.Wait;

                    // Restore from backup
                    if (_backupHandler.RestoreBackup(selectedBackupPath, _currentFile))
                    {
                        // Clear all caches for the current file
                        SDBCacheManager.Instance.ClearCache(_currentFile);

                        // Reload file after rollback
                        LoadSdbFile(_currentFile);

                        UpdateStatus("Rollback completed successfully");

                        MessageBox.Show(
                            "Rollback completed successfully. The file has been restored from the selected backup.",
                            "Rollback Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to restore from backup.", "Rollback Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    Mouse.OverrideCursor = null;
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show($"Failed to perform rollback: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}