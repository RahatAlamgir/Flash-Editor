using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>A soft light around an object (any kind of object can have one). It follows the real outline of the object.</summary>
/// <param name="Color">Glow color; its alpha is the strength of the light.</param>
/// <param name="Size">How far the glow reaches from the edge, in pixels.</param>
/// <param name="Intensity">How strong the glow is near the edge, in percent (100 = normal, more = brighter and wider looking).</param>
public sealed record GlowStyle(ColorRgba Color, double Size = 14, double Intensity = 100)
{
    /// <summary>What "add glow" starts with: a warm yellow light.</summary>
    public static GlowStyle Default { get; } = new(new ColorRgba(255, 214, 90, 235));

    /// <summary>How far the glow can reach beyond the object's outline, in pixels.</summary>
    public double Extent => Math.Max(0, Size) * 2 + 2;

    public GlowStyle Scaled(double factor) => this with { Size = Size * factor };
}

/// <summary>A shadow that falls inside the edges of an object, as if the object were hollowed out.</summary>
/// <param name="Color">Shadow color; its alpha is the strength.</param>
/// <param name="Distance">How far the shadow is shifted, in pixels.</param>
/// <param name="Angle">Direction the shadow is shifted to: 0 = right, 90 = down. The shadow shows on the opposite edges.</param>
/// <param name="Blur">How soft the shadow is, in pixels.</param>
public sealed record InnerShadowStyle(ColorRgba Color, double Distance = 6, double Angle = 45, double Blur = 10)
{
    public static InnerShadowStyle Default { get; } = new(new ColorRgba(0, 0, 0, 150));

    public double OffsetX => Distance * Math.Cos(Angle * Math.PI / 180);
    public double OffsetY => Distance * Math.Sin(Angle * Math.PI / 180);

    public InnerShadowStyle Scaled(double factor) => this with { Distance = Distance * factor, Blur = Blur * factor };

    public InnerShadowStyle Rotated(double degrees) => this with { Angle = ObjectBox.NormalizeAngle(Angle + degrees) };
}

/// <summary>A line around the outside of an object, following its real outline (also useful for pictures and text).</summary>
/// <param name="Color">Line color.</param>
/// <param name="Width">Thickness in pixels.</param>
public sealed record OutlineStyle(ColorRgba Color, double Width = 4)
{
    public static OutlineStyle Default { get; } = new(new ColorRgba(255, 255, 255, 255));

    /// <summary>How far the line reaches beyond the object, in pixels.</summary>
    public double Extent => Math.Max(0, Width) + 2;

    public OutlineStyle Scaled(double factor) => this with { Width = Width * factor };
}
