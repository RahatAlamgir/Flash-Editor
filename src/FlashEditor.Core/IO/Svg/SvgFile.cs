using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.IO.Svg;

/// <summary>
/// SVG (vector graphics) files. Saving writes shapes and text as real vector elements (they stay sharp at any size) and everything else as
/// embedded pictures. Opening understands the common simple elements: rectangles, circles, ellipses, lines, polygons, paths (curves are
/// flattened), text and embedded pictures, with fill, stroke and opacity. Gradients, filters, clipping and fonts that are not installed are
/// not supported.
/// </summary>
public static class SvgFile
{
    public const string Extension = ".svg";

    public static bool IsSvg(string path) => path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

    private static string F(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);

    private static string Hex(ColorRgba c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    // =============================================================== saving

    public static string Export(Document document)
    {
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{document.Width}\" height=\"{document.Height}\" viewBox=\"0 0 {document.Width} {document.Height}\">\n");
        foreach (var layer in document.Layers)
        {
            if (!layer.IsVisible || layer.IsAdjustment || layer.Opacity <= 0) continue;
            string common = Common(layer);
            var element = layer.Mask == null && layer.Object is { } obj ? ObjectElement(obj, common) : null;
            sb.Append(element ?? PictureElement(layer, common));
            sb.Append('\n');
        }
        sb.Append("</svg>\n");
        return sb.ToString();
    }

    public static void Save(Document document, string path) => File.WriteAllText(path, Export(document), new UTF8Encoding(false));

    private static string Common(Layer layer)
    {
        var sb = new StringBuilder();
        if (layer.Opacity < 0.999) sb.Append($" opacity=\"{F(layer.Opacity)}\"");
        if (layer.BlendMode != BlendMode.Normal) sb.Append($" style=\"mix-blend-mode:{BlendCss(layer.BlendMode)}\"");
        sb.Append($" id=\"{Escape(layer.Name)}\"");
        return sb.ToString();
    }

    private static string BlendCss(BlendMode mode) => mode switch
    {
        BlendMode.SoftLight => "soft-light",
        BlendMode.HardLight => "hard-light",
        _ => mode.ToString().ToLowerInvariant(),
    };

    private static string Escape(string s) => System.Security.SecurityElement.Escape(s) ?? "";

    private static string Paint(string attribute, ColorRgba? color)
        => color is { A: > 0 } c ? $" {attribute}=\"{Hex(c)}\"" + (c.A < 255 ? $" {attribute}-opacity=\"{F(c.A / 255.0)}\"" : "") : $" {attribute}=\"none\"";

    private static string Transform(ObjectBox b, bool flipX, bool flipY)
    {
        var c = b.Center;
        var parts = new List<string>();
        if (Math.Abs(b.Rotation) > 0.001) parts.Add($"rotate({F(b.Rotation)} {F(c.X)} {F(c.Y)})");
        if (flipX || flipY) parts.Add($"translate({F(c.X)} {F(c.Y)}) scale({(flipX ? -1 : 1)} {(flipY ? -1 : 1)}) translate({F(-c.X)} {F(-c.Y)})");
        return parts.Count == 0 ? "" : $" transform=\"{string.Join(" ", parts)}\"";
    }

