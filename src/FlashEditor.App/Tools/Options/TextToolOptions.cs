using System.Windows.Media;
using FlashEditor.Core.Common;

namespace FlashEditor.App.Tools.Options;

/// <summary>Settings for the Text tool.</summary>
public sealed class TextToolOptions : ObservableObject
{
    private FontFamily _fontFamily = new("Segoe UI");
    private double _fontSize = 48;
    private bool _bold;
    private bool _italic;

    /// <summary>All fonts installed on this PC, sorted by name.</summary>
    public static IReadOnlyList<FontFamily> InstalledFonts { get; } =
        Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();

    public FontFamily FontFamily { get => _fontFamily; set => SetField(ref _fontFamily, value ?? new FontFamily("Segoe UI")); }
    public double FontSize { get => _fontSize; set => SetField(ref _fontSize, Math.Clamp(Math.Round(value), 4, 1000)); }
    public bool Bold { get => _bold; set => SetField(ref _bold, value); }
    public bool Italic { get => _italic; set => SetField(ref _italic, value); }
}
