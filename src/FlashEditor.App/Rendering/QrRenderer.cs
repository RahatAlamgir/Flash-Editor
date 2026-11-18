using System.Windows;
using System.Windows.Media;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Qr;

namespace FlashEditor.App.Rendering;

/// <summary>Draws a <see cref="QrCodeObject"/> as vectors in its own box (0,0 to Width,Height).</summary>
internal static class QrRenderer
{
    public static void Draw(DrawingContext dc, QrCodeObject qr, double width, double height)
    {
        var back = qr.TransparentBackground ? null : Frozen(BitmapConverterColor(qr.Background));
        if (back != null) dc.DrawRectangle(back, null, new Rect(0, 0, width, height));

        bool[,] modules;
        try { modules = QrEncoder.Encode(string.IsNullOrEmpty(qr.Content) ? " " : qr.Content, qr.Level); }
        catch (ArgumentException)
        {
            // Too much text for a QR code: a crossed-out box tells why nothing is drawn.
            var pen = new Pen(Frozen(new SolidColorBrush(Color.FromRgb(220, 38, 38))), Math.Max(1, Math.Min(width, height) * 0.02));
            dc.DrawRectangle(null, pen, new Rect(2, 2, Math.Max(1, width - 4), Math.Max(1, height - 4)));
            dc.DrawLine(pen, new Point(2, 2), new Point(width - 2, height - 2));
            dc.DrawLine(pen, new Point(width - 2, 2), new Point(2, height - 2));
            return;
        }

        int n = modules.GetLength(0);
        double quiet = Math.Clamp(qr.Quiet, 0, 12);
        double module = Math.Min(width, height) / (n + 2 * quiet);
        double left = (width - module * n) / 2, top = (height - module * n) / 2;
        var brush = Frozen(BitmapConverterColor(qr.Foreground));

        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var ctx = geometry.Open())
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    if (!modules[y, x]) continue;
                    double px = left + x * module, py = top + y * module;
                    bool finder = (x < 7 && y < 7) || (x >= n - 7 && y < 7) || (x < 7 && y >= n - 7);   // the three big squares always stay square
                    if (qr.Style == QrStyle.Square || finder) AddSquare(ctx, px, py, module + 0.15);     // a hair larger: no gaps between neighbors
                    else if (qr.Style == QrStyle.Rounded) AddRounded(ctx, px, py, module, module * 0.32);
                    else AddDot(ctx, px + module / 2, py + module / 2, module * 0.46);
                }
        }
        geometry.Freeze();
        dc.DrawGeometry(brush, null, geometry);
    }

    private static void AddSquare(StreamGeometryContext ctx, double x, double y, double size)
    {
        ctx.BeginFigure(new Point(x, y), true, true);
        ctx.LineTo(new Point(x + size, y), false, false);
        ctx.LineTo(new Point(x + size, y + size), false, false);
        ctx.LineTo(new Point(x, y + size), false, false);
    }

    private static void AddRounded(StreamGeometryContext ctx, double x, double y, double size, double r)
    {
        ctx.BeginFigure(new Point(x + r, y), true, true);
        ctx.LineTo(new Point(x + size - r, y), false, false);
        ctx.ArcTo(new Point(x + size, y + r), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
        ctx.LineTo(new Point(x + size, y + size - r), false, false);
        ctx.ArcTo(new Point(x + size - r, y + size), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
        ctx.LineTo(new Point(x + r, y + size), false, false);
        ctx.ArcTo(new Point(x, y + size - r), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
        ctx.LineTo(new Point(x, y + r), false, false);
        ctx.ArcTo(new Point(x + r, y), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
    }

    private static void AddDot(StreamGeometryContext ctx, double cx, double cy, double r)
    {
        ctx.BeginFigure(new Point(cx - r, cy), true, true);
        ctx.ArcTo(new Point(cx + r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
        ctx.ArcTo(new Point(cx - r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise, false, false);
    }

    private static SolidColorBrush BitmapConverterColor(Core.Imaging.ColorRgba c) => new(Color.FromArgb(c.A, c.R, c.G, c.B));

    private static SolidColorBrush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
