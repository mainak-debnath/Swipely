using System.Globalization;

namespace TimeTracker.Converters // Ensure this namespace matches your project
{
    public class EnabledToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value is the IsEnabled property (a boolean)
            if (value is bool isEnabled && isEnabled)
            {
                return Color.FromArgb("#FF8C00"); // Enabled color
            }
            return Color.FromArgb("#FFE0B2"); // Disabled color
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}