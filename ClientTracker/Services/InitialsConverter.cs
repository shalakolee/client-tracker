using System.Globalization;

namespace ClientTracker.Services;

public sealed class InitialsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name || string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var parts = name.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return string.Empty;
        }

        var first = parts[0];
        return first[0].ToString().ToUpperInvariant();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
