using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using SDBEditor.Handlers;

namespace SDBEditor.Views
{
    /// <summary>
    /// Dialog for updating a sharelist with new entries and version information
    /// </summary>
    public class SharelistUpdateDialog : Window
    {
        private TextBox _versionTextBox;
        private TextBox _updateNotesTextBox;
        private ListBox _newEntriesListBox;
        private TextBlock _entryCountText;
        private TextBlock _originalVersionText;
        private int _newEntriesCount;

        public string NewVersion { get; private set; }
        public string UpdateNotes { get; private set; }
        public bool UpdateAccepted { get; private set; }

        public SharelistUpdateDialog(SharelistMetadata metadata, int newEntriesCount)
        {
            // Set initial values
            _newEntriesCount = newEntriesCount;
            NewVersion = SharelistManager.Instance.SuggestNextVersion();
            UpdateNotes = string.Empty;
            UpdateAccepted = false;

            // Configure window
            Title = "Update Sharelist";
            Width = 550;
            Height = 550;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

            // Create main container with border radius
            Border mainBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                Margin = new Thickness(0)
            };

            // Create layout
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Version info
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Update notes label
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Update notes
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // New entries label
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Entries list
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Header
            Border headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                Padding = new Thickness(20, 15, 20, 15)
            };

            StackPanel headerPanel = new StackPanel();

            TextBlock titleText = new TextBlock
            {
                Text = "Update Sharelist",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                FontSize = 18,
                FontWeight = FontWeights.Bold
            };
            headerPanel.Children.Add(titleText);

            StackPanel authorPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            authorPanel.Children.Add(new TextBlock
            {
                Text = "Author: ",
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontSize = 13
            });

            authorPanel.Children.Add(new TextBlock
            {
                Text = metadata.Author ?? "Anonymous",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            });
            headerPanel.Children.Add(authorPanel);

