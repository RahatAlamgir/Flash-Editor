using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Rendering;

/// <summary>
/// Ready-made graphics like the ones Canva offers: lines and dividers, arrows, blobs and shapes, decorations, frames and borders, patterns.
/// Each one is a group of shapes, so it can be ungrouped, recolored and changed.
/// </summary>
public static class Graphics
{
    public sealed record GraphicInfo(string Id, string Name, string Category);

    public static readonly string[] Categories = { "Lines and dividers", "Arrows", "Blobs and shapes", "Decorations", "Frames and borders", "Patterns" };

    private static GraphicInfo G(string id, string name, string category) => new(id, name, category);

    public static readonly IReadOnlyList<GraphicInfo> All = new[]
    {
        G("line", "Line", "Lines and dividers"), G("dashed", "Dashed line", "Lines and dividers"), G("dotted", "Dotted line", "Lines and dividers"),
        G("thick", "Thick line", "Lines and dividers"), G("double", "Double line", "Lines and dividers"), G("arrowline", "Arrow line", "Lines and dividers"),
        G("wave", "Wavy line", "Lines and dividers"), G("zigzag", "Zigzag", "Lines and dividers"), G("dividerstar", "Star divider", "Lines and dividers"),
        G("dividerdots", "Dot divider", "Lines and dividers"), G("underline", "Brush underline", "Lines and dividers"),

        G("arrowbig", "Big arrow", "Arrows"), G("arrowdouble", "Double arrow", "Arrows"), G("chevrons", "Chevrons", "Arrows"), G("curved", "Curved arrow", "Arrows"),
        G("loop", "Loop arrow", "Arrows"), G("swoosh", "Swoosh", "Arrows"), G("arrowpair", "Two arrows", "Arrows"),

        G("blob1", "Blob 1", "Blobs and shapes"), G("blob2", "Blob 2", "Blobs and shapes"), G("blob3", "Blob 3", "Blobs and shapes"), G("blob4", "Blob 4", "Blobs and shapes"),
        G("rings", "Rings", "Blobs and shapes"), G("cluster", "Circle cluster", "Blobs and shapes"), G("halfsun", "Half sun", "Blobs and shapes"),
        G("triangles", "Triangles", "Blobs and shapes"), G("pills", "Pills", "Blobs and shapes"),

        G("sparkles", "Sparkles", "Decorations"), G("confetti", "Confetti", "Decorations"), G("starsrow", "Row of stars", "Decorations"), G("hearts", "Hearts", "Decorations"),
        G("starburst", "Starburst", "Decorations"), G("sun", "Sun", "Decorations"), G("flower", "Flower", "Decorations"), G("moon", "Moon and stars", "Decorations"),
        G("clouds", "Clouds", "Decorations"), G("crown", "Crown", "Decorations"),

        G("border", "Simple border", "Frames and borders"), G("borderdouble", "Double border", "Frames and borders"), G("borderround", "Rounded border", "Frames and borders"),
        G("corners", "Corner marks", "Frames and borders"), G("ticket", "Ticket", "Frames and borders"), G("photo", "Photo frame", "Frames and borders"),

        G("dots", "Dots", "Patterns"), G("stripes", "Stripes", "Patterns"), G("checker", "Checkerboard", "Patterns"), G("circlegrid", "Circle grid", "Patterns"),
        G("plus", "Plus signs", "Patterns"), G("zigzagrows", "Zigzag rows", "Patterns"), G("waverows", "Wave rows", "Patterns"),
    };

    private static ColorRgba C(int r, int g, int b) => new((byte)r, (byte)g, (byte)b);

    private static readonly ColorRgba Blue = C(59, 130, 246), Pink = C(244, 114, 182), Amber = C(250, 204, 21), Green = C(34, 197, 94), Purple = C(139, 92, 246),
        Dark = C(30, 41, 59), Orange = C(249, 115, 22), Red = C(239, 68, 68), White = C(255, 255, 255), Sky = C(56, 189, 248), Gray = C(148, 163, 184);

    private static readonly ColorRgba[] Rainbow = { Blue, Pink, Amber, Green, Purple, Orange, Sky, Red };

