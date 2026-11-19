using System.Windows;
using System.Windows.Media;

namespace FlashEditor.App.Services;

/// <summary>
/// The colors of the icons. Every icon takes one of a few palette brushes (Icon.Blue, Icon.Green...). With "colorful icons" on, each brush has its
/// color; off, all of them are the plain light gray the icons had before. Switching changes the brushes themselves, so everything updates at once.
/// </summary>
public sealed class IconThemeState : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private int _version;

    /// <summary>Goes up each time the icon colors change, so bindings that use it look again.</summary>
    public int Version
    {
        get => _version;
        set { _version = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Version))); }
    }
}

public static class IconTheme
{
    /// <summary>Lets XAML bindings follow changes of the icon colors.</summary>
    public static IconThemeState State { get; } = new();

    private static readonly (string Key, string Hex)[] Palette =
    {
        ("Icon.Blue", "#5B9BFF"), ("Icon.Sky", "#38BDF8"), ("Icon.Teal", "#2DD4BF"), ("Icon.Green", "#4ADE80"), ("Icon.Lime", "#A3E635"),
        ("Icon.Amber", "#FBBF24"), ("Icon.Orange", "#FB923C"), ("Icon.Red", "#F87171"), ("Icon.Pink", "#F472B6"), ("Icon.Purple", "#A78BFA"),
        ("Icon.Slate", "#94A3B8"),
    };

    private static bool _colorful;

    /// <summary>The look of the icons: "Plain" (gray) or "Colorful" (every icon in its own color).</summary>
    public static string Style
    {
        get => _colorful ? "Colorful" : "Plain";
        set
        {
            _colorful = value == "Colorful";
            Apply();
        }
    }

    /// <summary>Gives every palette brush its color (call once at the start, and after the choice changed).</summary>
    public static void Apply()
    {
        var resources = Application.Current?.Resources;
        if (resources == null) return;
        var neutral = resources["IconBrush"] is SolidColorBrush plain ? plain.Color : Color.FromRgb(0xD8, 0xD8, 0xD8);
        foreach (var (key, hex) in Palette)
        {
            var color = _colorful ? (Color)ColorConverter.ConvertFromString(hex) : neutral;
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;                          // everything that uses it as a DynamicResource follows
        }
        State.Version++;
    }

    /// <summary>The palette brush with this name (Blue, Green...), or the plain icon brush.</summary>
    public static Brush Brush(string name)
        => Application.Current?.Resources["Icon." + name] as Brush ?? (Brush)Application.Current!.Resources["IconBrush"];

    /// <summary>Which color a tool of the toolbox has, by the name of its icon.</summary>
    public static Brush ForTool(string iconKey) => Brush(iconKey switch
    {
        "Move" => "Blue",
        "RectSelect" or "EllipseSelect" or "Lasso" or "PolygonLasso" or "Pen" or "QuickSelect" or "MagicWand" or "ClickSelect" => "Teal",
        "Crop" or "PerspectiveCrop" => "Green",
        "Brush" or "MaskBrush" or "Eraser" or "Refine" or "RemoveBrush" => "Orange",
        "Bucket" or "Text" => "Amber",
        "Eyedropper" => "Sky",
        "Clone" or "Heal" or "SpotHeal" or "Patch" => "Pink",
        "Tone" or "BlurSharpen" or "Liquify" => "Purple",
        "Curve" => "Lime",
        _ => "Slate",
    });
}

/// <summary>The color of the icon of a ribbon button (a palette brush). Without one, the icon keeps the plain color.</summary>
public static class RibbonIcon
{
    public static readonly DependencyProperty BrushProperty =
        DependencyProperty.RegisterAttached("Brush", typeof(Brush), typeof(RibbonIcon), new FrameworkPropertyMetadata(null));

    public static Brush? GetBrush(DependencyObject d) => (Brush?)d.GetValue(BrushProperty);
    public static void SetBrush(DependencyObject d, Brush? value) => d.SetValue(BrushProperty, value);
}
