using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SDBEditor.ViewModels
{
    /// <summary>
    /// View model representing a string entry for data binding
    /// </summary>
    public class StringEntryViewModel : INotifyPropertyChanged
    {
        private int _index;
        private uint _hashId;
        private string _hexValue;
        private string _text;

        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged();
                }
            }
        }

        public uint HashId
        {
            get => _hashId;
            set
            {
                if (_hashId != value)
                {
                    _hashId = value;
                    OnPropertyChanged();
                    // Update hex value as well
                    HexValue = value.ToString("X");
                }
            }
        }

        public string HexValue
        {
            get => _hexValue;
            set
            {
                if (_hexValue != value)
                {
                    _hexValue = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    // No special encoding/decoding needed here
                    // WPF handles the string display with the proper font and rendering settings
                    _text = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Force a UI refresh of this view model
        /// </summary>
        public void RefreshDisplay()
        {
            // This method is used to force UI refresh of the view model
            // Notification is triggered by changing a property
            OnPropertyChanged("Text");
        }

        public StringEntryViewModel(SDBEditor.Models.StringEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry), "Cannot create view model from null entry");
            }

            // Calculate index
            _index = 0; // Will be set by the caller

            // Set properties
            _hashId = entry.HashId;
            _hexValue = entry.HashId.ToString("X");
            _text = entry.Text ?? string.Empty; // Ensure text is never null
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}