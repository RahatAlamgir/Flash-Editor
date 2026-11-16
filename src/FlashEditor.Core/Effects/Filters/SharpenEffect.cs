using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>Unsharp mask: boosts the difference between the image and a blurred copy of it.</summary>
public sealed class SharpenEffect : ImageEffect
{
    public override string Name => "Sharpen";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("amount", "Amount", 0, 500, 100, 1, "%"),
        new EffectParameter("radius", "Radius", 0.5, 10, 1, 0.1, "px"),
    };

    public override int GetPadding(EffectValues values) => (int)Math.Ceiling(values["radius"] * 3);

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        double amount = values["amount"] / 100.0;
        var blurred = Blur.Gaussian(source, values["radius"], cancellation);
        var result = source.Clone();
        var s = source.Data;
        var bl = blurred.Data;
        var d = result.Data;
        int stride = source.Stride;

        Parallel.For(0, source.Height, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            int end = (y + 1) * stride;
            for (int i = y * stride; i < end; i += 4)
            {
                if (s[i + 3] == 0) continue;
                d[i] = ColorMath.ClampByte(s[i] + (s[i] - bl[i]) * amount);
                d[i + 1] = ColorMath.ClampByte(s[i + 1] + (s[i + 1] - bl[i + 1]) * amount);
                d[i + 2] = ColorMath.ClampByte(s[i + 2] + (s[i + 2] - bl[i + 2]) * amount);
            }
        });
        return result;
    }
}
