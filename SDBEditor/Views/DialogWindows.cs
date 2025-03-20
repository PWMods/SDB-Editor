using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SDBEditor.Handlers;
using SDBEditor.Models;
using SDBEditor.ViewModels;

namespace SDBEditor.Views
{
    /// <summary>
    /// Dialog for editing a string
    /// </summary>
    public class EditStringDialog : Window
    {
        private TextBox _textBox;

        public string UpdatedText { get; private set; }

        public EditStringDialog(string currentText)
        {
            // Set initial values
            UpdatedText = currentText;

            // Configure window
            Title = "Edit String";
            Width = 800;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

            // Create layout
            Grid grid = new Grid();
            grid.Margin = new Thickness(20);
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Create text box with improved Unicode support
            _textBox = new TextBox
            {
                Text = currentText,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10),
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic") // Better Unicode support
            };

            // Apply optimal text rendering settings for Unicode
            TextOptions.SetTextFormattingMode(_textBox, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(_textBox, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(_textBox, TextHintingMode.Auto);

            Grid.SetRow(_textBox, 0);
            grid.Children.Add(_textBox);

            // Create button panel
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttonPanel, 1);

            // Create OK button
            Button okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            okButton.Click += (s, e) =>
            {
                UpdatedText = _textBox.Text;
                DialogResult = true;
            };
            buttonPanel.Children.Add(okButton);

            // Create Cancel button
            Button cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            cancelButton.Click += (s, e) => DialogResult = false;
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(buttonPanel);

            // Set content
            Content = grid;
        }
    }

    /// <summary>
    /// Dialog for adding a new string
    /// </summary>
    public class AddStringDialog : Window
    {
        private TextBox _textBox;

        public string StringText { get; private set; }
        public uint HashId { get; private set; }

        public AddStringDialog(uint hashId, int nextIndex)
        {
            // Set initial values
            HashId = hashId;
            StringText = string.Empty;

            // Configure window
            Title = "Add New String";
            Width = 800;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

            // Create layout
            Grid grid = new Grid();
            grid.Margin = new Thickness(20);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Create info panel
            StackPanel infoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(infoPanel, 0);

            // Index info
            StackPanel indexPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 20, 0) };

