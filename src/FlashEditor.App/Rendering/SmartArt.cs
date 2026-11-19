using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Rendering;

/// <summary>
/// Diagrams like Office's SmartArt: processes, lists, cycles, hierarchies, relationships, matrices and pyramids. You give the words, the program
/// draws the shapes. The result is a group of shapes and text, so it can be ungrouped and changed.
/// </summary>
public static class SmartArt
{
    public sealed record LayoutInfo(string Id, string Name, string Category, int Min, int Max, string[] Sample);

    public static readonly string[] Categories = { "List", "Process", "Cycle", "Hierarchy", "Relationship", "Matrix", "Pyramid" };

    private static readonly string[] Four = { "Plan", "Design", "Build", "Launch" };
    private static readonly string[] Three = { "Idea", "Work", "Result" };

    private static LayoutInfo L(string id, string name, string category, int min, int max, string[]? sample = null) => new(id, name, category, min, max, sample ?? Four);

    public static readonly IReadOnlyList<LayoutInfo> Layouts = new[]
    {
        L("list", "Basic block list", "List", 2, 8), L("vlist", "Vertical bullet list", "List", 2, 8),
        L("process", "Basic process", "Process", 2, 6), L("vprocess", "Vertical process", "Process", 2, 6), L("chevron", "Chevron process", "Process", 2, 6),
        L("steps", "Step up process", "Process", 2, 6), L("timeline", "Timeline", "Process", 2, 6),
        L("cycle", "Basic cycle", "Cycle", 3, 8), L("hub", "Hub and spokes", "Cycle", 3, 8, new[] { "Goal", "Time", "People", "Money", "Tools" }),
        L("org", "Organization chart", "Hierarchy", 2, 6, new[] { "Manager", "Design", "Sales", "Support" }), L("tree", "Horizontal tree", "Hierarchy", 2, 7, new[] { "Product", "Idea", "Design", "Test" }),
        L("venn", "Basic Venn", "Relationship", 2, 4, Three), L("target", "Target", "Relationship", 2, 5, Three), L("funnel", "Funnel", "Relationship", 2, 5),
        L("matrix", "Basic matrix", "Matrix", 4, 4), L("pyramid", "Basic pyramid", "Pyramid", 2, 6, Three),
    };

    /// <summary>The color sets to choose from.</summary>
    public static readonly IReadOnlyList<(string Name, ColorRgba[] Colors)> Schemes = new (string, ColorRgba[])[]
    {
        ("Colorful", new[] { C(59, 130, 246), C(249, 115, 22), C(34, 197, 94), C(168, 85, 247), C(236, 72, 153), C(234, 179, 8), C(14, 165, 233), C(239, 68, 68) }),
        ("Blue", new[] { C(30, 64, 175), C(37, 99, 235), C(59, 130, 246), C(96, 165, 250), C(14, 116, 144), C(14, 165, 233), C(56, 189, 248), C(29, 78, 216) }),
        ("Green", new[] { C(21, 128, 61), C(34, 197, 94), C(22, 163, 74), C(101, 163, 13), C(5, 150, 105), C(132, 204, 22), C(13, 148, 136), C(74, 222, 128) }),
        ("Warm", new[] { C(220, 38, 38), C(249, 115, 22), C(234, 179, 8), C(236, 72, 153), C(190, 18, 60), C(251, 146, 60), C(245, 158, 11), C(244, 63, 94) }),
        ("Purple", new[] { C(88, 28, 135), C(124, 58, 237), C(168, 85, 247), C(192, 132, 252), C(190, 24, 93), C(217, 70, 239), C(99, 102, 241), C(139, 92, 246) }),
        ("Gray", new[] { C(30, 41, 59), C(51, 65, 85), C(71, 85, 105), C(100, 116, 139), C(30, 41, 59), C(51, 65, 85), C(71, 85, 105), C(100, 116, 139) }),
    };

    private static ColorRgba C(int r, int g, int b) => new((byte)r, (byte)g, (byte)b);
    private static readonly ColorRgba White = C(255, 255, 255), Dark = C(30, 41, 59), Soft = C(226, 232, 240);

    // ------------------------------------------------------------------ the drawing board

    private sealed class Board
    {
        private readonly double _s, _ox, _oy;
        private readonly ColorRgba[] _colors;
        public readonly List<GroupMember> Parts = new();
        public Board(double canvasWidth, double canvasHeight, double width, double designHeight, ColorRgba[] colors)
        {
            _s = width / 100.0;
            _ox = (canvasWidth - width) / 2;
            _oy = (canvasHeight - designHeight * _s) / 2;
            _colors = colors;
        }

