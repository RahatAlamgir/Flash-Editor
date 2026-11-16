using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>Blurs along a straight line, like a fast-moving camera or subject.</summary>
public sealed class MotionBlurEffect : ImageEffect
{
    public override string Name => "Motion Blur";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("angle", "Angle", -180, 180, 0, 1, "°"),
        new EffectParameter("distance", "Distance", 1, 200, 20, 1, "px"),
    };

    public override int GetPadding(EffectValues values) => (int)Math.Ceiling(values["distance"] / 2) + 1;

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        double angle = values["angle"] * Math.PI / 180;
        double distance = values["distance"];
        int samples = Math.Max(2, (int)Math.Ceiling(distance));
        double stepX = Math.Cos(angle) * distance / (samples - 1);
        double stepY = -Math.Sin(angle) * distance / (samples - 1);
        int w = source.Width, h = source.Height;
        var s = source.Data;
        var result = new PixelBuffer(w, h);
        var d = result.Data;

        Parallel.For(0, h, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double b = 0, g = 0, r = 0, a = 0;
                for (int k = 0; k < samples; k++)
                {
                    double t = k - (samples - 1) / 2.0;
                    int sx = Math.Clamp((int)Math.Round(x + t * stepX), 0, w - 1);
                    int sy = Math.Clamp((int)Math.Round(y + t * stepY), 0, h - 1);
                    int si = (sy * w + sx) * 4;
                    double pa = s[si + 3];
                    b += s[si] * pa; g += s[si + 1] * pa; r += s[si + 2] * pa; a += pa;
                }
                int di = (y * w + x) * 4;
                if (a <= 0) continue;
                d[di] = ColorMath.ClampByte(b / a);
                d[di + 1] = ColorMath.ClampByte(g / a);
                d[di + 2] = ColorMath.ClampByte(r / a);
                d[di + 3] = ColorMath.ClampByte(a / samples);
            }
        });
        return result;
    }
}