    /// <summary>Places design pictures in a space 100 wide and <c>height</c> tall, centered on the canvas, <paramref name="width"/> pixels wide.</summary>
    private sealed class Maker
    {
        private readonly double _s, _ox, _oy;
        public readonly List<GroupMember> Parts = new();
        public Maker(double canvasWidth, double canvasHeight, double width, double designHeight)
        {
            _s = width / 100.0;
            _ox = (canvasWidth - width) / 2;
            _oy = (canvasHeight - designHeight * _s) / 2;
        }

        private ObjectBox Box(double x, double y, double w, double h, double rotation = 0) => new(_ox + x * _s, _oy + y * _s, Math.Max(0.5, w * _s), Math.Max(0.5, h * _s), rotation);

        public void Shape(string name, ShapeKind kind, double x, double y, double w, double h, ColorRgba? fill, ColorRgba? stroke = null, double strokeWidth = 0, double radius = 0, double rotation = 0)
            => Parts.Add(new GroupMember(new ShapeObject(Box(x, y, w, h, rotation), kind, fill, stroke, strokeWidth * _s, CornerRadius: radius * _s), name, 1));

        /// <summary>A straight line through (cx, cy) of the given length and angle (degrees).</summary>
        public void Line(double cx, double cy, double length, double angle, ColorRgba color, double width, StrokeStyle dash = StrokeStyle.Solid, bool arrow = false, double dashLength = 0, double dashGap = 0, double bend = 0)
        {
            double thickness = Math.Max(1, width * _s);
            var box = new ObjectBox(_ox + (cx - length / 2) * _s, _oy + cy * _s - thickness / 2, length * _s, thickness, angle);
            Parts.Add(new GroupMember(new ShapeObject(box, ShapeKind.Line, null, color, thickness, Dash: dash, ArrowEnd: arrow, DashLength: dashLength * _s, DashGap: dashGap * _s, Bend: bend), "Line", 1));
        }

        public void Path(string name, double x, double y, double w, double h, IReadOnlyList<CurveNode> nodes, bool closed, ColorRgba? fill, ColorRgba? stroke = null, double strokeWidth = 0, double rotation = 0)
            => Parts.Add(new GroupMember(new ShapeObject(Box(x, y, w, h, rotation), ShapeKind.Path, fill, stroke, strokeWidth * _s, Nodes: nodes, Closed: closed), name, 1));

        public GroupObject Done() => GroupObject.Create(Parts);
    }

    // ---- curves in the unit box (0..1) -------------------------------------------------------------------------------------------

    private static PointD P(double x, double y) => new(x, y);

    /// <summary>A smooth wave: <paramref name="halfWaves"/> crests and troughs from the left to the right of the unit box.</summary>
    private static List<CurveNode> Wave(int halfWaves)
    {
        var nodes = new List<CurveNode>();
        double handle = 0.36 / halfWaves;
        for (int i = 0; i <= halfWaves; i++)
        {
            double x = i / (double)halfWaves, y = i % 2 == 0 ? 1 : 0;
            nodes.Add(new CurveNode(P(x, y), P(i == 0 ? x : x - handle, y), P(i == halfWaves ? x : x + handle, y)));
        }
        return nodes;
    }

    private static List<CurveNode> Zigzag(int points)
    {
        var nodes = new List<CurveNode>();
        for (int i = 0; i <= points; i++) nodes.Add(CurveNode.Corner(P(i / (double)points, i % 2 == 0 ? 1 : 0)));
        return nodes;
    }

    /// <summary>An organic closed shape: points around the center at the given distances (0..0.5), joined smoothly.</summary>
    private static List<CurveNode> Blob(params double[] radii)
    {
        int n = radii.Length;
        var nodes = new List<CurveNode>();
        for (int i = 0; i < n; i++)
        {
            double angle = i * Math.PI * 2 / n - Math.PI / 2, r = radii[i];
            var p = P(0.5 + Math.Cos(angle) * r, 0.5 + Math.Sin(angle) * r);
            double handle = r * (4.0 / 3.0) * Math.Tan(Math.PI / (2 * n));
            var tangent = P(-Math.Sin(angle), Math.Cos(angle));
            nodes.Add(new CurveNode(p, P(p.X - tangent.X * handle, p.Y - tangent.Y * handle), P(p.X + tangent.X * handle, p.Y + tangent.Y * handle)));
        }
        return nodes;
    }

