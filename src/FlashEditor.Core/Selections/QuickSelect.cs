using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Selections;

/// <summary>
/// "Quick select" brush: every brush dab looks at the colors under the brush and grows the selection
/// from there into connected pixels of similar color, up to a few brush widths away. Dragging along
/// an object therefore selects it piece by piece, following its edges.
/// </summary>
public static class QuickSelect
{
    /// <summary>How far (in brush radii) a dab may grow past the brush.</summary>
    public const double GrowRadii = 10;

    /// <summary>
    /// Adds one dab centered at (<paramref name="cx"/>, <paramref name="cy"/>) to <paramref name="mask"/>
    /// (255 = selected). Returns the area that changed.
    /// </summary>
    /// <param name="tolerance">How different a color may be from the colors under the brush (0..255).</param>
    public static PixelRect AddDab(PixelBuffer image, byte[] mask, double cx, double cy, double radius, int tolerance)
    {
        int w = image.Width, h = image.Height;
        radius = Math.Max(1, radius);
        var brush = PixelRect.FromBounds(cx - radius, cy - radius, cx + radius + 1, cy + radius + 1).Intersect(image.Bounds);
        if (brush.IsEmpty) return PixelRect.Empty;

        // Average color under the brush (the color we are "painting" the selection with).
        double r = 0, g = 0, b = 0, count = 0;
        for (int y = brush.Y; y < brush.Bottom; y++)
            for (int x = brush.X; x < brush.Right; x++)
            {
                if (Sq(x + 0.5 - cx) + Sq(y + 0.5 - cy) > radius * radius) continue;
                int i = image.IndexOf(x, y);
                if (image.Data[i + 3] < 16) continue;
                b += image.Data[i]; g += image.Data[i + 1]; r += image.Data[i + 2]; count++;
            }
        if (count == 0) return PixelRect.Empty;
        r /= count; g /= count; b /= count;
        double limit = tolerance * tolerance * 3.0;

        bool Matches(int x, int y)
        {
            int i = image.IndexOf(x, y);
            if (image.Data[i + 3] < 16) return false;
            return Sq(image.Data[i + 2] - r) + Sq(image.Data[i + 1] - g) + Sq(image.Data[i] - b) <= limit;
        }

        // Grow from the matching pixels under the brush, inside a window around the dab.
        double reach = radius * GrowRadii;
        var window = PixelRect.FromBounds(cx - reach, cy - reach, cx + reach + 1, cy + reach + 1).Intersect(image.Bounds);
        var visited = new bool[window.Width * window.Height];
        var queue = new Queue<(int X, int Y)>();
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        void Take(int x, int y)
        {
            int v = (y - window.Y) * window.Width + (x - window.X);
            if (visited[v]) return;
            visited[v] = true;
            if (!Matches(x, y)) return;
            mask[y * w + x] = 255;
            queue.Enqueue((x, y));
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
        }

        for (int y = brush.Y; y < brush.Bottom; y++)
            for (int x = brush.X; x < brush.Right; x++)
                if (Sq(x + 0.5 - cx) + Sq(y + 0.5 - cy) <= radius * radius) Take(x, y);

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x > window.X) Take(x - 1, y);
            if (x < window.Right - 1) Take(x + 1, y);
            if (y > window.Y) Take(x, y - 1);
            if (y < window.Bottom - 1) Take(x, y + 1);
        }
        return maxX < 0 ? PixelRect.Empty : new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static double Sq(double v) => v * v;
}