    /// <summary>A vector element for a plain shape or text; null when the object has effects or a form that is easier to keep as a picture.</summary>
    private static string? ObjectElement(LayerObject obj, string common)
    {
        if (obj.Shadow != null || obj.ThreeD != null || obj.Glow != null || obj.InnerShadow != null || obj.Outline != null || obj.Warp != null || obj.Gradient != null) return null;
        var b = obj.Box;
        switch (obj)
        {
            case ShapeObject { Vertices: null, Shape: ShapeKind.Rectangle } s:
            {
                string corner = s.CornerRadius > 0 ? $" rx=\"{F(Math.Min(s.CornerRadius, Math.Min(b.Width, b.Height) / 2))}\"" : "";
                return $"<rect x=\"{F(b.X)}\" y=\"{F(b.Y)}\" width=\"{F(b.Width)}\" height=\"{F(b.Height)}\"{corner}{Paint("fill", s.Fill)}{StrokeOf(s)}{Transform(b, s.FlipX, s.FlipY)}{common}/>";
            }
            case ShapeObject { Vertices: null, Shape: ShapeKind.Ellipse } s:
                return $"<ellipse cx=\"{F(b.Center.X)}\" cy=\"{F(b.Center.Y)}\" rx=\"{F(b.Width / 2)}\" ry=\"{F(b.Height / 2)}\"{Paint("fill", s.Fill)}{StrokeOf(s)}{Transform(b, s.FlipX, s.FlipY)}{common}/>";
            case ShapeObject s when s.CurrentVertices is { Count: >= 3 } vertices && ShapeVertices.IsPolygonal(s.Shape):
            {
                string points = string.Join(" ", vertices.Select(v => $"{F(b.X + v.X * b.Width)},{F(b.Y + v.Y * b.Height)}"));
                return $"<polygon points=\"{points}\"{Paint("fill", s.Fill)}{StrokeOf(s)}{Transform(b, s.FlipX, s.FlipY)}{common}/>";
            }
            case TextObject t:
            {
                var lines = t.Text.Replace("\r", "").Split('\n');
                double lineHeight = t.FontSize * 1.25 * t.LineSpacing;
                string anchor = t.Align switch { TextAlign.Center => "middle", TextAlign.Right => "end", _ => "start" };
                double x = t.Align switch { TextAlign.Center => b.X + b.Width / 2, TextAlign.Right => b.X + b.Width, _ => b.X };
                var sb = new StringBuilder();
                sb.Append($"<text font-family=\"{Escape(t.FontFamily)}\" font-size=\"{F(t.FontSize)}\" text-anchor=\"{anchor}\"{Paint("fill", t.Color)}");
                if (t.Bold) sb.Append(" font-weight=\"bold\"");
                if (t.Italic) sb.Append(" font-style=\"italic\"");
                if (t.OutlineWidth > 0 && t.OutlineColor is { } oc) sb.Append($" stroke=\"{Hex(oc)}\" stroke-width=\"{F(t.OutlineWidth * 2)}\" paint-order=\"stroke\" stroke-linejoin=\"round\"");
                sb.Append($"{Transform(b, t.FlipX, t.FlipY)}{common}>");
                for (int i = 0; i < lines.Length; i++)
                    sb.Append($"<tspan x=\"{F(x)}\" y=\"{F(b.Y + t.FontSize * 0.95 + i * lineHeight)}\">{Escape(lines[i])}</tspan>");
                sb.Append("</text>");
                return t.Arc != 0 ? null : sb.ToString();       // curved text is kept as a picture
            }
            case ImageObject image:
            {
                string data = Convert.ToBase64String(ImageEncoder.EncodePng(image.Source));
                return $"<image x=\"{F(b.X)}\" y=\"{F(b.Y)}\" width=\"{F(b.Width)}\" height=\"{F(b.Height)}\" preserveAspectRatio=\"none\" xlink:href=\"data:image/png;base64,{data}\"{Transform(b, image.FlipX, image.FlipY)}{common}/>";
            }
            default:
                return null;
        }
    }

    private static string StrokeOf(ShapeObject s)
        => s.Stroke is { A: > 0 } stroke && s.StrokeWidth > 0 ? $" stroke=\"{Hex(stroke)}\" stroke-width=\"{F(s.StrokeWidth)}\"" : "";

