using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OfficeOpenXml;
using SDBEditor.Models;

namespace SDBEditor.Handlers
{
    /// <summary>
    /// Handles Excel file operations for importing and exporting SDB data
    /// </summary>
    public class ExcelHandler
    {
        // License context for EPPlus (required starting with EPPlus 5.0)
        static ExcelHandler()
        {
            // Set the license context for non-commercial use (change as needed)
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
        }

        /// <summary>
        /// Read string entries from an Excel file
        /// </summary>
        /// <param name="filePath">Path to the Excel file</param>
        /// <param name="startRow">First row to import (1-based, default: 2 to skip header)</param>
        /// <param name="endRow">Last row to import (1-based, default: 0 means all rows)</param>
        /// <param name="columnIndex">Index of column to import text from (0-based, default: -1 means auto-detect)</param>
        /// <returns>List of strings found in the Excel file</returns>
        public static List<string> ReadExcelFile(string filePath, int startRow = 2, int endRow = 0, int columnIndex = -1)
        {
            List<string> importTexts = new List<string>();

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                // Get the first worksheet (or you can let the user select which sheet)
                var worksheet = package.Workbook.Worksheets[0];
                if (worksheet == null)
                    return importTexts;

                // Auto-detect the text column if not specified
                if (columnIndex < 0)
                {
                    // Check the header row (row 1) for a column named "Text" or similar
                    for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                    {
                        var cellValue = worksheet.Cells[1, col].Text;
                        if (!string.IsNullOrEmpty(cellValue) &&
                            cellValue.Contains("Text", StringComparison.OrdinalIgnoreCase))
                        {
                            columnIndex = col - 1; // Convert to 0-based
                            break;
                        }
                    }

                    // If no "Text" column found, default to column 3 (index 2) if available
                    if (columnIndex < 0 && worksheet.Dimension.End.Column >= 3)
                    {
                        columnIndex = 2;
                    }
                    // Otherwise use the last column
                    else if (columnIndex < 0 && worksheet.Dimension.End.Column > 0)
                    {
                        columnIndex = worksheet.Dimension.End.Column - 1;
                    }
                }

                // Convert to 1-based for EPPlus
                int excelColumnIndex = columnIndex + 1;

                // Validate column index
                if (excelColumnIndex <= 0 || excelColumnIndex > worksheet.Dimension.End.Column)
                {
                    throw new ArgumentException($"Invalid column index: {columnIndex}");
                }

                // If endRow is 0 or greater than the number of rows, set it to the max row
                if (endRow <= 0 || endRow > worksheet.Dimension.End.Row)
                {
                    endRow = worksheet.Dimension.End.Row;
                }

                // Validate startRow
                startRow = Math.Max(2, startRow); // Make sure we start after the header row (row 1)
                if (startRow > endRow)
                {
                    throw new ArgumentException("Start row cannot be greater than end row");
                }

                // Read data from the specified range
                for (int row = startRow; row <= endRow; row++)
                {
                    string cellValue = worksheet.Cells[row, excelColumnIndex].Text;

                    // Skip empty cells
                    if (!string.IsNullOrWhiteSpace(cellValue))
                    {
                        importTexts.Add(cellValue.Trim());
                    }
                }
            }

            return importTexts;
        }

