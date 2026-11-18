using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Rendering;

/// <summary>Draws a <see cref="ChartObject"/> as vectors in its own box (0,0 to Width,Height).</summary>
internal static class ChartRenderer
{
    private const string FontName = "Segoe UI";

    public static void Draw(DrawingContext dc, ChartObject chart, double width, double height)
    {
        if (chart.Background is { } bg) dc.DrawRectangle(Brush(bg), null, new Rect(0, 0, width, height));

        double basis = Math.Min(width, height);
        double font = Math.Clamp(basis * 0.045, 7, 26) * Math.Clamp(chart.TextScale, 0.4, 3);
        var ink = Brush(chart.Ink);
        var faint = Brush(chart.Ink with { A = 60 });
        double top = 0;

        if (!string.IsNullOrWhiteSpace(chart.Title))
        {
            var title = Text(chart.Title, font * 1.5, ink, bold: true, maxWidth: width * 0.92);
            dc.DrawText(title, new Point((width - title.Width) / 2, height * 0.03));
            top = height * 0.03 + title.Height + height * 0.02;
        }
        var items = chart.Items;
        var area = new Rect(width * 0.04, top, width * 0.92, Math.Max(10, height - top - height * 0.03));
        if (items.Count == 0)
        {
            var empty = Text("No numbers yet", font, faint);
            dc.DrawText(empty, new Point((width - empty.Width) / 2, area.Y + area.Height / 2 - empty.Height / 2));
            return;
        }

        switch (chart.Kind)
        {
            case ChartKind.Pie: DrawPie(dc, chart, area, font, ink, hole: 0); break;
            case ChartKind.Donut: DrawPie(dc, chart, area, font, ink, hole: 0.55); break;
            case ChartKind.Bars: DrawBars(dc, chart, area, font, ink, faint); break;
            case ChartKind.Line: DrawColumnsOrLine(dc, chart, area, font, ink, faint, line: true); break;
            default: DrawColumnsOrLine(dc, chart, area, font, ink, faint, line: false); break;
        }
    }

    // ---------------------------------------------------------------- columns and line

    private static void DrawColumnsOrLine(DrawingContext dc, ChartObject chart, Rect area, double font, Brush ink, Brush faint, bool line)
    {
        var items = chart.Items;
        var (min, max, step) = Scale(items);
        double labelRoom = chart.ShowLabels ? font * 1.9 : 4, valueRoom = chart.ShowGrid ? MeasureWidth(chart.Format(max), font) + font * 0.8 : 4;
        var plot = new Rect(area.X + valueRoom, area.Y + font * 1.2, Math.Max(10, area.Width - valueRoom), Math.Max(10, area.Height - labelRoom - font * 1.2));
        double Y(double v) => plot.Bottom - (v - min) / (max - min) * plot.Height;

        var gridPen = new Pen(faint, Math.Max(0.6, font * 0.07)); gridPen.Freeze();
        if (chart.ShowGrid)
            for (double v = min; v <= max + step * 0.001; v += step)
            {
                dc.DrawLine(gridPen, new Point(plot.Left, Y(v)), new Point(plot.Right, Y(v)));
                var t = Text(chart.Format(v), font * 0.85, ink);
                dc.DrawText(t, new Point(plot.Left - t.Width - font * 0.4, Y(v) - t.Height / 2));
            }
        double zero = Y(Math.Max(min, Math.Min(0, max)));

        double slot = plot.Width / items.Count;
        var points = new List<Point>();
        for (int i = 0; i < items.Count; i++)
        {
            double cx = plot.Left + slot * (i + 0.5), y = Y(items[i].Value);
            if (line) points.Add(new Point(cx, y));
            else
            {
                double barW = slot * Math.Clamp(chart.BarWidth, 0.1, 1);
                var rect = new Rect(cx - barW / 2, Math.Min(y, zero), barW, Math.Max(0.5, Math.Abs(zero - y)));
                dc.DrawRoundedRectangle(Brush(chart.ColorOf(i)), null, rect, Math.Min(barW * 0.12, 6), Math.Min(barW * 0.12, 6));
            }
            if (chart.ShowLabels)
            {
                var label = Text(items[i].Label, font * 0.9, ink, maxWidth: slot * 0.95);
                dc.DrawText(label, new Point(cx - label.Width / 2, plot.Bottom + font * 0.35));
            }
            if (chart.ShowValues && !line)
            {
                var value = Text(chart.Format(items[i].Value), font * 0.85, ink);
                dc.DrawText(value, new Point(cx - value.Width / 2, items[i].Value >= 0 ? y - value.Height - 1 : y + 1));
            }
        }

        if (line && points.Count > 0)
        {
            var color = chart.ColorOf(0);
            var pen = new Pen(Brush(color), Math.Max(1.5, font * 0.22)) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            for (int i = 1; i < points.Count; i++) dc.DrawLine(pen, points[i - 1], points[i]);
            for (int i = 0; i < points.Count; i++)
            {
                dc.DrawEllipse(Brush(Colors.White.ToRgba()), pen, points[i], font * 0.38, font * 0.38);
                if (chart.ShowValues)
                {
                    var value = Text(chart.Format(items[i].Value), font * 0.85, ink);
                    dc.DrawText(value, new Point(points[i].X - value.Width / 2, points[i].Y - value.Height - font * 0.5));
                }
            }
        }
        var axis = new Pen(ink, Math.Max(0.8, font * 0.09)); axis.Freeze();
        dc.DrawLine(axis, new Point(plot.Left, zero), new Point(plot.Right, zero));
    }

