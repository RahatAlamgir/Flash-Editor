using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// A drop shadow behind an object (shape, text, image or group). The shadow is the object's own outline,
/// blurred, tinted and shifted, so it follows the real shape (a heart casts a heart-shaped shadow).
/// Like in Canva the direction is on the canvas: rotating the object does not turn its shadow.
/// </summary>
/// <param name="Color">Shadow color; its alpha is the shadow's strength.</param>
/// <param name="Distance">How far the shadow is shifted, in pixels.</param>
/// <param name="Angle">Direction of the shift in degrees: 0 = right, 90 = down, 180 = left, -90 = up.</param>
/// <param name="Blur">How soft the edge is, in pixels (0 = sharp).</param>
/// <param name="Size">How big the shadow is compared to the object: 1 = same size, 0.5 = half (small), 2 = twice (big). It grows or shrinks around the middle of the object.</param>
public sealed record ShadowStyle(ColorRgba Color, double Distance = 8, double Angle = 45, double Blur = 12, double Size = 1.0)
{
    /// <summary>What "add shadow" starts with: soft, dark and a little to the bottom right.</summary>
    public static ShadowStyle Default { get; } = new(new ColorRgba(0, 0, 0, 115));

    public double OffsetX => Distance * Math.Cos(Angle * Math.PI / 180);
    public double OffsetY => Distance * Math.Sin(Angle * Math.PI / 180);

    /// <summary>How far the shadow can reach beyond the object's outline, in pixels.</summary>
    public double Extent => Math.Abs(Distance) + Math.Max(0, Blur) * 2 + 2;

    /// <summary>The same, for an object of the given size: a bigger shadow also reaches further out of the object.</summary>
    public double ExtentFor(double objectWidth, double objectHeight)
        => Extent + Math.Max(0, Size - 1) * Math.Max(objectWidth, objectHeight) / 2;

    /// <summary>The same shadow for an object that was scaled by <paramref name="factor"/>.</summary>
    public ShadowStyle Scaled(double factor) => this with { Distance = Distance * factor, Blur = Blur * factor };

    /// <summary>The same shadow after the whole picture was turned by <paramref name="degrees"/> (clockwise).</summary>
    public ShadowStyle Rotated(double degrees) => this with { Angle = ObjectBox.NormalizeAngle(Angle + degrees) };
}
