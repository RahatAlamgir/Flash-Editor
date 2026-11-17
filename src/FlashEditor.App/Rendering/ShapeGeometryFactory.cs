using System.Windows;
using System.Windows.Media;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Rendering;

/// <summary>Vector outline of each shape kind, fitted into a box of width x height (top-left at 0,0).</summary>
public static class ShapeGeometryFactory
{
    public static Geometry Build(ShapeObject shape, double width, double height)
    {
        double w = Math.Max(1, width), h = Math.Max(1, height);
        // Straight-sided shapes are drawn from their corner points (the normal ones, or the ones the user dragged). Only a plain
        // rectangle keeps its own drawing, which has rounded corners.
        var corners = shape.Vertices ?? (shape.Shape == ShapeKind.Rectangle ? null : ShapeVertices.For(shape));
        Geometry geometry = corners != null
            ? Polygon(corners.Select(c => new Point(c.X * w, c.Y * h)).ToArray())
            : shape.Shape switch
        {
            ShapeKind.Rectangle => new RectangleGeometry(new Rect(0, 0, w, h), Radius(shape, w, h), Radius(shape, w, h)),
            ShapeKind.Ellipse => new EllipseGeometry(new Rect(0, 0, w, h)),
            ShapeKind.Heart => Heart(w, h),
            ShapeKind.SpeechBubble => SpeechBubble(w, h, Radius(shape, w, h * 0.78)),
            ShapeKind.Line => new LineGeometry(new Point(0, h / 2), new Point(w, h / 2)),
            ShapeKind.Ring => Ring(w, h, Inner(shape)),
            ShapeKind.HalfRing => Arc(w / 2, h, w / 2, h, 180, 180, Inner(shape)),
            ShapeKind.HalfCircle => Arc(w / 2, h, w / 2, h, 180, 180, 0),
            ShapeKind.QuarterCircle => Arc(0, h, w, h, 270, 90, 0),
            ShapeKind.Pie => Pie(w, h, Math.Clamp(shape.Inner, 0, 0.95), Math.Clamp(shape.Sweep, 10, 360)),
            ShapeKind.Crescent => Crescent(w, h, Inner(shape)),
            ShapeKind.Frame => Frame(w, h, Inner(shape), Radius(shape, w, h)),
            ShapeKind.Cloud => Cloud(w, h),
            ShapeKind.Teardrop => Teardrop(w, h),
            ShapeKind.Shield => Shield(w, h),
            ShapeKind.OvalBubble => OvalBubble(w, h),
            _ => new RectangleGeometry(new Rect(0, 0, w, h)),
        };
        geometry.Freeze();
        return geometry;
    }

    private static double Radius(ShapeObject s, double w, double h) => Math.Clamp(s.CornerRadius, 0, Math.Min(w, h) / 2);

    private static double Inner(ShapeObject s) => Math.Clamp(s.Inner, 0.05, 0.95);

    /// <summary>
    /// A slice of an ellipse: from <paramref name="startDegrees"/> clockwise for <paramref name="sweepDegrees"/> (0 = right,
    /// 90 = down, 270 = up). <paramref name="inner"/> 0 gives a pie slice (point at the center); above 0 a ring segment
    /// with that fraction of the outer size cut out.
    /// </summary>
    private static Geometry Arc(double cx, double cy, double rx, double ry, double startDegrees, double sweepDegrees, double inner)
    {
        double a0 = startDegrees * Math.PI / 180, a1 = (startDegrees + sweepDegrees) * Math.PI / 180;
        Point On(double radiusX, double radiusY, double a) => new(cx + Math.Cos(a) * radiusX, cy + Math.Sin(a) * radiusY);

        var figure = new PathFigure { StartPoint = On(rx, ry, a0), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new ArcSegment(On(rx, ry, a1), new Size(rx, ry), 0, sweepDegrees > 180, SweepDirection.Clockwise, true));
        if (inner > 0)
        {
            figure.Segments.Add(new LineSegment(On(rx * inner, ry * inner, a1), true));
            figure.Segments.Add(new ArcSegment(On(rx * inner, ry * inner, a0), new Size(rx * inner, ry * inner), 0, sweepDegrees > 180, SweepDirection.Counterclockwise, true));
        }
        else
        {
            figure.Segments.Add(new LineSegment(new Point(cx, cy), true));
        }
        return new PathGeometry(new[] { figure });
    }

    /// <summary>A pie (sector) or ring segment starting at the top and going clockwise; a full turn is an ellipse or a ring.</summary>
    private static Geometry Pie(double w, double h, double inner, double sweep)
    {
        if (sweep >= 359.5) return inner > 0.02 ? Ring(w, h, inner) : new EllipseGeometry(new Rect(0, 0, w, h));
        return Arc(w / 2, h / 2, w / 2, h / 2, -90, sweep, inner > 0.02 ? inner : 0);
    }