    // ---------------------------------------------------------------- horizontal bars

    private static void DrawBars(DrawingContext dc, ChartObject chart, Rect area, double font, Brush ink, Brush faint)
    {
        var items = chart.Items;
        var (min, max, step) = Scale(items);
        double labelRoom = chart.ShowLabels ? items.Max(i => MeasureWidth(i.Label, font * 0.9)) + font * 0.6 : 4;
        labelRoom = Math.Min(labelRoom, area.Width * 0.4);
        double valueRoom = chart.ShowValues ? MeasureWidth(chart.Format(max), font * 0.85) + font * 0.8 : font * 0.4;
        var plot = new Rect(area.X + labelRoom, area.Y, Math.Max(10, area.Width - labelRoom - valueRoom), Math.Max(10, area.Height - (chart.ShowGrid ? font * 1.6 : 0)));
        double X(double v) => plot.Left + (v - min) / (max - min) * plot.Width;

        var gridPen = new Pen(faint, Math.Max(0.6, font * 0.07)); gridPen.Freeze();
        if (chart.ShowGrid)
            for (double v = min; v <= max + step * 0.001; v += step)
            {
                dc.DrawLine(gridPen, new Point(X(v), plot.Top), new Point(X(v), plot.Bottom));
                var t = Text(chart.Format(v), font * 0.85, ink);
                dc.DrawText(t, new Point(X(v) - t.Width / 2, plot.Bottom + font * 0.3));
            }
        double zero = X(Math.Max(min, Math.Min(0, max)));
        double slot = plot.Height / items.Count;
        for (int i = 0; i < items.Count; i++)
        {
            double cy = plot.Top + slot * (i + 0.5), x = X(items[i].Value), barH = slot * Math.Clamp(chart.BarWidth, 0.1, 1);
            var rect = new Rect(Math.Min(x, zero), cy - barH / 2, Math.Max(0.5, Math.Abs(x - zero)), barH);
            dc.DrawRoundedRectangle(Brush(chart.ColorOf(i)), null, rect, Math.Min(barH * 0.12, 6), Math.Min(barH * 0.12, 6));
            if (chart.ShowLabels)
            {
                var label = Text(items[i].Label, font * 0.9, ink, maxWidth: labelRoom - font * 0.4);
                dc.DrawText(label, new Point(plot.Left - label.Width - font * 0.4, cy - label.Height / 2));
            }
            if (chart.ShowValues)
            {
                var value = Text(chart.Format(items[i].Value), font * 0.85, ink);
                dc.DrawText(value, new Point(items[i].Value >= 0 ? x + font * 0.3 : x - value.Width - font * 0.3, cy - value.Height / 2));
            }
        }
        var axis = new Pen(ink, Math.Max(0.8, font * 0.09)); axis.Freeze();
        dc.DrawLine(axis, new Point(zero, plot.Top), new Point(zero, plot.Bottom));
    }

    // ---------------------------------------------------------------- pie and donut

