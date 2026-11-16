using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Selections;

/// <summary>Fills polygons into a byte mask using the even-odd rule (scanline algorithm).</summary>
public static class PolygonRasterizer
{
    /// <summary>
    /// Sets mask bytes to 255 for every pixel whose center lies inside any of the polygons
    /// (even-odd rule across all polygons, so a polygon inside another makes a hole).
    /// </summary>
    public static void Fill(byte[] mask, int width, int height, IEnumerable<IReadOnlyList<PointD>> polygons)
    {
        var edges = new List<(double x1, double y1, double x2, double y2)>();
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var poly in polygons)
        {
            if (poly.Count < 3) continue;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Count];
                if (a.Y == b.Y) continue; // horizontal edges never cross a scanline
                edges.Add((a.X, a.Y, b.X, b.Y));
                minY = Math.Min(minY, Math.Min(a.Y, b.Y));
                maxY = Math.Max(maxY, Math.Max(a.Y, b.Y));
            }
        }
        if (edges.Count == 0) return;

        int yStart = Math.Max(0, (int)Math.Floor(minY));
        int yEnd = Math.Min(height - 1, (int)Math.Ceiling(maxY));
        var crossings = new List<double>();

        for (int y = yStart; y <= yEnd; y++)
        {
            double sy = y + 0.5; // sample at pixel centers
            crossings.Clear();
            foreach (var (x1, y1, x2, y2) in edges)
            {
                bool spans = (y1 <= sy && y2 > sy) || (y2 <= sy && y1 > sy);
                if (!spans) continue;
                crossings.Add(x1 + (sy - y1) / (y2 - y1) * (x2 - x1));
            }
            if (crossings.Count < 2) continue;
            crossings.Sort();

            int row = y * width;
            for (int k = 0; k + 1 < crossings.Count; k += 2)
            {
                // Pixel x is inside when its center x+0.5 lies in [left, right).
                int from = Math.Max(0, (int)Math.Ceiling(crossings[k] - 0.5));
                int to = Math.Min(width - 1, (int)Math.Ceiling(crossings[k + 1] - 0.5) - 1);
                for (int x = from; x <= to; x++)
                    mask[row + x] ^= 255;
            }
        }
    }
}
