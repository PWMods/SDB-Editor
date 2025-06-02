using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Added for Mouse
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Threading.Tasks;
using SDBEditor.Handlers;
using SDBEditor.Models;
using SDBEditor.ViewModels;
using System.IO;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace SDBEditor.Views
{
    /// <summary>
    /// Dialog for editing a string with modern design
    /// </summary>
    public class EditStringDialog : Window
    {
        private TextBox _textBox;

        public string UpdatedText { get; private set; }

        public EditStringDialog(string currentText, int index, uint hashId)
        {
            // Set initial values
            UpdatedText = currentText;

            // Configure window
            Title = "Edit String";
            Width = 800;
            Height = 400;
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
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Badges panel (NEW)
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Create custom title bar
            Border titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                Height = 40,
                Padding = new Thickness(15, 0, 10, 0)
            };

            Grid titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Window buttons

            // Icon with glowing effect
            Rectangle iconRect = new Rectangle
            {
                Width = 16,
                Height = 16,
                Fill = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                RadiusX = 2,
                RadiusY = 2,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Add glow effect to icon
            iconRect.Effect = new DropShadowEffect
            {
                BlurRadius = 15,
                ShadowDepth = 0,
                Color = Color.FromRgb(0, 255, 80),
                Opacity = 0.7
            };

            Grid.SetColumn(iconRect, 0);
            titleGrid.Children.Add(iconRect);

            // Title text
            TextBlock titleText = new TextBlock
            {
                Text = "Edit String",
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

            // Make title bar draggable
            titleBar.MouseLeftButtonDown += (s, e) => DragMove();

            // NEW: Badges panel with Index and Hash ID
            Border badgesPanelBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
                Padding = new Thickness(20, 12, 20, 12)
            };

            StackPanel badgesPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Hash ID badge with gray background
            Border hashIdBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(150, 50, 50, 55)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 15, 0)
            };
            StackPanel hashIdPanel = new StackPanel { Orientation = Orientation.Horizontal };
            hashIdPanel.Children.Add(new TextBlock
            {
                Text = "Hash ID: ",
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });
            TextBlock hashValueDisplay = new TextBlock
            {
                Text = hashId.ToString(),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            hashIdPanel.Children.Add(hashValueDisplay);
            hashIdBadge.Child = hashIdPanel;
            badgesPanel.Children.Add(hashIdBadge);

            // Hex badge with green background
            Border hexBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(150, 0, 50, 25)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6)
            };
            StackPanel hexPanel = new StackPanel { Orientation = Orientation.Horizontal };
            hexPanel.Children.Add(new TextBlock
            {
                Text = "Hex: ",
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });
            TextBlock hexValueDisplay = new TextBlock
            {
                Text = hashId.ToString("X"),
                Foreground = new SolidColorBrush(Color.FromRgb(150, 255, 150)),
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas, Courier New, Monospace"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            hexPanel.Children.Add(hexValueDisplay);
            hexBadge.Child = hexPanel;
            badgesPanel.Children.Add(hexBadge);

            badgesPanelBorder.Child = badgesPanel;
            Grid.SetRow(badgesPanelBorder, 1);
            mainGrid.Children.Add(badgesPanelBorder);

            // Content area
            Grid contentGrid = new Grid
            {
                Margin = new Thickness(20)
            };

            // Create text box with improved styling
            _textBox = new TextBox
            {
                Text = currentText,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(83, 83, 83)),
                Padding = new Thickness(12),
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic"),
                FontSize = 13,
                CaretBrush = new SolidColorBrush(Color.FromRgb(0, 255, 80))
            };

            // Apply optimal text rendering settings for Unicode
            TextOptions.SetTextFormattingMode(_textBox, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(_textBox, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(_textBox, TextHintingMode.Auto);

            // Add subtle effect for depth
            _textBox.Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 2,
                Direction = 315,
                Color = Colors.Black,
                Opacity = 0.4
            };

            contentGrid.Children.Add(_textBox);
            Grid.SetRow(contentGrid, 2); // Updated to row 2 since we added the badges panel
            mainGrid.Children.Add(contentGrid);

            // Button panel
            Grid buttonPanel = new Grid
            {
                Margin = new Thickness(20, 10, 20, 20)
            };

            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Create Cancel button with modern styling
            Button cancelButton = CreateModernButton("Cancel", false);
            cancelButton.Width = 100;
            cancelButton.Height = 36;
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.Click += (s, e) => DialogResult = false;
            Grid.SetColumn(cancelButton, 1);
            buttonPanel.Children.Add(cancelButton);

            // Create OK button
            Button okButton = CreateModernButton("OK", true);
            okButton.Width = 100;
            okButton.Height = 36;
            okButton.Click += (s, e) =>
            {
                UpdatedText = _textBox.Text;
                DialogResult = true;
            };
            Grid.SetColumn(okButton, 2);
            buttonPanel.Children.Add(okButton);

            Grid.SetRow(buttonPanel, 3); // Updated to row 3
            mainGrid.Children.Add(buttonPanel);

            mainBorder.Child = mainGrid;
            Content = mainBorder;
        }

        /// <summary>
        /// Creates a modern button with hover and pressed states
        /// </summary>
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

        /// <summary>
        /// Creates a modern button template with hover and pressed states
        /// </summary>
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

    /// <summary>
    /// Dialog for adding a new string with modern design
    /// </summary>
    public class AddStringDialog : Window
    {
        private TextBox _textBox;
        private TextBox _hashIdTextBox;
        private CheckBox _useCustomHashCheckbox;
        private bool _useCustomHash = false;
        private TextBlock _hashValueDisplay;
        private TextBlock _hexValueDisplay;
        private DispatcherTimer _hashInputTimer;
        private Border _hashOptionsPanel;
        private Dictionary<string, uint> _validationCache = new Dictionary<string, uint>();

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
            Height = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(25, 25, 25));

            // Setup timer for hash ID validation
            _hashInputTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _hashInputTimer.Tick += (s, e) => {
                _hashInputTimer.Stop();
                ValidateHashID();
            };

            // Create main container with border radius
            Border mainBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(25, 25, 25)),
                Margin = new Thickness(0)
            };

            // Create layout
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Info panel
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Hash ID Options
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Label
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Text box
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Create title
            TextBlock titleText = new TextBlock
            {
                Text = "Add New String",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(20, 15, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(titleText, 0);
            mainGrid.Children.Add(titleText);

            // Create improved info panel with badges layout
            Border infoPanelBackground = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 35)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20, 15, 20, 15)
            };

            // Main vertical stack panel for info panel
            StackPanel hashPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // ----- Moved Badges Panel ABOVE the Checkbox -----

            // Horizontal stack panel for badges
            StackPanel badgesPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10)
            };

            // Index badge with orange background
            Border indexBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(150, 75, 50, 0)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 15, 0)
            };
            StackPanel indexPanel = new StackPanel { Orientation = Orientation.Horizontal };
            indexPanel.Children.Add(new TextBlock
            {
                Text = "Index: ",
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });
            indexPanel.Children.Add(new TextBlock
            {
                Text = nextIndex.ToString(),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            });
            indexBadge.Child = indexPanel;
            badgesPanel.Children.Add(indexBadge);

            // Hash ID badge with gray background
            Border hashIdBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(150, 50, 50, 55)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 15, 0)
            };
            StackPanel hashIdPanel = new StackPanel { Orientation = Orientation.Horizontal };
            hashIdPanel.Children.Add(new TextBlock
            {
                Text = "Hash ID: ",
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });
            _hashValueDisplay = new TextBlock
            {
                Text = HashId.ToString(),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            hashIdPanel.Children.Add(_hashValueDisplay);
            hashIdBadge.Child = hashIdPanel;
            badgesPanel.Children.Add(hashIdBadge);

            // Hex badge with green background
            Border hexBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(150, 0, 50, 25)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6)
            };
            StackPanel hexPanel = new StackPanel { Orientation = Orientation.Horizontal };
            hexPanel.Children.Add(new TextBlock
            {
                Text = "Hex: ",
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            });
            _hexValueDisplay = new TextBlock
            {
                Text = HashId.ToString("X"),
                Foreground = new SolidColorBrush(Color.FromRgb(150, 255, 150)),
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas, Courier New, Monospace"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            hexPanel.Children.Add(_hexValueDisplay);
            hexBadge.Child = hexPanel;
            badgesPanel.Children.Add(hexBadge);

            // Add badges FIRST (top)
            hashPanel.Children.Add(badgesPanel);

            // Now add Checkbox SECOND (below badges)
            _useCustomHashCheckbox = new CheckBox
            {
                Content = "Use Custom Hash ID",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 10, 10),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = false
            };
            _useCustomHashCheckbox.Checked += (s, e) =>
            {
                _useCustomHash = true;
                _hashOptionsPanel.Visibility = Visibility.Visible;
                _hashIdTextBox.IsEnabled = true;
                _hashIdTextBox.Focus();
                _hashIdTextBox.SelectAll();
            };
            _useCustomHashCheckbox.Unchecked += (s, e) =>
            {
                _useCustomHash = false;
                _hashOptionsPanel.Visibility = Visibility.Collapsed;
                _hashIdTextBox.IsEnabled = false;
                _hashIdTextBox.Text = HashId.ToString();
                _hashValueDisplay.Text = HashId.ToString();
                _hexValueDisplay.Text = HashId.ToString("X");
            };
            hashPanel.Children.Add(_useCustomHashCheckbox);

            // Finish assembling
            infoPanelBackground.Child = hashPanel;
            Grid.SetRow(infoPanelBackground, 1);
            mainGrid.Children.Add(infoPanelBackground);

            // Create collapsible hash options panel
            _hashOptionsPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 35)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20, 10, 20, 10),
                Margin = new Thickness(0, -16, 0, 0),
                Visibility = Visibility.Collapsed // Initially hidden
            };

            Grid hashOptionsGrid = new Grid();
            hashOptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Label
            hashOptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // TextBox
            hashOptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Spacer

            // Custom Hash ID label
            TextBlock hashIdFieldLabel = new TextBlock
            {
                Text = "Custom Hash ID:",
                Foreground = Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, -6, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hashIdFieldLabel, 0);
            hashOptionsGrid.Children.Add(hashIdFieldLabel);

            // Custom Hash ID input field
            _hashIdTextBox = new TextBox
            {
                Text = HashId.ToString(),
                IsEnabled = false,
                Width = 180,
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                Padding = new Thickness(10, 5, 10, 5),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontFamily = new FontFamily("Consolas, Courier New, Monospace"),
                Margin = new Thickness(0, -16, 0, 0)
            };
            
            // Improved validation with debouncing
            _hashIdTextBox.TextChanged += (s, e) =>
            {
                if (!_useCustomHash) return;

                string text = _hashIdTextBox.Text;
                if (string.IsNullOrEmpty(text))
                {
                    _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75));
                    return;
                }

                // Try parse as decimal first
                if (ulong.TryParse(text, out ulong tempValue))
                {
                    if (tempValue <= uint.MaxValue)
                    {
                        uint customHash = (uint)tempValue;
                        HashId = customHash;
                        _hashValueDisplay.Text = customHash.ToString();
                        _hexValueDisplay.Text = customHash.ToString("X");
                        _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                    }
                    else
                    {
                        _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                    }
                }
                // Try parse as hex with 0x prefix
                else if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    string hexPart = text.Substring(2);
                    if (ulong.TryParse(hexPart, System.Globalization.NumberStyles.HexNumber, null, out ulong tempHexValue))
                    {
                        if (tempHexValue <= uint.MaxValue)
                        {
                            uint hexValue = (uint)tempHexValue;
                            HashId = hexValue;
                            _hashValueDisplay.Text = hexValue.ToString();
                            _hexValueDisplay.Text = hexValue.ToString("X");
                            _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                        }
                        else
                        {
                            _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                        }
                    }
                    else
                    {
                        _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                    }
                }
                else
                {
                    _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                }
            };

            Grid.SetColumn(_hashIdTextBox, 1);
            hashOptionsGrid.Children.Add(_hashIdTextBox);

            _hashOptionsPanel.Child = hashOptionsGrid;
            Grid.SetRow(_hashOptionsPanel, 2);
            mainGrid.Children.Add(_hashOptionsPanel);

            // String text label
            TextBlock textLabel = new TextBlock
            {
                Text = "Enter the new string:",
                Foreground = Brushes.White,
                Margin = new Thickness(20, 10, 0, 10),
                FontSize = 14
            };
            Grid.SetRow(textLabel, 3);
            mainGrid.Children.Add(textLabel);

            // Improved text input area
            Border textBoxContainer = new Border
            {
                Margin = new Thickness(20, 0, 20, 0),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70)),
                CornerRadius = new CornerRadius(4),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 2,
                    Direction = 315,
                    Color = Colors.Black,
                    Opacity = 0.4
                }
            };

            // Create text box with improved styling
            _textBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.Transparent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12),
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic"),
                FontSize = 13,
                CaretBrush = new SolidColorBrush(Color.FromRgb(0, 255, 80))
            };

            // Apply optimal text rendering settings for Unicode
            TextOptions.SetTextFormattingMode(_textBox, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(_textBox, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(_textBox, TextHintingMode.Auto);

            textBoxContainer.Child = _textBox;
            Grid.SetRow(textBoxContainer, 4);
            mainGrid.Children.Add(textBoxContainer);

            // Create button panel with modern design
            Grid buttonPanel = new Grid
            {
                Margin = new Thickness(20, 20, 20, 20)
            };

            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Create Cancel button
            Button cancelButton = CreateModernButton("Cancel", false);
            cancelButton.Width = 100;
            cancelButton.Height = 36;
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.Click += (s, e) => DialogResult = false;
            Grid.SetColumn(cancelButton, 1);
            buttonPanel.Children.Add(cancelButton);

            // Create OK button
            Button okButton = CreateModernButton("OK", true);
            okButton.Width = 100;
            okButton.Height = 36;
            okButton.Click += (s, e) =>
            {
                StringText = _textBox.Text;

                // Get custom hash ID if enabled
                if (_useCustomHash && !string.IsNullOrEmpty(_hashIdTextBox.Text))
                {
                    // Try first as decimal
                    if (ulong.TryParse(_hashIdTextBox.Text, out ulong tempValue))
                    {
                        uint safeHashId = tempValue > uint.MaxValue ? uint.MaxValue : (uint)tempValue;
                        HashId = safeHashId;
                    }
                    // Then try as hex
                    else if (_hashIdTextBox.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
                             ulong.TryParse(_hashIdTextBox.Text.Substring(2),
                             System.Globalization.NumberStyles.HexNumber,
                             null, out ulong tempHexValue))
                    {
                        uint safeHashId = tempHexValue > uint.MaxValue ? uint.MaxValue : (uint)tempHexValue;
                        HashId = safeHashId;
                    }
                }

                DialogResult = true;
            };
            Grid.SetColumn(okButton, 2);
            buttonPanel.Children.Add(okButton);

            Grid.SetRow(buttonPanel, 5);
            mainGrid.Children.Add(buttonPanel);

            mainBorder.Child = mainGrid;
            Content = mainBorder;
        }
        
        // Helper method to validate hash ID
        private void ValidateHashID()
        {
            try
            {
                if (string.IsNullOrEmpty(_hashIdTextBox.Text))
                {
                    _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75));
                    return;
                }

                // Check validation cache first
                if (_validationCache.TryGetValue(_hashIdTextBox.Text, out uint cachedHash))
                {
                    UpdateHashDisplays(cachedHash);
                    _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                    return;
                }

                // Try parse as decimal first - use ulong for large values
                if (ulong.TryParse(_hashIdTextBox.Text, out ulong tempValue))
                {
                    // Make sure the value fits in uint range
                    if (tempValue <= uint.MaxValue)
                    {
                        uint customHash = (uint)tempValue;
                        HashId = customHash;
                        UpdateHashDisplays(customHash);
                        _validationCache[_hashIdTextBox.Text] = customHash;
                        _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                    }
                    else
                    {
                        _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                    }
                }
                // Try parse as hex with 0x prefix
                else if (_hashIdTextBox.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    string hexPart = _hashIdTextBox.Text.Substring(2);
                    if (ulong.TryParse(hexPart, System.Globalization.NumberStyles.HexNumber, null, out ulong tempHexValue))
                    {
                        if (tempHexValue <= uint.MaxValue)
                        {
                            uint hexValue = (uint)tempHexValue;
                            HashId = hexValue;
                            UpdateHashDisplays(hexValue);
                            _validationCache[_hashIdTextBox.Text] = hexValue;
                            _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                        }
                        else
                        {
                            _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                        }
                    }
                    else
                    {
                        _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                    }
                }
                else
                {
                    _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating hash ID: {ex.Message}");
                _hashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80));
            }
        }

        // Method to update display textblocks
        private void UpdateHashDisplays(uint hashId)
        {
            _hashValueDisplay.Text = hashId.ToString();
            _hexValueDisplay.Text = hashId.ToString("X");
        }

        /// <summary>
        /// Creates a modern button with hover and pressed states
        /// </summary>
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

        /// <summary>
        /// Creates a modern button template with rounded corners
        /// </summary>
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

    /// <summary>
    /// Dialog for adding multiple strings - Completely modernized
    /// </summary>
    public class AddMultipleStringsDialog : Window
    {
        private SDBHandler _sdbHandler;
        private List<Tuple<TextBox, TextBox, uint>> _stringEntries; // Text, HashID, Original HashID
        private StackPanel _entriesPanel;
        private ComboBox _countComboBox;
        private CheckBox _useCustomHashesCheckbox;
        private TextBox _baseHashIdTextBox;
        private bool _useCustomHashes = false;

        public int AddedCount { get; private set; }

        public AddMultipleStringsDialog(SDBHandler sdbHandler)
        {
            _sdbHandler = sdbHandler;
            _stringEntries = new List<Tuple<TextBox, TextBox, uint>>();
            AddedCount = 0;

            // Configure window
            Title = "Add Multiple Strings";
            Width = 1200;
            Height = 650; // Increased to accommodate additional controls
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
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Count selector
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Hash options
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Entries
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Create custom title bar
            Border titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                Height = 40,
                Padding = new Thickness(15, 0, 10, 0)
            };

            Grid titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Window buttons

            // Icon with glowing effect
            Rectangle iconRect = new Rectangle
            {
                Width = 16,
                Height = 16,
                Fill = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                RadiusX = 2,
                RadiusY = 2,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Add glow effect to icon
            iconRect.Effect = new DropShadowEffect
            {
                BlurRadius = 15,
                ShadowDepth = 0,
                Color = Color.FromRgb(0, 255, 80),
                Opacity = 0.7
            };

            Grid.SetColumn(iconRect, 0);
            titleGrid.Children.Add(iconRect);

            // Title text
            TextBlock titleText = new TextBlock
            {
                Text = "Add Multiple Strings",
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

            // Make title bar draggable
            titleBar.MouseLeftButtonDown += (s, e) => DragMove();

            // Count selector with modern styling
            Border countSelectorPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
                Padding = new Thickness(20, 15, 20, 15),
                CornerRadius = new CornerRadius(4, 4, 0, 0)
            };

            StackPanel countPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock countLabel = new TextBlock
            {
                Text = "Number of strings to add:",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0),
                FontSize = 13
            };
            countPanel.Children.Add(countLabel);

            // Create modern ComboBox style
            Style comboBoxStyle = new Style(typeof(ComboBox));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BackgroundProperty, new SolidColorBrush(Color.FromRgb(50, 50, 55))));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.ForegroundProperty, new SolidColorBrush(Color.FromRgb(50, 50, 55))));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BorderThicknessProperty, new Thickness(1)));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(70, 70, 75))));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.PaddingProperty, new Thickness(10, 5, 10, 5)));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.FontSizeProperty, 13.0));

            // Style for ComboBoxItem
            Style comboBoxItemStyle = new Style(typeof(ComboBoxItem));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(50, 50, 55))));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(10, 5, 10, 5)));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.FontSizeProperty, 13.0));

            // Add triggers for hover and selected states
            Trigger hoverTrigger = new Trigger { Property = ComboBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 100, 50))));
            hoverTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.Green));
            comboBoxItemStyle.Triggers.Add(hoverTrigger);

            Trigger selectedTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 150, 60))));
            selectedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            comboBoxItemStyle.Triggers.Add(selectedTrigger);

            // Create and configure ComboBox
            _countComboBox = new ComboBox
            {
                Width = 80,
                Height = 32,
                Style = comboBoxStyle,
                ItemContainerStyle = comboBoxItemStyle
            };

            // Add items
            for (int i = 1; i <= 100; i++)
            {
                _countComboBox.Items.Add(i.ToString());
            }
            _countComboBox.SelectedIndex = 0;
            _countComboBox.SelectionChanged += CountComboBox_SelectionChanged;
            countPanel.Children.Add(_countComboBox);

            countSelectorPanel.Child = countPanel;
            Grid.SetRow(countSelectorPanel, 1);
            mainGrid.Children.Add(countSelectorPanel);

            // Add Custom Hash Options Panel
            Border hashOptionsPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
                Padding = new Thickness(20, 15, 20, 15)
            };

            Grid hashOptionsGrid = new Grid();
            hashOptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Checkbox
            hashOptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Label
            hashOptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // TextBox
            hashOptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Spacer
            hashOptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Checkbox

            hashOptionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            hashOptionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Custom hashes checkbox
            _useCustomHashesCheckbox = new CheckBox
            {
                Content = "Enable Custom Hash IDs",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 20, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = false
            };
            _useCustomHashesCheckbox.Checked += (s, e) =>
            {
                _useCustomHashes = true;
                _baseHashIdTextBox.IsEnabled = true;
                UpdateHashIdInput(true);
            };
            _useCustomHashesCheckbox.Unchecked += (s, e) =>
            {
                _useCustomHashes = false;
                _baseHashIdTextBox.IsEnabled = false;
                UpdateHashIdInput(false);
            };
            Grid.SetColumn(_useCustomHashesCheckbox, 0);
            Grid.SetColumnSpan(_useCustomHashesCheckbox, 2);
            hashOptionsGrid.Children.Add(_useCustomHashesCheckbox);

            // Base Hash ID Label
            TextBlock baseHashLabel = new TextBlock
            {
                Text = "Base Hash ID:",
                Foreground = Brushes.White,
                Margin = new Thickness(24, 10, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(baseHashLabel, 1);
            Grid.SetColumn(baseHashLabel, 1);
            hashOptionsGrid.Children.Add(baseHashLabel);

            // Base Hash ID TextBox
            uint nextHashId = _sdbHandler.GenerateUniqueHashId();
            _baseHashIdTextBox = new TextBox
            {
                Text = nextHashId.ToString(),
                IsEnabled = false,
                Width = 120,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(83, 83, 83)),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 10, 0, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _baseHashIdTextBox.TextChanged += (s, e) =>
            {
                if (_useCustomHashes && uint.TryParse(_baseHashIdTextBox.Text, out uint baseHash))
                {
                    UpdateEntries(int.Parse(_countComboBox.SelectedItem.ToString()));
                }
                else if (_useCustomHashes && _baseHashIdTextBox.Text.StartsWith("0x") &&
                        uint.TryParse(_baseHashIdTextBox.Text.Substring(2),
                        System.Globalization.NumberStyles.HexNumber,
                        null, out uint hexValue))
                {
                    _baseHashIdTextBox.Text = hexValue.ToString();
                    UpdateEntries(int.Parse(_countComboBox.SelectedItem.ToString()));
                }
            };
            Grid.SetRow(_baseHashIdTextBox, 1);
            Grid.SetColumn(_baseHashIdTextBox, 2);
            hashOptionsGrid.Children.Add(_baseHashIdTextBox);

            // Help text
            TextBlock hashHelpText = new TextBlock
            {
                Text = "Custom Hash IDs: For first entry (sequential) or each entry individually",
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(10, 10, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(hashHelpText, 0);
            Grid.SetColumn(hashHelpText, 3);
            Grid.SetColumnSpan(hashHelpText, 2);
            hashOptionsGrid.Children.Add(hashHelpText);

            hashOptionsPanel.Child = hashOptionsGrid;
            Grid.SetRow(hashOptionsPanel, 2);
            mainGrid.Children.Add(hashOptionsPanel);

            // Create entries panel inside a scroll viewer with modern styling
            Border entriesContainer = new Border
            {
                Margin = new Thickness(20, 0, 20, 20),
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                BorderThickness = new Thickness(1, 0, 1, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                CornerRadius = new CornerRadius(0, 0, 4, 4)
            };

            // Add a subtle shadow effect for depth
            entriesContainer.Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 3,
                Direction = 315,
                Color = Colors.Black,
                Opacity = 0.4
            };

            // Create modern ScrollViewer with thin scrollbar
            ScrollViewer scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(1),
                Background = Brushes.Transparent
            };

            _entriesPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10)
            };

            scrollViewer.Content = _entriesPanel;
            entriesContainer.Child = scrollViewer;
            Grid.SetRow(entriesContainer, 3);
            mainGrid.Children.Add(entriesContainer);

            // Create button panel with modern design
            Grid buttonPanel = new Grid
            {
                Margin = new Thickness(20, 0, 20, 20)
            };

            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Create Cancel button
            Button cancelButton = CreateModernButton("Cancel", false);
            cancelButton.Width = 100;
            cancelButton.Height = 36;
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.Click += (s, e) => DialogResult = false;
            Grid.SetColumn(cancelButton, 1);
            buttonPanel.Children.Add(cancelButton);

            // Create OK button with ID for finding it later
            Button okButton = CreateModernButton("OK", true);
            okButton.Width = 100;
            okButton.Height = 36;
            okButton.Name = "OkButton";
            okButton.Click += OkButton_Click;
            Grid.SetColumn(okButton, 2);
            buttonPanel.Children.Add(okButton);

            Grid.SetRow(buttonPanel, 4);
            mainGrid.Children.Add(buttonPanel);

            mainBorder.Child = mainGrid;
            Content = mainBorder;

            // Initialize the first entry
            UpdateEntries(1);
        }

        // Fixed method without 'private' modifier
        async void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Show loading cursor to indicate processing
            Mouse.OverrideCursor = Cursors.Wait;

            // Disable the button to prevent multiple clicks
            var okButton = sender as Button;
            if (okButton != null)
                okButton.IsEnabled = false;

            // Execute validation and processing on background thread
            await ValidateAndProcessEntries();
        }

        // Fixed method without 'private' modifier
        async Task ValidateAndProcessEntries()
        {
            // Process all entries
            AddedCount = 0;

            // Create local collections to avoid UI thread access
            var usedHashIds = new HashSet<uint>();
            var warningMessages = new List<string>();
            var validEntries = new List<(string text, uint hashId)>();

            try
            {
                // First pass - validate IDs
                foreach (var entry in _stringEntries)
                {
                    string text = entry.Item1.Text.Trim();
                    if (string.IsNullOrEmpty(text)) continue;

                    uint hashId;
                    if (_useCustomHashes)
                    {
                        // Fix: Ensure we're correctly parsing the full value
                        string hashText = entry.Item2.Text.Trim();
                        if (!uint.TryParse(hashText, out hashId))
                        {
                            warningMessages.Add($"Invalid Hash ID: {hashText} - Entry skipped");
                            continue;
                        }

                        if (usedHashIds.Contains(hashId))
                        {
                            warningMessages.Add($"Duplicate Hash ID: {hashId} - Entry skipped");
                            continue;
                        }

                        usedHashIds.Add(hashId);
                    }
                    else
                    {
                        hashId = entry.Item3; // Use the original uint value directly
                    }

                    validEntries.Add((text, hashId));
                }

                // Show warnings if any
                bool proceed = true;

                if (warningMessages.Count > 0)
                {
                    // Use Dispatcher to show message box on UI thread
                    await Dispatcher.InvokeAsync(() => {
                        if (MessageBox.Show(
                            $"The following issues were found:\n\n{string.Join("\n", warningMessages)}\n\nDo you want to continue with valid entries only?",
                            "Validation Warnings",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        {
                            proceed = false;
                        }
                    });
                }

                if (!proceed)
                {
                    // Reset UI state
                    Dispatcher.Invoke(() => {
                        Mouse.OverrideCursor = null;
                        var okButton = this.FindName("OkButton") as Button;
                        if (okButton != null)
                            okButton.IsEnabled = true;
                    });
                    return;
                }

                // Second pass - add valid entries in batches
                const int batchSize = 50;

                for (int i = 0; i < validEntries.Count; i += batchSize)
                {
                    var batch = validEntries.Skip(i).Take(batchSize);

                    foreach (var (text, hashId) in batch)
                    {
                        if (_sdbHandler.AddString(text, hashId))
                        {
                            AddedCount++;
                        }
                    }

                    // Yield briefly to keep UI responsive
                    await Task.Delay(5);
                }

                // Complete the operation on UI thread
                Dispatcher.Invoke(() => {
                    DialogResult = true;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing entries: {ex.Message}");

                // Show error on UI thread
                Dispatcher.Invoke(() => {
                    MessageBox.Show(
                        $"An error occurred while processing entries: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    Mouse.OverrideCursor = null;
                    var okButton = this.FindName("OkButton") as Button;
                    if (okButton != null)
                        okButton.IsEnabled = true;
                });
            }
            finally
            {
                // Always reset cursor
                Dispatcher.Invoke(() => {
                    Mouse.OverrideCursor = null;
                });
            }
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

        private void CountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_countComboBox.SelectedItem != null)
                {
                    string countText = _countComboBox.SelectedItem.ToString();
                    if (int.TryParse(countText, out int count))
                    {
                        UpdateEntries(count);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in CountComboBox_SelectionChanged: {ex.Message}");
                // Default to 1 if there's an error
                UpdateEntries(1);
            }
        }

        private void UpdateEntries(int count)
        {
            _entriesPanel.Children.Clear();
            _stringEntries.Clear();

            uint baseHashId = 0;

            // Get starting hash ID
            if (_useCustomHashes && !string.IsNullOrEmpty(_baseHashIdTextBox.Text) &&
                uint.TryParse(_baseHashIdTextBox.Text, out uint customBaseHash))
            {
                baseHashId = customBaseHash;
            }
            else
            {
                baseHashId = _sdbHandler.GenerateUniqueHashId();
            }

            int baseIndex = _sdbHandler.GetAllStrings().Count;

            // Create modern style for text fields
            Style textBoxStyle = new Style(typeof(TextBox));
            textBoxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, new SolidColorBrush(Color.FromRgb(45, 45, 48))));
            textBoxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, Brushes.White));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(0)));
            textBoxStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(10, 6, 10, 6)));
            textBoxStyle.Setters.Add(new Setter(TextBox.FontFamilyProperty, new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic")));
            textBoxStyle.Setters.Add(new Setter(TextBox.FontSizeProperty, 13.0));
            textBoxStyle.Setters.Add(new Setter(TextBox.CaretBrushProperty, new SolidColorBrush(Color.FromRgb(0, 255, 80))));

            for (int i = 0; i < count; i++)
            {
                uint hashId = baseHashId + (uint)i;
                int index = baseIndex + i;

                // Create modern entry widget
                Border entryBorder = new Border
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Color.FromRgb(40, 40, 43))
                };

                // Add subtle hover effect using a unique style
                Style entryHoverStyle = new Style(typeof(Border));
                entryHoverStyle.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(40, 40, 43))));

                // Create the trigger for mouseover
                Trigger mouseOverTrigger = new Trigger { Property = Border.IsMouseOverProperty, Value = true };
                mouseOverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(50, 50, 53))));
                entryHoverStyle.Triggers.Add(mouseOverTrigger);

                entryBorder.Style = entryHoverStyle;

                Grid entryGrid = new Grid { Margin = new Thickness(12, 8, 12, 8) };
                entryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // Index
                entryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); // HashID (added 30px for width)
                entryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); // Hex
                entryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text

                // Index with orange background pill
                Border indexBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 255, 165, 0)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };

                TextBlock indexLabel = new TextBlock
                {
                    Text = $"Index: {index}",
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 0)),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12
                };

                indexBadge.Child = indexLabel;
                Grid.SetColumn(indexBadge, 0);
                entryGrid.Children.Add(indexBadge);

                // Hash ID with gray background pill - now contains TextBox when custom hashes enabled
                Border hashBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 180, 180, 180)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Grid hashGrid = new Grid();
                TextBlock hashLabel = new TextBlock
                {
                    Text = $"ID: ",
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                hashGrid.Children.Add(hashLabel);

                // Hash ID TextBox (editable when custom hashes enabled)
                TextBox hashIdTextBox = new TextBox
                {
                    Text = hashId.ToString(), // Convert uint to string
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Margin = new Thickness(18, 0, 0, 0),
                    FontSize = 12,
                    IsEnabled = _useCustomHashes,
                    Width = 90
                };
                // Make sure the text field gets the full value:
                hashIdTextBox.Text = hashId.ToString();
                // Listen for changes to update the hex display
                hashIdTextBox.TextChanged += (s, e) =>
                {
                    if (!_useCustomHashes) return;

                    // Find the Hex label in the same row
                    var parentGrid = hashIdTextBox.Parent as Grid;
                    if (parentGrid != null && parentGrid.Parent is Border hashBorder)
                    {
                        var entryBorderGrid = hashBorder.Parent as Grid;
                        if (entryBorderGrid != null)
                        {
                            var hexBorder = entryBorderGrid.Children.OfType<Border>().FirstOrDefault(b =>
                                Grid.GetColumn(b) == 2);

                            if (hexBorder != null && hexBorder.Child is StackPanel hexPanel)
                            {
                                var hexValue = hexPanel.Children.OfType<TextBlock>().ElementAtOrDefault(1);
                                if (hexValue != null && uint.TryParse(hashIdTextBox.Text, out uint parsedHash))
                                {
                                    // Ensure we are correctly showing the full hex value
                                    hexValue.Text = parsedHash.ToString("X");
                                }
                            }
                        }
                    }
                };

                hashGrid.Children.Add(hashIdTextBox);

                hashBadge.Child = hashGrid;
                Grid.SetColumn(hashBadge, 1);
                entryGrid.Children.Add(hashBadge);


                // Text input with improved styling
                Border textBoxBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 33)),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(0)
                };

                TextBox textBox = new TextBox
                {
                    Style = textBoxStyle,
                    Height = 32,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0)
                };

                // Apply optimal text rendering settings for Unicode
                TextOptions.SetTextFormattingMode(textBox, TextFormattingMode.Ideal);
                TextOptions.SetTextRenderingMode(textBox, TextRenderingMode.ClearType);
                TextOptions.SetTextHintingMode(textBox, TextHintingMode.Auto);

                textBoxBorder.Child = textBox;
                Grid.SetColumn(textBoxBorder, 3);
                entryGrid.Children.Add(textBoxBorder);

                entryBorder.Child = entryGrid;
                _entriesPanel.Children.Add(entryBorder);
                _stringEntries.Add(new Tuple<TextBox, TextBox, uint>(textBox, hashIdTextBox, hashId));
            }
        }

        /// <summary>
        /// Updates the hash ID input fields visibility and state
        /// </summary>
        private void UpdateHashIdInput(bool enabled)
        {
            // Reset the hash ID input field text if needed
            if (!enabled)
            {
                uint baseHashId = _sdbHandler.GenerateUniqueHashId();
                _baseHashIdTextBox.Text = baseHashId.ToString();
            }

            // Update all entry fields
            foreach (var entry in _stringEntries)
            {
                // Get the TextBox for hash ID
                TextBox hashIdTextBox = entry.Item2;

                // Get the original uint value
                uint originalHash = entry.Item3;

                // Enable/disable the TextBox
                hashIdTextBox.IsEnabled = enabled;

                if (!enabled)
                {
                    // Reset to original value - convert uint to string explicitly 
                    // and ensure we're not truncating anywhere
                    string hashIdStr = originalHash.ToString();
                    Console.WriteLine($"Resetting hash ID to: {hashIdStr}");
                    hashIdTextBox.Text = hashIdStr;
                }
            }

            // If needed, regenerate entries
            if (_stringEntries.Count > 0)
            {
                int count = 1;
                if (_countComboBox.SelectedItem != null)
                {
                    count = int.Parse(_countComboBox.SelectedItem.ToString());
                }
                UpdateEntries(count);
            }
        }
    }

    /// <summary>
    /// Modern dialog for selecting a language
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

            // Create main container with border radius
            Border mainBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                Margin = new Thickness(0)
            };

            // Create layout
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Label
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // ComboBox
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Spacer
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Create custom title bar
            Border titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                Height = 40,
                Padding = new Thickness(15, 0, 10, 0)
            };

            Grid titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title

            // Icon with glowing effect
            Rectangle iconRect = new Rectangle
            {
                Width = 16,
                Height = 16,
                Fill = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                RadiusX = 2,
                RadiusY = 2,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Add glow effect to icon
            iconRect.Effect = new DropShadowEffect
            {
                BlurRadius = 15,
                ShadowDepth = 0,
                Color = Color.FromRgb(0, 255, 80),
                Opacity = 0.7
            };

            Grid.SetColumn(iconRect, 0);
            titleGrid.Children.Add(iconRect);

            // Title text
            TextBlock titleText = new TextBlock
            {
                Text = "Select Language",
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

            // Make title bar draggable
            titleBar.MouseLeftButtonDown += (s, e) => DragMove();

            // Create language selector label
            TextBlock languageLabel = new TextBlock
            {
                Text = "Choose language:",
                Foreground = Brushes.White,
                Margin = new Thickness(20, 20, 20, 5),
                FontSize = 13
            };
            Grid.SetRow(languageLabel, 1);
            mainGrid.Children.Add(languageLabel);

            // Create ComboBox with modern styling
            Border comboBoxBorder = new Border
            {
                Margin = new Thickness(20, 0, 20, 0),
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(0)
            };

            // Create ComboBox style
            Style comboBoxStyle = new Style(typeof(ComboBox));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BackgroundProperty, Brushes.Transparent));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.ForegroundProperty, Brushes.White));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BorderThicknessProperty, new Thickness(0)));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.PaddingProperty, new Thickness(10, 5, 10, 5)));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.FontSizeProperty, 13.0));

            // Style for ComboBoxItem
            Style comboBoxItemStyle = new Style(typeof(ComboBoxItem));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(50, 50, 55))));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(10, 8, 10, 8)));
            comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.FontSizeProperty, 13.0));

            // Add triggers for hover and selected states
            Trigger itemHoverTrigger = new Trigger { Property = ComboBoxItem.IsMouseOverProperty, Value = true };
            itemHoverTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 100, 50))));
            itemHoverTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            comboBoxItemStyle.Triggers.Add(itemHoverTrigger);

            Trigger itemSelectedTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            itemSelectedTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 150, 60))));
            itemSelectedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
            comboBoxItemStyle.Triggers.Add(itemSelectedTrigger);

            _languageComboBox = new ComboBox
            {
                Style = comboBoxStyle,
                ItemContainerStyle = comboBoxItemStyle,
                Height = 36
            };

            foreach (string language in availableLanguages)
            {
                _languageComboBox.Items.Add(language);
            }

            if (_languageComboBox.Items.Count > 0)
            {
                _languageComboBox.SelectedIndex = 0;
            }

            comboBoxBorder.Child = _languageComboBox;
            Grid.SetRow(comboBoxBorder, 2);
            mainGrid.Children.Add(comboBoxBorder);

            // Create button panel with modern design
            Grid buttonPanel = new Grid
            {
                Margin = new Thickness(20, 20, 20, 20)
            };

            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Create Cancel button
            Button cancelButton = CreateModernButton("Cancel", false);
            cancelButton.Width = 90;
            cancelButton.Height = 36;
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.Click += (s, e) => DialogResult = false;
            Grid.SetColumn(cancelButton, 1);
            buttonPanel.Children.Add(cancelButton);

            // Create OK button
            Button okButton = CreateModernButton("OK", true);
            okButton.Width = 90;
            okButton.Height = 36;
            okButton.Click += (s, e) =>
            {
                SelectedLanguage = _languageComboBox.SelectedItem?.ToString();
                DialogResult = true;
            };
            Grid.SetColumn(okButton, 2);
            buttonPanel.Children.Add(okButton);

            Grid.SetRow(buttonPanel, 4);
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

    /// <summary>
    /// Modern dialog for selecting a backup for rollback
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

            // Create main container with border radius
            Border mainBorder = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                Margin = new Thickness(0)
            };

            // Create layout
            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title bar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Backup list
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Create custom title bar
            Border titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                Height = 40,
                Padding = new Thickness(15, 0, 10, 0)
            };

            Grid titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title

            // Icon with glowing effect
            Rectangle iconRect = new Rectangle
            {
                Width = 16,
                Height = 16,
                Fill = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                RadiusX = 2,
                RadiusY = 2,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Add glow effect to icon
            iconRect.Effect = new DropShadowEffect
            {
                BlurRadius = 15,
                ShadowDepth = 0,
                Color = Color.FromRgb(0, 255, 80),
                Opacity = 0.7
            };

            Grid.SetColumn(iconRect, 0);
            titleGrid.Children.Add(iconRect);

            // Title text
            TextBlock titleText = new TextBlock
            {
                Text = "Select Backup for Rollback",
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

            // Make title bar draggable
            titleBar.MouseLeftButtonDown += (s, e) => DragMove();

            // Create header with improved styling
            TextBlock headerLabel = new TextBlock
            {
                Text = "Select a backup to restore",
                Foreground = Brushes.White,
                FontSize = 15,
                Margin = new Thickness(20, 15, 20, 15)
            };
            Grid.SetRow(headerLabel, 1);
            mainGrid.Children.Add(headerLabel);

            // Create backup list container
            Border listBoxBorder = new Border
            {
                Margin = new Thickness(20, 0, 20, 20),
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                CornerRadius = new CornerRadius(4)
            };

            // Add shadow effect
            listBoxBorder.Effect = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 2,
                Direction = 315,
                Color = Colors.Black,
                Opacity = 0.4
            };

            // Create ListBox with modern styling
            Style listBoxStyle = new Style(typeof(ListBox));
            listBoxStyle.Setters.Add(new Setter(ListBox.BackgroundProperty, Brushes.Transparent));
            listBoxStyle.Setters.Add(new Setter(ListBox.ForegroundProperty, Brushes.White));
            listBoxStyle.Setters.Add(new Setter(ListBox.BorderThicknessProperty, new Thickness(0)));
            listBoxStyle.Setters.Add(new Setter(ListBox.PaddingProperty, new Thickness(5)));
            listBoxStyle.Setters.Add(new Setter(ListBox.FontSizeProperty, 13.0));

            // Style for ListBoxItems
            Style listBoxItemStyle = new Style(typeof(ListBoxItem));
            listBoxItemStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(12, 8, 12, 8)));
            listBoxItemStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(3)));
            listBoxItemStyle.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(45, 45, 48))));
            listBoxItemStyle.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));

            // Add triggers for hover and selected
            Trigger itemHoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
            itemHoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(60, 60, 65))));
            itemHoverTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0, 255, 80))));
            listBoxItemStyle.Triggers.Add(itemHoverTrigger);

            Trigger itemSelectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            itemSelectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 100, 40))));
            itemSelectedTrigger.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, Brushes.White));
            listBoxItemStyle.Triggers.Add(itemSelectedTrigger);

            _backupListBox = new ListBox
            {
                Style = listBoxStyle,
                ItemContainerStyle = listBoxItemStyle,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial Unicode MS, MS Gothic") // Better Unicode support
            };

            // Apply optimal text rendering settings
            TextOptions.SetTextFormattingMode(_backupListBox, TextFormattingMode.Ideal);
            TextOptions.SetTextRenderingMode(_backupListBox, TextRenderingMode.ClearType);

            // Add items with improved data templates
            foreach (var backup in backups)
            {
                // Create a data template for each entry
                Grid itemGrid = new Grid();
                itemGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                itemGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Format timestamp nicely
                TextBlock timestampBlock = new TextBlock
                {
                    Text = backup.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                Grid.SetRow(timestampBlock, 0);
                itemGrid.Children.Add(timestampBlock);

                // Description with subtle coloring
                TextBlock descriptionBlock = new TextBlock
                {
                    Text = backup.Description,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12
                };
                Grid.SetRow(descriptionBlock, 1);
                itemGrid.Children.Add(descriptionBlock);

                ListBoxItem item = new ListBoxItem
                {
                    Content = itemGrid,
                    Tag = backup.FilePath
                };

                _backupListBox.Items.Add(item);
            }

            if (_backupListBox.Items.Count > 0)
            {
                _backupListBox.SelectedIndex = 0;
            }

            listBoxBorder.Child = _backupListBox;
            Grid.SetRow(listBoxBorder, 2);
            mainGrid.Children.Add(listBoxBorder);

            // Create button panel with modern design
            Grid buttonPanel = new Grid
            {
                Margin = new Thickness(20, 0, 20, 20)
            };

            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Create Cancel button
            Button cancelButton = CreateModernButton("Cancel", false);
            cancelButton.Width = 100;
            cancelButton.Height = 36;
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.Click += (s, e) => DialogResult = false;
            Grid.SetColumn(cancelButton, 1);
            buttonPanel.Children.Add(cancelButton);

            // Create Restore button
            Button restoreButton = CreateModernButton("Restore", true);
            restoreButton.Width = 100;
            restoreButton.Height = 36;
            restoreButton.Click += (s, e) =>
            {
                if (_backupListBox.SelectedItem is ListBoxItem selectedItem)
                {
                    SelectedBackupPath = selectedItem.Tag.ToString();
                    DialogResult = true;
                }
            };
            Grid.SetColumn(restoreButton, 2);
            buttonPanel.Children.Add(restoreButton);

            Grid.SetRow(buttonPanel, 3);
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

        /// <summary>
        /// Dialog for setting sharelist metadata when exporting
        /// </summary>
        public class SharelistMetadataDialog : Window
        {
            private TextBox _authorTextBox;
            private TextBox _versionTextBox;
            private TextBox _descriptionTextBox;
            private TextBox _twitterTextBox;
            private TextBox _patreonTextBox;

            public SharelistMetadata Metadata { get; private set; }

            public SharelistMetadataDialog(SharelistMetadata existingMetadata = null)
            {
                Title = "Sharelist Information";
                Width = 500;
                Height = 800;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

                Metadata = existingMetadata ?? new SharelistMetadata();

                Border mainBorder = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                    Margin = new Thickness(0)
                };

                Grid mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Title
                TextBlock titleText = new TextBlock
                {
                    Text = "Add Your Information",
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                    FontSize = 18,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(20, 20, 20, 10),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetRow(titleText, 0);
                mainGrid.Children.Add(titleText);

                // Scrollable content
                ScrollViewer scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(20, 10, 20, 10)
                };

                StackPanel contentPanel = new StackPanel();

                // Author field
                contentPanel.Children.Add(CreateLabel("Author Name*"));
                _authorTextBox = CreateTextBox(Metadata.Author ?? Environment.UserName);
                contentPanel.Children.Add(_authorTextBox);

                // Version field
                contentPanel.Children.Add(CreateLabel("Version*"));
                _versionTextBox = CreateTextBox(Metadata.Version ?? "1.0");
                contentPanel.Children.Add(_versionTextBox);

                // Description field
                contentPanel.Children.Add(CreateLabel("Description"));
                _descriptionTextBox = CreateTextBox(Metadata.Description, 80);
                contentPanel.Children.Add(_descriptionTextBox);

                // Social section header
                Border socialHeader = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 20, 0, 10)
                };
                TextBlock socialText = new TextBlock
                {
                    Text = "Social / Support Links (Optional)",
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                    FontWeight = FontWeights.SemiBold
                };
                socialHeader.Child = socialText;
                contentPanel.Children.Add(socialHeader);

                // Twitter field
                contentPanel.Children.Add(CreateLabel("Twitter Handle"));
                _twitterTextBox = CreateTextBox(Metadata.TwitterHandle);
                _twitterTextBox.TextChanged += (s, e) => {
                    if (!string.IsNullOrEmpty(_twitterTextBox.Text) && !_twitterTextBox.Text.StartsWith("@"))
                        _twitterTextBox.Text = "@" + _twitterTextBox.Text;
                };
                contentPanel.Children.Add(_twitterTextBox);

                // Patreon field
                contentPanel.Children.Add(CreateLabel("Patreon URL"));
                _patreonTextBox = CreateTextBox(Metadata.PatreonUrl);
                contentPanel.Children.Add(_patreonTextBox);

                scrollViewer.Content = contentPanel;
                Grid.SetRow(scrollViewer, 1);
                mainGrid.Children.Add(scrollViewer);

                // Buttons
                Grid buttonPanel = new Grid
                {
                    Margin = new Thickness(20)
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

                Button saveButton = CreateModernButton("Save", true);
                saveButton.Width = 100;
                saveButton.Height = 36;
                saveButton.Click += SaveButton_Click;
                Grid.SetColumn(saveButton, 2);
                buttonPanel.Children.Add(saveButton);

                Grid.SetRow(buttonPanel, 2);
                mainGrid.Children.Add(buttonPanel);

                mainBorder.Child = mainGrid;
                Content = mainBorder;
            }

            private TextBlock CreateLabel(string text)
            {
                return new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 10, 0, 5),
                    FontSize = 13
                };
            }

            private TextBox CreateTextBox(string text, int height = 32)
            {
                var textBox = new TextBox
                {
                    Text = text ?? "",
                    Height = height,
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                    Padding = new Thickness(8, 5, 8, 5),
                    VerticalContentAlignment = height > 32 ? VerticalAlignment.Top : VerticalAlignment.Center
                };

                if (height > 32)
                {
                    textBox.TextWrapping = TextWrapping.Wrap;
                    textBox.AcceptsReturn = true;
                    textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                }

                return textBox;
            }

            private void SaveButton_Click(object sender, RoutedEventArgs e)
            {
                if (string.IsNullOrWhiteSpace(_authorTextBox.Text))
                {
                    MessageBox.Show("Please enter an author name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_versionTextBox.Text))
                {
                    MessageBox.Show("Please enter a version.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Metadata = new SharelistMetadata
                {
                    Author = _authorTextBox.Text.Trim(),
                    Version = _versionTextBox.Text.Trim(),
                    Description = _descriptionTextBox.Text.Trim(),
                    TwitterHandle = _twitterTextBox.Text.Trim(),
                    PatreonUrl = _patreonTextBox.Text.Trim(),
                    CreatedDate = Metadata?.CreatedDate ?? DateTime.Now,
                    ModifiedDate = DateTime.Now
                };

                DialogResult = true;
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

                Style buttonStyle = new Style(typeof(Button));
                Color bgColor = isPrimary ? Color.FromRgb(0, 170, 70) : Color.FromRgb(60, 60, 65);
                Color hoverColor = isPrimary ? Color.FromRgb(0, 200, 80) : Color.FromRgb(75, 75, 80);
                Color pressedColor = isPrimary ? Color.FromRgb(0, 230, 100) : Color.FromRgb(50, 50, 55);

                buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(bgColor)));
                buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
                buttonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(15, 8, 15, 8)));
                buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, CreateButtonTemplate()));

                Trigger mouseOverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                mouseOverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(hoverColor)));
                buttonStyle.Triggers.Add(mouseOverTrigger);

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

        /// <summary>
        /// Dialog for displaying sharelist metadata when importing
        /// </summary>
        /// <summary>
        /// Dialog for displaying sharelist metadata when importing
        /// </summary>
        /// <summary>
        /// Dialog for displaying sharelist metadata when importing
        /// </summary>
        public class SharelistInfoDialog : Window
        {
            // Add the enum for import actions
            public enum ImportAction
            {
                JustImport,
                PrepareForUpdate,
                MergeImmediately
            }

            // Add property for selected action
            public ImportAction SelectedAction { get; private set; }

            // Add ComboBox field
            private ComboBox _actionComboBox;

            public SharelistInfoDialog(SharelistMetadata metadata, int entryCount)
            {
                Title = "Sharelist Information";
                Width = 450;
                MinHeight = 600;
                MaxHeight = 1000;
                SizeToContent = SizeToContent.Height;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

                // Default to just import
                SelectedAction = ImportAction.JustImport;

                Border mainBorder = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                    Margin = new Thickness(0)
                };

                Grid mainGrid = new Grid();
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Action selection
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

                // Header with author and version
                Border headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                    Padding = new Thickness(20, 15, 20, 15)
                };

                StackPanel headerPanel = new StackPanel();

                TextBlock titleText = new TextBlock
                {
                    Text = metadata.Author ?? "Unknown Author",
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                    FontSize = 20,
                    FontWeight = FontWeights.Bold
                };
                headerPanel.Children.Add(titleText);

                TextBlock versionText = new TextBlock
                {
                    Text = $"Version {metadata.Version ?? "1.0"} • {entryCount} entries",
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                    FontSize = 14,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                headerPanel.Children.Add(versionText);

                headerBorder.Child = headerPanel;
                Grid.SetRow(headerBorder, 0);
                mainGrid.Children.Add(headerBorder);

                // Content
                ScrollViewer scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(20, 20, 20, 10),
                    MaxHeight = 400
                };

                StackPanel contentPanel = new StackPanel();

                // Description with improved styling
                if (!string.IsNullOrWhiteSpace(metadata.Description))
                {
                    TextBlock descLabel = new TextBlock
                    {
                        Text = "Description",
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    contentPanel.Children.Add(descLabel);

                    Border descBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 0, 0, 15)
                    };

                    TextBlock descText = new TextBlock
                    {
                        Text = metadata.Description,
                        Foreground = Brushes.White,
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 20
                    };

                    descBorder.Child = descText;
                    contentPanel.Children.Add(descBorder);
                }

                // Social links
                bool hasLinks = !string.IsNullOrWhiteSpace(metadata.TwitterHandle) ||
                               !string.IsNullOrWhiteSpace(metadata.PatreonUrl) ||
                               !string.IsNullOrWhiteSpace(metadata.DiscordUrl) ||
                               !string.IsNullOrWhiteSpace(metadata.WebsiteUrl);

                if (hasLinks)
                {
                    TextBlock linksLabel = new TextBlock
                    {
                        Text = "Support / Links",
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    contentPanel.Children.Add(linksLabel);

                    // Create horizontal panel for Twitter and Patreon
                    bool hasTwitter = !string.IsNullOrWhiteSpace(metadata.TwitterHandle);
                    bool hasPatreon = !string.IsNullOrWhiteSpace(metadata.PatreonUrl);

                    if (hasTwitter || hasPatreon)
                    {
                        Grid socialGrid = new Grid();
                        socialGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        if (hasTwitter && hasPatreon)
                        {
                            socialGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                            socialGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        }

                        int columnIndex = 0;

                        if (hasTwitter)
                        {
                            Border twitterButton = CreateLinkButton("Twitter", metadata.TwitterHandle, $"https://twitter.com/{metadata.TwitterHandle.TrimStart('@')}");
                            twitterButton.Margin = new Thickness(0, 0, hasPatreon ? 5 : 0, 8);
                            Grid.SetColumn(twitterButton, columnIndex);
                            socialGrid.Children.Add(twitterButton);
                            columnIndex += hasPatreon ? 2 : 1;
                        }

                        if (hasTwitter && hasPatreon)
                        {
                            // Add vertical divider
                            Border divider = new Border
                            {
                                Width = 1,
                                Background = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                                Margin = new Thickness(5, 0, 5, 8),
                                VerticalAlignment = VerticalAlignment.Stretch
                            };
                            Grid.SetColumn(divider, 1);
                            socialGrid.Children.Add(divider);
                        }

                        if (hasPatreon)
                        {
                            Border patreonButton = CreateLinkButton("Patreon", "Support on Patreon", metadata.PatreonUrl);
                            patreonButton.Margin = new Thickness(hasTwitter ? 5 : 0, 0, 0, 8);
                            Grid.SetColumn(patreonButton, columnIndex);
                            socialGrid.Children.Add(patreonButton);
                        }

                        contentPanel.Children.Add(socialGrid);
                    }

                    if (!string.IsNullOrWhiteSpace(metadata.DiscordUrl))
                        contentPanel.Children.Add(CreateLinkButton("Discord", metadata.DiscordUrl, metadata.DiscordUrl));

                    if (!string.IsNullOrWhiteSpace(metadata.WebsiteUrl))
                        contentPanel.Children.Add(CreateLinkButton("Website", metadata.WebsiteUrl, metadata.WebsiteUrl));
                }

                // Dates
                TextBlock datesText = new TextBlock
                {
                    Text = $"Created: {metadata.CreatedDate:yyyy-MM-dd} • Modified: {metadata.ModifiedDate:yyyy-MM-dd}",
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                    FontSize = 11,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                contentPanel.Children.Add(datesText);

                scrollViewer.Content = contentPanel;
                Grid.SetRow(scrollViewer, 1);
                mainGrid.Children.Add(scrollViewer);

                // Action selection panel
                Border actionPanelBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                    Padding = new Thickness(20, 15, 20, 15),
                    Margin = new Thickness(0)
                };

                StackPanel actionPanel = new StackPanel();

                TextBlock actionLabel = new TextBlock
                {
                    Text = "Import Options",
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                actionPanel.Children.Add(actionLabel);

                // Create ComboBox
                _actionComboBox = new ComboBox
                {
                    Height = 36,
                    FontSize = 13,
                    Margin = new Thickness(0),
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                    BorderThickness = new Thickness(1)
                };

                // Apply rounded corners using a style
                Style comboBoxStyle = new Style(typeof(ComboBox));
                comboBoxStyle.Setters.Add(new Setter(ComboBox.OverridesDefaultStyleProperty, true));
                comboBoxStyle.Setters.Add(new Setter(ComboBox.TemplateProperty, CreateSimpleComboBoxTemplate()));
                _actionComboBox.Style = comboBoxStyle;

                // Style for ComboBoxItem
                Style comboBoxItemStyle = new Style(typeof(ComboBoxItem));
                comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(30, 30, 30))));
                comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
                comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.BorderThicknessProperty, new Thickness(0)));
                comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(10, 8, 10, 8)));
                comboBoxItemStyle.Setters.Add(new Setter(ComboBoxItem.FontSizeProperty, 13.0));

                // Add triggers for hover and selected states
                Trigger itemHoverTrigger = new Trigger { Property = ComboBoxItem.IsMouseOverProperty, Value = true };
                itemHoverTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 100, 50))));
                itemHoverTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
                comboBoxItemStyle.Triggers.Add(itemHoverTrigger);

                Trigger itemSelectedTrigger = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
                itemSelectedTrigger.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 150, 60))));
                itemSelectedTrigger.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, Brushes.White));
                comboBoxItemStyle.Triggers.Add(itemSelectedTrigger);

                _actionComboBox.ItemContainerStyle = comboBoxItemStyle;

                // Force dark theme colors
                _actionComboBox.Resources.Add(SystemColors.WindowBrushKey, new SolidColorBrush(Color.FromRgb(30, 30, 30)));
                _actionComboBox.Resources.Add(SystemColors.HighlightBrushKey, new SolidColorBrush(Color.FromRgb(0, 100, 50)));
                _actionComboBox.Resources.Add(SystemColors.ControlBrushKey, new SolidColorBrush(Color.FromRgb(30, 30, 30)));
                _actionComboBox.Resources.Add(SystemColors.WindowTextBrushKey, Brushes.White);
                _actionComboBox.Resources.Add(SystemColors.ControlTextBrushKey, Brushes.White);
                _actionComboBox.Resources.Add(SystemColors.GrayTextBrushKey, new SolidColorBrush(Color.FromRgb(180, 180, 180)));

                // Add combo box items without individual styling
                _actionComboBox.Items.Add("Add (No SDB Import)");
                _actionComboBox.Items.Add("Update Sharelist");
                _actionComboBox.Items.Add("Merge To SDB");

                _actionComboBox.SelectedIndex = 2; // Default to "Just keep in sharelist"
                SelectedAction = ImportAction.MergeImmediately;
                // Update the selection changed handler
                _actionComboBox.SelectionChanged += (s, e) =>
                {
                    switch (_actionComboBox.SelectedIndex)
                    {
                        case 0:
                            SelectedAction = ImportAction.JustImport;
                            break;
                        case 1:
                            SelectedAction = ImportAction.PrepareForUpdate;
                            break;
                        case 2:
                            SelectedAction = ImportAction.MergeImmediately;
                            break;
                    }
                };

                actionPanel.Children.Add(_actionComboBox);
                actionPanelBorder.Child = actionPanel;
                Grid.SetRow(actionPanelBorder, 2);
                mainGrid.Children.Add(actionPanelBorder);

                // Buttons
                StackPanel buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(20)
                };

                Button importButton = CreateModernButton("Import", true);
                importButton.Width = 100;
                importButton.Height = 36;
                importButton.Margin = new Thickness(0, 0, 10, 0);
                importButton.Click += (s, e) => DialogResult = true;
                buttonPanel.Children.Add(importButton);

                Button cancelButton = CreateModernButton("Cancel", false);
                cancelButton.Width = 100;
                cancelButton.Height = 36;
                cancelButton.Click += (s, e) => DialogResult = false;
                buttonPanel.Children.Add(cancelButton);

                Grid.SetRow(buttonPanel, 3);
                mainGrid.Children.Add(buttonPanel);

                mainBorder.Child = mainGrid;
                Content = mainBorder;
            }

            private ControlTemplate CreateSimpleComboBoxTemplate()
            {
                ControlTemplate template = new ControlTemplate(typeof(ComboBox));

                // Root border with rounded corners
                FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
                border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(ComboBox.BackgroundProperty));
                border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(ComboBox.BorderBrushProperty));
                border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(ComboBox.BorderThicknessProperty));
                border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

                // Grid for layout
                FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));

                // Toggle button
                FrameworkElementFactory toggleButton = new FrameworkElementFactory(typeof(ToggleButton));
                toggleButton.SetValue(ToggleButton.NameProperty, "toggleButton");
                toggleButton.SetValue(ToggleButton.ClickModeProperty, ClickMode.Press);
                toggleButton.SetValue(ToggleButton.BackgroundProperty, Brushes.Transparent);
                toggleButton.SetValue(ToggleButton.BorderBrushProperty, Brushes.Transparent);
                toggleButton.SetValue(ToggleButton.BorderThicknessProperty, new Thickness(0));
                toggleButton.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen") { RelativeSource = RelativeSource.TemplatedParent, Mode = BindingMode.TwoWay });

                // Content for toggle button
                FrameworkElementFactory toggleGrid = new FrameworkElementFactory(typeof(Grid));

                // Selected item presenter
                FrameworkElementFactory contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
                contentPresenter.SetValue(ContentPresenter.MarginProperty, new Thickness(10, 0, 30, 0));
                contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                contentPresenter.SetBinding(ContentPresenter.ContentProperty, new Binding("SelectionBoxItem") { RelativeSource = RelativeSource.TemplatedParent });

                // Arrow
                FrameworkElementFactory arrow = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
                arrow.SetValue(System.Windows.Shapes.Path.FillProperty, Brushes.White);
                arrow.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 0 0 L 4 4 L 8 0 Z"));
                arrow.SetValue(System.Windows.Shapes.Path.HorizontalAlignmentProperty, HorizontalAlignment.Right);
                arrow.SetValue(System.Windows.Shapes.Path.VerticalAlignmentProperty, VerticalAlignment.Center);
                arrow.SetValue(System.Windows.Shapes.Path.MarginProperty, new Thickness(0, 0, 10, 0));

                toggleGrid.AppendChild(contentPresenter);
                toggleGrid.AppendChild(arrow);
                toggleButton.AppendChild(toggleGrid);

                // Popup
                FrameworkElementFactory popup = new FrameworkElementFactory(typeof(Popup));
                popup.SetValue(Popup.NameProperty, "Popup");
                popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom);
                popup.SetValue(Popup.AllowsTransparencyProperty, true);
                popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen") { RelativeSource = RelativeSource.TemplatedParent });

                // Popup content
                FrameworkElementFactory popupBorder = new FrameworkElementFactory(typeof(Border));
                popupBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(30, 30, 30)));
                popupBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(70, 70, 75)));
                popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
                popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                popupBorder.SetBinding(Border.MinWidthProperty, new Binding("ActualWidth") { RelativeSource = RelativeSource.TemplatedParent });

                FrameworkElementFactory scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
                scrollViewer.SetValue(ScrollViewer.MaxHeightProperty, 200.0);
                scrollViewer.SetValue(ScrollViewer.CanContentScrollProperty, true);

                FrameworkElementFactory itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
                scrollViewer.AppendChild(itemsPresenter);
                popupBorder.AppendChild(scrollViewer);
                popup.AppendChild(popupBorder);

                grid.AppendChild(toggleButton);
                grid.AppendChild(popup);
                border.AppendChild(grid);

                template.VisualTree = border;
                return template;
            }

            private Border CreateLinkButton(string label, string text, string url)
            {
                Border linkBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(50, 50, 55)),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(0, 0, 0, 8),
                    Cursor = Cursors.Hand
                };

                linkBorder.MouseEnter += (s, e) => linkBorder.Background = new SolidColorBrush(Color.FromRgb(60, 60, 65));
                linkBorder.MouseLeave += (s, e) => linkBorder.Background = new SolidColorBrush(Color.FromRgb(50, 50, 55));
                linkBorder.MouseLeftButtonUp += (s, e) => {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = url,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                };

                Grid linkGrid = new Grid
                {
                    Margin = new Thickness(10, 8, 10, 8)
                };
                linkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                linkGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                TextBlock labelText = new TextBlock
                {
                    Text = label + ":",
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(labelText, 0);
                linkGrid.Children.Add(labelText);

                TextBlock linkText = new TextBlock
                {
                    Text = text,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 200, 255)),
                    TextDecorations = TextDecorations.Underline,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumn(linkText, 1);
                linkGrid.Children.Add(linkText);

                linkBorder.Child = linkGrid;
                return linkBorder;
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

                Style buttonStyle = new Style(typeof(Button));
                Color bgColor = isPrimary ? Color.FromRgb(0, 170, 70) : Color.FromRgb(60, 60, 65);
                Color hoverColor = isPrimary ? Color.FromRgb(0, 200, 80) : Color.FromRgb(75, 75, 80);
                Color pressedColor = isPrimary ? Color.FromRgb(0, 230, 100) : Color.FromRgb(50, 50, 55);

                buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(bgColor)));
                buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
                buttonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(15, 8, 15, 8)));
                buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, CreateButtonTemplate()));

                Trigger mouseOverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                mouseOverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(hoverColor)));
                buttonStyle.Triggers.Add(mouseOverTrigger);

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

        public class ThemedMessageBox : Window
        {
            public bool IsYesClicked { get; private set; }

            public ThemedMessageBox(string title, string message, Window owner)
            {
                // Basic window setup
                Title = title;
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                SizeToContent = SizeToContent.WidthAndHeight;
                ResizeMode = ResizeMode.NoResize;
                WindowStyle = WindowStyle.None;
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)); // #2D2D30
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 63, 70));
                BorderThickness = new Thickness(1, 1, 1, 1);

                // Main container
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Add title bar
                var titleBar = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(37, 37, 38)),
                    Padding = new Thickness(15, 12, 15, 12)
                };
                var titleText = new TextBlock
                {
                    Text = title,
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold
                };
                titleBar.Child = titleText;
                Grid.SetRow(titleBar, 0);
                grid.Children.Add(titleBar);

                // Add message
                var messageBlock = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    Margin = new Thickness(20, 20, 20, 20),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 350
                };
                Grid.SetRow(messageBlock, 1);
                grid.Children.Add(messageBlock);

                // Add buttons
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 20, 20)
                };

                var yesButton = new Button
                {
                    Content = "Yes",
                    Width = 100,
                    Height = 36,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = new SolidColorBrush(Color.FromRgb(0, 170, 68)),
                    Foreground = Brushes.White
                };

                var noButton = new Button
                {
                    Content = "No",
                    Width = 100,
                    Height = 36,
                    Background = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
                    Foreground = Brushes.White
                };

                yesButton.Click += (s, e) => { IsYesClicked = true; DialogResult = true; };
                noButton.Click += (s, e) => { IsYesClicked = false; DialogResult = false; };

                buttonPanel.Children.Add(yesButton);
                buttonPanel.Children.Add(noButton);
                Grid.SetRow(buttonPanel, 2);
                grid.Children.Add(buttonPanel);

                Content = grid;
                MouseLeftButtonDown += (s, e) => DragMove();
            }

            public static bool Show(string title, string message, Window owner)
            {
                var dialog = new ThemedMessageBox(title, message, owner);
                dialog.ShowDialog();
                return dialog.IsYesClicked;
            }
        }

        /// <summary>
        /// Dialog for creating a new SDB file with custom settings
        /// </summary>
        public class NewSdbDialog : Window
        {
            private ComboBox _gameVersionComboBox;
            private ComboBox _languageComboBox;
            private CheckBox _mangledFormatCheckbox;
            private TextBox _baseHashIdTextBox;
            private uint _baseHashId = 0xF0000000; // Default starting hash ID (approx. 4 billion)

            public string GameVersion { get; private set; }
            public string Language { get; private set; }
            public bool UseMangled { get; private set; }
            public uint BaseHashId { get; private set; }

            public NewSdbDialog()
            {
                // Configure window
                Title = "Create New SDB";
                Width = 500;
                Height = 400;
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
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title bar
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Content
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Spacer
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

                // Create custom title bar
                Border titleBar = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                    Height = 40,
                    Padding = new Thickness(15, 0, 10, 0)
                };

                Grid titleGrid = new Grid();
                titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
                titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title

                // Icon with glowing effect
                Rectangle iconRect = new Rectangle
                {
                    Width = 16,
                    Height = 16,
                    Fill = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                    RadiusX = 2,
                    RadiusY = 2,
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Add glow effect to icon
                iconRect.Effect = new DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Color = Color.FromRgb(0, 255, 80),
                    Opacity = 0.7
                };

                Grid.SetColumn(iconRect, 0);
                titleGrid.Children.Add(iconRect);

                // Title text
                TextBlock titleText = new TextBlock
                {
                    Text = "Create New SDB",
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

                // Make title bar draggable
                titleBar.MouseLeftButtonDown += (s, e) => DragMove();

                // Content panel
                StackPanel contentPanel = new StackPanel
                {
                    Margin = new Thickness(20)
                };

                // Game Version Selection
                TextBlock gameVersionLabel = new TextBlock
                {
                    Text = "Game Version:",
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 5),
                    FontSize = 13
                };
                contentPanel.Children.Add(gameVersionLabel);

                _gameVersionComboBox = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                    Height = 32
                };

                // Add game versions
                _gameVersionComboBox.Items.Add("WWE2K25");
                _gameVersionComboBox.Items.Add("WWE2K24");
                _gameVersionComboBox.Items.Add("WWE2K23");
                _gameVersionComboBox.Items.Add("WWE2K22");
                _gameVersionComboBox.Items.Add("WWE2K20");
                _gameVersionComboBox.Items.Add("WWE2K19");

                // Set default to latest game
                _gameVersionComboBox.SelectedIndex = 0;
                contentPanel.Children.Add(_gameVersionComboBox);

                // Language Selection
                TextBlock languageLabel = new TextBlock
                {
                    Text = "Language:",
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 5),
                    FontSize = 13
                };
                contentPanel.Children.Add(languageLabel);

                _languageComboBox = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                    Height = 32
                };

                // Add languages
                _languageComboBox.Items.Add("ENG");
                _languageComboBox.Items.Add("FRA");
                _languageComboBox.Items.Add("GER");
                _languageComboBox.Items.Add("ITA");
                _languageComboBox.Items.Add("SPA");
                _languageComboBox.Items.Add("ARA");

                // Set default to English
                _languageComboBox.SelectedIndex = 0;
                contentPanel.Children.Add(_languageComboBox);

                // Mangled Format Checkbox
                _mangledFormatCheckbox = new CheckBox
                {
                    Content = "Use Mangled Format (not recommended for mods)",
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 15),
                    IsChecked = false
                };
                contentPanel.Children.Add(_mangledFormatCheckbox);

                // Base Hash ID
                TextBlock baseHashLabel = new TextBlock
                {
                    Text = "Base Hash ID (starting point for auto-generated hashes):",
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 0, 5),
                    FontSize = 13
                };
                contentPanel.Children.Add(baseHashLabel);

                _baseHashIdTextBox = new TextBox
                {
                    Text = "4026531840", // Matching 0xF0000000
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                    Padding = new Thickness(8, 5, 8, 5),
                    Height = 32
                };
                _baseHashIdTextBox.TextChanged += (s, e) =>
                {
                    if (uint.TryParse(_baseHashIdTextBox.Text, out uint baseHash))
                    {
                        _baseHashId = baseHash;
                        _baseHashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0)); // Green for valid input
                    }
                    else
                    {
                        _baseHashIdTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 80, 80)); // Red for invalid input
                    }
                };
                contentPanel.Children.Add(_baseHashIdTextBox);

                // Add info label about hash IDs
                TextBlock infoLabel = new TextBlock
                {
                    Text = "This is just a starting point. You can still enter custom hash IDs directly when adding strings.",
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0),
                    FontStyle = FontStyles.Italic,
                    FontSize = 12
                };
                contentPanel.Children.Add(infoLabel);

                Grid.SetRow(contentPanel, 1);
                mainGrid.Children.Add(contentPanel);

                // Button panel
                Grid buttonPanel = new Grid
                {
                    Margin = new Thickness(20, 0, 20, 20)
                };

                buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                buttonPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Create Cancel button
                Button cancelButton = CreateModernButton("Cancel", false);
                cancelButton.Width = 100;
                cancelButton.Height = 36;
                cancelButton.Margin = new Thickness(0, 0, 10, 0);
                cancelButton.Click += (s, e) => DialogResult = false;
                Grid.SetColumn(cancelButton, 1);
                buttonPanel.Children.Add(cancelButton);

                // Create Create button
                Button createButton = CreateModernButton("Create", true);
                createButton.Width = 100;
                createButton.Height = 36;
                createButton.Click += (s, e) =>
                {
                    // Validate and save settings
                    if (!uint.TryParse(_baseHashIdTextBox.Text, out uint baseHash))
                    {
                        MessageBox.Show("Please enter a valid Base Hash ID.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    GameVersion = _gameVersionComboBox.SelectedItem.ToString();
                    Language = _languageComboBox.SelectedItem.ToString();
                    UseMangled = _mangledFormatCheckbox.IsChecked == true;
                    BaseHashId = baseHash;

                    DialogResult = true;
                };
                Grid.SetColumn(createButton, 2);
                buttonPanel.Children.Add(createButton);

                Grid.SetRow(buttonPanel, 3);
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

            /// <summary>
            /// Enhanced Merge SDB Dialog with multiple options
            /// </summary>
            public class EnhancedMergeSdbDialog : Window
            {
                private RadioButton _mergeAllRadio;
                private RadioButton _mergeSharelistRadio;
                private RadioButton _mergeFromFileRadio;
                private RadioButton _mergeSpecificRadio;
                private TextBox _specificHashIdsTextBox;
                private TextBlock _sharelistCountText;
                private TextBlock _filePathText;
                private Button _browseFileButton;
                private string _selectedFilePath;

                public enum MergeMode
                {
                    MergeAll,
                    MergeFromSharelist,
                    MergeFromFile,
                    MergeSpecific
                }

                public MergeMode SelectedMode { get; private set; }
                public string FilePath { get; private set; }
                public List<uint> SpecificHashIds { get; private set; }

                public EnhancedMergeSdbDialog()
                {
                    Title = "Merge SDB Options";
                    Width = 600;
                    Height = 600;
                    WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));

                    // Create main container
                    Border mainBorder = new Border
                    {
                        CornerRadius = new CornerRadius(6),
                        Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                        Margin = new Thickness(0)
                    };

                    Grid mainGrid = new Grid();
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Title
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Content
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

                    // Title
                    TextBlock titleText = new TextBlock
                    {
                        Text = "Choose Merge Method",
                        Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 80)),
                        FontSize = 18,
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(20, 20, 20, 10),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    Grid.SetRow(titleText, 0);
                    mainGrid.Children.Add(titleText);

                    // Content
                    StackPanel contentPanel = new StackPanel
                    {
                        Margin = new Thickness(30, 10, 30, 20)
                    };

                    // Option 1: Merge All
                    Border option1Border = CreateOptionBorder();
                    StackPanel option1Panel = new StackPanel();
                    _mergeAllRadio = new RadioButton
                    {
                        Content = "Merge All Entries",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold,
                        IsChecked = true,
                        GroupName = "MergeOptions"
                    };
                    option1Panel.Children.Add(_mergeAllRadio);
                    option1Panel.Children.Add(new TextBlock
                    {
                        Text = "Merge all entries from another SDB file",
                        Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                        Margin = new Thickness(20, 5, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    });
                    option1Border.Child = option1Panel;
                    contentPanel.Children.Add(option1Border);

                    // Option 2: Merge from Sharelist
                    Border option2Border = CreateOptionBorder();
                    StackPanel option2Panel = new StackPanel();
                    _mergeSharelistRadio = new RadioButton
                    {
                        Content = "Merge from Current Sharelist",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold,
                        GroupName = "MergeOptions"
                    };
                    option2Panel.Children.Add(_mergeSharelistRadio);

                    _sharelistCountText = new TextBlock
                    {
                        Text = $"Add {SharelistManager.Instance.Entries.Count} entries from your sharelist",
                        Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                        Margin = new Thickness(20, 5, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    option2Panel.Children.Add(_sharelistCountText);

                    if (SharelistManager.Instance.Entries.Count == 0)
                    {
                        _mergeSharelistRadio.IsEnabled = false;
                        _sharelistCountText.Text = "Sharelist is empty - add entries first";
                        _sharelistCountText.Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100));
                    }

                    option2Border.Child = option2Panel;
                    contentPanel.Children.Add(option2Border);

                    // Option 3: Merge from File
                    Border option3Border = CreateOptionBorder();
                    StackPanel option3Panel = new StackPanel();
                    _mergeFromFileRadio = new RadioButton
                    {
                        Content = "Merge from Sharelist File",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold,
                        GroupName = "MergeOptions"
                    };
                    option3Panel.Children.Add(_mergeFromFileRadio);

                    StackPanel filePanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(20, 5, 0, 0)
                    };

                    _filePathText = new TextBlock
                    {
                        Text = "No file selected",
                        Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = 250,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    filePanel.Children.Add(_filePathText);

                    _browseFileButton = new Button
                    {
                        Content = "Browse...",
                        Margin = new Thickness(10, 0, 0, 0),
                        Padding = new Thickness(10, 5, 10, 5),
                        Height = 26,
                        Background = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0)
                    };
                    _browseFileButton.Click += BrowseFile_Click;
                    filePanel.Children.Add(_browseFileButton);

                    option3Panel.Children.Add(filePanel);
                    option3Border.Child = option3Panel;
                    contentPanel.Children.Add(option3Border);

                    // Option 4: Specific Hash IDs
                    Border option4Border = CreateOptionBorder();
                    StackPanel option4Panel = new StackPanel();
                    _mergeSpecificRadio = new RadioButton
                    {
                        Content = "Merge Specific Hash IDs",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold,
                        GroupName = "MergeOptions"
                    };
                    option4Panel.Children.Add(_mergeSpecificRadio);

                    TextBlock specificHelp = new TextBlock
                    {
                        Text = "Enter hash IDs separated by commas or ranges (e.g., 1234, 5678, 1000-1050)",
                        Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                        Margin = new Thickness(20, 5, 0, 5),
                        TextWrapping = TextWrapping.Wrap
                    };
                    option4Panel.Children.Add(specificHelp);

                    _specificHashIdsTextBox = new TextBox
                    {
                        Height = 60,
                        Margin = new Thickness(20, 5, 0, 0),
                        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 75)),
                        Padding = new Thickness(8, 5, 8, 5),
                        TextWrapping = TextWrapping.Wrap,
                        AcceptsReturn = true,
                        IsEnabled = false
                    };
                    option4Panel.Children.Add(_specificHashIdsTextBox);

                    option4Border.Child = option4Panel;
                    contentPanel.Children.Add(option4Border);

                    // Enable/disable controls based on selection
                    _mergeAllRadio.Checked += (s, e) => UpdateControlStates();
                    _mergeSharelistRadio.Checked += (s, e) => UpdateControlStates();
                    _mergeFromFileRadio.Checked += (s, e) => UpdateControlStates();
                    _mergeSpecificRadio.Checked += (s, e) => UpdateControlStates();

                    Grid.SetRow(contentPanel, 1);
                    mainGrid.Children.Add(contentPanel);

                    // Buttons
                    Grid buttonPanel = new Grid
                    {
                        Margin = new Thickness(20)
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

                    Button mergeButton = CreateModernButton("Merge", true);
                    mergeButton.Width = 100;
                    mergeButton.Height = 36;
                    mergeButton.Click += MergeButton_Click;
                    Grid.SetColumn(mergeButton, 2);
                    buttonPanel.Children.Add(mergeButton);

                    Grid.SetRow(buttonPanel, 2);
                    mainGrid.Children.Add(buttonPanel);

                    mainBorder.Child = mainGrid;
                    Content = mainBorder;
                }

                private Border CreateOptionBorder()
                {
                    return new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(35, 35, 38)),
                        BorderThickness = new Thickness(1),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(15, 12, 15, 12),
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                }

                private void UpdateControlStates()
                {
                    _specificHashIdsTextBox.IsEnabled = _mergeSpecificRadio.IsChecked == true;
                    _browseFileButton.IsEnabled = _mergeFromFileRadio.IsChecked == true;
                }

                private void BrowseFile_Click(object sender, RoutedEventArgs e)
                {
                    var openDialog = new OpenFileDialog
                    {
                        Filter = "Sharelist Files (*.sharesdb)|*.sharesdb|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                        Title = "Select Sharelist File"
                    };

                    if (openDialog.ShowDialog() == true)
                    {
                        _selectedFilePath = openDialog.FileName;
                        _filePathText.Text = System.IO.Path.GetFileName(_selectedFilePath);
                    }
                }

                private void MergeButton_Click(object sender, RoutedEventArgs e)
                {
                    // Determine selected mode
                    if (_mergeAllRadio.IsChecked == true)
                    {
                        SelectedMode = MergeMode.MergeAll;
                    }
                    else if (_mergeSharelistRadio.IsChecked == true)
                    {
                        SelectedMode = MergeMode.MergeFromSharelist;
                    }
                    else if (_mergeFromFileRadio.IsChecked == true)
                    {
                        if (string.IsNullOrEmpty(_selectedFilePath))
                        {
                            MessageBox.Show("Please select a sharelist file.", "No File Selected",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        SelectedMode = MergeMode.MergeFromFile;
                        FilePath = _selectedFilePath;
                    }
                    else if (_mergeSpecificRadio.IsChecked == true)
                    {
                        if (string.IsNullOrWhiteSpace(_specificHashIdsTextBox.Text))
                        {
                            MessageBox.Show("Please enter at least one hash ID.", "No IDs Specified",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        // Parse hash IDs
                        SpecificHashIds = ParseHashIds(_specificHashIdsTextBox.Text);
                        if (SpecificHashIds.Count == 0)
                        {
                            MessageBox.Show("No valid hash IDs found. Please check your input.", "Invalid Input",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        SelectedMode = MergeMode.MergeSpecific;
                    }

                    DialogResult = true;
                }

                private List<uint> ParseHashIds(string input)
                {
                    var hashIds = new HashSet<uint>();
                    string[] parts = input.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string part in parts)
                    {
                        string trimmed = part.Trim();

                        // Check for range (e.g., "1000-1050")
                        if (trimmed.Contains("-"))
                        {
                            string[] rangeParts = trimmed.Split('-');
                            if (rangeParts.Length == 2 &&
                                uint.TryParse(rangeParts[0].Trim(), out uint start) &&
                                uint.TryParse(rangeParts[1].Trim(), out uint end))
                            {
                                for (uint i = start; i <= end && i >= start; i++) // Check for overflow
                                {
                                    hashIds.Add(i);
                                }
                            }
                        }
                        else if (uint.TryParse(trimmed, out uint id))
                        {
                            hashIds.Add(id);
                        }
                    }

                    return hashIds.ToList();
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

                    Style buttonStyle = new Style(typeof(Button));
                    Color bgColor = isPrimary ? Color.FromRgb(0, 170, 70) : Color.FromRgb(60, 60, 65);
                    Color hoverColor = isPrimary ? Color.FromRgb(0, 200, 80) : Color.FromRgb(75, 75, 80);
                    Color pressedColor = isPrimary ? Color.FromRgb(0, 230, 100) : Color.FromRgb(50, 50, 55);

                    buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(bgColor)));
                    buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
                    buttonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(15, 8, 15, 8)));
                    buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, CreateButtonTemplate()));

                    Trigger mouseOverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                    mouseOverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(hoverColor)));
                    buttonStyle.Triggers.Add(mouseOverTrigger);

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
}