        public ColorRgba Color(int i) => _colors[i % _colors.Length];

        public void Shape(string name, ShapeKind kind, double x, double y, double w, double h, ColorRgba? fill, ColorRgba? stroke = null, double strokeWidth = 0, double radius = 0, double rotation = 0)
            => Parts.Add(new GroupMember(new ShapeObject(new ObjectBox(_ox + x * _s, _oy + y * _s, Math.Max(0.5, w * _s), Math.Max(0.5, h * _s), rotation), kind, fill, stroke, strokeWidth * _s, CornerRadius: radius * _s), name, 1));

        public void Line(double cx, double cy, double length, double angle, ColorRgba color, double width)
        {
            double thickness = Math.Max(1, width * _s);
            Parts.Add(new GroupMember(new ShapeObject(new ObjectBox(_ox + (cx - length / 2) * _s, _oy + cy * _s - thickness / 2, length * _s, thickness, angle), ShapeKind.Line, null, color, thickness), "Connector", 1));
        }

        /// <summary>A flat-sided shape through the given points (design units).</summary>
        public void Poly(string name, ColorRgba fill, params (double X, double Y)[] pts)
        {
            double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X), minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
            double w = Math.Max(0.01, maxX - minX), h = Math.Max(0.01, maxY - minY);
            var nodes = pts.Select(p => CurveNode.Corner(new PointD((p.X - minX) / w, (p.Y - minY) / h))).ToList();
            Parts.Add(new GroupMember(new ShapeObject(new ObjectBox(_ox + minX * _s, _oy + minY * _s, w * _s, h * _s), ShapeKind.Path, fill, null, 0, Nodes: nodes, Closed: true), name, 1));
        }

