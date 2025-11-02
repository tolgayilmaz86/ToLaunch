using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ToLaunch.Converters;

// Converts an integer count to bool (true when > 0).
// If ConverterParameter is "Invert" (case-insensitive), result is inverted.
public class CountToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count;
        if (value is int i)
            count = i;
        else if (value is long l)
            count = (int)l;
        else if (value is null)
            count = 0;
        else
        {
            // attempt parse
            if (!int.TryParse(value.ToString(), out count))
                count = 0;
        }

        var result = count > 0;

        if (parameter is string p && string.Equals(p, "Invert", StringComparison.OrdinalIgnoreCase))
            result = !result;

        return result;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}