    /// <summary>Any layer as a picture (cropped to what is visible, with its mask applied).</summary>
    private static string PictureElement(Layer layer, string common)
    {
        var pixels = layer.MaskedPixels();
        var bounds = pixels.VisibleBounds();
        if (bounds.IsEmpty) return $"<g{common}/>";
        string data = Convert.ToBase64String(ImageEncoder.EncodePng(pixels.Crop(bounds)));
        return $"<image x=\"{bounds.X}\" y=\"{bounds.Y}\" width=\"{bounds.Width}\" height=\"{bounds.Height}\" xlink:href=\"data:image/png;base64,{data}\"{common}/>";
    }

    // =============================================================== opening

    /// <summary>Opens an .svg file as a document: every element becomes a layer holding an editable object.</summary>
    public static Document Open(string path) => Import(File.ReadAllText(path));

    public static Document Import(string svg)
    {
        var (width, height, objects) = Parse(svg);
        var document = new Document(width, height);
        var layers = new List<Layer>();
        var background = new Layer("Background", width, height);
        layers.Add(background);
        int number = 0;
        foreach (var (name, obj) in objects)
        {
            var layer = new Layer(name.Length > 0 ? name : $"{obj.KindName} {++number}", width, height);
            layer.SetObject(obj);
            layers.Add(layer);
        }
        document.SetLayers(layers, layers[^1]);
        return document;
    }

    /// <summary>The size of the picture and its elements as editable objects (bottom to top), without drawing them.</summary>
    public static (int Width, int Height, List<(string Name, LayerObject Object)> Objects) Parse(string svg)
    {
        var root = XDocument.Parse(svg).Root ?? throw new InvalidDataException("This is not an SVG picture.");
        if (root.Name.LocalName != "svg") throw new InvalidDataException("This is not an SVG picture.");

        double vbX = 0, vbY = 0, vbW = 0, vbH = 0;
        if (root.Attribute("viewBox")?.Value is { } vb)
        {
            var parts = vb.Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(P).ToArray();
            if (parts.Length == 4) (vbX, vbY, vbW, vbH) = (parts[0], parts[1], parts[2], parts[3]);
        }
        double w = Length(root.Attribute("width")?.Value), h = Length(root.Attribute("height")?.Value);
        if (w <= 0) w = vbW > 0 ? vbW : 800;
        if (h <= 0) h = vbH > 0 ? vbH : vbH > 0 ? vbH : 600;
        double scale = vbW > 0 ? w / vbW : 1;
        int width = (int)Math.Clamp(Math.Round(w), 1, 16000), height = (int)Math.Clamp(Math.Round(h), 1, 16000);

        var result = new List<(string, LayerObject)>();
        var inherit = new Style(ColorRgba.Black, null, 1, 1, 1);
        Walk(root, new Frame(scale, -vbX * scale, -vbY * scale), inherit, result);
        return (width, height, result);
    }

    private readonly record struct Frame(double Scale, double DX, double DY)
    {
        public double X(double v) => v * Scale + DX;
        public double Y(double v) => v * Scale + DY;
    }

    private readonly record struct Style(ColorRgba? Fill, ColorRgba? Stroke, double StrokeWidth, double Opacity, double _unused);

