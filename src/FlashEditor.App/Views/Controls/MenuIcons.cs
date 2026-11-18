using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FlashEditor.App.Views.Controls;

/// <summary>Small icons (the line drawings in Themes/Icons.xaml) for menu items.</summary>
public static class MenuIcons
{
    /// <summary>The icon called <paramref name="key"/> (without the "Icon." prefix), or null when there is none.</summary>
    public static object? Of(FrameworkElement owner, string? key)
    {
        if (key == null || owner.TryFindResource("Icon." + key) is not Geometry geometry) return null;
        var path = new Path { Data = geometry, Style = (Style)owner.FindResource("IconPath") };
        return new Viewbox { Width = 15, Height = 15, Child = path };
    }
}
