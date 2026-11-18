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
    Path,
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
/// <summary>How the border or line of a shape is drawn.</summary>
public enum StrokeStyle { Solid, Dashed, Dotted }

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
    IReadOnlyList<PointD>? Vertices = null,
    bool GradientBorder = false,
    StrokeStyle Dash = StrokeStyle.Solid,
    bool ArrowStart = false,
    bool ArrowEnd = false,
    double DashLength = 0,
    double DashGap = 0,
    IReadOnlyList<CurveNode>? Nodes = null,
    bool Closed = false,
    double Bend = 0,
    LineBendStyle BendStyle = LineBendStyle.Arc,
    PictureFill? Picture = null) : LayerObject(Box, FlipX, FlipY)
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

    /// <summary>True for shapes made of straight sides: dragging a corner moves just that corner of the outline.</summary>
    public bool HasStraightSides => ShapeVertices.IsPolygonal(Shape);

    /// <summary>Every shape can be reshaped (except a line): straight-sided shapes by their own corners, curved ones by the four corners of the box.</summary>
    public override bool CanReshape => !IsLineLike;

    /// <summary>True once the corners were dragged: the shape has a custom outline.</summary>
    public override bool HasCustomOutline => Vertices != null || Warp != null;

    /// <summary>The corner points now: the custom ones, or those of the normal outline (null for shapes with curved sides).</summary>
    public IReadOnlyList<PointD>? CurrentVertices => Vertices ?? ShapeVertices.For(this);

    public override IReadOnlyList<PointD>? ReshapePoints
        => !CanReshape ? null : HasStraightSides ? CurrentVertices : Warp ?? DefaultCorners;

    public override LayerObject WithReshapePoints(IReadOnlyList<PointD> points)
        => HasStraightSides ? this with { Vertices = points, CornerRadius = 0 } : this with { Warp = points };

    public override LayerObject WithoutReshape() => this with { Vertices = null, Warp = null };

    protected override IReadOnlyList<PointD>? ReshapeOutline => Vertices ?? Warp;
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
        ShapeKind.Path => "Curve",
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