            headerBorder.Child = headerPanel;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // Version Info
            Border versionBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 43)),
                Padding = new Thickness(20, 12, 20, 12),
                Margin = new Thickness(0, 0, 0, 0)
            };

            Grid versionGrid = new Grid();
            versionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            versionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            versionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            versionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Original version
            TextBlock originalLabel = new TextBlock
            {
                Text = "Original Version:",
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetRow(originalLabel, 0);
            Grid.SetColumn(originalLabel, 0);
            versionGrid.Children.Add(originalLabel);

            _originalVersionText = new TextBlock
            {
                Text = metadata.Version ?? "1.0",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_originalVersionText, 0);
            Grid.SetColumn(_originalVersionText, 1);
            versionGrid.Children.Add(_originalVersionText);

            // New version
            TextBlock newVersionLabel = new TextBlock
            {
                Text = "New Version:",
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 10, 10, 0)
            };
            Grid.SetRow(newVersionLabel, 1);
            Grid.SetColumn(newVersionLabel, 0);
            versionGrid.Children.Add(newVersionLabel);

            _versionTextBox = new TextBox
            {
                Text = NewVersion,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                Padding = new Thickness(8, 5, 8, 5),
                Height = 32,
                Margin = new Thickness(0, 10, 0, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_versionTextBox, 1);
            Grid.SetColumn(_versionTextBox, 1);
            versionGrid.Children.Add(_versionTextBox);

            versionBorder.Child = versionGrid;
            Grid.SetRow(versionBorder, 1);
            mainGrid.Children.Add(versionBorder);

            // Update Notes Label
            TextBlock updateNotesLabel = new TextBlock
            {
                Text = "Update Notes (optional):",
                Foreground = Brushes.White,
                Margin = new Thickness(20, 15, 0, 5),
                FontSize = 13
            };
            Grid.SetRow(updateNotesLabel, 2);
            mainGrid.Children.Add(updateNotesLabel);

            // Update Notes TextBox
            _updateNotesTextBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 80,
                Margin = new Thickness(20, 0, 20, 15),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                Padding = new Thickness(10, 8, 10, 8),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(_updateNotesTextBox, 3);
            mainGrid.Children.Add(_updateNotesTextBox);

            // New Entries Label
            Border newEntriesHeaderBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                Padding = new Thickness(20, 10, 20, 10),
                Margin = new Thickness(0, 0, 0, 0)
            };

            StackPanel newEntriesHeaderPanel = new StackPanel { Orientation = Orientation.Horizontal };
            newEntriesHeaderPanel.Children.Add(new TextBlock
            {
                Text = "New Entries: ",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });

            // Entry count with badge
            Border countBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0, 100, 40)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(5, 0, 0, 0)
            };

            _entryCountText = new TextBlock
            {
                Text = newEntriesCount.ToString(),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 12
            };
            countBadge.Child = _entryCountText;
            newEntriesHeaderPanel.Children.Add(countBadge);

            newEntriesHeaderBorder.Child = newEntriesHeaderPanel;
            Grid.SetRow(newEntriesHeaderBorder, 4);
            mainGrid.Children.Add(newEntriesHeaderBorder);

            // New Entries List
            Border entriesListBorder = new Border
            {
                Margin = new Thickness(20, 10, 20, 15),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75))
            };

            _newEntriesListBox = new ListBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic")
            };

            // Add any new entries from the sharelist (marked with IsNewAddition = true)
            foreach (var entry in SharelistManager.Instance.Entries.Where(e => e.IsNewAddition))
            {
                Grid itemGrid = new Grid { Margin = new Thickness(5) };
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Hash ID
                TextBlock hashIdText = new TextBlock
                {
                    Text = entry.HashId.ToString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 80)),
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(hashIdText, 0);
                itemGrid.Children.Add(hashIdText);

                // Description/Text
                TextBlock descText = new TextBlock
                {
                    Text = entry.Description,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(descText, 1);
                itemGrid.Children.Add(descText);

                _newEntriesListBox.Items.Add(itemGrid);
            }

            // If there are no new entries, show a message
            if (_newEntriesListBox.Items.Count == 0)
            {
                _newEntriesListBox.Items.Add(new TextBlock
                {
                    Text = "No new entries to add. Make changes first before updating.",
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                    Margin = new Thickness(10),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            entriesListBorder.Child = _newEntriesListBox;
            Grid.SetRow(entriesListBorder, 5);
            mainGrid.Children.Add(entriesListBorder);

            // Buttons
            Grid buttonPanel = new Grid
            {
                Margin = new Thickness(20, 0, 20, 20)
            };

            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button cancelButton = CreateModernButton("Cancel", false);
            cancelButton.Width = 100;
            cancelButton.Height = 36;
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.Click += (s, e) => DialogResult = false;
            Grid.SetColumn(cancelButton, 1);
            buttonPanel.Children.Add(cancelButton);

            Button updateButton = CreateModernButton("Update", true);
            updateButton.Width = 100;
            updateButton.Height = 36;
            updateButton.Click += (s, e) =>
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(_versionTextBox.Text))
                {
                    MessageBox.Show("Please enter a new version number.", "Missing Version",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_newEntriesCount == 0)
                {
                    var result = MessageBox.Show("There are no new entries to add. Are you sure you want to create an update with only version changes?",
                                               "No New Entries",
                                               MessageBoxButton.YesNo,
                                               MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                        return;
                }

                // Save the values
                NewVersion = _versionTextBox.Text.Trim();
                UpdateNotes = _updateNotesTextBox.Text.Trim();
                UpdateAccepted = true;
                DialogResult = true;
            };
            Grid.SetColumn(updateButton, 2);
            buttonPanel.Children.Add(updateButton);

            Grid.SetRow(buttonPanel, 6);
            mainGrid.Children.Add(buttonPanel);

            mainBorder.Child = mainGrid;
            Content = mainBorder;
        }

        private Button CreateModernButton(string content, bool isPrimary)
        {
            Button button = new Button
            {
                Content = content,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                BorderThickness = new Thickness(0)
            };

            // Set up the button style
            Style buttonStyle = new Style(typeof(Button));

            // Default appearance
            Color bgColor = isPrimary ? Color.FromRgb(0, 170, 70) : Color.FromRgb(60, 60, 65);
            Color hoverColor = isPrimary ? Color.FromRgb(0, 200, 80) : Color.FromRgb(75, 75, 80);
            Color pressedColor = isPrimary ? Color.FromRgb(0, 230, 100) : Color.FromRgb(50, 50, 55);

            buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(bgColor)));
            buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            buttonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(15, 8, 15, 8)));
            buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, CreateButtonTemplate()));

            // Hover state
            Trigger mouseOverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            mouseOverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(hoverColor)));
            buttonStyle.Triggers.Add(mouseOverTrigger);

            // Pressed state
            Trigger pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(pressedColor)));
            buttonStyle.Triggers.Add(pressedTrigger);

            button.Style = buttonStyle;

            return button;
        }

        private ControlTemplate CreateButtonTemplate()
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));

            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

            // Add subtle shadow to button
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
}