        /// <summary>Words centered at a place; long words wrap and the letters shrink until everything fits in the given room.</summary>
        public void Text(string text, double cx, double cy, double maxW, double maxH, double size, ColorRgba color, bool bold = true)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;
            double pxW = maxW * _s, pxH = maxH * _s;
            for (double px = size * _s; ; px *= 0.9)
            {
                string wrapped = Wrap(text, pxW, px, bold);
                var t = new TextObject(new ObjectBox(0, 0, 1, 1), wrapped, "Segoe UI", Math.Max(5, px), color, Bold: bold, Align: TextAlign.Center);
                var (w, h) = ObjectRendering.Renderer.MeasureText(t);
                if ((w <= pxW && h <= pxH) || px <= 6)
                {
                    Parts.Add(new GroupMember(t with { Box = new ObjectBox(_ox + cx * _s - w / 2, _oy + cy * _s - h / 2, w, h) }, text.Replace("\n", " "), 1));
                    return;
                }
            }
        }

        private static string Wrap(string text, double maxPixels, double size, bool bold)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            string line = "";
            foreach (var word in words)
            {
                string trial = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Measure(trial, size, bold) > maxPixels) { lines.Add(line); line = word; }
                else line = trial;
            }
            if (line.Length > 0) lines.Add(line);
            return string.Join("\n", lines);
        }

        private static double Measure(string text, double size, bool bold)
            => ObjectRendering.Renderer.MeasureText(new TextObject(new ObjectBox(0, 0, 1, 1), text, "Segoe UI", Math.Max(5, size), White, Bold: bold)).Width;

        public GroupObject Done() => GroupObject.Create(Parts);
    }

    // ------------------------------------------------------------------ the layouts

    /// <summary>The diagram for these words, centered on a canvas and about <paramref name="width"/> pixels wide.</summary>
    public static GroupObject Build(string id, IReadOnlyList<string> items, string scheme, double canvasWidth, double canvasHeight, double width)
    {
        var info = Layouts.First(l => l.Id == id);
        var words = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Take(info.Max).ToList();
        while (words.Count < info.Min) words.Add("Item " + (words.Count + 1));
        int n = words.Count;
        var colors = (Schemes.FirstOrDefault(s => s.Name == scheme).Colors ?? Schemes[0].Colors);
        Board b;

        switch (id)
        {
            case "process":
            {
                double gap = 9, bw = (100 - gap * (n - 1)) / n, h = Math.Min(30, bw * 1.1);
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                for (int i = 0; i < n; i++)
                {
                    double x = i * (bw + gap);
                    b.Shape("Step", ShapeKind.Rectangle, x, 0, bw, h, b.Color(i), radius: 3);
                    b.Text(words[i], x + bw / 2, h / 2, bw * 0.86, h * 0.8, 6, White);
                    if (i < n - 1) b.Shape("Arrow", ShapeKind.Arrow, x + bw + 1.2, h / 2 - 3.5, gap - 2.4, 7, Dark);
                }
                break;
            }
            case "vprocess":
            {
                double bh = 15, gap = 9, bw = 62, h = n * bh + (n - 1) * gap;
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                for (int i = 0; i < n; i++)
                {
                    double y = i * (bh + gap);
                    b.Shape("Step", ShapeKind.Rectangle, 19, y, bw, bh, b.Color(i), radius: 3);
                    b.Text(words[i], 50, y + bh / 2, bw * 0.9, bh * 0.8, 7, White);
                    if (i < n - 1) b.Shape("Arrow", ShapeKind.Triangle, 45, y + bh + 1.5, 10, gap - 3, Dark, rotation: 180);
                }
                break;
            }
            case "chevron":
            {
                double w = 100 / (n * 0.86 + 0.14), h = Math.Min(30, w * 0.9);
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                for (int i = 0; i < n; i++)
                {
                    double x = i * w * 0.86;
                    b.Shape("Chevron", ShapeKind.Chevron, x, 0, w, h, b.Color(i));
                    b.Text(words[i], x + w * 0.52, h / 2, w * 0.5, h * 0.7, 6.5, White);
                }
                break;
            }
            case "steps":
            {
                double bw = 100.0 / n - 1.5, h = 62;
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                for (int i = 0; i < n; i++)
                {
                    double bh = 20 + (n == 1 ? 0 : i * 40.0 / (n - 1)), x = i * (bw + 1.5);
                    b.Shape("Step", ShapeKind.Rectangle, x, h - bh, bw, bh, b.Color(i), radius: 2);
                    b.Text(words[i], x + bw / 2, h - bh / 2, bw * 0.88, bh * 0.7, 6.5, White);
                }
                break;
            }
            case "timeline":
            {
                double h = 56;
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                b.Line(50, h / 2, 100, 0, Dark, 1.6);
                double slot = 100.0 / n;
                for (int i = 0; i < n; i++)
                {
                    double cx = slot * (i + 0.5);
                    b.Shape("Dot", ShapeKind.Ellipse, cx - 4, h / 2 - 4, 8, 8, b.Color(i), White, 1.2);
                    bool up = i % 2 == 0;
                    b.Line(cx, up ? h / 2 - 9 : h / 2 + 9, 10, 90, b.Color(i), 0.9);
                    b.Text(words[i], cx, up ? 8 : h - 8, slot * 1.5, 12, 6.5, Dark);
                }
                break;
            }
            case "list":
            {
                int cols = n <= 3 ? n : n <= 4 ? 2 : 3, rows = (n + cols - 1) / cols;
                double gap = 4, bw = (100 - gap * (cols - 1)) / cols, bh = Math.Min(26, bw * 0.62), h = rows * bh + (rows - 1) * gap;
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                for (int i = 0; i < n; i++)
                {
                    double x = (i % cols) * (bw + gap), y = (i / cols) * (bh + gap);
                    b.Shape("Box", ShapeKind.Rectangle, x, y, bw, bh, b.Color(i), radius: 4);
                    b.Text(words[i], x + bw / 2, y + bh / 2, bw * 0.84, bh * 0.76, 7, White);
                }
                break;
            }
            case "vlist":
            {
                double rh = 13, gap = 5, h = n * rh + (n - 1) * gap;
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                for (int i = 0; i < n; i++)
                {
                    double y = i * (rh + gap);
                    b.Shape("Bar", ShapeKind.Rectangle, 6, y, 94, rh, Soft, radius: rh / 2);
                    b.Shape("Mark", ShapeKind.Ellipse, 0, y - 1, rh + 2, rh + 2, b.Color(i), White, 1);
                    b.Text((i + 1).ToString(), (rh + 2) / 2, y + rh / 2, rh * 0.7, rh * 0.7, 7, White);
                    b.Text(words[i], 20 + 38, y + rh / 2, 74, rh * 0.8, 7, Dark, bold: false);
                }
                break;
            }
            case "cycle":
            {
                b = new Board(canvasWidth, canvasHeight, width, 100, colors);
                b.Shape("Ring", ShapeKind.Ellipse, 14, 14, 72, 72, null, Soft, 2);
                double r = Math.Min(15, 100.0 / n * 0.9);
                for (int i = 0; i < n; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI * 2 / n, cx = 50 + Math.Cos(a) * 36, cy = 50 + Math.Sin(a) * 36;
                    b.Shape("Node", ShapeKind.Ellipse, cx - r, cy - r, r * 2, r * 2, b.Color(i), White, 1.4);
                    b.Text(words[i], cx, cy, r * 1.55, r * 1.35, 6, White);
                    double m = a + Math.PI / n;
                    b.Shape("Arrow", ShapeKind.Triangle, 50 + Math.Cos(m) * 36 - 3.5, 50 + Math.Sin(m) * 36 - 3.5, 7, 7, Dark, rotation: m * 180 / Math.PI + 180);
                }
                break;
            }
            case "hub":
            {
                b = new Board(canvasWidth, canvasHeight, width, 100, colors);
                int spokes = n - 1;
                double r = Math.Min(14, 100.0 / spokes * 0.85);
                for (int i = 0; i < spokes; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI * 2 / spokes, cx = 50 + Math.Cos(a) * 36, cy = 50 + Math.Sin(a) * 36;
                    b.Line(50 + Math.Cos(a) * 22, 50 + Math.Sin(a) * 22, 22, a * 180 / Math.PI, Soft, 3);
                }
                b.Shape("Hub", ShapeKind.Ellipse, 31, 31, 38, 38, Dark);
                b.Text(words[0], 50, 50, 31, 26, 9, White);
                for (int i = 0; i < spokes; i++)
                {
                    double a = -Math.PI / 2 + i * Math.PI * 2 / spokes, cx = 50 + Math.Cos(a) * 36, cy = 50 + Math.Sin(a) * 36;
                    b.Shape("Node", ShapeKind.Ellipse, cx - r, cy - r, r * 2, r * 2, b.Color(i), White, 1.2);
                    b.Text(words[i + 1], cx, cy, r * 1.6, r * 1.35, 6, White);
                }
                break;
            }
            case "org":
            {
                int kids = n - 1;
                double bw = Math.Min(30, (100 - 4.0 * (kids - 1)) / kids), bh = 14, gap = 4, top = 18, h = 18 + 14 + 14;
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                double total = kids * bw + (kids - 1) * gap, start = (100 - total) / 2;
                double midY = bh + (h - bh * 2) / 2 + 0;
                midY = bh + 9;
                b.Line(50, bh + 4.5, 9, 90, Dark, 1.2);
                if (kids > 1) b.Line(50, midY, total - bw, 0, Dark, 1.2);
                for (int i = 0; i < kids; i++)
                {
                    double cx = start + i * (bw + gap) + bw / 2;
                    b.Line(cx, midY + (h - bh - midY) / 2, h - bh - midY, 90, Dark, 1.2);
                    b.Shape("Box", ShapeKind.Rectangle, cx - bw / 2, h - bh, bw, bh, b.Color(i + 1), radius: 3);
                    b.Text(words[i + 1], cx, h - bh / 2, bw * 0.88, bh * 0.78, 6.5, White);
                }
                b.Shape("Top", ShapeKind.Rectangle, 50 - 19, 0, 38, bh, b.Color(0), radius: 3);
                b.Text(words[0], 50, bh / 2, 34, bh * 0.78, 7, White);
                _ = top;
                break;
            }
            case "tree":
            {
                int kids = n - 1;
                double bh = 11, gap = 4, h = kids * bh + (kids - 1) * gap, rootH = 22;
                h = Math.Max(h, rootH);
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                double offset = (h - (kids * bh + (kids - 1) * gap)) / 2;
                b.Shape("Root", ShapeKind.Rectangle, 0, h / 2 - rootH / 2, 32, rootH, b.Color(0), radius: 3);
                b.Text(words[0], 16, h / 2, 29, rootH * 0.78, 7.5, White);
                b.Line(36, h / 2, 8, 0, Dark, 1.2);
                if (kids > 1) b.Line(40, h / 2, (kids - 1) * (bh + gap), 90, Dark, 1.2);
                for (int i = 0; i < kids; i++)
                {
                    double y = offset + i * (bh + gap);
                    b.Line(44, y + bh / 2, 8, 0, Dark, 1.2);
                    b.Shape("Leaf", ShapeKind.Rectangle, 48, y, 52, bh, b.Color(i + 1), radius: 3);
                    b.Text(words[i + 1], 74, y + bh / 2, 47, bh * 0.78, 6.5, White);
                }
                break;
            }
            case "venn":
            {
                double r = n == 2 ? 30 : n == 3 ? 29 : 25;
                double h = n == 2 ? 62 : 100;
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                var centers = n == 2 ? new[] { (36.0, 31.0), (64.0, 31.0) }
                    : n == 3 ? new[] { (50.0, 31.0), (34.0, 62.0), (66.0, 62.0) }
                    : new[] { (50.0, 28.0), (28.0, 50.0), (72.0, 50.0), (50.0, 72.0) };
                for (int i = 0; i < n; i++)
                {
                    var c = colors[i % colors.Length];
                    b.Shape("Circle", ShapeKind.Ellipse, centers[i].Item1 - r, centers[i].Item2 - r, r * 2, r * 2, c with { A = 150 });
                }
                for (int i = 0; i < n; i++)
                {
                    double ox = centers[i].Item1 - 50, oy = centers[i].Item2 - (h / 2);
                    double len = Math.Max(1, Math.Sqrt(ox * ox + oy * oy));
                    double tx = n == 2 ? centers[i].Item1 + (i == 0 ? -9 : 9) : centers[i].Item1 + ox / len * 8, ty = n == 2 ? centers[i].Item2 : centers[i].Item2 + oy / len * 8;
                    b.Text(words[i], tx, ty, r * 1.1, r * 0.8, 7, White);
                }
                break;
            }
            case "target":
            {
                b = new Board(canvasWidth, canvasHeight, width, 100, colors);
                for (int i = 0; i < n; i++)
                {
                    double r = 50 - i * (46.0 / n);
                    b.Shape("Ring", ShapeKind.Ellipse, 50 - r, 50 - r, r * 2, r * 2, b.Color(i));
                }
                for (int i = 0; i < n; i++)
                {
                    double r = 50 - i * (46.0 / n), band = 46.0 / n;
                    b.Text(words[i], 50, i == n - 1 ? 50 : 50 - r + band / 2, band * 3.2, band * 0.8, 7, White);
                }
                break;
            }
            case "funnel":
            {
                double sh = 17, h = n * sh;
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                for (int i = 0; i < n; i++)
                {
                    double w0 = 100 - i * (62.0 / n), w1 = 100 - (i + 1) * (62.0 / n), y0 = i * sh, y1 = y0 + sh - 1.2;
                    b.Poly("Layer", b.Color(i), (50 - w0 / 2, y0), (50 + w0 / 2, y0), (50 + w1 / 2, y1), (50 - w1 / 2, y1));
                    b.Text(words[i], 50, (y0 + y1) / 2, w1 * 0.9, sh * 0.75, 7, White);
                }
                break;
            }
            case "matrix":
            {
                double gap = 4, bw = (100 - gap) / 2, bh = Math.Min(30, bw * 0.62), h = bh * 2 + gap;
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                for (int i = 0; i < 4; i++)
                {
                    double x = (i % 2) * (bw + gap), y = (i / 2) * (bh + gap);
                    b.Shape("Cell", ShapeKind.Rectangle, x, y, bw, bh, b.Color(i), radius: 4);
                    b.Text(words[i], x + bw / 2, y + bh / 2, bw * 0.84, bh * 0.76, 8, White);
                }
                break;
            }
            case "pyramid":
            {
                double sh = 18, h = n * sh;
                b = new Board(canvasWidth, canvasHeight, width, h, colors);
                for (int i = 0; i < n; i++)
                {
                    double w0 = 100.0 * i / n, w1 = 100.0 * (i + 1) / n, y0 = i * sh, y1 = y0 + sh - 1.2;
                    if (i == 0) b.Poly("Top", b.Color(i), (50, y0), (50 + w1 / 2, y1), (50 - w1 / 2, y1));
                    else b.Poly("Layer", b.Color(i), (50 - w0 / 2, y0), (50 + w0 / 2, y0), (50 + w1 / 2, y1), (50 - w1 / 2, y1));
                    b.Text(words[i], 50, i == 0 ? y1 - sh * 0.3 : (y0 + y1) / 2, Math.Max(10, w0 * 0.78 + (i == 0 ? 8 : 0)), sh * 0.7, 7, White);
                }
                break;
            }
            default: throw new ArgumentException("Unknown layout: " + id);
        }
        return b.Done();
    }
}
