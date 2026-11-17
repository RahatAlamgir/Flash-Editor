using System.Globalization;
using System.Windows.Data;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Converters;

/// <summary>Shape kind to its readable name ("HalfRing" becomes "Half ring").</summary>
public sealed class ShapeNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ShapeKind kind ? ShapeNames.Of(kind) : value?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
