using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SDBEditor.Handlers; // ✅ Added to enable FunctionEntry mapping

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
                    HexValue = value.ToString("X");
                    OnPropertyChanged(nameof(FunctionEntry)); // Notify UI that label may change too
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
                    _text = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Human-friendly label from metadata, like "Nickname", "Full Name", etc.
        /// </summary>
        public string FunctionEntry => SdbFunctionEntryMapper.Lookup(HashId);


        /// <summary>
        /// Force a UI refresh of this view model
        /// </summary>
        public void RefreshDisplay()
        {
            OnPropertyChanged("Text");
            OnPropertyChanged(nameof(FunctionEntry));
        }

        public StringEntryViewModel(SDBEditor.Models.StringEntry entry)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry), "Cannot create view model from null entry");
            }

            _index = 0;
            _hashId = entry.HashId;
            _hexValue = entry.HashId.ToString("X");
            _text = entry.Text ?? string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
