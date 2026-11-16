using System.Globalization;
using System.Windows.Data;

namespace FlashEditor.App.Converters;

/// <summary>True when all bound values are equal (used to check the selected item in a button row).</summary>
public sealed class EqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.Length >= 2 && Equals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
