using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Converters;

/// <summary>Converts a core <see cref="ColorRgba"/> into a WPF brush for display.</summary>
public sealed class ColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ColorRgba c) return Brushes.Transparent;
        var brush = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
