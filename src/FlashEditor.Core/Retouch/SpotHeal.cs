using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Retouch;

/// <summary>Spot Healing: finds a clean place near a blemish to heal it from, without the user choosing one.</summary>
public static class SpotHeal
{
    /// <summary>
    /// Looks around <paramref name="area"/> (the painted blemish) for the place whose surroundings look most like the surroundings
    /// of the blemish, and returns how far it is (dx, dy), or null when there is no room for any candidate.
    /// </summary>
    public static (int Dx, int Dy)? FindOffset(PixelBuffer picture, PixelRect area)
    {
        if (area.IsEmpty) return null;
        int size = Math.Max(area.Width, area.Height);
        int ring = Math.Clamp(size / 3, 4, 20);
        var around = area.Inflate(ring).Intersect(picture.Bounds);       // the blemish with a band of its surroundings
        double reach = size + 2 * ring;

        (int Dx, int Dy)? best = null;
        double bestScore = double.MaxValue;
        foreach (double distance in new[] { 1.1, 1.6, 2.3, 3.2 })
            for (int k = 0; k < 16; k++)
            {
                double angle = k * Math.PI / 8;
                int dx = (int)Math.Round(Math.Cos(angle) * reach * distance), dy = (int)Math.Round(Math.Sin(angle) * reach * distance);
                var moved = new PixelRect(around.X + dx, around.Y + dy, around.Width, around.Height);
                if (moved.Intersect(picture.Bounds) != moved) continue;     // the copied place must be on the picture

                double score = BandDifference(picture, around, area, dx, dy);
                if (score < bestScore) { bestScore = score; best = (dx, dy); }
            }
        return best;
    }

    /// <summary>How different the band around the blemish is from the same band at the candidate place (smaller = more alike).</summary>
    private static double BandDifference(PixelBuffer p, PixelRect around, PixelRect hole, int dx, int dy)
    {
        double sum = 0;
        long count = 0;
        var d = p.Data;
        for (int y = around.Y; y < around.Bottom; y += 2)
            for (int x = around.X; x < around.Right; x += 2)
            {
                if (hole.Contains(x, y)) continue;                           // only the band, not the blemish itself
                int i = p.IndexOf(x, y), j = p.IndexOf(x + dx, y + dy);
                double db = d[i] - d[j], dg = d[i + 1] - d[j + 1], dr = d[i + 2] - d[j + 2], da = d[i + 3] - d[j + 3];
                sum += db * db + dg * dg + dr * dr + da * da;
                count++;
            }
        return count == 0 ? double.MaxValue : sum / count;
    }
}
