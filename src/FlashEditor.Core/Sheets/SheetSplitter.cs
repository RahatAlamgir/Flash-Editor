using FlashEditor.Core.Effects.Background;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Sheets;

/// <summary>Settings for <see cref="SheetSplitter"/>.</summary>
/// <param name="Gap">Parts closer than this (pixels) belong to the same piece (e.g. an icon and its label).</param>
/// <param name="MinSize">Pieces smaller than this (width and height, pixels) are ignored as specks.</param>
/// <param name="Tolerance">How different from the background color a pixel must be to count as part of a piece (0..255).</param>
/// <param name="RemoveBackground">Make the sheet's background color transparent inside each piece.</param>
/// <param name="Padding">Empty space kept around each piece.</param>
public sealed record SplitSettings(int Gap = 8, int MinSize = 8, int Tolerance = 30, bool RemoveBackground = true, int Padding = 0);

/// <summary>One item found on the sheet: where it was, and its pixels.</summary>
public sealed record SheetPiece(PixelRect Bounds, PixelBuffer Pixels);

/// <summary>
/// Splits an asset / sprite sheet into its separate items, returned in reading order (top row first,
/// left to right).
/// <list type="bullet">
/// <item>With a selection: every separate selected area is one piece (you decide what the items are,
/// using any selection tool), cut out in the selection's shape.</item>
/// <item>Without: items are found automatically on a transparent or plain-colored background.</item>
/// </list>
/// </summary>
public static class SheetSplitter
{
    /// <param name="selectionMask">Optional selection (0..255 per pixel, canvas-sized). When given,
    /// its separate areas are the pieces.</param>
    public static IReadOnlyList<SheetPiece> Split(PixelBuffer sheet, SplitSettings settings, byte[]? selectionMask = null,
        CancellationToken cancellation = default)
    {
        int w = sheet.Width, h = sheet.Height;
        bool[] isPiece;
        (double R, double G, double B)? background;
        if (selectionMask != null)
        {
            isPiece = new bool[w * h];
            for (int p = 0; p < isPiece.Length; p++)
                isPiece[p] = selectionMask[p] >= 64 && sheet.Data[p * 4 + 3] >= 24;
            background = settings.RemoveBackground && !TransparentBorder(sheet)
                ? BackgroundRemovalEffect.DetectBorderColors(sheet).FirstOrDefault()
                : null;
        }
        else
        {
            isPiece = ForegroundMask(sheet, settings.Tolerance, out background);
        }
        cancellation.ThrowIfCancellationRequested();

        // Grow every part by half the gap, so parts closer than the gap touch and become one piece.
        var joined = settings.Gap > 0 ? Dilate(isPiece, w, h, (settings.Gap + 1) / 2) : isPiece;
        cancellation.ThrowIfCancellationRequested();

        var labels = new int[w * h];
        var components = FindBoxes(joined, isPiece, w, h, labels)
            .Where(c => c.Box.Width >= settings.MinSize && c.Box.Height >= settings.MinSize)
            .ToDictionary(c => c.Box, c => c.Label);

        var pieces = new List<SheetPiece>(components.Count);
        foreach (var box in ReadingOrder(components.Keys.ToList()))
        {
            cancellation.ThrowIfCancellationRequested();
            var area = box.Inflate(settings.Padding);
            var pixels = sheet.Crop(area); // outside the sheet becomes transparent
            if (selectionMask != null) KeepSelectedArea(pixels, area, selectionMask, labels, components[box], w, h);
            if (settings.RemoveBackground && background is { } bg) ClearBackground(pixels, bg, settings.Tolerance);
            pieces.Add(new SheetPiece(area, pixels));
        }
        return pieces;
    }