            TextBlock indexLabel = new TextBlock
            {
                Text = $"Index: {nextIndex}",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            indexPanel.Children.Add(indexLabel);

            Button copyIndexButton = new Button
            {
                Content = "Copy",
                Width = 60,
                Height = 25,
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            copyIndexButton.Click += (s, e) => Clipboard.SetText(nextIndex.ToString());
            indexPanel.Children.Add(copyIndexButton);

            infoPanel.Children.Add(indexPanel);

            // Hash ID info
            StackPanel hashPanel = new StackPanel { Orientation = Orientation.Horizontal };

            TextBlock hashLabel = new TextBlock
            {
                Text = $"Hash ID: {HashId}",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            hashPanel.Children.Add(hashLabel);

            Button copyHashButton = new Button
            {
                Content = "Copy",
                Width = 60,
                Height = 25,
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            copyHashButton.Click += (s, e) => Clipboard.SetText(HashId.ToString());
            hashPanel.Children.Add(copyHashButton);

            infoPanel.Children.Add(hashPanel);

            // Hex info
            StackPanel hexPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 0) };

            TextBlock hexLabel = new TextBlock
            {
                Text = $"Hex: {HashId:X}",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            hexPanel.Children.Add(hexLabel);

            Button copyHexButton = new Button
            {
                Content = "Copy",
                Width = 60,
                Height = 25,
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            copyHexButton.Click += (s, e) => Clipboard.SetText(HashId.ToString("X"));
            hexPanel.Children.Add(copyHexButton);

            infoPanel.Children.Add(hexPanel);

            grid.Children.Add(infoPanel);

            // Create text box
            TextBlock textLabel = new TextBlock
            {
                Text = "Enter the new string:",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(textLabel, 1);
            grid.Children.Add(textLabel);

            _textBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 25, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10),
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic") // Better Unicode support
            };

            // Apply optimal text rendering settings for Unicode
            TextOptions.SetTextFormattingMode(_textBox, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(_textBox, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(_textBox, TextHintingMode.Auto);

            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);

            // Create button panel
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(buttonPanel, 2);

            // Create OK button
            Button okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            okButton.Click += (s, e) =>
            {
                StringText = _textBox.Text;
                DialogResult = true;
            };
            buttonPanel.Children.Add(okButton);

            // Create Cancel button
            Button cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            cancelButton.Click += (s, e) => DialogResult = false;
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(buttonPanel);

            // Set content
            Content = grid;
        }
    }

    /// <summary>
    /// Dialog for adding multiple strings
    /// </summary>
    public class AddMultipleStringsDialog : Window
    {
        private SDBHandler _sdbHandler;
        private List<Tuple<TextBox, uint>> _stringEntries;
        private StackPanel _entriesPanel;
        private ComboBox _countComboBox;

        public int AddedCount { get; private set; }

        public AddMultipleStringsDialog(SDBHandler sdbHandler)
        {
            _sdbHandler = sdbHandler;
            _stringEntries = new List<Tuple<TextBox, uint>>();
            AddedCount = 0;

            // Configure window
            Title = "Add Multiple Strings";
            Width = 1200;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

            // Create layout
            Grid grid = new Grid();
            grid.Margin = new Thickness(20);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Create count selector
            StackPanel countPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 10)
            };

            TextBlock countLabel = new TextBlock
            {
                Text = "Number of strings to add:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            countPanel.Children.Add(countLabel);

            _countComboBox = new ComboBox
            {
                Width = 60,
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            for (int i = 1; i <= 50; i++)
            {
                _countComboBox.Items.Add(i.ToString());
            }
            _countComboBox.SelectedIndex = 0;
            _countComboBox.SelectionChanged += CountComboBox_SelectionChanged;

            countPanel.Children.Add(_countComboBox);

            Grid.SetRow(countPanel, 0);
            grid.Children.Add(countPanel);

            // Create entries panel inside a scroll viewer
            ScrollViewer scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            _entriesPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 10, 0, 0)
            };

            scrollViewer.Content = _entriesPanel;
            Grid.SetRow(scrollViewer, 1);
            grid.Children.Add(scrollViewer);

            // Create button panel
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            // Create OK button
            Button okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            okButton.Click += OkButton_Click;
            buttonPanel.Children.Add(okButton);

            // Create Cancel button
            Button cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            cancelButton.Click += (s, e) => DialogResult = false;
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            // Set content
            Content = grid;

            // Initialize the first entry
            UpdateEntries(1);
        }

        private void CountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_countComboBox.SelectedItem != null)
            {
                UpdateEntries(int.Parse(_countComboBox.SelectedItem.ToString()));
            }
        }

        private void UpdateEntries(int count)
        {
            _entriesPanel.Children.Clear();
            _stringEntries.Clear();

            uint baseHashId = _sdbHandler.GenerateUniqueHashId();
            int baseIndex = _sdbHandler.GetAllStrings().Count;

            for (int i = 0; i < count; i++)
            {
                uint hashId = baseHashId + (uint)i;
                int index = baseIndex + i;

                // Create entry widget
                Grid entryGrid = new Grid
                {
                    Margin = new Thickness(0, 0, 0, 5)
                };

                entryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                entryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                entryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                entryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Index
                TextBlock indexLabel = new TextBlock
                {
                    Text = $"Index: {index}",
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 10, 0)
                };
                Grid.SetColumn(indexLabel, 0);
                entryGrid.Children.Add(indexLabel);

                // Hash ID
                TextBlock hashLabel = new TextBlock
                {
                    Text = $"Hash ID: {hashId}",
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 10, 0)
                };
                Grid.SetColumn(hashLabel, 1);
                entryGrid.Children.Add(hashLabel);

                // Hex
                TextBlock hexLabel = new TextBlock
                {
                    Text = $"Hex: {hashId:X}",
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 10, 0)
                };
                Grid.SetColumn(hexLabel, 2);
                entryGrid.Children.Add(hexLabel);

                // Text input with Unicode support
                TextBox textBox = new TextBox
                {
                    Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(5),
                    Margin = new Thickness(5, 2, 0, 2),
                    Height = 28,
                    FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic") // Better Unicode support
                };

                // Apply optimal text rendering settings for Unicode
                TextOptions.SetTextFormattingMode(textBox, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(textBox, TextRenderingMode.ClearType);
                TextOptions.SetTextHintingMode(textBox, TextHintingMode.Auto);

                Grid.SetColumn(textBox, 3);
                entryGrid.Children.Add(textBox);

                _entriesPanel.Children.Add(entryGrid);
                _stringEntries.Add(new Tuple<TextBox, uint>(textBox, hashId));
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Process all entries
            AddedCount = 0;
            foreach (var entry in _stringEntries)
            {
                string text = entry.Item1.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    if (_sdbHandler.AddString(text, entry.Item2))
                    {
                        AddedCount++;
                    }
                }
            }

            DialogResult = true;
        }
    }

    /// <summary>
    /// Dialog for selecting a language
    /// </summary>
    public class LanguageSelectionDialog : Window
    {
        private ComboBox _languageComboBox;

        public string SelectedLanguage { get; private set; }

        public LanguageSelectionDialog(List<string> availableLanguages)
        {
            // Configure window
            Title = "Select Language";
            Width = 400;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

            // Create layout
            Grid grid = new Grid();
            grid.Margin = new Thickness(20);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Create title
            TextBlock titleLabel = new TextBlock
            {
                Text = "Select Language",
                Foreground = Brushes.White,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(titleLabel, 0);
            grid.Children.Add(titleLabel);

            // Create language selector
            TextBlock languageLabel = new TextBlock
            {
                Text = "Choose language:",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(languageLabel, 1);
            grid.Children.Add(languageLabel);

            _languageComboBox = new ComboBox
            {
                Margin = new Thickness(0, 5, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            foreach (string language in availableLanguages)
            {
                _languageComboBox.Items.Add(language);
            }

            if (_languageComboBox.Items.Count > 0)
            {
                _languageComboBox.SelectedIndex = 0;
            }

            Grid.SetRow(_languageComboBox, 2);
            grid.Children.Add(_languageComboBox);

            // Create button panel
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            // Create OK button
            Button okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            okButton.Click += (s, e) =>
            {
                SelectedLanguage = _languageComboBox.SelectedItem?.ToString();
                DialogResult = true;
            };
            buttonPanel.Children.Add(okButton);

            // Create Cancel button
            Button cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            cancelButton.Click += (s, e) => DialogResult = false;
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 3);
            grid.Children.Add(buttonPanel);

            // Set content
            Content = grid;
        }
    }

    /// <summary>
    /// Dialog for selecting a backup for rollback
    /// </summary>
    public class RollbackDialog : Window
    {
        private ListBox _backupListBox;

        public string SelectedBackupPath { get; private set; }

        public RollbackDialog(List<BackupEntry> backups)
        {
            // Configure window
            Title = "Select Backup for Rollback";
            Width = 600;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

            // Create layout
            Grid grid = new Grid();
            grid.Margin = new Thickness(20);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Create title
            TextBlock titleLabel = new TextBlock
            {
                Text = "Select a Backup to Restore",
                Foreground = Brushes.White,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 20)
            };
            Grid.SetRow(titleLabel, 0);
            grid.Children.Add(titleLabel);

            // Create backup list
            _backupListBox = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic") // Better Unicode support
            };

            // Apply optimal text rendering settings for Unicode
            TextOptions.SetTextFormattingMode(_backupListBox, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(_backupListBox, TextRenderingMode.ClearType);

            foreach (var backup in backups)
            {
                string displayName = $"{backup.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")}: {backup.Description}";
                ListBoxItem item = new ListBoxItem
                {
                    Content = displayName,
                    Tag = backup.FilePath
                };
                _backupListBox.Items.Add(item);
            }

            if (_backupListBox.Items.Count > 0)
            {
                _backupListBox.SelectedIndex = 0;
            }

            Grid.SetRow(_backupListBox, 1);
            grid.Children.Add(_backupListBox);

            // Create button panel
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            // Create OK button
            Button okButton = new Button
            {
                Content = "Restore",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            okButton.Click += (s, e) =>
            {
                if (_backupListBox.SelectedItem is ListBoxItem selectedItem)
                {
                    SelectedBackupPath = selectedItem.Tag.ToString();
                    DialogResult = true;
                }
            };
            buttonPanel.Children.Add(okButton);

            // Create Cancel button
            Button cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5),
                Background = new SolidColorBrush(Color.FromRgb(59, 59, 59)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            cancelButton.Click += (s, e) => DialogResult = false;
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            // Set content
            Content = grid;
        }
    }
}