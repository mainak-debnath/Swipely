using System.Globalization;

namespace TimeTracker.Converters // Ensure this namespace matches your project
{
    public class EnabledToColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isEnabled && isEnabled)
            {
                return Color.FromArgb("#D97706");
            }
            return Color.FromArgb("#E7EEF8");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
