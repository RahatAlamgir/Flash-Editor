using System.Windows;
using System.Globalization;
using System.Windows.Media;
using FlashEditor.Core.Snapping;

namespace FlashEditor.App.Views.Editor;

/// <summary>
/// Draws the grid, the guide lines and the lines something just snapped to, in picture coordinates (it sits inside the zoomed
/// world of the canvas). Lines keep a thickness of one screen pixel at every zoom level.
/// </summary>
public sealed class AidsOverlay : FrameworkElement
{
    private static readonly Color GridColor = Color.FromArgb(0x5A, 0x38, 0xB6, 0xFF);
    private static readonly Color GridMajorColor = Color.FromArgb(0x8C, 0x38, 0xB6, 0xFF);
    private static readonly Color GuideColor = Color.FromRgb(0x00, 0xD8, 0xE8);
    private static readonly Color SnapColor = Color.FromRgb(0xFF, 0x2D, 0x9A);

    public double Zoom { get; set; } = 1;

    /// <summary>The part of the picture space that is on screen (guides and snap lines are drawn across all of it).</summary>
    public Rect Visible { get; set; }

    public Size PageSize { get; set; }
    public bool ShowGrid { get; set; }
    public double GridSize { get; set; } = 50;
    public bool ShowGuides { get; set; } = true;
    public IReadOnlyList<double> VerticalGuides { get; set; } = Array.Empty<double>();
    public IReadOnlyList<double> HorizontalGuides { get; set; } = Array.Empty<double>();

    /// <summary>A guide being pulled out of a ruler right now (NaN = none).</summary>
    public double TempVertical { get; set; } = double.NaN;
    public double TempHorizontal { get; set; } = double.NaN;

    public IReadOnlyList<double> SnapX { get; set; } = Array.Empty<double>();
    public IReadOnlyList<double> SnapY { get; set; } = Array.Empty<double>();

    /// <summary>Equal gaps to show as measured stretches with the distance in pixels.</summary>
    public IReadOnlyList<GapMark> Gaps { get; set; } = Array.Empty<GapMark>();

    protected override void OnRender(DrawingContext dc)
    {
        double px = 1 / Math.Max(Zoom, 1e-6);
        var visible = Visible;

        if (ShowGrid && GridSize >= 1 && PageSize.Width > 0)
        {
            // When zoomed far out, only every n-th line is drawn so the lines never crowd together.
            double step = GridSize;
            int every = 1;
            while (step * Zoom < 8 && every < 1024) { step *= 2; every *= 2; }
            var pen = Pen(GridColor, px);
            var major = Pen(GridMajorColor, px);

            for (double x = 0; x <= PageSize.Width + 1e-6; x += step)
                dc.DrawLine(IsMajor(x, step) ? major : pen, new Point(x, 0), new Point(x, PageSize.Height));
            for (double y = 0; y <= PageSize.Height + 1e-6; y += step)
                dc.DrawLine(IsMajor(y, step) ? major : pen, new Point(0, y), new Point(PageSize.Width, y));
        }

        if (ShowGuides)
        {
            var pen = Pen(GuideColor, px);
            foreach (var x in VerticalGuides) dc.DrawLine(pen, new Point(x, visible.Top), new Point(x, visible.Bottom));
            foreach (var y in HorizontalGuides) dc.DrawLine(pen, new Point(visible.Left, y), new Point(visible.Right, y));
        }
        if (!double.IsNaN(TempVertical)) dc.DrawLine(Pen(GuideColor, px), new Point(TempVertical, visible.Top), new Point(TempVertical, visible.Bottom));
        if (!double.IsNaN(TempHorizontal)) dc.DrawLine(Pen(GuideColor, px), new Point(visible.Left, TempHorizontal), new Point(visible.Right, TempHorizontal));

        var snap = Pen(SnapColor, px);
        foreach (var x in SnapX) dc.DrawLine(snap, new Point(x, visible.Top), new Point(x, visible.Bottom));
        foreach (var y in SnapY) dc.DrawLine(snap, new Point(visible.Left, y), new Point(visible.Right, y));

        foreach (var gap in Gaps) DrawGap(dc, gap, snap, px);
    }

    /// <summary>A measured gap: a line with a small cross-bar at each end and the distance in a pink label (all sized in screen pixels).</summary>
    private void DrawGap(DrawingContext dc, GapMark gap, Pen pen, double px)
    {
        double tick = 5 * px;
        var brush = new SolidColorBrush(SnapColor);
        brush.Freeze();
        var text = new FormattedText(gap.Value.ToString("0", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"), 11 * px, Brushes.White, 1.0);

        Point a, b, labelCenter;
        if (gap.Horizontal)
        {
            a = new Point(gap.From, gap.Cross); b = new Point(gap.To, gap.Cross);
            dc.DrawLine(pen, a, b);
            dc.DrawLine(pen, new Point(a.X, a.Y - tick), new Point(a.X, a.Y + tick));
            dc.DrawLine(pen, new Point(b.X, b.Y - tick), new Point(b.X, b.Y + tick));
            labelCenter = new Point((a.X + b.X) / 2, a.Y - 11 * px);
        }
        else
        {
            a = new Point(gap.Cross, gap.From); b = new Point(gap.Cross, gap.To);
            dc.DrawLine(pen, a, b);
            dc.DrawLine(pen, new Point(a.X - tick, a.Y), new Point(a.X + tick, a.Y));
            dc.DrawLine(pen, new Point(b.X - tick, b.Y), new Point(b.X + tick, b.Y));
            labelCenter = new Point(a.X + 14 * px + text.Width / 2, (a.Y + b.Y) / 2);
        }

        var box = new Rect(labelCenter.X - text.Width / 2 - 4 * px, labelCenter.Y - text.Height / 2 - 1 * px, text.Width + 8 * px, text.Height + 2 * px);
        dc.DrawRoundedRectangle(brush, null, box, 3 * px, 3 * px);
        dc.DrawText(text, new Point(labelCenter.X - text.Width / 2, labelCenter.Y - text.Height / 2));
    }

    /// <summary>Every fifth grid line is drawn a little stronger.</summary>
    private static bool IsMajor(double position, double step) => Math.Abs(position / (step * 5) - Math.Round(position / (step * 5))) < 1e-6;

    private static Pen Pen(Color color, double thickness)
    {
        var pen = new Pen(new SolidColorBrush(color), thickness);
        pen.Freeze();
        return pen;
    }
}
