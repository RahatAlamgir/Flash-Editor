using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

public enum ShapeKind
{
    Rectangle,
    Ellipse,
    Triangle,
    Star,
    Polygon,
    Heart,
    Arrow,
    SpeechBubble,
    Line,

    // Added later (kept at the end so existing values do not change)
    Ring,
    HalfRing,
    HalfCircle,
    QuarterCircle,
    Pie,
    Crescent,
    Diamond,
    RightTriangle,
    Trapezoid,
    Parallelogram,
    Cross,
    Frame,
    Chevron,
    Lightning,
    Cloud,
    Teardrop,
    Pentagon,
    Hexagon,
    Octagon,
    Sparkle,
    Burst,
    DoubleArrow,
    Check,
    XMark,
    Home,
    Tag,
    Shield,
    OvalBubble,
}

/// <summary>
/// A vector shape with an optional fill and an optional border.
/// </summary>
/// <param name="Fill">Fill color, or null for no fill.</param>
/// <param name="Stroke">Border color, or null for no border.</param>
/// <param name="StrokeWidth">Border thickness in pixels (for a line: its thickness).</param>
/// <param name="CornerRadius">Rounded corners for rectangles, frames and speech bubbles (pixels).</param>
/// <param name="Points">Number of points (star) or sides (polygon).</param>
/// <param name="Inner">
/// 0..1. For ring-like shapes (ring, half ring, pie, frame) the size of the hole compared to the shape; for a
/// crescent how far the bite is shifted; for a cross how thick its arms are.
/// </param>
/// <param name="Sweep">Angle in degrees (10..360) a pie covers.</param>
/// <param name="Vertices">
/// Custom corner points, in the box (0,0 = top-left corner of the box, 1,1 = bottom-right), set when the user dragged the
/// corners of a straight-sided shape (reshape). Null = the normal outline of <see cref="Shape"/>.
/// </param>
public sealed record ShapeObject(
    ObjectBox Box,
    ShapeKind Shape,
    ColorRgba? Fill,
    ColorRgba? Stroke,
    double StrokeWidth,
    double CornerRadius = 0,
    int Points = 5,
    bool FlipX = false,
    bool FlipY = false,
    double Inner = 0.5,
    double Sweep = 270,
    IReadOnlyList<PointD>? Vertices = null) : LayerObject(Box, FlipX, FlipY)
{
    public override string KindName => "Shape";

    public override string SuggestedLayerName => ShapeNames.Of(Shape);

    public override bool KeepsAspectByDefault => false;
    public override bool IsLineLike => Shape == ShapeKind.Line;

    // The settings below describe the normal outline; once the corners were dragged, the outline is custom and they do not apply.
    public bool HasCorners => Vertices == null && Shape is ShapeKind.Rectangle or ShapeKind.SpeechBubble or ShapeKind.Frame;
    public bool HasPoints => Vertices == null && Shape is ShapeKind.Star or ShapeKind.Polygon;
    public bool HasInner => Vertices == null && ShapeNames.HasInner(Shape);
    public bool HasSweep => Vertices == null && Shape == ShapeKind.Pie;

    /// <summary>True for shapes made of straight sides: their corner points can be dragged (reshape).</summary>
    public bool CanReshape => ShapeVertices.IsPolygonal(Shape);

    /// <summary>True once the corners were dragged: the shape has a custom outline.</summary>
    public bool HasCustomOutline => Vertices != null;

    /// <summary>A point of the canvas as a position in the shape (0,0 = top-left of its box, 1,1 = bottom-right), however it is turned or mirrored.</summary>
    public PointD ToUnit(PointD canvasPoint)
    {
        var local = Box.ToLocal(canvasPoint);
        double x = FlipX ? -local.X : local.X, y = FlipY ? -local.Y : local.Y;
        return new PointD(x / Math.Max(1e-9, Box.Width) + 0.5, y / Math.Max(1e-9, Box.Height) + 0.5);
    }

    /// <summary>Where a position in the shape (0..1 in both directions) is on the canvas.</summary>
    public PointD FromUnit(PointD unit)
    {
        double x = (unit.X - 0.5) * Box.Width * (FlipX ? -1 : 1);
        double y = (unit.Y - 0.5) * Box.Height * (FlipY ? -1 : 1);
        return Box.ToCanvas(x, y);
    }

    /// <summary>The corner points now: the custom ones, or those of the normal outline (null for shapes with curved sides).</summary>
    public IReadOnlyList<PointD>? CurrentVertices => Vertices ?? ShapeVertices.For(this);
    public bool CanFill => Shape != ShapeKind.Line;
}

/// <summary>Names (and which extra settings) of the shapes, for the shape gallery and layer names.</summary>
public static class ShapeNames
{
    public static string Of(ShapeKind kind) => kind switch
    {
        ShapeKind.SpeechBubble => "Speech bubble",
        ShapeKind.HalfRing => "Half ring",
        ShapeKind.HalfCircle => "Half circle",
        ShapeKind.QuarterCircle => "Quarter circle",
        ShapeKind.RightTriangle => "Right triangle",
        ShapeKind.Lightning => "Lightning bolt",
        ShapeKind.DoubleArrow => "Double arrow",
        ShapeKind.XMark => "X mark",
        ShapeKind.OvalBubble => "Oval bubble",
        _ => kind.ToString(),
    };

    /// <summary>Shapes that use the "Inner" setting.</summary>
    public static bool HasInner(ShapeKind kind) => kind is ShapeKind.Ring or ShapeKind.HalfRing or ShapeKind.Pie
        or ShapeKind.Crescent or ShapeKind.Cross or ShapeKind.Frame;

    /// <summary>What the "Inner" setting means for this shape, for the label next to its slider.</summary>
    public static string InnerLabel(ShapeKind kind) => kind switch
    {
        ShapeKind.Crescent => "Bite",
        ShapeKind.Cross => "Arms",
        _ => "Hole",
    };
}
