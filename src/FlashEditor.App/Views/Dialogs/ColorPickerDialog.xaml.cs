using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlashEditor.App.Rendering;
using FlashEditor.App.Services;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>
/// HSV color picker: a saturation/brightness square, a hue strip, hex and RGB inputs, and quick swatches.
/// </summary>
public partial class ColorPickerDialog : Window
{
    private const int AreaSize = 256;
    private static readonly string[] SwatchHex =
    {
        "#000000", "#404040", "#808080", "#C0C0C0", "#FFFFFF", "#FF3B30", "#FF9500", "#FFCC00",
        "#34C759", "#00C7BE", "#30B0C7", "#007AFF", "#5856D6", "#AF52DE", "#FF2D55", "#A2845E",
    };

    private readonly WriteableBitmap _svBitmap = new(AreaSize, AreaSize, 96, 96, PixelFormats.Bgr32, null);
    private double _hue, _saturation, _value;
    private bool _updatingText;

    public ColorPickerDialog(ColorRgba initial, string title)
    {
        InitializeComponent();
        Title = title;
        SvImage.Source = _svBitmap;
        OldSwatch.Fill = new SolidColorBrush(BitmapConverter.ToWpf(initial with { A = 255 }));
        SwatchList.ItemsSource = SwatchHex
            .Select(h => new SolidColorBrush((Color)ColorConverter.ConvertFromString(h)))
            .ToList();
        FillColorLists();
        SetColor(initial);
    }

    public ColorRgba SelectedColor { get; private set; }

    private static SolidColorBrush BrushOf(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private void FillColorLists()
    {
        BrandList.ItemsSource = AppSettings.Current.BrandColors.Select(BrushOf).ToList();
        BrandList.Visibility = BrandTitle.Visibility = AppSettings.Current.BrandColors.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RecentList.ItemsSource = AppSettings.Current.RecentColors.Select(BrushOf).ToList();
        MineList.ItemsSource = AppSettings.Current.Swatches.Select(BrushOf).ToList();
        NoSwatchesHint.Visibility = AppSettings.Current.Swatches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSaveSwatch(object sender, RoutedEventArgs e)
    {
        AppSettings.Current.AddSwatch(SelectedColor with { A = 255 });
        FillColorLists();
    }

    private void OnSwatchRemove(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SolidColorBrush brush })
        {
            var c = BitmapConverter.FromWpf(brush.Color);
            AppSettings.Current.RemoveSwatch(AppSettings.ToHex(c));
            FillColorLists();
        }
        e.Handled = true;
    }

    private void SetColor(ColorRgba color)
    {
        ColorMath.RgbToHsv(color.R / 255.0, color.G / 255.0, color.B / 255.0, out _hue, out _saturation, out _value);
        RedrawSquare();
        Refresh();
    }

    /// <summary>Paints the saturation/brightness square for the current hue.</summary>
    private void RedrawSquare()
    {
        var pixels = new byte[AreaSize * AreaSize * 4];
        for (int y = 0; y < AreaSize; y++)
        {
            double v = 1 - y / (double)(AreaSize - 1);
            for (int x = 0; x < AreaSize; x++)
            {
                var c = ColorMath.FromHsv(_hue, x / (double)(AreaSize - 1), v);
                int i = (y * AreaSize + x) * 4;
                pixels[i] = c.B; pixels[i + 1] = c.G; pixels[i + 2] = c.R;
            }
        }
        _svBitmap.WritePixels(new Int32Rect(0, 0, AreaSize, AreaSize), pixels, AreaSize * 4, 0);
    }

    /// <summary>Updates markers, swatch and text boxes from the HSV values.</summary>
    private void Refresh()
    {
        SelectedColor = ColorMath.FromHsv(_hue, _saturation, _value);
        NewSwatch.Fill = new SolidColorBrush(BitmapConverter.ToWpf(SelectedColor));

        Canvas.SetLeft(SvMarker, _saturation * (AreaSize - 1) - 6);
        Canvas.SetTop(SvMarker, (1 - _value) * (AreaSize - 1) - 6);
        SvMarker.Stroke = _value > 0.6 && _saturation < 0.4 ? Brushes.Black : Brushes.White;
        Canvas.SetTop(HueMarker, _hue / 360.0 * (AreaSize - 1) - 2);

        _updatingText = true;
        HexBox.Text = SelectedColor.ToHex();
        RedBox.Text = SelectedColor.R.ToString();
        GreenBox.Text = SelectedColor.G.ToString();
        BlueBox.Text = SelectedColor.B.ToString();
        _updatingText = false;
    }

    // ---- Mouse on the square ----
    private void OnSvMouseDown(object sender, MouseButtonEventArgs e)
    {
        SvArea.CaptureMouse();
        PickSv(e.GetPosition(SvArea));
    }

    private void OnSvMouseMove(object sender, MouseEventArgs e)
    {
        if (SvArea.IsMouseCaptured) PickSv(e.GetPosition(SvArea));
    }

    private void PickSv(Point p)
    {
        _saturation = Math.Clamp(p.X / (AreaSize - 1), 0, 1);
        _value = Math.Clamp(1 - p.Y / (AreaSize - 1), 0, 1);
        Refresh();
    }

    // ---- Mouse on the hue strip ----
    private void OnHueMouseDown(object sender, MouseButtonEventArgs e)
    {
        HueArea.CaptureMouse();
        PickHue(e.GetPosition(HueArea));
    }

    private void OnHueMouseMove(object sender, MouseEventArgs e)
    {
        if (HueArea.IsMouseCaptured) PickHue(e.GetPosition(HueArea));
    }

    private void PickHue(Point p)
    {
        _hue = Math.Clamp(p.Y / (AreaSize - 1), 0, 1) * 359.9;
        RedrawSquare();
        Refresh();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e) => ((UIElement)sender).ReleaseMouseCapture();

    // ---- Text inputs ----
    private void OnTextKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnTextCommitted(sender, e);
            e.Handled = true;
        }
    }

    private void OnTextCommitted(object sender, RoutedEventArgs e)
    {
        if (_updatingText) return;
        if (sender == HexBox)
        {
            if (ColorRgba.TryParseHex(HexBox.Text, out var c)) SetColor(c with { A = 255 });
            else Refresh();
            return;
        }
        if (byte.TryParse(RedBox.Text, out var r) && byte.TryParse(GreenBox.Text, out var g) && byte.TryParse(BlueBox.Text, out var b))
            SetColor(new ColorRgba(r, g, b));
        else
            Refresh();
    }

    private void OnSwatchClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SolidColorBrush brush })
            SetColor(BitmapConverter.FromWpf(brush.Color));
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        // Apply a value still being typed.
        if (Keyboard.FocusedElement is TextBox box) OnTextCommitted(box, e);
        AppSettings.Current.AddRecentColor(SelectedColor with { A = 255 });
        DialogResult = true;
    }
}
