using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>
/// Median filter: replaces each pixel with the middle value of its neighbors. Removes speckles
/// and grain while keeping edges sharper than a blur would.
/// </summary>
public sealed class ReduceNoiseEffect : ImageEffect
{
    public override string Name => "Reduce Noise";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("radius", "Radius", 1, 3, 1, 1, "px"),
        new EffectParameter("strength", "Strength", 0, 100, 100, 1, "%"),
    };

    public override int GetPadding(EffectValues values) => (int)values["radius"];

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        int radius = Math.Clamp((int)values["radius"], 1, 3);
        double strength = values["strength"] / 100.0;
        int w = source.Width, h = source.Height, size = (2 * radius + 1) * (2 * radius + 1);
        var s = source.Data;
        var result = source.Clone();
        var d = result.Data;

        Parallel.For(0, h, new ParallelOptions { CancellationToken = cancellation }, () => new byte[size], (y, _, window) =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                if (s[i + 3] == 0) continue;
                for (int c = 0; c < 3; c++)
                {
                    int n = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int row = Math.Clamp(y + dy, 0, h - 1) * w;
                        for (int dx = -radius; dx <= radius; dx++)
                            window[n++] = s[(row + Math.Clamp(x + dx, 0, w - 1)) * 4 + c];
                    }
                    Array.Sort(window);
                    byte median = window[size / 2];
                    d[i + c] = ColorMath.ClampByte(s[i + c] + (median - s[i + c]) * strength);
                }
            }
            return window;
        }, _ => { });
        return result;
    }
}
