using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FlashEditor.App.Converters;

/// <summary>Visible when the value is not null (or when null, if the parameter is "invert").</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool visible = value != null;
        if (parameter as string == "invert") visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
