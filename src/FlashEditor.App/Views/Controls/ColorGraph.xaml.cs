using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Views.Controls;

/// <summary>
/// Pick a color by dragging on a graph: the square chooses saturation (left to right) and
/// brightness (top to bottom), the strip on the right chooses the hue.
/// </summary>
public partial class ColorGraph : UserControl
{
    private const int Size = 240;
    private readonly WriteableBitmap _bitmap = new(Size, Size, 96, 96, PixelFormats.Bgr32, null);
    private double _hue, _saturation, _value;

    public ColorGraph()
    {
        InitializeComponent();
        SvImage.Source = _bitmap;
        Redraw();
        UpdateMarkers();
    }

    /// <summary>Raised while the user drags (and after <see cref="Color"/> is set in code).</summary>
    public event EventHandler? ColorChanged;

    public ColorRgba Color
    {
        get => ColorMath.FromHsv(_hue, _saturation, _value);
        set
        {
            ColorMath.RgbToHsv(value.R / 255.0, value.G / 255.0, value.B / 255.0, out double h, out _saturation, out _value);
            if (_saturation > 0.001) _hue = h; // keep the hue of gray colors
            Redraw();
            UpdateMarkers();
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Redraw()
    {
        var pixels = new byte[Size * Size * 4];
        for (int y = 0; y < Size; y++)
        {
            double v = 1 - y / (double)(Size - 1);
            for (int x = 0; x < Size; x++)
            {
                var c = ColorMath.FromHsv(_hue, x / (double)(Size - 1), v);
                int i = (y * Size + x) * 4;
                pixels[i] = c.B; pixels[i + 1] = c.G; pixels[i + 2] = c.R;
            }
        }
        _bitmap.WritePixels(new Int32Rect(0, 0, Size, Size), pixels, Size * 4, 0);
    }

    private void UpdateMarkers()
    {
        Canvas.SetLeft(SvMarker, _saturation * (Size - 1) - 7);
        Canvas.SetTop(SvMarker, (1 - _value) * (Size - 1) - 7);
        SvMarker.Stroke = _value > 0.6 && _saturation < 0.4 ? Brushes.Black : Brushes.White;
        Canvas.SetTop(HueMarker, _hue / 360.0 * (Size - 1) - 2.5);
    }

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
        _saturation = Math.Clamp(p.X / (Size - 1), 0, 1);
        _value = Math.Clamp(1 - p.Y / (Size - 1), 0, 1);
        UpdateMarkers();
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

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
        _hue = Math.Clamp(p.Y / (Size - 1), 0, 1) * 359.9;
        Redraw();
        UpdateMarkers();
        ColorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e) => ((UIElement)sender).ReleaseMouseCapture();
}