    private static Geometry Ring(double w, double h, double inner)
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new EllipseGeometry(new Rect(0, 0, w, h)));
        group.Children.Add(new EllipseGeometry(new Rect(w * (1 - inner) / 2, h * (1 - inner) / 2, w * inner, h * inner)));
        return group;
    }

    private static Geometry Frame(double w, double h, double inner, double radius)
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(new Rect(0, 0, w, h), radius, radius));
        double hw = w * inner, hh = h * inner, inset = Math.Min(radius, Math.Min(hw, hh) / 2) * 0.5;
        group.Children.Add(new RectangleGeometry(new Rect((w - hw) / 2, (h - hh) / 2, hw, hh), inset, inset));
        return group;
    }

    /// <summary>A full circle with a circle bitten out of it (a moon). <paramref name="bite"/> is how far the bite is shifted.</summary>
    private static Geometry Crescent(double w, double h, double bite)
    {
        var moon = new EllipseGeometry(new Rect(0, 0, w, h));
        var cut = new EllipseGeometry(new Rect(w * bite * 0.6, h * 0.04, w * 0.92, h * 0.92));
        return new CombinedGeometry(GeometryCombineMode.Exclude, moon, cut);
    }

    private static Geometry Cloud(double w, double h)
    {
        Geometry Part(Geometry g) => g;
        Geometry cloud = new RectangleGeometry(new Rect(w * 0.04, h * 0.5, w * 0.92, h * 0.46), h * 0.23, h * 0.23);
        foreach (var bump in new[]
                 {
                     new Rect(w * 0.02, h * 0.34, w * 0.42, h * 0.54),
                     new Rect(w * 0.22, h * 0.04, w * 0.54, h * 0.7),
                     new Rect(w * 0.52, h * 0.22, w * 0.46, h * 0.6),
                 })
            cloud = new CombinedGeometry(GeometryCombineMode.Union, Part(cloud), new EllipseGeometry(bump));
        return cloud;
    }

    /// <summary>A shield: flat top with a small dip, curving down to a point.</summary>
    private static Geometry Shield(double w, double h)
    {
        var figure = new PathFigure { StartPoint = new Point(0, h * 0.12), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new BezierSegment(new Point(w * 0.2, h * 0.12), new Point(w * 0.38, h * 0.08), new Point(w * 0.5, 0), true));
        figure.Segments.Add(new BezierSegment(new Point(w * 0.62, h * 0.08), new Point(w * 0.8, h * 0.12), new Point(w, h * 0.12), true));
        figure.Segments.Add(new LineSegment(new Point(w, h * 0.5), true));
        figure.Segments.Add(new BezierSegment(new Point(w, h * 0.78), new Point(w * 0.78, h * 0.92), new Point(w * 0.5, h), true));
        figure.Segments.Add(new BezierSegment(new Point(w * 0.22, h * 0.92), new Point(0, h * 0.78), new Point(0, h * 0.5), true));
        return new PathGeometry(new[] { figure });
    }

    /// <summary>A round speech bubble: an ellipse (the top 80% of the height) with a small tail at the bottom left.</summary>
    private static Geometry OvalBubble(double w, double h)
    {
        var body = new EllipseGeometry(new Rect(0, 0, w, h * 0.82));
        var tail = Polygon(new Point(w * 0.2, h * 0.7), new Point(w * 0.4, h * 0.78), new Point(w * 0.1, h));
        return new CombinedGeometry(GeometryCombineMode.Union, body, tail);
    }

    private static Geometry Teardrop(double w, double h)
    {
        var figure = new PathFigure { StartPoint = new Point(w / 2, 0), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new BezierSegment(new Point(w * 0.62, h * 0.22), new Point(w, h * 0.44), new Point(w, h * 0.68), true));
        figure.Segments.Add(new BezierSegment(new Point(w, h * 0.87), new Point(w * 0.78, h), new Point(w / 2, h), true));
        figure.Segments.Add(new BezierSegment(new Point(w * 0.22, h), new Point(0, h * 0.87), new Point(0, h * 0.68), true));
        figure.Segments.Add(new BezierSegment(new Point(0, h * 0.44), new Point(w * 0.38, h * 0.22), new Point(w / 2, 0), true));
        return new PathGeometry(new[] { figure });
    }

    private static Geometry Polygon(params Point[] points)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
        return new PathGeometry(new[] { figure });
    }

    private static Geometry Heart(double w, double h)
    {
        var figure = new PathFigure { StartPoint = new Point(w * 0.5, h), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new BezierSegment(new Point(w * 0.15, h * 0.72), new Point(0, h * 0.45), new Point(0, h * 0.28), true));
        figure.Segments.Add(new BezierSegment(new Point(0, h * 0.06), new Point(w * 0.18, 0), new Point(w * 0.29, 0), true));
        figure.Segments.Add(new BezierSegment(new Point(w * 0.40, 0), new Point(w * 0.48, h * 0.08), new Point(w * 0.5, h * 0.17), true));
        figure.Segments.Add(new BezierSegment(new Point(w * 0.52, h * 0.08), new Point(w * 0.60, 0), new Point(w * 0.71, 0), true));
        figure.Segments.Add(new BezierSegment(new Point(w * 0.82, 0), new Point(w, h * 0.06), new Point(w, h * 0.28), true));
        figure.Segments.Add(new BezierSegment(new Point(w, h * 0.45), new Point(w * 0.85, h * 0.72), new Point(w * 0.5, h), true));
        return new PathGeometry(new[] { figure });
    }

    /// <summary>Rounded box on top (78% of the height) with a tail at the bottom left.</summary>
    private static Geometry SpeechBubble(double w, double h, double radius)
    {
        double bodyH = h * 0.78;
        var body = new RectangleGeometry(new Rect(0, 0, w, bodyH), radius, radius);
        var tail = Polygon(new Point(w * 0.18, bodyH - 1), new Point(w * 0.40, bodyH - 1), new Point(w * 0.12, h));
        return new CombinedGeometry(GeometryCombineMode.Union, body, tail);
    }
}
