using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Filters;

/// <summary>Small helpers shared by neighborhood filters (emboss, edges...).</summary>
internal static class Convolution
{
    /// <summary>
    /// Runs <paramref name="compute"/> for every pixel with the brightness (0..255) of its 3x3
    /// neighborhood (edges are clamped). Alpha is kept.
    /// </summary>
    public static PixelBuffer MapLuma3x3(PixelBuffer source, Func<double[], (byte B, byte G, byte R)> compute,
        CancellationToken cancellation)
    {
        int w = source.Width, h = source.Height;
        var luma = new float[w * h];
        var s = source.Data;
        for (int i = 0, p = 0; p < luma.Length; i += 4, p++)
            luma[p] = (float)(0.114 * s[i] + 0.587 * s[i + 1] + 0.299 * s[i + 2]);

        var result = source.Clone();
        var d = result.Data;
        Parallel.For(0, h, new ParallelOptions { CancellationToken = cancellation }, () => new double[9], (y, _, n) =>
        {
            for (int x = 0; x < w; x++)
            {
                int k = 0;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = Math.Clamp(y + dy, 0, h - 1) * w;
                    for (int dx = -1; dx <= 1; dx++)
                        n[k++] = luma[yy + Math.Clamp(x + dx, 0, w - 1)];
                }
                var (b, g, r) = compute(n);
                int i = (y * w + x) * 4;
                d[i] = b; d[i + 1] = g; d[i + 2] = r;
            }
            return n;
        }, _ => { });
        return result;
    }
}
