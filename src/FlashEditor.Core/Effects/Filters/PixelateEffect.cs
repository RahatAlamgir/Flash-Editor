using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>Replaces each square block of pixels with its average color.</summary>
public sealed class PixelateEffect : ImageEffect
{
    public override string Name => "Pixelate";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("size", "Cell size", 2, 200, 10, 1, "px"),
    };

    public override int GetPadding(EffectValues values) => (int)values["size"];

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        int cell = Math.Max(1, (int)values["size"]);
        int w = source.Width, h = source.Height;
        var result = new PixelBuffer(w, h);
        int cellRows = (h + cell - 1) / cell;

        Parallel.For(0, cellRows, new ParallelOptions { CancellationToken = cancellation }, cy =>
        {
            int y0 = cy * cell, y1 = Math.Min(h, y0 + cell);
            for (int x0 = 0; x0 < w; x0 += cell)
            {
                int x1 = Math.Min(w, x0 + cell);
                double b = 0, g = 0, r = 0, a = 0;
                int count = 0;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        int i = source.IndexOf(x, y);
                        double pa = source.Data[i + 3];
                        b += source.Data[i] * pa; g += source.Data[i + 1] * pa; r += source.Data[i + 2] * pa;
                        a += pa;
                        count++;
                    }

                byte ob = 0, og = 0, or = 0, oa = 0;
                if (a > 0)
                {
                    ob = ColorMath.ClampByte(b / a); og = ColorMath.ClampByte(g / a); or = ColorMath.ClampByte(r / a);
                    oa = ColorMath.ClampByte(a / count);
                }
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        int i = result.IndexOf(x, y);
                        result.Data[i] = ob; result.Data[i + 1] = og; result.Data[i + 2] = or; result.Data[i + 3] = oa;
                    }
            }
        });
        return result;
    }
}
