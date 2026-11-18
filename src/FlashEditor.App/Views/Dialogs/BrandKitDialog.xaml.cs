using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.App.Services;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>Settings > Brand Kit: the colors and fonts used again and again, kept for every project.</summary>
public partial class BrandKitDialog : Window
{
    private readonly List<ColorRgba> _colors;
    private readonly PixelBuffer? _picture;
    private readonly Func<ColorRgba, string, ColorRgba?> _pickColor;

    /// <param name="picture">The picture being edited (for "Add colors from this picture"), or null.</param>
    /// <param name="pickColor">Opens the color picker: (start color, title) -> chosen color or null.</param>
    public BrandKitDialog(PixelBuffer? picture, Func<ColorRgba, string, ColorRgba?> pickColor)
    {
        InitializeComponent();
        _picture = picture;
        _pickColor = pickColor;
        _colors = AppSettings.Current.BrandColors.Select(Parse).ToList();
        FromPictureButton.IsEnabled = picture != null;

        var fonts = AppSettings.Current.BrandFonts;
        SelectFont(Font1, fonts.ElementAtOrDefault(0));
        SelectFont(Font2, fonts.ElementAtOrDefault(1));
        SelectFont(Font3, fonts.ElementAtOrDefault(2));
        Refresh();
    }

    private static ColorRgba Parse(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex);
        return new ColorRgba(c.R, c.G, c.B);
    }

    private static void SelectFont(ComboBox box, string? name)
        => box.SelectedItem = name == null ? null : Tools.Options.TextToolOptions.InstalledFonts.FirstOrDefault(f => f.Source == name);

    private void Refresh()
    {
        ColorList.ItemsSource = _colors.Select(c => new SolidColorBrush(BitmapConverter.ToWpf(c))).ToList();
        AddColorButton.IsEnabled = _colors.Count < AppSettings.MaxBrandColors;
        ColorHint.Text = _colors.Count == 0 ? "No colors yet. Add one, or take them from the picture you are working on." : $"{_colors.Count} of {AppSettings.MaxBrandColors} colors.";
    }

    private int IndexOf(object sender)
        => sender is FrameworkElement { DataContext: SolidColorBrush brush } ? _colors.FindIndex(c => BitmapConverter.ToWpf(c) == brush.Color) : -1;

    private void OnColorClick(object sender, MouseButtonEventArgs e)
    {
        int index = IndexOf(sender);
        if (index < 0) return;
        if (_pickColor(_colors[index], "Brand color") is { } picked) { _colors[index] = picked with { A = 255 }; Refresh(); }
    }

    private void OnColorRemove(object sender, MouseButtonEventArgs e)
    {
        int index = IndexOf(sender);
        if (index >= 0) { _colors.RemoveAt(index); Refresh(); }
        e.Handled = true;
    }

    private void OnAddColor(object sender, RoutedEventArgs e)
    {
        if (_pickColor(new ColorRgba(59, 130, 246), "Add a brand color") is { } picked) { _colors.Add(picked with { A = 255 }); Refresh(); }
    }

    private void OnFromPicture(object sender, RoutedEventArgs e)
    {
        if (_picture == null) return;
        foreach (var color in PaletteExtractor.Extract(_picture, 6))
        {
            if (_colors.Count >= AppSettings.MaxBrandColors) break;
            if (!_colors.Contains(color)) _colors.Add(color);
        }
        Refresh();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var fonts = new[] { Font1, Font2, Font3 }
            .Select(b => (b.SelectedItem as FontFamily)?.Source)
            .OfType<string>().Distinct().ToList();
        AppSettings.Current.SetBrand(_colors.Select(AppSettings.ToHex).ToList(), fonts);
        DialogResult = true;
    }
}
