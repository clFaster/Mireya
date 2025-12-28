using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Mireya.Client.Avalonia.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return true;
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        if (value is bool boolValue)
            return !boolValue;
        return true;
    }
}