    private static void DrawPie(DrawingContext dc, ChartObject chart, Rect area, double font, Brush ink, double hole)
    {
        var items = chart.Items.Where(i => i.Value > 0).ToList();
        double total = items.Sum(i => i.Value);
        if (total <= 0)
        {
            var t = Text("Pie charts need positive numbers", font, ink);
            dc.DrawText(t, new Point(area.X + (area.Width - t.Width) / 2, area.Y + area.Height / 2));
            return;
        }

        double legendWidth = chart.ShowLabels ? Math.Min(area.Width * 0.42, items.Max(i => MeasureWidth(i.Label, font * 0.95)) + font * 2.4) : 0;
        double diameter = Math.Max(10, Math.Min(area.Width - legendWidth - (legendWidth > 0 ? font : 0), area.Height));
        var center = new Point(area.X + (area.Width - legendWidth - (legendWidth > 0 ? font : 0)) / 2, area.Y + area.Height / 2);
        double r = diameter / 2;

        double angle = -Math.PI / 2;
        for (int i = 0; i < items.Count; i++)
        {
            double sweep = items[i].Value / total * Math.PI * 2;
            var color = Brush(chart.ColorOf(chart.Items.ToList().IndexOf(items[i])));
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var a0 = Point(center, r, angle);
                var a1 = Point(center, r, angle + sweep - (items.Count > 1 ? 0 : 1e-4));
                if (items.Count == 1) { ctx.BeginFigure(Point(center, r, angle), true, true); ctx.ArcTo(Point(center, r, angle + Math.PI), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false); ctx.ArcTo(Point(center, r, angle), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false); }
                else
                {
                    ctx.BeginFigure(center, true, true);
                    ctx.LineTo(a0, false, false);
                    ctx.ArcTo(a1, new Size(r, r), 0, sweep > Math.PI, SweepDirection.Clockwise, false, false);
                }
            }
            geometry.Freeze();
            var pen = new Pen(chart.Background is { } bgc ? Brush(bgc) : Brushes.White, Math.Max(1, r * 0.015)); pen.Freeze();
            dc.DrawGeometry(color, pen, geometry);

            if (chart.ShowValues && sweep > 0.18)
            {
                double mid = angle + sweep / 2, rr = r * (hole > 0 ? (1 + hole) / 2 : 0.66);
                var label = Text($"{items[i].Value / total * 100:0}%", font * 0.95, Brushes.White, bold: true);
                dc.DrawText(label, new Point(center.X + Math.Cos(mid) * rr - label.Width / 2, center.Y + Math.Sin(mid) * rr - label.Height / 2));
            }
            angle += sweep;
        }
        if (hole > 0) dc.DrawEllipse(chart.Background is { } hb ? Brush(hb) : Brushes.White, null, center, r * hole, r * hole);

        if (legendWidth > 0)
        {
            double x = area.Right - legendWidth, rowH = font * 1.6, y = area.Y + (area.Height - rowH * chart.Items.Count) / 2;
            for (int i = 0; i < chart.Items.Count; i++)
            {
                dc.DrawRoundedRectangle(Brush(chart.ColorOf(i)), null, new Rect(x, y + rowH * i + rowH * 0.18, font * 0.9, font * 0.9), 2, 2);
                var label = Text(chart.Items[i].Label, font * 0.95, ink, maxWidth: legendWidth - font * 1.4);
                dc.DrawText(label, new Point(x + font * 1.3, y + rowH * i + (rowH - label.Height) / 2));
            }
        }
    }

    private static Point Point(Point c, double r, double angle) => new(c.X + Math.Cos(angle) * r, c.Y + Math.Sin(angle) * r);

    // ---------------------------------------------------------------- helpers

    /// <summary>A neat range for the numbers: it includes zero, and the lines are a round step apart.</summary>
    private static (double Min, double Max, double Step) Scale(IReadOnlyList<ChartItem> items)
    {
        double lo = Math.Min(0, items.Min(i => i.Value)), hi = Math.Max(0, items.Max(i => i.Value));
        if (hi - lo < 1e-9) hi = lo + 1;
        double raw = (hi - lo) / 4, magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double normalized = raw / magnitude;
        double step = (normalized < 1.5 ? 1 : normalized < 3.5 ? 2 : normalized < 7.5 ? 5 : 10) * magnitude;
        return (Math.Floor(lo / step) * step, Math.Ceiling(hi / step) * step, step);
    }

    private static string Number(double v) => Math.Abs(v) >= 1000 ? v.ToString("#,0.##", CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);

    private static FormattedText Text(string s, double size, Brush brush, bool bold = false, double maxWidth = 0)
    {
        var text = new FormattedText(s ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily(FontName), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), Math.Max(4, size), brush, 1.0);
        if (maxWidth > 0) { text.MaxTextWidth = Math.Max(4, maxWidth); text.MaxLineCount = 1; text.Trimming = TextTrimming.CharacterEllipsis; }
        return text;
    }

    private static double MeasureWidth(string s, double size) => Text(s, size, Brushes.Black).Width;

    private static SolidColorBrush Brush(ColorRgba c)
    {
        var b = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
        b.Freeze();
        return b;
    }

    private static ColorRgba ToRgba(this Color c) => new(c.R, c.G, c.B, c.A);
}
