using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FlashEditor.Core.Barcodes;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Rendering;

/// <summary>Draws a <see cref="BarcodeObject"/> as vectors in its own box (0,0 to Width,Height).</summary>
internal static class BarcodeRenderer
{
    private const int Quiet = 10;                    // the empty margin left and right, in modules

    public static void Draw(DrawingContext dc, BarcodeObject code, double width, double height)
    {
        var back = code.TransparentBackground ? null : Brush(code.Background);
        if (back != null) dc.DrawRectangle(back, null, new Rect(0, 0, width, height));
        var ink = Brush(code.Foreground);

        BarcodeData data;
        try { data = BarcodeEncoder.Encode(code.Content, code.Kind); }
        catch (FormatException ex)
        {
            // The text does not fit this kind of barcode: say why instead of drawing something wrong.
            var pen = new Pen(Brush(new Core.Imaging.ColorRgba(220, 38, 38)), Math.Max(1, Math.Min(width, height) * 0.02)); pen.Freeze();
            dc.DrawRectangle(null, pen, new Rect(2, 2, Math.Max(1, width - 4), Math.Max(1, height - 4)));
            var message = Text(ex.Message, Math.Clamp(height * 0.11, 7, 16), Brush(new Core.Imaging.ColorRgba(185, 28, 28)), Math.Max(10, width - 12));
            dc.DrawText(message, new Point(6, Math.Max(4, (height - message.Height) / 2)));
            return;
        }

        double module = width / (data.Width + Quiet * 2), left = module * Quiet;
        double font = code.ShowText ? Math.Clamp(Math.Min(height * 0.2, module * 9), 6, 40) : 0;
        double textZone = code.ShowText ? font * 1.35 : 0;
        double top = height * 0.05, barBottom = Math.Max(top + 2, height - textZone - height * 0.04);

        // The bars: neighbouring dark modules are drawn as one rectangle.
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            int i = 0;
            while (i < data.Width)
            {
                if (!data.Modules[i]) { i++; continue; }
                int start = i;
                while (i < data.Width && data.Modules[i]) i++;
                bool guard = data.Guards.Any(g => start >= g.From && i <= g.To);
                double bottom = guard && code.ShowText ? Math.Min(height - height * 0.04, barBottom + font * 0.6) : barBottom;
                double x0 = left + start * module, x1 = left + i * module;
                ctx.BeginFigure(new Point(x0, top), true, true);
                ctx.LineTo(new Point(x1, top), false, false);
                ctx.LineTo(new Point(x1, bottom), false, false);
                ctx.LineTo(new Point(x0, bottom), false, false);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(ink, null, geometry);

        if (code.ShowText)
            foreach (var (text, center) in data.Labels)
            {
                var label = Text(text, font, ink, maxWidth: data.Width * module);
                dc.DrawText(label, new Point(left + center * module - label.Width / 2, height - textZone - height * 0.02 + (textZone - label.Height) / 2));
            }
    }

    private static FormattedText Text(string s, double size, Brush brush, double maxWidth = 0)
    {
        var text = new FormattedText(s ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), Math.Max(4, size), brush, 1.0);
        if (maxWidth > 0) { text.MaxTextWidth = maxWidth; text.Trimming = TextTrimming.CharacterEllipsis; }
        return text;
    }

    private static SolidColorBrush Brush(Core.Imaging.ColorRgba c)
    {
        var b = new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
        b.Freeze();
        return b;
    }
}
