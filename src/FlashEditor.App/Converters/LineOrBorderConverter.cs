using System.Globalization;
using System.Windows.Data;

namespace FlashEditor.App.Converters;

/// <summary>Label for the stroke checkbox: "Line" for lines, "Border" for other shapes.</summary>
public sealed class LineOrBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Line" : "Border";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
