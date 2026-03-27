using System;
using System.Globalization;
using System.Windows.Data;

namespace EchoX.Converters
{
    public class GreaterThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double val && double.TryParse(parameter?.ToString(), out double limit))
            {
                return val > limit;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
