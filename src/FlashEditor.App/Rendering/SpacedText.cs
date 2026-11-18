using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Rendering;

/// <summary>
/// Text with extra space between the letters and / or the words. The normal text drawing cannot do that, so every letter is placed by hand
/// (the lines, alignment and line spacing work as for normal text). Only used when one of the two spacings is not zero.
/// </summary>
internal static class SpacedText
{
    public static bool Applies(TextObject text) => Math.Abs(text.LetterSpacing) > 0.0005 || Math.Abs(text.WordSpacing) > 0.0005;

    /// <summary>The outline of all letters, and the size the text takes (with the same little margin as normal text).</summary>
    public static (Geometry Geometry, double Width, double Height) Build(TextObject text)
    {
        var typeface = new Typeface(new FontFamily(text.FontFamily),
            text.Italic ? FontStyles.Italic : FontStyles.Normal,
            text.Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);
        double size = Math.Max(1, text.FontSize);
        double letterGap = text.LetterSpacing * size, wordGap = text.WordSpacing * size;
        double lineHeight = Math.Max(1, new FontFamily(text.FontFamily).LineSpacing * size * (Math.Abs(text.LineSpacing - 1) > 0.001 ? text.LineSpacing : 1));
        FormattedText Measure(string s) => new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, Brushes.Black, 1.0);

        var lines = (string.IsNullOrEmpty(text.Text) ? " " : text.Text.Replace("\r", "")).Split('\n');
        var widths = new double[lines.Length];
        var starts = new List<double[]>();
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
            double last = line.Length == 0 ? 0 : Measure(line[^1].ToString()).WidthIncludingTrailingWhitespace;
            widths[l] = line.Length == 0 ? 0 : Math.Max(1, x[^1] + last);
        }
        double maxWidth = Math.Max(1, widths.Max());

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        for (int l = 0; l < lines.Length; l++)
        {
            double offset = text.Align switch
            {
                TextAlign.Center => (maxWidth - widths[l]) / 2,
                TextAlign.Right => maxWidth - widths[l],
                _ => 0,
            };
            for (int i = 0; i < lines[l].Length; i++)
            {
                if (char.IsWhiteSpace(lines[l][i])) continue;
                var glyph = Measure(lines[l][i].ToString());
                var shape = glyph.BuildGeometry(new Point(offset + starts[l][i], l * lineHeight));
                if (shape != null && !shape.IsEmpty()) group.Children.Add(shape);
            }
        }
        group.Freeze();
        return (group, Math.Ceiling(maxWidth) + 2, Math.Ceiling(lineHeight * lines.Length) + 2);
    }
}
