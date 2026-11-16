using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>Darkens (or lightens) the corners to draw the eye to the center.</summary>
public sealed class VignetteEffect : ImageEffect
{
    public override string Name => "Vignette";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("amount", "Amount (- lighten / + darken)", -100, 100, 50),
        new EffectParameter("size", "Clear center size", 0, 100, 50, 1, "%"),
        new EffectParameter("softness", "Softness", 1, 100, 60, 1, "%"),
    };

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        double amount = values["amount"] / 100.0;
        double inner = values["size"] / 100.0 * 0.9;
        double softness = Math.Max(0.01, values["softness"] / 100.0);
        double cx = source.Width / 2.0, cy = source.Height / 2.0;

        var result = source.Clone();
        var d = result.Data;
        Parallel.For(0, source.Height, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            double ny = (y + 0.5 - cy) / cy;
            for (int x = 0; x < source.Width; x++)
            {
                int i = result.IndexOf(x, y);
                if (d[i + 3] == 0) continue;
                double nx = (x + 0.5 - cx) / cx;
                double distance = Math.Sqrt((nx * nx + ny * ny) / 2); // 0 center .. 1 corner
                double t = Math.Clamp((distance - inner) / softness, 0, 1);
                t = t * t * (3 - 2 * t); // smooth step
                double k = t * amount;
                for (int c = 0; c < 3; c++)
                    d[i + c] = ColorMath.ClampByte(k >= 0 ? d[i + c] * (1 - k) : d[i + c] + (255 - d[i + c]) * -k);
            }
        });
        return result;
    }
}
