using System.Globalization;
using System.Security;
using System.Text;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.IO.Pptx;

/// <summary>
/// One slide being built: a list of shapes, text boxes and pictures in stacking order (first = at the back). All positions and sizes are in
/// pixels of the page (96 per inch); the writer turns them into the units PowerPoint uses.
/// </summary>
public sealed class PptxSlide
{
    private readonly StringBuilder _xml = new();
    private int _nextId = 2;

    /// <summary>The pictures used by this slide (the first one is relationship rId2).</summary>
    public List<(byte[] Data, bool Png)> Media { get; } = new();

    private const double Emu = 9525;

    private static string E(double px) => ((long)Math.Round(px * Emu)).ToString(CultureInfo.InvariantCulture);

    /// <summary>Text for an XML attribute or element: escaped, without characters XML does not allow.</summary>
    public static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
            if (c == '\t' || c == '\n' || c == '\r' || (c >= 0x20 && c != 0xFFFE && c != 0xFFFF && !char.IsSurrogate(c))) sb.Append(c);
            else if (char.IsSurrogate(c)) sb.Append(c);
        return SecurityElement.Escape(sb.ToString()) ?? "";
    }

    /// <summary>The scale is applied by <see cref="ToXml"/> (slides that are too big for PowerPoint are shrunk).</summary>
    internal string ToXml(double scale)
    {
        string xml = _xml.ToString();
        // Offsets and sizes were written as plain EMU numbers: scale them with a marker.
        return System.Text.RegularExpressions.Regex.Replace(xml, "\\{\\{(-?\\d+)\\}\\}", m => ((long)Math.Round(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * scale)).ToString(CultureInfo.InvariantCulture));
    }

    private static string Mark(double px) => "{{" + E(px) + "}}";

    private string Xfrm(double x, double y, double w, double h, double rotation, bool flipX, bool flipY)
    {
        // The box is given unrotated, top-left at (x, y): PowerPoint also turns shapes around their center.
        long rot = (long)Math.Round(((rotation % 360) + 360) % 360 * 60000);
        string attrs = (rot != 0 ? $" rot=\"{rot}\"" : "") + (flipX ? " flipH=\"1\"" : "") + (flipY ? " flipV=\"1\"" : "");
        return $"<a:xfrm{attrs}><a:off x=\"{Mark(x)}\" y=\"{Mark(y)}\"/><a:ext cx=\"{Mark(Math.Max(0, w))}\" cy=\"{Mark(Math.Max(0, h))}\"/></a:xfrm>";
    }

    private static string Color(ColorRgba c, double opacity = 1)
    {
        double alpha = c.A / 255.0 * Math.Clamp(opacity, 0, 1);
        string a = alpha >= 0.999 ? "" : $"<a:alpha val=\"{(int)Math.Round(alpha * 100000)}\"/>";
        return $"<a:srgbClr val=\"{c.R:X2}{c.G:X2}{c.B:X2}\">{a}</a:srgbClr>";
    }

    // ------------------------------------------------------------------ pictures

    public void AddPicture(byte[] data, bool png, string name, double x, double y, double w, double h,
        double rotation = 0, bool flipX = false, bool flipY = false, double opacity = 1)
    {
        Media.Add((data, png));
        string rel = "rId" + (Media.Count + 1);
        string alpha = opacity >= 0.999 ? "" : $"<a:alphaModFix amt=\"{(int)Math.Round(Math.Clamp(opacity, 0, 1) * 100000)}\"/>";
        int id = _nextId++;
        _xml.Append($"<p:pic><p:nvPicPr><p:cNvPr id=\"{id}\" name=\"{Escape(name)}\" descr=\"{Escape(name)}\"/><p:cNvPicPr><a:picLocks noChangeAspect=\"1\"/></p:cNvPicPr><p:nvPr/></p:nvPicPr>"
            + $"<p:blipFill><a:blip r:embed=\"{rel}\">{alpha}</a:blip><a:stretch><a:fillRect/></a:stretch></p:blipFill>"
            + $"<p:spPr>{Xfrm(x, y, w, h, rotation, flipX, flipY)}<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom></p:spPr></p:pic>");
    }

    // ------------------------------------------------------------------ shapes

    /// <param name="preset">The PowerPoint shape name (rect, ellipse, star5, ...).</param>
    /// <param name="adjust">Optional shape settings (name, value), e.g. ("adj", 16667) for rounded corners.</param>
    public void AddShape(string preset, string name, double x, double y, double w, double h, ColorRgba? fill, ColorRgba? stroke, double strokeWidth,
        double rotation = 0, bool flipX = false, bool flipY = false, double opacity = 1, string dash = "solid",
        bool arrowStart = false, bool arrowEnd = false, IReadOnlyList<(string Name, int Value)>? adjust = null)
    {
        int id = _nextId++;
        string gd = adjust is { Count: > 0 } ? string.Concat(adjust.Select(g => $"<a:gd name=\"{g.Name}\" fmla=\"val {g.Value}\"/>")) : "";
        string fillXml = fill is { } f ? $"<a:solidFill>{Color(f, opacity)}</a:solidFill>" : "<a:noFill/>";
        string line;
        if (stroke is { } s && strokeWidth > 0)
        {
            string dashXml = dash == "solid" ? "" : $"<a:prstDash val=\"{dash}\"/>";
            string heads = (arrowStart ? "<a:headEnd type=\"triangle\"/>" : "") + (arrowEnd ? "<a:tailEnd type=\"triangle\"/>" : "");
            line = $"<a:ln w=\"{E(strokeWidth)}\"><a:solidFill>{Color(s, opacity)}</a:solidFill>{dashXml}{heads}</a:ln>";
        }
        else line = "<a:ln><a:noFill/></a:ln>";
        _xml.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"{Escape(name)}\"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>"
            + $"<p:spPr>{Xfrm(x, y, w, h, rotation, flipX, flipY)}<a:prstGeom prst=\"{preset}\"><a:avLst>{gd}</a:avLst></a:prstGeom>{fillXml}{line}</p:spPr>"
            + "<p:txBody><a:bodyPr/><a:lstStyle/><a:p><a:endParaRPr lang=\"en-US\"/></a:p></p:txBody></p:sp>");
    }

    // ------------------------------------------------------------------ text

    /// <param name="fontSizePx">Font size in pixels (96 per inch); PowerPoint wants points.</param>
    /// <param name="align">"l", "ctr" or "r".</param>
    /// <param name="lineSpacing">1 = normal.</param>
    /// <param name="letterSpacingPx">Extra space between letters in pixels.</param>
    public void AddText(string text, string name, double x, double y, double w, double h, string fontFamily, double fontSizePx, ColorRgba color,
        bool bold, bool italic, string align, double rotation = 0, double opacity = 1, double lineSpacing = 1, double letterSpacingPx = 0)
    {
        int id = _nextId++;
        int size = Math.Max(100, (int)Math.Round(fontSizePx * 0.75 * 100));
        string spacing = Math.Abs(lineSpacing - 1) < 0.01 ? "" : $"<a:lnSpc><a:spcPct val=\"{(int)Math.Round(lineSpacing * 100000)}\"/></a:lnSpc>";
        string spc = Math.Abs(letterSpacingPx) < 0.01 ? "" : $" spc=\"{(int)Math.Round(letterSpacingPx * 75)}\"";
        string font = Escape(fontFamily);
        var paragraphs = new StringBuilder();
        foreach (string line in text.Replace("\r", "").Split('\n'))
        {
            string run = line.Length == 0
                ? $"<a:endParaRPr lang=\"en-US\" sz=\"{size}\"/>"
                : $"<a:r><a:rPr lang=\"en-US\" sz=\"{size}\" b=\"{(bold ? 1 : 0)}\" i=\"{(italic ? 1 : 0)}\"{spc} dirty=\"0\"><a:solidFill>{Color(color, opacity)}</a:solidFill><a:latin typeface=\"{font}\"/><a:cs typeface=\"{font}\"/></a:rPr><a:t>{Escape(line)}</a:t></a:r>";
            paragraphs.Append($"<a:p><a:pPr algn=\"{align}\">{spacing}</a:pPr>{run}</a:p>");
        }
        _xml.Append($"<p:sp><p:nvSpPr><p:cNvPr id=\"{id}\" name=\"{Escape(name)}\"/><p:cNvSpPr txBox=\"1\"/><p:nvPr/></p:nvSpPr>"
            + $"<p:spPr>{Xfrm(x, y, w, h, rotation, false, false)}<a:prstGeom prst=\"rect\"><a:avLst/></a:prstGeom><a:noFill/></p:spPr>"
            + $"<p:txBody><a:bodyPr wrap=\"none\" lIns=\"0\" tIns=\"0\" rIns=\"0\" bIns=\"0\" rtlCol=\"0\" anchor=\"t\"><a:noAutofit/></a:bodyPr><a:lstStyle/>{paragraphs}</p:txBody></p:sp>");
    }
}