    private static void Walk(XElement parent, Frame frame, Style inherited, List<(string, LayerObject)> result)
    {
        foreach (var e in parent.Elements())
        {
            var style = StyleOf(e, inherited);
            string name = e.Attribute("id")?.Value ?? "";
            switch (e.Name.LocalName)
            {
                case "g":
                case "svg":
                    Walk(e, Group(e, frame), style, result);
                    break;
                case "rect":
                {
                    double x = frame.X(P(e, "x")), y = frame.Y(P(e, "y")), w = P(e, "width") * frame.Scale, h = P(e, "height") * frame.Scale;
                    if (w <= 0 || h <= 0) break;
                    double rx = Math.Max(P(e, "rx"), P(e, "ry")) * frame.Scale;
                    result.Add((name, New(new ShapeObject(new ObjectBox(x, y, w, h), ShapeKind.Rectangle, style.Fill, style.Stroke, style.StrokeWidth * frame.Scale, CornerRadius: rx), style)));
                    break;
                }
                case "circle":
                case "ellipse":
                {
                    double rx = (e.Name.LocalName == "circle" ? P(e, "r") : P(e, "rx")) * frame.Scale;
                    double ry = (e.Name.LocalName == "circle" ? P(e, "r") : P(e, "ry")) * frame.Scale;
                    if (rx <= 0 || ry <= 0) break;
                    double cx = frame.X(P(e, "cx")), cy = frame.Y(P(e, "cy"));
                    result.Add((name, New(new ShapeObject(new ObjectBox(cx - rx, cy - ry, rx * 2, ry * 2), ShapeKind.Ellipse, style.Fill, style.Stroke, style.StrokeWidth * frame.Scale), style)));
                    break;
                }
                case "line":
                {
                    var points = new List<PointD> { new(frame.X(P(e, "x1")), frame.Y(P(e, "y1"))), new(frame.X(P(e, "x2")), frame.Y(P(e, "y2"))) };
                    AddPolygon(result, name, points, style with { Fill = null }, frame, closed: false);
                    break;
                }
                case "polygon":
                case "polyline":
                {
                    var numbers = (e.Attribute("points")?.Value ?? "").Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(P).ToArray();
                    var points = new List<PointD>();
                    for (int i = 0; i + 1 < numbers.Length; i += 2) points.Add(new PointD(frame.X(numbers[i]), frame.Y(numbers[i + 1])));
                    bool closed = e.Name.LocalName == "polygon";
                    AddPolygon(result, name, points, closed ? style : style with { Fill = null }, frame, closed);
                    break;
                }
                case "path":
                    foreach (var (points, closed) in FlattenPath(e.Attribute("d")?.Value ?? "", frame))
                        AddPolygon(result, name, points, closed ? style : style with { Fill = null }, frame, closed);
                    break;
                case "text":
                {
                    string text = string.Concat(e.Nodes().Select(n => n is XText t ? t.Value : n is XElement c ? c.Value : "")).Trim();
                    if (text.Length == 0) break;
                    double size = Math.Max(1, Length(e.Attribute("font-size")?.Value ?? StyleAttribute(e, "font-size"), 16) * frame.Scale);
                    string family = (e.Attribute("font-family")?.Value ?? StyleAttribute(e, "font-family") ?? "Segoe UI").Split(',')[0].Trim().Trim('\'', '"');
                    string anchor = e.Attribute("text-anchor")?.Value ?? "start";
                    double estimated = Math.Max(size, text.Length * size * 0.55), height = size * 1.35;
                    double x = frame.X(P(e, "x")), y = frame.Y(P(e, "y")) - size * 0.95;
                    double left = anchor == "middle" ? x - estimated / 2 : anchor == "end" ? x - estimated : x;
                    var align = anchor == "middle" ? TextAlign.Center : anchor == "end" ? TextAlign.Right : TextAlign.Left;
                    bool bold = (e.Attribute("font-weight")?.Value ?? StyleAttribute(e, "font-weight")) is "bold" or "700" or "800" or "900";
                    bool italic = (e.Attribute("font-style")?.Value ?? StyleAttribute(e, "font-style")) == "italic";
                    result.Add((name, New(new TextObject(new ObjectBox(left, y, estimated, height), text, family, size, style.Fill ?? ColorRgba.Black, bold, italic, align), style)));
                    break;
                }
                case "image":
                {
                    string href = e.Attributes().FirstOrDefault(a => a.Name.LocalName == "href")?.Value ?? "";
                    int comma = href.IndexOf(',');
                    if (!href.StartsWith("data:image", StringComparison.OrdinalIgnoreCase) || comma < 0 || !href[..comma].Contains("base64")) break;
                    try
                    {
                        using var stream = new MemoryStream(Convert.FromBase64String(href[(comma + 1)..]));
                        var picture = ImageDecoder.Load(stream, href.Contains("jpeg") || href.Contains("jpg") ? ".jpg" : ".png");
                        double w = P(e, "width") * frame.Scale, h = P(e, "height") * frame.Scale;
                        if (w <= 0) w = picture.Width * frame.Scale;
                        if (h <= 0) h = picture.Height * frame.Scale;
                        result.Add((name, New(new ImageObject(new ObjectBox(frame.X(P(e, "x")), frame.Y(P(e, "y")), w, h), picture, name.Length > 0 ? name : "Image"), style)));
                    }
                    catch (Exception) { /* a picture that cannot be read is skipped */ }
                    break;
                }
            }
        }
    }

