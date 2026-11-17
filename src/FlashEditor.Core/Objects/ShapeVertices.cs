using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// The corner points of the straight-sided shapes, in the box (0,0 = top-left corner of the box, 1,1 = bottom-right).
/// The drawing code builds these shapes from the points, and "reshape" lets the user drag them.
/// </summary>
public static class ShapeVertices
{
    /// <summary>Shapes made of straight sides, whose corners can be dragged.</summary>
    public static bool IsPolygonal(ShapeKind kind) => kind is
        ShapeKind.Rectangle or ShapeKind.Triangle or ShapeKind.Star or ShapeKind.Polygon or ShapeKind.Arrow or ShapeKind.Diamond
        or ShapeKind.RightTriangle or ShapeKind.Trapezoid or ShapeKind.Parallelogram or ShapeKind.Cross or ShapeKind.Chevron
        or ShapeKind.Lightning or ShapeKind.Pentagon or ShapeKind.Hexagon or ShapeKind.Octagon or ShapeKind.Sparkle
        or ShapeKind.Burst or ShapeKind.DoubleArrow or ShapeKind.Check or ShapeKind.XMark or ShapeKind.Home or ShapeKind.Tag;

    /// <summary>The corner points of the normal outline of a shape, or null when the shape has curved sides.</summary>
    public static IReadOnlyList<PointD>? For(ShapeObject shape)
    {
        PointD[] p(params (double X, double Y)[] points) => points.Select(t => new PointD(t.X, t.Y)).ToArray();

        return shape.Shape switch
        {
            ShapeKind.Rectangle => p((0, 0), (1, 0), (1, 1), (0, 1)),
            ShapeKind.Triangle => p((0.5, 0), (1, 1), (0, 1)),
            ShapeKind.RightTriangle => p((0, 0), (0, 1), (1, 1)),
            ShapeKind.Diamond => p((0.5, 0), (1, 0.5), (0.5, 1), (0, 0.5)),
            ShapeKind.Trapezoid => p((0.2, 0), (0.8, 0), (1, 1), (0, 1)),
            ShapeKind.Parallelogram => p((0.25, 0), (1, 0), (0.75, 1), (0, 1)),
            ShapeKind.Chevron => p((0, 0), (0.6, 0), (1, 0.5), (0.6, 1), (0, 1), (0.4, 0.5)),
            ShapeKind.Lightning => p((0.6, 0), (0.2, 0.55), (0.47, 0.55), (0.35, 1), (0.8, 0.4), (0.52, 0.4), (0.75, 0)),
            ShapeKind.Star => Star(Math.Clamp(shape.Points, 3, 24), 0.45),
            ShapeKind.Polygon => Star(Math.Clamp(shape.Points, 3, 24), 1.0),
            ShapeKind.Pentagon => Star(5, 1.0),
            ShapeKind.Hexagon => Star(6, 1.0),
            ShapeKind.Octagon => Star(8, 1.0),
            ShapeKind.Sparkle => Star(4, 0.28),
            ShapeKind.Burst => Star(16, 0.82),
            ShapeKind.Arrow => Arrow(shape.Box),
            ShapeKind.DoubleArrow => p((0, 0.5), (0.25, 0), (0.25, 0.3), (0.75, 0.3), (0.75, 0), (1, 0.5), (0.75, 1), (0.75, 0.7), (0.25, 0.7), (0.25, 1)),
            ShapeKind.Cross => Cross(Math.Clamp(shape.Inner, 0.1, 0.9) * 0.8),
            ShapeKind.Check => p((0.05, 0.55), (0.18, 0.42), (0.40, 0.64), (0.82, 0.15), (0.95, 0.28), (0.40, 0.90)),
            ShapeKind.XMark => p((0.18, 0), (0.5, 0.32), (0.82, 0), (1, 0.18), (0.68, 0.5), (1, 0.82), (0.82, 1), (0.5, 0.68), (0.18, 1), (0, 0.82), (0.32, 0.5), (0, 0.18)),
            ShapeKind.Home => p((0.5, 0), (1, 0.45), (0.85, 0.45), (0.85, 1), (0.15, 1), (0.15, 0.45), (0, 0.45)),
            ShapeKind.Tag => p((0, 0.15), (0.7, 0.15), (1, 0.5), (0.7, 0.85), (0, 0.85)),
            _ => null,
        };
    }

    /// <summary>A star (innerRatio below 1) or a regular polygon (innerRatio 1) with the first point at the top.</summary>
    private static PointD[] Star(int points, double innerRatio)
    {
        bool polygon = innerRatio >= 1;
        int count = polygon ? points : points * 2;
        var list = new PointD[count];
        for (int i = 0; i < count; i++)
        {
            double r = polygon || i % 2 == 0 ? 1 : innerRatio;
            double a = -Math.PI / 2 + 2 * Math.PI * i / count;
            list[i] = new PointD(0.5 + Math.Cos(a) * 0.5 * r, 0.5 + Math.Sin(a) * 0.5 * r);
        }
        return list;
    }

    /// <summary>A block arrow pointing right; the head keeps the same size on the page whatever the box is (as before).</summary>
    private static PointD[] Arrow(ObjectBox box)
    {
        double w = Math.Max(1, box.Width), h = Math.Max(1, box.Height);
        double head = Math.Min(w * 0.45, h * 0.9) / w;          // the head's length as a part of the width
        double neck = 1 - head;
        return new[]
        {
            new PointD(0, 0.3), new PointD(neck, 0.3), new PointD(neck, 0), new PointD(1, 0.5),
            new PointD(neck, 1), new PointD(neck, 0.7), new PointD(0, 0.7),
        };
    }

    /// <summary>A plus sign; <paramref name="arm"/> is how thick the arms are compared to the whole shape.</summary>
    private static PointD[] Cross(double arm)
    {
        double a = (1 - arm) / 2, b = 1 - a;
        return new[]
        {
            new PointD(a, 0), new PointD(b, 0), new PointD(b, a), new PointD(1, a), new PointD(1, b), new PointD(b, b),
            new PointD(b, 1), new PointD(a, 1), new PointD(a, b), new PointD(0, b), new PointD(0, a), new PointD(a, a),
        };
    }
}
