using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace PressureTestApp.UI.Converters;

public sealed class NullToZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value ?? 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value ?? 0.0;
}
