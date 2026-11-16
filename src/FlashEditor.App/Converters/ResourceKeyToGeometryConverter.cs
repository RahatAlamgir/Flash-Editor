using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace FlashEditor.App.Converters;

/// <summary>Turns an icon name like "Brush" into the Geometry resource "Icon.Brush".</summary>
public sealed class ResourceKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string key ? Application.Current.TryFindResource("Icon." + key) as Geometry : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
