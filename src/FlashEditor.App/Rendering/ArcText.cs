using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Rendering;

/// <summary>Text that follows a circle: every letter is placed on the arc and turned to match it ("curved text").</summary>
internal static class ArcText
{
    /// <summary>
    /// The outline of all letters of <paramref name="text"/> on an arc of <see cref="TextObject.Arc"/> degrees. A positive arc bends
    /// the text like a rainbow (the middle is higher than the ends); a negative arc like a smile. Letter, word and line gaps work as for
    /// straight text: several lines follow concentric arcs, the next line below the one before. The geometry is in the text's own space:
    /// its bounds say how big the curved text is.
    /// </summary>
    public static Geometry Build(TextObject text)
    {
        var lines = (string.IsNullOrEmpty(text.Text) ? " " : text.Text.Replace("\r", "")).Split('\n');
        var typeface = new Typeface(new FontFamily(text.FontFamily),
            text.Italic ? FontStyles.Italic : FontStyles.Normal,
            text.Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);
        double size = Math.Max(1, text.FontSize);
        double letterGap = text.LetterSpacing * size, wordGap = text.WordSpacing * size;
        double lineHeight = Math.Max(1, new FontFamily(text.FontFamily).LineSpacing * size * (Math.Abs(text.LineSpacing - 1) > 0.001 ? text.LineSpacing : 1));
        FormattedText Measure(string s) => new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, Brushes.Black, 1.0);

        // Where each letter starts along its line (the font's own spacing plus the letter and word gaps), and how long every line is.
        var starts = new List<double[]>();
        var widths = new double[lines.Length];
        for (int l = 0; l < lines.Length; l++)
        {
            string line = lines[l];
            var x = new double[line.Length];
            int spaces = 0;
            for (int i = 0; i < line.Length; i++)
            {
                x[i] = (i == 0 ? 0 : Measure(line[..i]).WidthIncludingTrailingWhitespace) + i * letterGap + spaces * wordGap;
                if (line[i] == ' ') spaces++;
            }
            starts.Add(x);
            widths[l] = line.Length == 0 ? 0 : x[^1] + Measure(line[^1].ToString()).WidthIncludingTrailingWhitespace;
        }
        double total = Math.Max(1, widths.Max());
        double arc = Math.Clamp(text.Arc, -360, 360) * Math.PI / 180;
        double radius = total / Math.Max(Math.Abs(arc), 1e-6);          // the longest line takes the whole arc
        bool up = arc > 0;

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        for (int l = 0; l < lines.Length; l++)
        {
            // The next line is on a smaller circle for a rainbow (it is closer to the middle) and a bigger one for a smile.
            double lineRadius = up ? Math.Max(1, radius - l * lineHeight) : radius + l * lineHeight;
            double offset = text.Align switch
            {
                TextAlign.Left => 0,
                TextAlign.Right => total - widths[l],
                _ => (total - widths[l]) / 2,
            };
            for (int i = 0; i < lines[l].Length; i++)
            {
                if (char.IsWhiteSpace(lines[l][i])) continue;
                var glyph = Measure(lines[l][i].ToString());
                double centerX = offset + starts[l][i] + glyph.WidthIncludingTrailingWhitespace / 2;
                double phi = (centerX - total / 2) / lineRadius;                // angle from the middle of the arc

                // The letter, drawn with its baseline middle at the origin and its top pointing up.
                var shape = glyph.BuildGeometry(new Point(-glyph.WidthIncludingTrailingWhitespace / 2, -glyph.Baseline)).Clone();
                var place = new TransformGroup();
                if (up)
                {
                    place.Children.Add(new RotateTransform(phi * 180 / Math.PI));
                    place.Children.Add(new TranslateTransform(lineRadius * Math.Sin(phi), -lineRadius * Math.Cos(phi) + radius));
                }
                else
                {
                    place.Children.Add(new RotateTransform(-phi * 180 / Math.PI));
                    place.Children.Add(new TranslateTransform(lineRadius * Math.Sin(phi), lineRadius * Math.Cos(phi) - radius));
                }
                shape.Transform = place;
                group.Children.Add(shape);
            }
        }
        group.Freeze();
        return group;
    }

    /// <summary>How big the curved text is (the bounds of its letters, with a little room around).</summary>
    public static (double Width, double Height) Measure(TextObject text)
    {
        var bounds = Build(text).Bounds;
        if (bounds.IsEmpty) return (Math.Max(1, text.FontSize), Math.Max(1, text.FontSize));
        return (Math.Ceiling(bounds.Width) + 2 + text.OutlineWidth * 2, Math.Ceiling(bounds.Height) + 2 + text.OutlineWidth * 2);
    }
}
