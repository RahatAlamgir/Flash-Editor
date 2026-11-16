using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Adjustments;

/// <summary>
/// Auto Levels: stretches each color channel so its darkest values become black and brightest
/// become white (ignoring the extreme 0.5%). Fixes flat, hazy or color-cast photos in one click.
/// </summary>
public sealed class AutoContrastEffect : ImageEffect
{
    public override string Name => "Auto Levels";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("strength", "Strength", 0, 100, 100, 1, "%"),
        EffectParameter.Toggle("perChannel", "Also fix color cast (each channel separately)", true),
    };

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        double strength = values["strength"] / 100.0;
        bool perChannel = values["perChannel"] >= 0.5;
        var data = source.Data;

        // Histograms of the visible pixels.
        var hist = new long[3, 256];
        long count = 0;
        for (int i = 0; i < data.Length; i += 4)
        {
            if (data[i + 3] == 0) continue;
            hist[0, data[i]]++; hist[1, data[i + 1]]++; hist[2, data[i + 2]]++;
            count++;
        }
        var result = source.Clone();
        if (count == 0) return result;

        long clip = (long)(count * 0.005);
        var luts = new byte[3][];
        (int Low, int High)[] ranges = new (int, int)[3];
        for (int c = 0; c < 3; c++) ranges[c] = FindRange(hist, c, clip);
        if (!perChannel)
        {
            int low = ranges.Min(r => r.Low), high = ranges.Max(r => r.High);
            for (int c = 0; c < 3; c++) ranges[c] = (low, high);
        }

        for (int c = 0; c < 3; c++)
        {
            var (low, high) = ranges[c];
            double span = Math.Max(1, high - low);
            luts[c] = new byte[256];
            for (int v = 0; v < 256; v++)
            {
                double stretched = (v - low) * 255.0 / span;
                luts[c][v] = ColorMath.ClampByte(v + (stretched - v) * strength);
            }
        }

        var d = result.Data;
        int stride = result.Stride;
        Parallel.For(0, result.Height, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            int end = (y + 1) * stride;
            for (int i = y * stride; i < end; i += 4)
            {
                if (d[i + 3] == 0) continue;
                d[i] = luts[0][d[i]]; d[i + 1] = luts[1][d[i + 1]]; d[i + 2] = luts[2][d[i + 2]];
            }
        });
        return result;
    }

    private static (int Low, int High) FindRange(long[,] hist, int channel, long clip)
    {
        int low = 0, high = 255;
        long sum = 0;
        for (; low < 255; low++) { sum += hist[channel, low]; if (sum > clip) break; }
        sum = 0;
        for (; high > 0; high--) { sum += hist[channel, high]; if (sum > clip) break; }
        return high <= low ? (0, 255) : (low, high);
    }
}
