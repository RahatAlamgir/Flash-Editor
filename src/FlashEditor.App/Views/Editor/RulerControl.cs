using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FlashEditor.App.Views.Editor;

/// <summary>
/// A ruler along the top or left edge of the canvas. Numbers are in image pixels; the spacing of the marks follows the zoom
/// (always a "nice" distance such as 10, 50, 100 px). A thin line shows where the mouse is.
/// </summary>
public sealed class RulerControl : FrameworkElement
{
    public const double Thickness = 20;

    private static readonly double[] NiceSteps = { 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000 };
    private static readonly Brush BackBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)));
    private static readonly Brush PageBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)));
    private static readonly Brush TextBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)));
    private static readonly Pen TickPen = FrozenPen(Color.FromRgb(0x8A, 0x8A, 0x8A));
    private static readonly Pen MinorPen = FrozenPen(Color.FromRgb(0x5E, 0x5E, 0x5E));
    private static readonly Pen EdgePen = FrozenPen(Color.FromRgb(0x4A, 0x4A, 0x4A));
    private static readonly Pen MarkerPen = FrozenPen(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly Typeface Face = new("Segoe UI");

    private double _zoom = 1, _offset, _pageLength, _marker = double.NaN;

    /// <summary>True for the ruler on the left edge (measures downwards).</summary>
    public bool IsVertical { get; set; }

    /// <summary>The picture's zoom, the screen position of the picture's 0 along this ruler, and the picture's length in pixels.</summary>
    public void SetView(double zoom, double offset, double pageLength)
    {
        _zoom = zoom; _offset = offset; _pageLength = pageLength;
        InvalidateVisual();
    }

    /// <summary>Screen position (along this ruler) of the mouse, or NaN for none.</summary>
    public void SetMarker(double position)
    {
        if (position.Equals(_marker)) return;
        _marker = position;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => IsVertical ? new Size(Thickness, 0) : new Size(0, Thickness);

    protected override void OnRender(DrawingContext dc)
    {
        double length = IsVertical ? ActualHeight : ActualWidth;
        double across = Thickness;
        if (length <= 0 || _zoom <= 0) return;

        dc.DrawRectangle(BackBrush, null, Rect(0, length, 0, across));
        double pageStart = _offset, pageEnd = _offset + _pageLength * _zoom;
        dc.DrawRectangle(PageBrush, null, Rect(Math.Max(0, pageStart), Math.Min(length, pageEnd), 0, across));

        double major = NiceSteps.FirstOrDefault(s => s * _zoom >= 70, NiceSteps[^1]);
        int parts = Parts(major);
        double minor = major / parts;
        bool drawMinor = parts > 1 && minor * _zoom >= 4;

        double first = -_offset / _zoom, last = (length - _offset) / _zoom;
        double step = drawMinor ? minor : major;
        for (double v = Math.Floor(first / step) * step; v <= last + step; v += step)
        {
            double pos = Math.Round(_offset + v * _zoom) + 0.5;
            if (pos < -1 || pos > length + 1) continue;
            bool isMajor = Math.Abs(v / major - Math.Round(v / major)) < 1e-6;
            if (isMajor)
            {
                dc.DrawLine(TickPen, Point(pos, 0), Point(pos, across));
                DrawLabel(dc, Math.Round(v).ToString(CultureInfo.InvariantCulture), pos + 3);
            }
            else
            {
                bool half = parts % 2 == 0 && Math.Abs(v / major * 2 - Math.Round(v / major * 2)) < 1e-6;
                dc.DrawLine(MinorPen, Point(pos, across - (half ? 9 : 5)), Point(pos, across));
            }
        }

        // Border towards the canvas.
        if (IsVertical) dc.DrawLine(EdgePen, new Point(across - 0.5, 0), new Point(across - 0.5, length));
        else dc.DrawLine(EdgePen, new Point(0, across - 0.5), new Point(length, across - 0.5));

        if (!double.IsNaN(_marker) && _marker >= 0 && _marker <= length)
            dc.DrawLine(MarkerPen, Point(_marker, 0), Point(_marker, across));
    }

    /// <summary>How many small marks make up one numbered distance (10 -> 10, 20 -> 4, 50 -> 5, ...).</summary>
    private static int Parts(double major)
    {
        double mantissa = major / Math.Pow(10, Math.Floor(Math.Log10(major)));
        if (major < 10) return major == 1 ? 1 : major == 2 ? 2 : 5;
        return mantissa < 1.5 ? 10 : mantissa < 3 ? 4 : 5;
    }

    private void DrawLabel(DrawingContext dc, string text, double pos)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 9, TextBrush, 1.0);
        if (IsVertical)
        {
            dc.PushTransform(new RotateTransform(-90, 0, 0));
            // After the turn, x runs upwards: place the text so it reads bottom to top, next to the mark.
            dc.DrawText(formatted, new Point(-pos - formatted.Width, 1));
            dc.Pop();
        }
        else dc.DrawText(formatted, new Point(pos, 2));
    }

    private Rect Rect(double from, double to, double crossFrom, double crossTo)
        => IsVertical ? new Rect(crossFrom, from, crossTo - crossFrom, Math.Max(0, to - from)) : new Rect(from, crossFrom, Math.Max(0, to - from), crossTo - crossFrom);

    private Point Point(double along, double cross) => IsVertical ? new Point(cross, along) : new Point(along, cross);

    private static Brush Frozen(SolidColorBrush b) { b.Freeze(); return b; }

    private static Pen FrozenPen(Color c) { var p = new Pen(new SolidColorBrush(c), 1); p.Freeze(); return p; }
}