    private static List<CurveNode> Swoosh()
        => new() { new CurveNode(P(0, 0.85), P(0, 0.85), P(0.25, 0.1)), new CurveNode(P(1, 0.25), P(0.55, 0.0), P(1, 0.25)) };

    private static List<CurveNode> Loop()
        => new()
        {
            new CurveNode(P(0, 0.9), P(0, 0.9), P(0.25, 0.9)),
            new CurveNode(P(0.62, 0.55), P(0.5, 0.95), P(0.75, 0.15)),
            new CurveNode(P(0.42, 0.18), P(0.5, 0.05), P(0.3, 0.3)),
            new CurveNode(P(1, 0.7), P(0.7, 0.4), P(1, 0.7)),
        };

    private static List<CurveNode> Crown()
        => new()
        {
            CurveNode.Corner(P(0, 1)), CurveNode.Corner(P(0, 0.25)), CurveNode.Corner(P(0.25, 0.55)), CurveNode.Corner(P(0.5, 0)),
            CurveNode.Corner(P(0.75, 0.55)), CurveNode.Corner(P(1, 0.25)), CurveNode.Corner(P(1, 1)),
        };

    // ---- the graphics ---------------------------------------------------------------------------------------------------------

    /// <summary>The graphic, centered on a canvas, about <paramref name="width"/> pixels wide.</summary>
    public static GroupObject Build(string id, double canvasWidth, double canvasHeight, double width)
    {
        var rng = new Random(7);
        Maker m;
        switch (id)
        {
            // lines and dividers
            case "line": m = new Maker(canvasWidth, canvasHeight, width, 4); m.Line(50, 2, 100, 0, Dark, 1.6); break;
            case "dashed": m = new Maker(canvasWidth, canvasHeight, width, 4); m.Line(50, 2, 100, 0, Dark, 1.6, StrokeStyle.Dashed, dashLength: 5, dashGap: 3.5); break;
            case "dotted": m = new Maker(canvasWidth, canvasHeight, width, 4); m.Line(50, 2, 100, 0, Pink, 2.2, StrokeStyle.Dotted, dashGap: 3.2); break;
            case "thick": m = new Maker(canvasWidth, canvasHeight, width, 8); m.Line(50, 4, 100, 0, Blue, 5); break;
            case "double": m = new Maker(canvasWidth, canvasHeight, width, 8); m.Line(50, 1.5, 100, 0, Dark, 1.2); m.Line(50, 6.5, 100, 0, Dark, 1.2); break;
            case "arrowline": m = new Maker(canvasWidth, canvasHeight, width, 10); m.Line(48, 5, 92, 0, Dark, 2, arrow: true); break;
            case "wave": m = new Maker(canvasWidth, canvasHeight, width, 14); m.Path("Wave", 0, 2, 100, 10, Wave(10), false, null, Blue, 2.4); break;
            case "zigzag": m = new Maker(canvasWidth, canvasHeight, width, 14); m.Path("Zigzag", 0, 2, 100, 10, Zigzag(14), false, null, Orange, 2.4); break;
            case "dividerstar":
                m = new Maker(canvasWidth, canvasHeight, width, 12);
                m.Line(22, 6, 44, 0, Dark, 1.2); m.Line(78, 6, 44, 0, Dark, 1.2); m.Shape("Star", ShapeKind.Star, 44, 0, 12, 12, Amber); break;
            case "dividerdots":
                m = new Maker(canvasWidth, canvasHeight, width, 8);
                m.Line(20, 4, 36, 0, Gray, 1); m.Line(80, 4, 36, 0, Gray, 1);
                for (int i = 0; i < 3; i++) m.Shape("Dot", ShapeKind.Ellipse, 44 + i * 6, 1.8, 4.4, 4.4, i == 1 ? Pink : Gray);
                break;
            case "underline": m = new Maker(canvasWidth, canvasHeight, width, 12); m.Path("Brush", 0, 2, 100, 8, new List<CurveNode> { new(P(0, 0.7), P(0, 0.7), P(0.3, 0.2)), new(P(1, 0.3), P(0.65, 0.9), P(1, 0.3)) }, false, null, Amber, 4); break;

            // arrows
            case "arrowbig": m = new Maker(canvasWidth, canvasHeight, width, 50); m.Shape("Arrow", ShapeKind.Arrow, 0, 0, 100, 50, Blue); break;
            case "arrowdouble": m = new Maker(canvasWidth, canvasHeight, width, 40); m.Shape("Arrow", ShapeKind.DoubleArrow, 0, 0, 100, 40, Orange); break;
            case "chevrons":
                m = new Maker(canvasWidth, canvasHeight, width, 40);
                for (int i = 0; i < 3; i++) m.Shape("Chevron", ShapeKind.Chevron, i * 31, 0, 28, 40, i == 0 ? Sky : i == 1 ? Blue : Purple);
                break;
            case "curved": m = new Maker(canvasWidth, canvasHeight, width, 40); m.Line(50, 22, 96, 0, Dark, 2.2, arrow: true, bend: -0.55); break;
            case "loop": m = new Maker(canvasWidth, canvasHeight, width, 50); m.Path("Loop", 0, 0, 100, 50, Loop(), false, null, Dark, 2.2); m.Shape("Head", ShapeKind.Triangle, 92, 29, 10, 12, Dark, rotation: 100); break;
            case "swoosh": m = new Maker(canvasWidth, canvasHeight, width, 40); m.Path("Swoosh", 0, 0, 100, 40, Swoosh(), false, null, Pink, 3.5); m.Shape("Head", ShapeKind.Triangle, 92, 3, 11, 13, Pink, rotation: 80); break;
            case "arrowpair":
                m = new Maker(canvasWidth, canvasHeight, width, 22);
                m.Line(50, 4, 90, 0, Dark, 2, arrow: true); m.Line(50, 18, 90, 180, Dark, 2, arrow: true); break;

            // blobs and shapes
            case "blob1": m = new Maker(canvasWidth, canvasHeight, width, 100); m.Path("Blob", 0, 0, 100, 100, Blob(0.46, 0.38, 0.47, 0.4, 0.45, 0.37, 0.48, 0.41), true, Pink); break;
            case "blob2": m = new Maker(canvasWidth, canvasHeight, width, 100); m.Path("Blob", 0, 0, 100, 100, Blob(0.42, 0.48, 0.36, 0.47, 0.4, 0.45, 0.37), true, Blue); break;
            case "blob3": m = new Maker(canvasWidth, canvasHeight, width, 100); m.Path("Blob", 0, 0, 100, 100, Blob(0.48, 0.4, 0.44, 0.49, 0.38, 0.46, 0.43, 0.36, 0.47), true, Amber); break;
            case "blob4": m = new Maker(canvasWidth, canvasHeight, width, 100); m.Path("Blob", 0, 0, 100, 100, Blob(0.4, 0.47, 0.5, 0.41, 0.36, 0.45), true, Green); break;
            case "rings":
                m = new Maker(canvasWidth, canvasHeight, width, 100);
                for (int i = 0; i < 4; i++) m.Shape("Ring", ShapeKind.Ellipse, 5 + i * 12, 5 + i * 12, 90 - i * 24, 90 - i * 24, null, Rainbow[i], 5);
                break;
            case "cluster":
                m = new Maker(canvasWidth, canvasHeight, width, 80);
                m.Shape("Circle", ShapeKind.Ellipse, 0, 20, 46, 46, Blue); m.Shape("Circle", ShapeKind.Ellipse, 34, 0, 34, 34, Pink); m.Shape("Circle", ShapeKind.Ellipse, 48, 38, 52, 42, Amber);
                m.Shape("Circle", ShapeKind.Ellipse, 22, 58, 20, 20, Purple); break;
            case "halfsun":
                m = new Maker(canvasWidth, canvasHeight, width, 50);
                m.Shape("Half", ShapeKind.HalfCircle, 0, 0, 100, 50, Orange); m.Shape("Half", ShapeKind.HalfCircle, 16, 16, 68, 34, Amber); m.Shape("Half", ShapeKind.HalfCircle, 32, 32, 36, 18, Red); break;
            case "triangles":
                m = new Maker(canvasWidth, canvasHeight, width, 60);
                m.Shape("Triangle", ShapeKind.Triangle, 0, 22, 44, 38, Blue); m.Shape("Triangle", ShapeKind.Triangle, 30, 0, 44, 38, Pink); m.Shape("Triangle", ShapeKind.Triangle, 56, 22, 44, 38, Amber); break;
            case "pills":
                m = new Maker(canvasWidth, canvasHeight, width, 60);
                m.Shape("Pill", ShapeKind.Rectangle, 0, 0, 100, 16, Blue, radius: 8); m.Shape("Pill", ShapeKind.Rectangle, 14, 22, 86, 16, Pink, radius: 8); m.Shape("Pill", ShapeKind.Rectangle, 0, 44, 64, 16, Amber, radius: 8); break;

            // decorations
            case "sparkles":
                m = new Maker(canvasWidth, canvasHeight, width, 90);
                m.Shape("Sparkle", ShapeKind.Sparkle, 20, 24, 60, 60, Amber); m.Shape("Sparkle", ShapeKind.Sparkle, 0, 0, 30, 30, Pink); m.Shape("Sparkle", ShapeKind.Sparkle, 70, 0, 24, 24, Sky); break;
            case "confetti":
                m = new Maker(canvasWidth, canvasHeight, width, 70);
                for (int i = 0; i < 34; i++)
                {
                    var color = Rainbow[rng.Next(Rainbow.Length)];
                    double x = rng.NextDouble() * 94, y = rng.NextDouble() * 64, size = 3 + rng.NextDouble() * 4, turn = rng.NextDouble() * 180;
                    int kind = i % 3;
                    m.Shape("Piece", kind == 0 ? ShapeKind.Rectangle : kind == 1 ? ShapeKind.Ellipse : ShapeKind.Triangle, x, y, kind == 0 ? size * 1.8 : size, size, color, rotation: turn);
                }
                break;
            case "starsrow":
                m = new Maker(canvasWidth, canvasHeight, width, 20);
                for (int i = 0; i < 5; i++) m.Shape("Star", ShapeKind.Star, i * 20, 0, 20, 20, Amber);
                break;
            case "hearts":
                m = new Maker(canvasWidth, canvasHeight, width, 70);
                m.Shape("Heart", ShapeKind.Heart, 30, 18, 50, 46, Red); m.Shape("Heart", ShapeKind.Heart, 0, 0, 28, 26, Pink); m.Shape("Heart", ShapeKind.Heart, 70, 4, 26, 24, C(251, 113, 133)); break;
            case "starburst": m = new Maker(canvasWidth, canvasHeight, width, 100); m.Shape("Burst", ShapeKind.Burst, 0, 0, 100, 100, Amber); m.Shape("Burst", ShapeKind.Burst, 18, 18, 64, 64, Orange); break;
            case "sun":
                m = new Maker(canvasWidth, canvasHeight, width, 100);
                for (int i = 0; i < 8; i++) m.Line(50 + Math.Cos(i * Math.PI / 4) * 38, 50 + Math.Sin(i * Math.PI / 4) * 38, 18, i * 45, Amber, 4);
                m.Shape("Sun", ShapeKind.Ellipse, 26, 26, 48, 48, Amber); break;
            case "flower":
                m = new Maker(canvasWidth, canvasHeight, width, 100);
                for (int i = 0; i < 6; i++) m.Shape("Petal", ShapeKind.Ellipse, 36, 4, 28, 44, i % 2 == 0 ? Pink : C(251, 113, 133), rotation: i * 60);
                m.Shape("Center", ShapeKind.Ellipse, 36, 36, 28, 28, Amber); break;
            case "moon":
                m = new Maker(canvasWidth, canvasHeight, width, 90);
                m.Shape("Moon", ShapeKind.Crescent, 0, 0, 70, 90, Amber); m.Shape("Star", ShapeKind.Star, 68, 10, 20, 20, Amber); m.Shape("Star", ShapeKind.Star, 78, 50, 14, 14, Amber); break;
            case "clouds":
                m = new Maker(canvasWidth, canvasHeight, width, 60);
                m.Shape("Cloud", ShapeKind.Cloud, 20, 0, 80, 42, Sky); m.Shape("Cloud", ShapeKind.Cloud, 0, 24, 60, 34, C(186, 230, 253)); break;
            case "crown": m = new Maker(canvasWidth, canvasHeight, width, 70); m.Path("Crown", 0, 0, 100, 70, Crown(), true, Amber); m.Shape("Gem", ShapeKind.Ellipse, 42, 40, 16, 16, Red); break;

            // frames and borders
            case "border": m = new Maker(canvasWidth, canvasHeight, width, 70); m.Shape("Border", ShapeKind.Rectangle, 0, 0, 100, 70, null, Dark, 1.4); break;
            case "borderdouble":
                m = new Maker(canvasWidth, canvasHeight, width, 70);
                m.Shape("Border", ShapeKind.Rectangle, 0, 0, 100, 70, null, Dark, 1.8); m.Shape("Inner", ShapeKind.Rectangle, 4, 4, 92, 62, null, Dark, 0.8); break;
            case "borderround": m = new Maker(canvasWidth, canvasHeight, width, 70); m.Shape("Border", ShapeKind.Rectangle, 0, 0, 100, 70, null, Blue, 2.6, radius: 14); break;
            case "corners":
                m = new Maker(canvasWidth, canvasHeight, width, 70);
                foreach (var (cx, cy, sx, sy) in new[] { (0.0, 0.0, 1, 1), (100.0, 0.0, -1, 1), (0.0, 70.0, 1, -1), (100.0, 70.0, -1, -1) })
                {
                    m.Line(cx + sx * 9, cy, 18, 0, Dark, 1.8);
                    m.Line(cx, cy + sy * 9, 18, 90, Dark, 1.8);
                }
                break;
            case "ticket":
                m = new Maker(canvasWidth, canvasHeight, width, 50);
                m.Shape("Ticket", ShapeKind.Rectangle, 0, 0, 100, 50, Amber, radius: 4); m.Shape("Notch", ShapeKind.Ellipse, -6, 19, 12, 12, White); m.Shape("Notch", ShapeKind.Ellipse, 94, 19, 12, 12, White);
                m.Line(70, 25, 40, 90, C(161, 98, 7), 1, StrokeStyle.Dashed, dashLength: 3, dashGap: 2); break;
            case "photo":
                m = new Maker(canvasWidth, canvasHeight, width, 90);
                m.Shape("Frame", ShapeKind.Rectangle, 0, 0, 100, 90, White, C(203, 213, 225), 1, 2); m.Shape("Picture", ShapeKind.Rectangle, 6, 6, 88, 66, C(226, 232, 240)); break;

            // patterns
            case "dots":
                m = new Maker(canvasWidth, canvasHeight, width, 50);
                for (int r = 0; r < 5; r++) for (int c = 0; c < 10; c++) m.Shape("Dot", ShapeKind.Ellipse, c * 10 + 2, r * 10 + 2, 5, 5, Blue);
                break;
            case "stripes":
                m = new Maker(canvasWidth, canvasHeight, width, 60);
                for (int i = 0; i < 8; i++) m.Shape("Stripe", ShapeKind.Rectangle, 0, i * 8, 100, 4, i % 2 == 0 ? Pink : C(251, 207, 232));
                break;
            case "checker":
                m = new Maker(canvasWidth, canvasHeight, width, 50);
                for (int r = 0; r < 5; r++) for (int c = 0; c < 10; c++) if ((r + c) % 2 == 0) m.Shape("Square", ShapeKind.Rectangle, c * 10, r * 10, 10, 10, Dark);
                break;
            case "circlegrid":
                m = new Maker(canvasWidth, canvasHeight, width, 50);
                for (int r = 0; r < 5; r++) for (int c = 0; c < 10; c++) m.Shape("Circle", ShapeKind.Ellipse, c * 10 + 1, r * 10 + 1, 8, 8, null, Purple, 1.2);
                break;
            case "plus":
                m = new Maker(canvasWidth, canvasHeight, width, 50);
                for (int r = 0; r < 5; r++) for (int c = 0; c < 10; c++) m.Shape("Plus", ShapeKind.Cross, c * 10 + 1.5, r * 10 + 1.5, 7, 7, Green);
                break;
            case "zigzagrows":
                m = new Maker(canvasWidth, canvasHeight, width, 50);
                for (int r = 0; r < 4; r++) m.Path("Zigzag", 0, r * 14, 100, 8, Zigzag(16), false, null, Orange, 1.6);
                break;
            case "waverows":
                m = new Maker(canvasWidth, canvasHeight, width, 50);
                for (int r = 0; r < 4; r++) m.Path("Wave", 0, r * 14, 100, 8, Wave(12), false, null, Sky, 1.6);
                break;
            default: throw new ArgumentException("Unknown graphic: " + id);
        }
        return m.Done();
    }
}