    /// <summary>
    /// True for pixels that belong to an item. A mostly transparent border means "transparent
    /// background"; otherwise the dominant border color is the background.
    /// </summary>
    public static bool[] ForegroundMask(PixelBuffer sheet, int tolerance, out (double R, double G, double B)? background)
    {
        int w = sheet.Width, h = sheet.Height;
        var mask = new bool[w * h];
        background = TransparentBorder(sheet) ? null : BackgroundRemovalEffect.DetectBorderColors(sheet).FirstOrDefault();
        double limit = tolerance * tolerance * 3.0;
        var bg = background;

        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = sheet.IndexOf(x, y);
                if (sheet.Data[i + 3] < 24) continue;
                if (bg is { } c)
                {
                    double dr = sheet.Data[i + 2] - c.R, dg = sheet.Data[i + 1] - c.G, db = sheet.Data[i] - c.B;
                    if (dr * dr + dg * dg + db * db <= limit) continue;
                }
                mask[y * w + x] = true;
            }
        });
        return mask;
    }

    private static bool TransparentBorder(PixelBuffer sheet)
    {
        int transparent = 0, total = 0;
        void Check(int x, int y) { total++; if (sheet.GetPixel(x, y).A < 24) transparent++; }
        for (int x = 0; x < sheet.Width; x++) { Check(x, 0); Check(x, sheet.Height - 1); }
        for (int y = 0; y < sheet.Height; y++) { Check(0, y); Check(sheet.Width - 1, y); }
        return transparent > total / 2;
    }

    /// <summary>Square dilation (separable running count: fast for any radius).</summary>
    private static bool[] Dilate(bool[] mask, int w, int h, int r)
    {
        var horizontal = new bool[mask.Length];
        Parallel.For(0, h, y =>
        {
            int row = y * w, count = 0;
            for (int x = 0; x < Math.Min(r, w); x++) if (mask[row + x]) count++;
            for (int x = 0; x < w; x++)
            {
                if (x + r < w && mask[row + x + r]) count++;
                if (x - r - 1 >= 0 && mask[row + x - r - 1]) count--;
                horizontal[row + x] = count > 0;
            }
        });
        var result = new bool[mask.Length];
        Parallel.For(0, w, x =>
        {
            int count = 0;
            for (int y = 0; y < Math.Min(r, h); y++) if (horizontal[y * w + x]) count++;
            for (int y = 0; y < h; y++)
            {
                if (y + r < h && horizontal[(y + r) * w + x]) count++;
                if (y - r - 1 >= 0 && horizontal[(y - r - 1) * w + x]) count--;
                result[y * w + x] = count > 0;
            }
        });
        return result;
    }

    /// <summary>Keeps only this piece's selected pixels (soft selection edges stay soft).</summary>
    private static void KeepSelectedArea(PixelBuffer pixels, PixelRect area, byte[] mask, int[] labels, int label, int w, int h)
    {
        for (int y = 0; y < area.Height; y++)
        {
            int sy = area.Y + y;
            for (int x = 0; x < area.Width; x++)
            {
                int sx = area.X + x;
                int i = pixels.IndexOf(x, y);
                bool inside = sx >= 0 && sy >= 0 && sx < w && sy < h && labels[sy * w + sx] == label;
                double coverage = inside ? mask[sy * w + sx] / 255.0 : 0;
                pixels.Data[i + 3] = ColorMath.ClampByte(pixels.Data[i + 3] * coverage);
            }
        }
    }

    /// <summary>
    /// Bounding boxes (of the real item pixels) of each connected group in the joined mask; also fills
    /// <paramref name="labels"/> with each pixel's group number (1, 2, 3...; 0 = none).
    /// </summary>
    private static List<(PixelRect Box, int Label)> FindBoxes(bool[] joined, bool[] real, int w, int h, int[] labels)
    {
        var seen = new bool[joined.Length];
        var boxes = new List<(PixelRect, int)>();
        int label = 0;
        var stack = new Stack<int>();
        for (int start = 0; start < joined.Length; start++)
        {
            if (!joined[start] || seen[start]) continue;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            label++;
            seen[start] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                labels[p] = label;
                int x = p % w, y = p / w;
                if (real[p])
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= w) continue;
                        int q = ny * w + nx;
                        if (joined[q] && !seen[q]) { seen[q] = true; stack.Push(q); }
                    }
                }
            }
            if (maxX >= 0) boxes.Add((new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1), label));
        }
        return boxes;
    }

    /// <summary>Rows (pieces that overlap vertically) top to bottom, each row left to right.</summary>
    private static IEnumerable<PixelRect> ReadingOrder(List<PixelRect> boxes)
    {
        var rows = new List<(int Top, int Bottom, List<PixelRect> Items)>();
        foreach (var box in boxes.OrderBy(b => b.Y))
        {
            int index = rows.FindIndex(r => box.Y < r.Bottom && box.Bottom > r.Top);
            if (index < 0)
            {
                rows.Add((box.Y, box.Bottom, new List<PixelRect> { box }));
                continue;
            }
            var row = rows[index];
            row.Items.Add(box);
            rows[index] = (Math.Min(row.Top, box.Y), Math.Max(row.Bottom, box.Bottom), row.Items);
        }
        return rows.OrderBy(r => r.Top).SelectMany(r => r.Items.OrderBy(b => b.X));
    }

    /// <summary>Background-colored pixels become transparent (with a soft edge for anti-aliasing).</summary>
    private static void ClearBackground(PixelBuffer piece, (double R, double G, double B) bg, int tolerance)
    {
        double inner = tolerance * 0.6, outer = tolerance * 1.4 + 1;
        var d = piece.Data;
        for (int i = 0; i < d.Length; i += 4)
        {
            double dr = d[i + 2] - bg.R, dg = d[i + 1] - bg.G, db = d[i] - bg.B;
            double distance = Math.Sqrt((dr * dr + dg * dg + db * db) / 3);
            if (distance >= outer) continue;
            double keep = distance <= inner ? 0 : (distance - inner) / (outer - inner);
            d[i + 3] = ColorMath.ClampByte(d[i + 3] * keep);
        }
    }
}
