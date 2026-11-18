namespace FlashEditor.Core.Imaging;

/// <summary>Finds the main colors of a picture (for "Add colors from this picture"). Plain k-means on a small copy: no AI model.</summary>
public static class PaletteExtractor
{
    /// <summary>
    /// The <paramref name="count"/> most typical colors, the most common first. Nearly transparent pixels are ignored.
    /// Colors that are almost the same are merged, so fewer than <paramref name="count"/> may come back for a plain picture.
    /// </summary>
    public static IReadOnlyList<ColorRgba> Extract(PixelBuffer image, int count = 5)
    {
        count = Math.Clamp(count, 1, 16);

        // A sample of at most ~4000 pixels, spread over the whole picture.
        var samples = new List<(float R, float G, float B)>();
        int total = image.Width * image.Height;
        int step = Math.Max(1, (int)Math.Sqrt(total / 4000.0));
        for (int y = 0; y < image.Height; y += step)
            for (int x = 0; x < image.Width; x += step)
            {
                int i = image.IndexOf(x, y);
                if (image.Data[i + 3] < 128) continue;
                samples.Add((image.Data[i + 2], image.Data[i + 1], image.Data[i]));
            }
        if (samples.Count == 0) return Array.Empty<ColorRgba>();

        // Start from colors spread through the (sorted by brightness) samples, then let them settle.
        var ordered = samples.OrderBy(s => s.R + s.G + s.B).ToList();
        int k = Math.Min(count, ordered.Count);
        var centers = Enumerable.Range(0, k).Select(c => ordered[(int)((c + 0.5) * ordered.Count / k)]).ToArray();
        var assignment = new int[samples.Count];

        for (int round = 0; round < 12; round++)
        {
            bool changed = false;
            for (int s = 0; s < samples.Count; s++)
            {
                int best = 0;
                double bestDistance = double.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    double d = Distance(samples[s], centers[c]);
                    if (d < bestDistance) { bestDistance = d; best = c; }
                }
                if (assignment[s] != best || round == 0) { assignment[s] = best; changed = true; }
            }
            var sums = new (double R, double G, double B, int N)[k];
            for (int s = 0; s < samples.Count; s++)
            {
                var (r, g, b) = samples[s];
                ref var sum = ref sums[assignment[s]];
                sum.R += r; sum.G += g; sum.B += b; sum.N++;
            }
            for (int c = 0; c < k; c++)
                if (sums[c].N > 0) centers[c] = ((float)(sums[c].R / sums[c].N), (float)(sums[c].G / sums[c].N), (float)(sums[c].B / sums[c].N));
            if (!changed) break;
        }

        var sizes = new int[k];
        foreach (int a in assignment) sizes[a]++;
        var result = new List<ColorRgba>();
        foreach (int c in Enumerable.Range(0, k).Where(c => sizes[c] > 0).OrderByDescending(c => sizes[c]))
        {
            var color = new ColorRgba((byte)Math.Round(centers[c].R), (byte)Math.Round(centers[c].G), (byte)Math.Round(centers[c].B));
            if (result.All(r => Distance((r.R, r.G, r.B), centers[c]) > 18 * 18)) result.Add(color);   // skip near-duplicates
        }
        return result;
    }

    private static double Distance((float R, float G, float B) a, (float R, float G, float B) b)
    {
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return dr * dr + dg * dg + db * db;
    }
}
