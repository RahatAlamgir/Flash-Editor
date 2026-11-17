using System.Globalization;
using System.Windows.Data;

namespace FlashEditor.App.Converters;

/// <summary>True gives full opacity (1), false a dimmed look (0.4): for labels of settings that currently do nothing.</summary>
public sealed class BoolToDimConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 1.0 : 0.4;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