    private static LayerObject New(LayerObject obj, Style style) => obj;

    private static void AddPolygon(List<(string, LayerObject)> result, string name, List<PointD> points, Style style, Frame frame, bool closed)
    {
        if (points.Count < 2) return;
        double minX = points.Min(p => p.X), minY = points.Min(p => p.Y), maxX = points.Max(p => p.X), maxY = points.Max(p => p.Y);
        double w = Math.Max(1, maxX - minX), h = Math.Max(1, maxY - minY);
        var unit = points.Select(p => new PointD((p.X - minX) / w, (p.Y - minY) / h)).ToList();
        result.Add((name, new ShapeObject(new ObjectBox(minX, minY, w, h), ShapeKind.Polygon, closed ? style.Fill : null, style.Stroke ?? (closed ? null : style.Fill ?? ColorRgba.Black),
            style.StrokeWidth * frame.Scale, Vertices: unit)));
    }

    private static Frame Group(XElement g, Frame frame)
    {
        string? t = g.Attribute("transform")?.Value;
        if (t == null) return frame;
        double dx = 0, dy = 0, scale = 1;
        foreach (var part in t.Split(')', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('(');
            if (pieces.Length != 2) continue;
            var args = pieces[1].Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(P).ToArray();
            string kind = pieces[0].Trim();
            if (kind == "translate" && args.Length >= 1) { dx += args[0]; dy += args.Length > 1 ? args[1] : 0; }
            else if (kind == "scale" && args.Length >= 1) scale *= args[0];
        }
        return new Frame(frame.Scale * scale, frame.DX + dx * frame.Scale, frame.DY + dy * frame.Scale);
    }

    // ---- paint
    private static string? StyleAttribute(XElement e, string name)
    {
        var style = e.Attribute("style")?.Value;
        if (style == null) return null;
        foreach (var declaration in style.Split(';'))
        {
            var kv = declaration.Split(':', 2);
            if (kv.Length == 2 && kv[0].Trim() == name) return kv[1].Trim();
        }
        return null;
    }

    private static Style StyleOf(XElement e, Style inherited)
    {
        string? Get(string n) => e.Attribute(n)?.Value ?? StyleAttribute(e, n);
        var fill = inherited.Fill;
        var stroke = inherited.Stroke;
        double opacity = inherited.Opacity;
        if (Get("opacity") is { } o) opacity *= Math.Clamp(P(o), 0, 1);
        double fillOpacity = Get("fill-opacity") is { } fo ? Math.Clamp(P(fo), 0, 1) : 1;
        double strokeOpacity = Get("stroke-opacity") is { } so ? Math.Clamp(P(so), 0, 1) : 1;
        if (Get("fill") is { } f) fill = Color(f);
        if (Get("stroke") is { } s) stroke = Color(s);
        double sw = Get("stroke-width") is { } w ? Math.Max(0, P(w)) : inherited.StrokeWidth;
        if (fill is { } fc) fill = fc with { A = (byte)Math.Round(fc.A * opacity * fillOpacity) };
        if (stroke is { } sc) stroke = sc with { A = (byte)Math.Round(sc.A * opacity * strokeOpacity) };
        return new Style(fill, stroke, sw, inherited.Opacity * 1, 1);
    }

    private static readonly Dictionary<string, ColorRgba> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = new(0, 0, 0), ["white"] = new(255, 255, 255), ["red"] = new(255, 0, 0), ["green"] = new(0, 128, 0), ["blue"] = new(0, 0, 255),
        ["yellow"] = new(255, 255, 0), ["orange"] = new(255, 165, 0), ["purple"] = new(128, 0, 128), ["gray"] = new(128, 128, 128), ["grey"] = new(128, 128, 128),
        ["pink"] = new(255, 192, 203), ["brown"] = new(165, 42, 42), ["cyan"] = new(0, 255, 255), ["magenta"] = new(255, 0, 255), ["lime"] = new(0, 255, 0),
        ["navy"] = new(0, 0, 128), ["teal"] = new(0, 128, 128), ["silver"] = new(192, 192, 192), ["gold"] = new(255, 215, 0),
    };

