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
        Geometry geometry = shape.Shape switch
        {
            ShapeKind.Rectangle => new RectangleGeometry(new Rect(0, 0, w, h), Radius(shape, w, h), Radius(shape, w, h)),
            ShapeKind.Ellipse => new EllipseGeometry(new Rect(0, 0, w, h)),
            ShapeKind.Triangle => Polygon(new Point(w / 2, 0), new Point(w, h), new Point(0, h)),
            ShapeKind.Star => Star(w, h, Math.Clamp(shape.Points, 3, 24), 0.45),
            ShapeKind.Polygon => Star(w, h, Math.Clamp(shape.Points, 3, 24), 1.0),
            ShapeKind.Heart => Heart(w, h),
            ShapeKind.Arrow => Arrow(w, h),
            ShapeKind.SpeechBubble => SpeechBubble(w, h, Radius(shape, w, h * 0.78)),
            ShapeKind.Line => new LineGeometry(new Point(0, h / 2), new Point(w, h / 2)),
            _ => new RectangleGeometry(new Rect(0, 0, w, h)),
        };
        geometry.Freeze();
        return geometry;
    }

    private static double Radius(ShapeObject s, double w, double h) => Math.Clamp(s.CornerRadius, 0, Math.Min(w, h) / 2);

    private static Geometry Polygon(params Point[] points)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
        return new PathGeometry(new[] { figure });
    }

    /// <summary>Star (innerRatio &lt; 1) or regular polygon (innerRatio = 1), first point at the top.</summary>
    private static Geometry Star(double w, double h, int points, double innerRatio)
    {
        bool isPolygon = innerRatio >= 1;
        int count = isPolygon ? points : points * 2;
        var list = new Point[count];
        for (int i = 0; i < count; i++)
        {
            double r = isPolygon || i % 2 == 0 ? 1 : innerRatio;
            double a = -Math.PI / 2 + 2 * Math.PI * i / count;
            list[i] = new Point(w / 2 + Math.Cos(a) * w / 2 * r, h / 2 + Math.Sin(a) * h / 2 * r);
        }
        return Polygon(list);
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

    /// <summary>Block arrow pointing right.</summary>
    private static Geometry Arrow(double w, double h)
    {
        double head = Math.Min(w * 0.45, h * 0.9);
        double shaftTop = h * 0.3, shaftBottom = h * 0.7;
        return Polygon(
            new Point(0, shaftTop), new Point(w - head, shaftTop), new Point(w - head, 0),
            new Point(w, h / 2),
            new Point(w - head, h), new Point(w - head, shaftBottom), new Point(0, shaftBottom));
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
