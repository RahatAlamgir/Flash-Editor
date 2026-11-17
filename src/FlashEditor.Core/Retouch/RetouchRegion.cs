using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Retouch;

/// <summary>Applies a retouch operation to a whole area (the Patch tool) instead of along a brush stroke.</summary>
public static class RetouchRegion
{
    /// <summary>
    /// Sets the pixels of <paramref name="target"/> inside <paramref name="rect"/> to the result of <paramref name="op"/> on
    /// <paramref name="original"/>, where <paramref name="region"/> is selected (and also <paramref name="limit"/>, if given).
    /// Pixels outside the region inside the rectangle are put back as they were. Returns the rectangle that was processed.
    /// </summary>
    public static PixelRect Apply(PixelBuffer target, PixelBuffer original, IRetouchOp op, Selection region, Selection? limit, PixelRect rect)
    {
        rect = rect.Intersect(target.Bounds);
        if (rect.IsEmpty) return rect;
        var dst = target.Data;
        var src = original.Data;
        Parallel.For(rect.Y, rect.Bottom, y =>
        {
            for (int x = rect.X; x < rect.Right; x++)
            {
                int i = original.IndexOf(x, y);
                dst[i] = src[i]; dst[i + 1] = src[i + 1]; dst[i + 2] = src[i + 2]; dst[i + 3] = src[i + 3];
                double strength = region.Coverage(x, y) * (limit?.Coverage(x, y) ?? 1.0);
                if (strength > 0) op.Apply(original, dst, x, y, strength);
            }
        });
        return rect;
    }
}
