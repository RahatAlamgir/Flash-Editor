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
}

/// <summary>
/// A vector shape with an optional fill and an optional border.
/// </summary>
/// <param name="Fill">Fill color, or null for no fill.</param>
/// <param name="Stroke">Border color, or null for no border.</param>
/// <param name="StrokeWidth">Border thickness in pixels (for a line: its thickness).</param>
/// <param name="CornerRadius">Rounded corners for rectangles and speech bubbles (pixels).</param>
/// <param name="Points">Number of points (star) or sides (polygon).</param>
public sealed record ShapeObject(
    ObjectBox Box,
    ShapeKind Shape,
    ColorRgba? Fill,
    ColorRgba? Stroke,
    double StrokeWidth,
    double CornerRadius = 0,
    int Points = 5,
    bool FlipX = false,
    bool FlipY = false) : LayerObject(Box, FlipX, FlipY)
{
    public override string KindName => "Shape";

    public override string SuggestedLayerName => Shape switch
    {
        ShapeKind.SpeechBubble => "Speech bubble",
        _ => Shape.ToString(),
    };

    public override bool KeepsAspectByDefault => false;
    public override bool IsLineLike => Shape == ShapeKind.Line;

    public bool HasCorners => Shape is ShapeKind.Rectangle or ShapeKind.SpeechBubble;
    public bool HasPoints => Shape is ShapeKind.Star or ShapeKind.Polygon;
    public bool CanFill => Shape != ShapeKind.Line;
}
