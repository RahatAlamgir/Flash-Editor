using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// One point of a curve (a Bezier path). <see cref="HandleIn"/> and <see cref="HandleOut"/> are the places of the two curve handles
/// (where the curve comes from and where it goes). A handle on the point itself means "no bend on that side". Inside a shape the
/// positions are in the box of the shape (0,0 = top-left, 1,1 = bottom-right).
/// </summary>
public sealed record CurveNode(PointD Point, PointD HandleIn, PointD HandleOut)
{
    /// <summary>A sharp corner (no handles).</summary>
    public static CurveNode Corner(PointD point) => new(point, point, point);

    /// <summary>A smooth point: the handles are opposite each other, as far apart as <paramref name="handleOut"/> is from the point.</summary>
    public static CurveNode Smooth(PointD point, PointD handleOut)
        => new(point, new PointD(2 * point.X - handleOut.X, 2 * point.Y - handleOut.Y), handleOut);

    public bool HasHandleIn => HandleIn != Point;
    public bool HasHandleOut => HandleOut != Point;

    /// <summary>True when both handles exist and lie on one line through the point (a smooth join).</summary>
    public bool IsSmooth
    {
        get
        {
            if (!HasHandleIn || !HasHandleOut) return false;
            double ax = HandleIn.X - Point.X, ay = HandleIn.Y - Point.Y, bx = HandleOut.X - Point.X, by = HandleOut.Y - Point.Y;
            double cross = ax * by - ay * bx, scale = Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by);
            return scale > 1e-12 && Math.Abs(cross) / scale < 0.02 && ax * bx + ay * by < 0;
        }
    }

    public CurveNode Moved(double dx, double dy)
        => new(new PointD(Point.X + dx, Point.Y + dy), new PointD(HandleIn.X + dx, HandleIn.Y + dy), new PointD(HandleOut.X + dx, HandleOut.Y + dy));

    public CurveNode Mapped(Func<PointD, PointD> map) => new(map(Point), map(HandleIn), map(HandleOut));
}

