using System.Globalization;
using System.Windows.Data;
using FlashEditor.App.Services;

namespace FlashEditor.App.Converters;

/// <summary>The name of a tool's icon → the color of that icon (see <see cref="IconTheme"/>).</summary>
public sealed class ToolIconBrushConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => IconTheme.ForTool(value as string ?? "");

    /// <summary>With the version of the icon colors as the second value, the binding looks again when the colors change.</summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) => IconTheme.ForTool(values.Length > 0 ? values[0] as string ?? "" : "");

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
