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

            // Configure UI for better Unicode display
            ConfigureUnicodeSupport();

            // Initialize handlers
            _sdbHandler = new SDBHandler();
            _metadataHandler = new MetadataHandler();
            _backupHandler = new BackupHandler();

            // Initialize observable collection for DataGrid
            _stringsCollection = new ObservableCollection<StringEntryViewModel>();
            StringsDataGrid.ItemsSource = _stringsCollection;

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
            return extension == ".sdb" || extension == ".csv" || extension == ".xlsx" || extension == ".xls";
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
                StringsDataGrid.SelectionMode = DataGridSelectionMode.Single;
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

                // Clear SDBCacheManager caches for the previous file if different
                if (!string.IsNullOrEmpty(_currentFile) && _currentFile != filePath)
                {
                    SDBCacheManager.Instance.ClearCache(_currentFile);
                }

                try
                {
                    // Check if we have cached parsed entries first (fastest)
                    var cachedParsedEntries = SDBCacheManager.Instance.GetCachedParsedEntries(filePath);

                    if (cachedParsedEntries != null)
                    {
                        // Use the cached data directly
                        _sdbHandler.SetStrings(cachedParsedEntries);

                        // Update file info
                        _currentFile = filePath;

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
                if (gameName.Contains("2K25")) gameVersion = "25";  // Added WWE 2K25 support
                else if (gameName.Contains("2K24")) gameVersion = "24";
                else if (gameName.Contains("2K23")) gameVersion = "23";
                else if (gameName.Contains("2K22")) gameVersion = "22";
                else if (gameName.Contains("2K20")) gameVersion = "20";
                else if (gameName.Contains("2K19")) gameVersion = "19";
                // Extract any other versions
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
            GameVersionText.Text = $"Official Game Title: WWE2K{gameVersion}";

            // Force check mangled status from handler
            bool isActuallyMangled = _sdbHandler.IsMangled;

            // Ensure we display accurate information
            GameExtraInfoText.Text = $", Language: {_sdbHandler.Language}, Mangled: {isActuallyMangled}";

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
                    // Update caches - this is now handled directly in SDBHandler.SaveSDB
                    _currentFile = filePath;
                    SaveLastFile(filePath);
                    UpdateStatus($"Saved and cached: {FormatPathForDisplay(filePath)}");
                }

                Mouse.OverrideCursor = null;
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
                    Description = "Select Tribute or Baked Directory",
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

                    // Save using SDBHandler - caching is now handled inside SaveSDB
                    if (_sdbHandler.SaveSDB(sdbFile))
                    {
                        UpdateStatus($"Auto-saved to: {FormatPathForDisplay(sdbFile)}");
                    }
                    else
                    {
                        throw new Exception("Failed to save SDB file");
                    }

                    Mouse.OverrideCursor = null;
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
        private void DisplayStrings()
        {
            Mouse.OverrideCursor = Cursors.Wait;
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

                // Get strings from cache if available first
                List<StringEntry> strings = SDBCacheManager.Instance.GetCachedParsedEntries(_currentFile);

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

                // Update collection with performance optimizations
                PopulateStringCollection(strings);

                // Reset scrolling behavior to prevent glitches
                ResetScrollViewer();

                UpdateStatus($"Loaded {strings.Count} strings from cache");
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
        private void PopulateStringCollection(List<StringEntry> strings)
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

            // Disable UI updates during manipulation
            StringsDataGrid.BeginInit();

            try
            {
                // Clear the collection - do this inside a try/catch to prevent crashes
                try
                {
                    _stringsCollection.Clear();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error clearing collection: {ex.Message}");
                    // Try to recreate the collection if clearing failed
                    _stringsCollection = new ObservableCollection<StringEntryViewModel>();
                    StringsDataGrid.ItemsSource = _stringsCollection;
                }

                // For large collections, use chunking to improve performance
                if (strings.Count > 1000)
                {
                    // Load in smaller chunks to avoid UI freezing
                    int initialChunkSize = 300; // Reduced from 500 to prevent memory pressure
                    LoadInitialChunk(strings, initialChunkSize);

                    // Schedule loading of remaining items
                    ScheduleRemainingItemsLoad(strings, initialChunkSize);
                }
                else
                {
                    // For smaller collections, load everything at once with error handling
                    for (int i = 0; i < strings.Count; i++)
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
                            Console.WriteLine($"Error adding item {i}: {ex.Message}");
                            // Continue with next item rather than crashing
                        }
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

                // Force a complete refresh of the DataGrid to ensure proper display
                ForceDataGridRefresh();
            }
            finally
            {
                // Re-enable UI updates
                StringsDataGrid.EndInit();
            }

            // Only apply animation on initial load, not during search
            if (!_initialAnimationPlayed)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyLoadAnimation();
                    _initialAnimationPlayed = true;
                }), DispatcherPriority.Loaded);
            }

            // Adjust row heights for multi-line content
            AdjustRowHeightsForMultilineContent();
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

            // Check if the file has been modified externally
            RefreshCacheIfNeeded();

            DisplayStrings();
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

        private void CopyIndexMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (StringsDataGrid?.SelectedItem is StringEntryViewModel selectedItem)
            {
                Clipboard.SetText(selectedItem.Index.ToString());
                UpdateStatus("Index copied to clipboard");
            }
        }

        private void CopyHashMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (StringsDataGrid?.SelectedItem is StringEntryViewModel selectedItem)
            {
                Clipboard.SetText(selectedItem.HashId.ToString());
                UpdateStatus("Hash ID copied to clipboard");
            }
        }

        private void CopyHexMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (StringsDataGrid?.SelectedItem is StringEntryViewModel selectedItem)
            {
                Clipboard.SetText(selectedItem.HexValue);
                UpdateStatus("Hex value copied to clipboard");
            }
        }

        private void CopyReverseHexMenuItem_Click(object sender, RoutedEventArgs e)
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

                Clipboard.SetText(reversedHex);
                UpdateStatus("Reversed hex value copied to clipboard");
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
                // Create edit dialog
                var editDialog = new EditStringDialog(selectedItem.Text)
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

                    Mouse.OverrideCursor = Cursors.Wait;

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

                        // Refresh display to reflect changes - caches are updated in AddString
                        DisplayStrings();
                        UpdateStatus($"Merged SDBs: {addedCount} strings added, {duplicateCount} duplicates skipped");

                        MessageBox.Show(
                            $"Merge completed successfully.\nStrings added: {addedCount}\nDuplicates skipped: {duplicateCount}",
                            "Merge Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to load second SDB file for merging.", "Merge Failed", MessageBoxButton.OK, MessageBoxImage.Error);
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