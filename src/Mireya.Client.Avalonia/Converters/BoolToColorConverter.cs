using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Mireya.Client.Avalonia.Converters;

public class BoolToColorConverter : IValueConverter
{
    public string TrueColor { get; set; } = "#D32F2F";
    public string FalseColor { get; set; } = "#4CAF50";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Brush.Parse(TrueColor) : Brush.Parse(FalseColor);
        }
        return Brush.Parse(FalseColor);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
