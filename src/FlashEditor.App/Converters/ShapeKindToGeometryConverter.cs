using System.Globalization;
using System.Windows.Data;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Converters;

/// <summary>Small outline of a shape kind, for the shape gallery buttons.</summary>
public sealed class ShapeKindToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ShapeKind kind) return null;
        double h = kind == ShapeKind.Line ? 2 : kind is ShapeKind.Arrow ? 12 : 20;
        var shape = new ShapeObject(new ObjectBox(0, 0, 20, h), kind, null, ColorRgba.Black, 1,
            CornerRadius: kind == ShapeKind.SpeechBubble ? 4 : 0, Points: kind == ShapeKind.Polygon ? 6 : 5,
            Inner: kind == ShapeKind.Pie ? 0 : 0.5, Sweep: 270);
        return ShapeGeometryFactory.Build(shape, 20, h);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