/// <summary>Maths for curves made of Bezier segments: sampling, the box around them, finding a spot and adding a point.</summary>
public static class CurvePath
{
    private static PointD Lerp(PointD a, PointD b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static PointD Cubic(PointD p0, PointD p1, PointD p2, PointD p3, double t)
    {
        var a = Lerp(p0, p1, t); var b = Lerp(p1, p2, t); var c = Lerp(p2, p3, t);
        return Lerp(Lerp(a, b, t), Lerp(b, c, t), t);
    }

    /// <summary>How many segments the curve has (one more for a closed curve: the last point joins the first).</summary>
    public static int SegmentCount(int nodes, bool closed) => nodes < 2 ? 0 : closed ? nodes : nodes - 1;

    /// <summary>The curve as a list of points close enough together to look smooth.</summary>
    public static List<PointD> Flatten(IReadOnlyList<CurveNode> nodes, bool closed, int stepsPerSegment = 24)
    {
        var points = new List<PointD>();
        if (nodes.Count == 0) return points;
        points.Add(nodes[0].Point);
        for (int s = 0; s < SegmentCount(nodes.Count, closed); s++)
        {
            var a = nodes[s]; var b = nodes[(s + 1) % nodes.Count];
            for (int i = 1; i <= stepsPerSegment; i++) points.Add(Cubic(a.Point, a.HandleOut, b.HandleIn, b.Point, i / (double)stepsPerSegment));
        }
        return points;
    }

    /// <summary>The smallest box around the curve itself (not around its handles).</summary>
    public static (double X, double Y, double Width, double Height) Bounds(IReadOnlyList<CurveNode> nodes, bool closed)
    {
        var points = Flatten(nodes, closed);
        if (points.Count == 0) return (0, 0, 1, 1);
        double minX = points.Min(p => p.X), maxX = points.Max(p => p.X), minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);
        return (minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Puts a curve given in canvas pixels into a box: the smallest box around it, and the points as fractions of that box. A straight
    /// horizontal or vertical curve gets a box one pixel thick.
    /// </summary>
    public static (ObjectBox Box, IReadOnlyList<CurveNode> Nodes) Fit(IReadOnlyList<CurveNode> canvasNodes, bool closed)
    {
        var (x, y, w, h) = Bounds(canvasNodes, closed);
        double width = Math.Max(1, w), height = Math.Max(1, h);
        PointD ToUnit(PointD p) => new((p.X - x) / width, (p.Y - y) / height);
        return (new ObjectBox(x, y, width, height), canvasNodes.Select(n => n.Mapped(ToUnit)).ToList());
    }

    /// <summary>The place on the curve nearest to <paramref name="point"/>: which segment, how far along it (0..1) and how far away it is.</summary>
    public static (int Segment, double T, double Distance) Nearest(IReadOnlyList<CurveNode> nodes, bool closed, PointD point, int steps = 40)
    {
        int bestSegment = -1; double bestT = 0, best = double.MaxValue;
        for (int s = 0; s < SegmentCount(nodes.Count, closed); s++)
        {
            var a = nodes[s]; var b = nodes[(s + 1) % nodes.Count];
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                var p = Cubic(a.Point, a.HandleOut, b.HandleIn, b.Point, t);
                double d = Math.Sqrt((p.X - point.X) * (p.X - point.X) + (p.Y - point.Y) * (p.Y - point.Y));
                if (d < best) { best = d; bestSegment = s; bestT = t; }
            }
        }
        return (bestSegment, bestT, best);
    }

    /// <summary>A new point on a segment at <paramref name="t"/> (0..1) that keeps the curve exactly the same (de Casteljau).</summary>
    public static List<CurveNode> Split(IReadOnlyList<CurveNode> nodes, bool closed, int segment, double t)
    {
        var result = nodes.ToList();
        int next = (segment + 1) % nodes.Count;
        var p0 = nodes[segment].Point; var p1 = nodes[segment].HandleOut; var p2 = nodes[next].HandleIn; var p3 = nodes[next].Point;
        var a = Lerp(p0, p1, t); var b = Lerp(p1, p2, t); var c = Lerp(p2, p3, t);
        var d = Lerp(a, b, t); var e = Lerp(b, c, t); var q = Lerp(d, e, t);

        result[segment] = nodes[segment] with { HandleOut = a };
        result[next] = nodes[next] with { HandleIn = c };
        result.Insert(segment + 1, new CurveNode(q, d, e));
        return result;
    }

    /// <summary>The unit vectors that point out of the two ends of an open curve (for arrow heads). Positions are in whatever space the nodes are in.</summary>
    public static (PointD StartOutward, PointD EndOutward) EndDirections(IReadOnlyList<CurveNode> nodes)
    {
        var first = nodes[0]; var second = nodes[1]; var last = nodes[^1]; var before = nodes[^2];
        PointD StartTarget() => first.HasHandleOut ? first.HandleOut : second.HasHandleIn ? second.HandleIn : second.Point;
        PointD EndTarget() => last.HasHandleIn ? last.HandleIn : before.HasHandleOut ? before.HandleOut : before.Point;
        var s = StartTarget(); var e = EndTarget();
        PointD Unit(double x, double y) { double d = Math.Sqrt(x * x + y * y); return d < 1e-9 ? new PointD(1, 0) : new PointD(x / d, y / d); }
        return (Unit(first.Point.X - s.X, first.Point.Y - s.Y), Unit(last.Point.X - e.X, last.Point.Y - e.Y));
    }

    /// <summary>Gives a corner point smooth handles (in the direction from the previous to the next point), or takes them away from a smooth one.</summary>
    public static List<CurveNode> ToggleSmooth(IReadOnlyList<CurveNode> nodes, bool closed, int index)
    {
        var result = nodes.ToList();
        var node = nodes[index];
        if (node.HasHandleIn || node.HasHandleOut) { result[index] = CurveNode.Corner(node.Point); return result; }

        int n = nodes.Count;
        var prev = nodes[(index - 1 + n) % n].Point; var next = nodes[(index + 1) % n].Point;
        if (!closed && index == 0) prev = node.Point;
        if (!closed && index == n - 1) next = node.Point;
        double dx = (next.X - prev.X) / 6, dy = (next.Y - prev.Y) / 6;
        if (dx == 0 && dy == 0) return result;
        result[index] = new CurveNode(node.Point, new PointD(node.Point.X - dx, node.Point.Y - dy), new PointD(node.Point.X + dx, node.Point.Y + dy));
        return result;
    }
}
