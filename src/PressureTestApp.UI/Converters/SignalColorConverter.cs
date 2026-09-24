using Avalonia.Data.Converters;
using Avalonia.Media;
using System;

namespace PressureTestApp.UI.Converters;

public class SignalColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isActive)
        {
            if (isActive)
            {
                // 根据参数决定颜色，默认绿色，参数为"alarm"时为红色
                var param = parameter as string;
                if (param?.Equals("alarm", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    return new SolidColorBrush(Color.FromRgb(229, 62, 62)); // 红色告警
                }
                return new SolidColorBrush(Color.FromRgb(46, 213, 115)); // 绿色正常
            }
            return new SolidColorBrush(Color.FromRgb(100, 100, 100)); // 灰色未激活
        }
        return new SolidColorBrush(Color.FromRgb(100, 100, 100));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}