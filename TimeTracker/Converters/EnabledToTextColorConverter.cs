using System.Globalization;

namespace TimeTracker.Converters // Ensure this namespace matches your project
{
    public class EnabledToTextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled && isEnabled)
            {
                return Colors.White; // Enabled text color
            }
            return Colors.Gray; // Disabled text color
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}