    /// <summary>A color as written in SVG, or null for "none" (and for things that cannot be shown, like gradients).</summary>
    private static ColorRgba? Color(string value)
    {
        value = value.Trim();
        if (value is "none" or "transparent" || value.StartsWith("url(", StringComparison.Ordinal)) return null;
        if (value.StartsWith('#'))
        {
            string hex = value[1..];
            if (hex.Length == 3) hex = string.Concat(hex.Select(c => $"{c}{c}"));
            if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                return new ColorRgba((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return null;
        }
        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var numbers = value[(value.IndexOf('(') + 1)..].TrimEnd(')').Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.TrimEnd('%')).Select(P).ToArray();
            if (numbers.Length >= 3) return new ColorRgba((byte)Math.Clamp(numbers[0], 0, 255), (byte)Math.Clamp(numbers[1], 0, 255), (byte)Math.Clamp(numbers[2], 0, 255));
            return null;
        }
        return Named.TryGetValue(value, out var named) ? named : null;
    }

    private static double P(string s) => double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    private static double P(XElement e, string attribute) => e.Attribute(attribute)?.Value is { } v ? Length(v) : 0;

    /// <summary>A length such as "120", "120px" or "12.5pt" in pixels (percent and other units count as plain numbers).</summary>
    private static double Length(string? value, double fallback = 0)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        value = value.Trim();
        double factor = 1;
        if (value.EndsWith("pt", StringComparison.Ordinal)) { factor = 96.0 / 72; value = value[..^2]; }
        else if (value.EndsWith("px", StringComparison.Ordinal)) value = value[..^2];
        else if (value.EndsWith("mm", StringComparison.Ordinal)) { factor = 96 / 25.4; value = value[..^2]; }
        else if (value.EndsWith("cm", StringComparison.Ordinal)) { factor = 96 / 2.54; value = value[..^2]; }
        else if (value.EndsWith("in", StringComparison.Ordinal)) { factor = 96; value = value[..^2]; }
        else if (value.EndsWith('%')) return fallback;
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v * factor : fallback;
    }

    // ---- paths: lines and curves flattened into points
    private static IEnumerable<(List<PointD> Points, bool Closed)> FlattenPath(string d, Frame frame)
    {
        var tokens = new List<string>();
        var number = new StringBuilder();
        void Flush() { if (number.Length > 0) { tokens.Add(number.ToString()); number.Clear(); } }
        foreach (char c in d)
        {
            if (char.IsLetter(c) && c != 'e' && c != 'E') { Flush(); tokens.Add(c.ToString()); }
            else if (c is ' ' or ',' or '\t' or '\n' or '\r') Flush();
            else if (c == '-' && number.Length > 0 && number[^1] != 'e' && number[^1] != 'E') { Flush(); number.Append(c); }
            else if (c == '.' && number.ToString().Contains('.')) { Flush(); number.Append(c); }
            else number.Append(c);
        }
        Flush();

        var current = new List<PointD>();
        double x = 0, y = 0, startX = 0, startY = 0, lastCx = 0, lastCy = 0;
        char command = 'M';
        int i = 0;
        double Next() => i < tokens.Count ? P(tokens[i++]) : 0;
        bool HasNumber() => i < tokens.Count && !(tokens[i].Length == 1 && char.IsLetter(tokens[i][0]));
        void Add(double px, double py) => current.Add(new PointD(frame.X(px), frame.Y(py)));
        var finished = new List<(List<PointD>, bool)>();

        while (i < tokens.Count)
        {
            if (tokens[i].Length == 1 && char.IsLetter(tokens[i][0])) command = tokens[i++][0];
            else if (command is 'M') command = 'L';
            else if (command is 'm') command = 'l';
            bool rel = char.IsLower(command);
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    if (current.Count > 1) finished.Add((current, false));
                    current = new List<PointD>();
                    x = Next() + (rel ? x : 0); y = Next() + (rel ? y : 0); startX = x; startY = y; Add(x, y);
                    break;
                case 'L': x = Next() + (rel ? x : 0); y = Next() + (rel ? y : 0); Add(x, y); break;
                case 'H': x = Next() + (rel ? x : 0); Add(x, y); break;
                case 'V': y = Next() + (rel ? y : 0); Add(x, y); break;
                case 'C':
                {
                    double x1 = Next() + (rel ? x : 0), y1 = Next() + (rel ? y : 0), x2 = Next() + (rel ? x : 0), y2 = Next() + (rel ? y : 0), x3 = Next() + (rel ? x : 0), y3 = Next() + (rel ? y : 0);
                    Bezier(x, y, x1, y1, x2, y2, x3, y3, Add);
                    lastCx = x2; lastCy = y2; x = x3; y = y3;
                    break;
                }
                case 'S':
                {
                    double x2 = Next() + (rel ? x : 0), y2 = Next() + (rel ? y : 0), x3 = Next() + (rel ? x : 0), y3 = Next() + (rel ? y : 0);
                    Bezier(x, y, 2 * x - lastCx, 2 * y - lastCy, x2, y2, x3, y3, Add);
                    lastCx = x2; lastCy = y2; x = x3; y = y3;
                    break;
                }
                case 'Q':
                {
                    double x1 = Next() + (rel ? x : 0), y1 = Next() + (rel ? y : 0), x2 = Next() + (rel ? x : 0), y2 = Next() + (rel ? y : 0);
                    Bezier(x, y, x + 2.0 / 3 * (x1 - x), y + 2.0 / 3 * (y1 - y), x2 + 2.0 / 3 * (x1 - x2), y2 + 2.0 / 3 * (y1 - y2), x2, y2, Add);
                    lastCx = x1; lastCy = y1; x = x2; y = y2;
                    break;
                }
                case 'A':
                    Next(); Next(); Next(); Next(); Next();                      // radii, rotation, flags: arcs are drawn as straight lines to their end
                    x = Next() + (rel ? x : 0); y = Next() + (rel ? y : 0); Add(x, y);
                    break;
                case 'Z':
                    if (current.Count > 1) finished.Add((current, true));
                    current = new List<PointD>();
                    x = startX; y = startY;
                    command = 'M';
                    if (!HasNumber()) { }
                    break;
                default:
                    i++;
                    break;
            }
        }
        if (current.Count > 1) finished.Add((current, false));
        return finished;
    }

    private static void Bezier(double x0, double y0, double x1, double y1, double x2, double y2, double x3, double y3, Action<double, double> add)
    {
        for (int s = 1; s <= 16; s++)
        {
            double t = s / 16.0, u = 1 - t;
            add(u * u * u * x0 + 3 * u * u * t * x1 + 3 * u * t * t * x2 + t * t * t * x3,
                u * u * u * y0 + 3 * u * u * t * y1 + 3 * u * t * t * y2 + t * t * t * y3);
        }
    }
}
