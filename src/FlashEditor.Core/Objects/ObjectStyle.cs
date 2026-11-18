using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// A look that can be saved under a name and put on other objects: shadow, 3D, gradient and, for shapes and text, the colors.
/// Size, position, rotation, text and pictures are never part of it.
/// </summary>
/// <param name="Kind">What the look was taken from: "shape", "text", "image" or "group". Colors and the gradient only go onto the same kind of object.</param>
/// <param name="Fill">Fill color of a shape, or the color of a text.</param>
public sealed record ObjectStyle(
    string Kind,
    ShadowStyle? Shadow = null,
    ThreeDStyle? ThreeD = null,
    GradientStyle? Gradient = null,
    bool GradientBorder = false,
    ColorRgba? Fill = null,
    ColorRgba? Stroke = null,
    double StrokeWidth = 0,
    GlowStyle? Glow = null,
    InnerShadowStyle? InnerShadow = null,
    OutlineStyle? Outline = null)
{
    public static string KindOf(LayerObject obj) => obj switch
    {
        ShapeObject => "shape",
        TextObject => "text",
        ImageObject => "image",
        _ => "group",
    };

    /// <summary>The look of an object.</summary>
    public static ObjectStyle From(LayerObject obj) => obj switch
    {
        ShapeObject s => new ObjectStyle("shape", s.Shadow, s.ThreeD, s.Gradient, s.GradientBorder, s.Fill, s.Stroke, s.StrokeWidth, s.Glow, s.InnerShadow, s.Outline),
        TextObject t => new ObjectStyle("text", t.Shadow, t.ThreeD, t.Gradient, false, t.Color, Glow: t.Glow, InnerShadow: t.InnerShadow, Outline: t.Outline),
        ImageObject i => new ObjectStyle("image", i.Shadow, i.ThreeD, i.Gradient, Glow: i.Glow, InnerShadow: i.InnerShadow, Outline: i.Outline),
        _ => new ObjectStyle("group", obj.Shadow, obj.ThreeD, Glow: obj.Glow, InnerShadow: obj.InnerShadow, Outline: obj.Outline),
    };

    /// <summary>The object with this look. Shadow and 3D go onto anything; the rest only onto the same kind of object.</summary>
    public LayerObject ApplyTo(LayerObject obj)
    {
        var result = obj with { Shadow = Shadow, ThreeD = ThreeD, Glow = Glow, InnerShadow = InnerShadow, Outline = Outline };   // (brightness / blur are not part of a saved look)
        if (KindOf(obj) != Kind) return result;

        switch (result)
        {
            case ShapeObject shape:
                return shape with
                {
                    Gradient = Gradient,
                    GradientBorder = GradientBorder,
                    Fill = shape.CanFill ? Fill : shape.Fill,
                    Stroke = Stroke,
                    StrokeWidth = Stroke != null ? Math.Max(StrokeWidth, 1) : shape.StrokeWidth,
                };
            case TextObject text:
                return text with { Gradient = Gradient, Color = Fill ?? text.Color };
            case ImageObject image:
                return image with { Gradient = Gradient };
            default:
                return result;
        }
    }
}

/// <summary>A look with the name the user gave it.</summary>
public sealed record SavedStyle(string Name, ObjectStyle Style);
