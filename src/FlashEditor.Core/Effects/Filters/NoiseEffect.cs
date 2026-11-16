using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>Adds random grain. Uses a fixed seed per instance so the live preview doesn't flicker.</summary>
public sealed class NoiseEffect : ImageEffect
{
    private readonly int _seed = Random.Shared.Next();

    public override string Name => "Add Noise";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("amount", "Amount", 0, 100, 20, 1, "%"),
        EffectParameter.Toggle("mono", "Monochromatic", true),
    };

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        double sigma = values["amount"] * 0.8; // 100% ≈ very strong grain
        bool mono = values.GetBool("mono");
        var result = source.Clone();
        var d = result.Data;
        int stride = result.Stride;

        Parallel.For(0, result.Height, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            var random = new Random(HashCode.Combine(_seed, y)); // deterministic per row
            int end = (y + 1) * stride;
            for (int i = y * stride; i < end; i += 4)
            {
                if (d[i + 3] == 0) continue;
                if (mono)
                {
                    double n = Gaussian(random) * sigma;
                    d[i] = ColorMath.ClampByte(d[i] + n);
                    d[i + 1] = ColorMath.ClampByte(d[i + 1] + n);
                    d[i + 2] = ColorMath.ClampByte(d[i + 2] + n);
                }
                else
                {
                    d[i] = ColorMath.ClampByte(d[i] + Gaussian(random) * sigma);
                    d[i + 1] = ColorMath.ClampByte(d[i + 1] + Gaussian(random) * sigma);
                    d[i + 2] = ColorMath.ClampByte(d[i + 2] + Gaussian(random) * sigma);
                }
            }
        });
        return result;
    }

    /// <summary>Normally-distributed random number (Box-Muller).</summary>
    private static double Gaussian(Random random)
    {
        double u1 = 1.0 - random.NextDouble();
        double u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
