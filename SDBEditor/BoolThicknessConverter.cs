using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SDBEditor
{
    public class BoolToThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                // Return a visible border thickness for new additions
                return new Thickness(2);
            }

            // Return zero thickness for existing entries
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}