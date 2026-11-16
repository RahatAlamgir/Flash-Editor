using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>
/// "Make a blurry picture cleaner": removes a little noise, sharpens fine detail (ignoring tiny
/// differences, which are noise), and adds clarity (mid-size contrast). Works well on slightly soft,
/// out-of-focus or upscaled photos. It cannot invent detail that is completely gone.
/// </summary>
public sealed class EnhanceEffect : ImageEffect
{
    public override string Name => "Enhance (Clean Up Blur)";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("sharpen", "Sharpen", 0, 300, 120, 1, "%"),
        new EffectParameter("radius", "Detail size", 0.5, 5, 1.5, 0.1, "px"),
        new EffectParameter("clarity", "Clarity", 0, 100, 30, 1, "%"),
        new EffectParameter("denoise", "Noise reduction", 0, 100, 30, 1, "%"),
        new EffectParameter("threshold", "Ignore tiny details", 0, 30, 4),
    };

    private const double ClarityRadius = 20;

    public override int GetPadding(EffectValues values) => (int)Math.Ceiling(Math.Max(values["radius"], ClarityRadius) * 3) + 1;

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        double sharpen = values["sharpen"] / 100;
        double clarity = values["clarity"] / 100 * 0.6;
        double threshold = values["threshold"];

        // 1) Noise first, so sharpening does not make grain worse.
        var clean = values["denoise"] > 0
            ? new ReduceNoiseEffect().Apply(source, new EffectValues(new ReduceNoiseEffect().Parameters) { ["radius"] = 1, ["strength"] = values["denoise"] }, cancellation)
            : source.Clone();

        // 2) Fine detail (unsharp mask with a threshold) and 3) clarity (large-radius local contrast).
        var fine = sharpen > 0 ? Blur.Gaussian(clean, values["radius"], cancellation) : clean;
        var broad = clarity > 0 ? Blur.Gaussian(clean, ClarityRadius, cancellation) : clean;
        var result = clean.Clone();
        var c = clean.Data;
        var f = fine.Data;
        var g = broad.Data;
        var d = result.Data;
        int stride = result.Stride;

        Parallel.For(0, result.Height, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            int end = (y + 1) * stride;
            for (int i = y * stride; i < end; i += 4)
            {
                if (c[i + 3] == 0) continue;
                for (int k = 0; k < 3; k++)
                {
                    double v = c[i + k];
                    double detail = v - f[i + k];
                    double magnitude = Math.Abs(detail);
                    // Soft threshold: differences smaller than the threshold are treated as noise.
                    if (magnitude > threshold) v += Math.Sign(detail) * (magnitude - threshold) * sharpen;
                    v += (c[i + k] - g[i + k]) * clarity;
                    d[i + k] = ColorMath.ClampByte(v);
                }
            }
        });
        return result;
    }
}
