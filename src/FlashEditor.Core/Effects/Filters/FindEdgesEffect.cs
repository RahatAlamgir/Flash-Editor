using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>Sobel edge detection: outlines like a pencil sketch (or bright lines on black).</summary>
public sealed class FindEdgesEffect : ImageEffect
{
    public override string Name => "Find Edges";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("strength", "Strength", 0.2, 4, 1, 0.1),
        EffectParameter.Toggle("sketch", "Dark lines on white (sketch)", true),
    };

    public override int GetPadding(EffectValues values) => 1;

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        double strength = values["strength"];
        bool sketch = values["sketch"] >= 0.5;
        return Convolution.MapLuma3x3(source, n =>
        {
            double gx = (n[2] + 2 * n[5] + n[8]) - (n[0] + 2 * n[3] + n[6]);
            double gy = (n[6] + 2 * n[7] + n[8]) - (n[0] + 2 * n[1] + n[2]);
            double magnitude = Math.Min(255, Math.Sqrt(gx * gx + gy * gy) * strength / 2);
            byte v = ColorMath.ClampByte(sketch ? 255 - magnitude : magnitude);
            return (v, v, v);
        }, cancellation);
    }
}
