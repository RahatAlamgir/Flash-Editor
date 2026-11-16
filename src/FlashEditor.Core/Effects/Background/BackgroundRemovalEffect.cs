using FlashEditor.Core.Effects.Filters;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects.Background;

/// <summary>
/// Removes the background by finding the main colors along the image border and flood-filling
/// inwards through pixels similar to them. Works best for subjects on a fairly plain background
/// (product photos, logos, portraits on studio backgrounds). Areas of the subject that touch the
/// border with the same color as the background may also be removed; lower the tolerance then.
/// </summary>
public sealed class BackgroundRemovalEffect : ImageEffect
{
    public override string Name => "Remove Background";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("tolerance", "Tolerance", 1, 100, 25, 1, "%"),
        new EffectParameter("softness", "Edge softness", 0, 100, 40, 1, "%"),
        new EffectParameter("feather", "Feather", 0, 10, 1, 0.5, "px"),
    };

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        int w = source.Width, h = source.Height;
        var background = DetectBorderColors(source);
        if (background.Count == 0) return source.Clone(); // border already transparent

        double tolerance = values["tolerance"] * 2.2;                     // in RGB distance units (0..441)
        double softZone = tolerance * (values["softness"] / 100.0) + 1;    // partial-transparency band
        double feather = values["feather"];

        // 1) Distance of every pixel to the closest background color.
        var distance = new float[w * h];
        Parallel.For(0, h, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = source.IndexOf(x, y);
                distance[y * w + x] = source.Data[i + 3] < 8 ? 0f : (float)Nearest(source.Data, i, background);
            }
        });

        // 2) Flood fill from the border through background-like pixels.
        var removed = new float[w * h]; // 1 = background, 0 = keep, between = partially transparent
        var queue = new Queue<int>();
        void TrySeed(int x, int y)
        {
            int p = y * w + x;
            if (removed[p] == 0 && distance[p] <= tolerance) { removed[p] = 1; queue.Enqueue(p); }
        }
        for (int x = 0; x < w; x++) { TrySeed(x, 0); TrySeed(x, h - 1); }
        for (int y = 0; y < h; y++) { TrySeed(0, y); TrySeed(w - 1, y); }

        while (queue.Count > 0)
        {
            int p = queue.Dequeue();
            int px = p % w, py = p / w;
            Visit(px - 1, py); Visit(px + 1, py); Visit(px, py - 1); Visit(px, py + 1);
        }

        void Visit(int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int q = y * w + x;
            if (removed[q] != 0) return;
            float d = distance[q];
            if (d <= tolerance)
            {
                removed[q] = 1;
                queue.Enqueue(q);
            }
            else if (d <= tolerance + softZone)
            {
                // Edge pixel next to the background: partially transparent (anti-aliased cut-out).
                removed[q] = (float)(1 - (d - tolerance) / softZone);
            }
        }
        cancellation.ThrowIfCancellationRequested();

        // 3) Optional feathering of the cut-out edge.
        if (feather > 0) Blur.GaussianInPlace(removed, w, h, 1, feather, cancellation);

        // 4) Apply as transparency.
        var result = source.Clone();
        Parallel.For(0, h, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                float r = removed[y * w + x];
                if (r <= 0) continue;
                int i = result.IndexOf(x, y);
                result.Data[i + 3] = ColorMath.ClampByte(result.Data[i + 3] * (1 - Math.Min(1f, r)));
            }
        });
        return result;
    }

    /// <summary>
    /// Finds up to 6 dominant background colors along the image border. A color only counts as
    /// background when it shows up on at least two sides: a subject that touches one edge (a person
    /// cut off at the bottom) must not be mistaken for background.
    /// </summary>
    public static IReadOnlyList<(double R, double G, double B)> DetectBorderColors(PixelBuffer image)
    {
        int w = image.Width, h = image.Height;
        var buckets = new Dictionary<int, (double R, double G, double B, int Count, int[] PerSide)>();
        var sideTotals = new int[4];
        int total = 0;

        void Add(int x, int y, int side)
        {
            int i = image.IndexOf(x, y);
            if (image.Data[i + 3] < 128) return;
            byte b = image.Data[i], g = image.Data[i + 1], r = image.Data[i + 2];
            int key = (r >> 4) << 8 | (g >> 4) << 4 | (b >> 4); // 16 levels per channel
            if (!buckets.TryGetValue(key, out var e)) e = (0, 0, 0, 0, new int[4]);
            e.PerSide[side]++;
            buckets[key] = (e.R + r, e.G + g, e.B + b, e.Count + 1, e.PerSide);
            sideTotals[side]++;
            total++;
        }

        for (int x = 0; x < w; x++) { Add(x, 0, 0); if (h > 1) Add(x, h - 1, 2); }
        for (int y = 1; y < h - 1; y++) { Add(0, y, 3); if (w > 1) Add(w - 1, y, 1); }
        if (total == 0) return Array.Empty<(double, double, double)>();

        // On how many sides does this color cover a noticeable share (3%) of that side?
        int Sides(int[] perSide) => Enumerable.Range(0, 4).Count(s => sideTotals[s] > 0 && perSide[s] >= sideTotals[s] * 0.03);
        int sidesWithPixels = sideTotals.Count(t => t > 0);
        int requiredSides = Math.Min(2, sidesWithPixels);

        var result = buckets.Values
            .Where(e => e.Count >= total * 0.02 && Sides(e.PerSide) >= requiredSides)
            .OrderByDescending(e => e.Count)
            .Take(6)
            .Select(e => (e.R / e.Count, e.G / e.Count, e.B / e.Count))
            .ToList();

        if (result.Count == 0) // very noisy border: use its overall average
        {
            var all = buckets.Values.Aggregate((R: 0.0, G: 0.0, B: 0.0, C: 0),
                (acc, e) => (acc.R + e.R, acc.G + e.G, acc.B + e.B, acc.C + e.Count));
            result.Add((all.R / all.C, all.G / all.C, all.B / all.C));
        }
        return result;
    }

    private static double Nearest(byte[] data, int i, IReadOnlyList<(double R, double G, double B)> colors)
    {
        double best = double.MaxValue;
        foreach (var (r, g, b) in colors)
        {
            double dr = data[i + 2] - r, dg = data[i + 1] - g, db = data[i] - b;
            double d = dr * dr + dg * dg + db * db;
            if (d < best) best = d;
        }
        return Math.Sqrt(best);
    }
}