        /// <summary>
        /// Read the full Excel file into a list of lists for advanced processing
        /// </summary>
        /// <param name="filePath">Path to the Excel file</param>
        /// <returns>List of rows, where each row is a list of cell values</returns>
        public static List<List<object>> ReadExcelFileAsTable(string filePath)
        {
            var result = new List<List<object>>();

            using (var package = new ExcelPackage(new FileInfo(filePath)))
            {
                var worksheet = package.Workbook.Worksheets[0];
                if (worksheet == null)
                    return result;

                for (int row = 1; row <= worksheet.Dimension.End.Row; row++)
                {
                    var rowData = new List<object>();
                    for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                    {
                        var cell = worksheet.Cells[row, col];

                        // Add the cell value to the row data
                        if (cell.Value == null)
                        {
                            rowData.Add(string.Empty);
                        }
                        else if (cell.Value is DateTime dateTime)
                        {
                            rowData.Add(dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                        else
                        {
                            rowData.Add(cell.Value);
                        }
                    }
                    result.Add(rowData);
                }
            }

            return result;
        }

        /// <summary>
        /// Export string data to an Excel file
        /// </summary>
        /// <param name="filePath">Path to save the Excel file</param>
        /// <param name="strings">Collection of string entries to export</param>
        /// <returns>True if export successful, false otherwise</returns>
        public static bool ExportToExcel(string filePath, List<StringEntry> strings)
        {
            try
            {
                using (var package = new ExcelPackage())
                {
                    // Create a new worksheet
                    var worksheet = package.Workbook.Worksheets.Add("SDB Strings");

                    // Add headers
                    worksheet.Cells[1, 1].Value = "Index";
                    worksheet.Cells[1, 2].Value = "String Hash ID";
                    worksheet.Cells[1, 3].Value = "Hex";
                    worksheet.Cells[1, 4].Value = "Text";

                    // Style headers
                    using (var range = worksheet.Cells[1, 1, 1, 4])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }

                    // Add data rows
                    for (int i = 0; i < strings.Count; i++)
                    {
                        var entry = strings[i];
                        if (entry == null) continue;

                        int row = i + 2; // +2 because row 1 is header, and i is 0-based
                        worksheet.Cells[row, 1].Value = i;
                        worksheet.Cells[row, 2].Value = entry.HashId;
                        worksheet.Cells[row, 3].Value = entry.HashId.ToString("X");
                        worksheet.Cells[row, 4].Value = entry.Text ?? string.Empty;
                    }

                    // Auto-size columns
                    worksheet.Cells.AutoFitColumns();

                    // Freeze the header row
                    worksheet.View.FreezePanes(2, 1);

                    // Save the Excel file
                    package.SaveAs(new FileInfo(filePath));
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excel export failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Process Excel file for import, detecting new entries vs. existing ones
        /// </summary>
        /// <param name="filePath">Path to the Excel file</param>
        /// <param name="existingTexts">Set of existing texts (lowercase for case-insensitive comparison)</param>
        /// <param name="startRow">First row to import (1-based)</param>
        /// <param name="endRow">Last row to import (1-based, 0 means all)</param>
        /// <param name="columnIndex">Column to import from (0-based, -1 means auto-detect)</param>
        /// <returns>Tuple containing lists of new entries, duplicate entries, and errors</returns>
        public static Tuple<List<string>, List<string>, List<string>> ProcessExcelFileWithDuplicateDetection(
            string filePath,
            HashSet<string> existingTexts,
            int startRow = 2,
            int endRow = 0,
            int columnIndex = -1)
        {
            var newEntries = new List<string>();
            var duplicateEntries = new List<string>();
            var errorEntries = new List<string>();

            try
            {
                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    if (worksheet == null)
                        return new Tuple<List<string>, List<string>, List<string>>(
                            newEntries, duplicateEntries, errorEntries);

                    // Auto-detect the text column if not specified
                    if (columnIndex < 0)
                    {
                        // Check the header row for a column named "Text" or similar
                        for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                        {
                            var cellValue = worksheet.Cells[1, col].Text;
                            if (!string.IsNullOrEmpty(cellValue) &&
                                cellValue.Contains("Text", StringComparison.OrdinalIgnoreCase))
                            {
                                columnIndex = col - 1; // Convert to 0-based
                                break;
                            }
                        }

                        // If no "Text" column found, default to column 3 (index 2) if available
                        if (columnIndex < 0 && worksheet.Dimension.End.Column >= 3)
                        {
                            columnIndex = 2;
                        }
                        // Otherwise use the last column
                        else if (columnIndex < 0 && worksheet.Dimension.End.Column > 0)
                        {
                            columnIndex = worksheet.Dimension.End.Column - 1;
                        }
                    }

                    // Convert to 1-based for EPPlus
                    int excelColumnIndex = columnIndex + 1;

                    // Validate column index
                    if (excelColumnIndex <= 0 || excelColumnIndex > worksheet.Dimension.End.Column)
                    {
                        throw new ArgumentException($"Invalid column index: {columnIndex}");
                    }

                    // If endRow is 0 or greater than the number of rows, set it to the max row
                    if (endRow <= 0 || endRow > worksheet.Dimension.End.Row)
                    {
                        endRow = worksheet.Dimension.End.Row;
                    }

                    // Validate startRow
                    startRow = Math.Max(2, startRow); // Make sure we start after the header row
                    if (startRow > endRow)
                    {
                        throw new ArgumentException("Start row cannot be greater than end row");
                    }

                    // Process each row
                    for (int row = startRow; row <= endRow; row++)
                    {
                        try
                        {
                            string cellValue = worksheet.Cells[row, excelColumnIndex].Text;

                            // Skip empty cells
                            if (string.IsNullOrWhiteSpace(cellValue) || cellValue.ToLower() == "nan")
                            {
                                continue;
                            }

                            string trimmedValue = cellValue.Trim();

                            // Check for duplicates
                            if (existingTexts.Contains(trimmedValue.ToLower()))
                            {
                                duplicateEntries.Add(trimmedValue);
                            }
                            else
                            {
                                newEntries.Add(trimmedValue);
                                // Add to existing texts set to catch duplicates within the file itself
                                existingTexts.Add(trimmedValue.ToLower());
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing Excel row {row}: {ex.Message}");
                            errorEntries.Add($"Row {row}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excel processing failed: {ex.Message}");
                errorEntries.Add($"General error: {ex.Message}");
            }

            return new Tuple<List<string>, List<string>, List<string>>(
                newEntries, duplicateEntries, errorEntries);
        }

        /// <summary>
        /// Get column names from the first row of the Excel file
        /// </summary>
        /// <param name="filePath">Path to the Excel file</param>
        /// <returns>List of column names or null if an error occurs</returns>
        public static List<string> GetExcelColumnNames(string filePath)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    if (worksheet == null)
                        return null;

                    var columnNames = new List<string>();
                    for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                    {
                        string cellValue = worksheet.Cells[1, col].Text ?? $"Column {col}";
                        columnNames.Add(cellValue);
                    }

                    return columnNames;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get Excel column names: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the total number of rows in the Excel file
        /// </summary>
        /// <param name="filePath">Path to the Excel file</param>
        /// <returns>Total number of rows or 0 if an error occurs</returns>
        public static int GetExcelRowCount(string filePath)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    return worksheet?.Dimension?.End.Row ?? 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get Excel row count: {ex.Message}");
                return 0;
            }
        }
    }
}