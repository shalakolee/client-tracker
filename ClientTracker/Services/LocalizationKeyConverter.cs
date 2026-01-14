using System.Globalization;

namespace ClientTracker.Services;

public class LocalizationKeyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key)
        {
            return string.Empty;
        }

        return LocalizationResourceManager.Instance[key];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() ?? string.Empty;
    }
}
