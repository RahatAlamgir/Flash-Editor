using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;

namespace FlashEditor.AI.Inpainting;

/// <summary>Which part of the picture the model looks at, and the size that part is shown to the model at.</summary>
/// <param name="Left">Left edge of the square window in picture coordinates (may be negative when the window sticks out of the picture).</param>
/// <param name="Top">Top edge of the window.</param>
/// <param name="Side">Side of the square window, in picture pixels.</param>
public readonly record struct InpaintWindow(int Left, int Top, int Side)
{
    public int Right => Left + Side;
    public int Bottom => Top + Side;
}

/// <summary>
/// The picture math around the fill model, without any AI runtime (so it can be tested): choosing the window around the selection,
/// widening the mask, cutting out and resizing the model input, and blending the model's answer back with a soft edge.
/// </summary>
public static class InpaintGeometry
{
    /// <summary>
    /// A square window around the selected area. The model works on 512 x 512, so a small selection gets a 512 pixel window (full detail,
    /// no resizing); a big one gets a window one and a half times its size (shrunk to fit the model).
    /// </summary>
    public static InpaintWindow PlanWindow(int imageWidth, int imageHeight, PixelRect area, int modelSize = 512)
    {
        int longest = Math.Max(area.Width, area.Height);
        int side = Math.Max(modelSize, (int)Math.Ceiling(longest * 1.5));
        int left = area.X + area.Width / 2 - side / 2;
        int top = area.Y + area.Height / 2 - side / 2;
        // Keep the window on the picture when it fits there; otherwise center it (the edges are padded).
        left = side <= imageWidth ? Math.Clamp(left, 0, imageWidth - side) : (imageWidth - side) / 2;
        top = side <= imageHeight ? Math.Clamp(top, 0, imageHeight - side) : (imageHeight - side) / 2;
        return new InpaintWindow(left, top, side);
    }

    /// <summary>How far the selection is widened for the model: enough to cover the soft edge of an object and thin parts the selection just missed (a shield rim, a cape end); more for big ones.</summary>
    public static int DilationFor(PixelRect area) => Math.Clamp((int)Math.Round(Math.Max(area.Width, area.Height) * 0.04), 4, 30);

    /// <summary>The mask (0..255 per pixel) made bigger by <paramref name="radius"/> pixels in every direction, inside <paramref name="region"/> only.</summary>
    public static byte[] Dilate(byte[] mask, int width, int height, PixelRect region, int radius)
    {
        var result = (byte[])mask.Clone();
        region = region.Inflate(radius).Intersect(new PixelRect(0, 0, width, height));
        if (region.IsEmpty || radius <= 0) return result;

        // A square grows in two passes (along the rows, then along the columns).
        var rows = new byte[mask.Length];
        Parallel.For(region.Y, region.Bottom, y =>
        {
            for (int x = region.X; x < region.Right; x++)
            {
                byte best = 0;
                int from = Math.Max(0, x - radius), to = Math.Min(width - 1, x + radius);
                for (int k = from; k <= to; k++) if (mask[y * width + k] > best) best = mask[y * width + k];
                rows[y * width + x] = best;
            }
        });
        Parallel.For(region.Y, region.Bottom, y =>
        {
            for (int x = region.X; x < region.Right; x++)
            {
                byte best = 0;
                int from = Math.Max(0, y - radius), to = Math.Min(height - 1, y + radius);
                for (int k = from; k <= to; k++) if (rows[k * width + x] > best) best = rows[k * width + x];
                result[y * width + x] = best;
            }
        });
        return result;
    }

    /// <summary>
    /// The window of the picture as the model wants it: planar RGB floats 0..1 (<c>image</c>, 3 x size x size) and a hole mask
    /// (<c>mask</c>, size x size, 1 = fill this). Where the window sticks out of the picture the edge pixels are repeated and nothing is asked
    /// to be filled.
    /// </summary>
    public static (float[] Image, float[] Mask) Prepare(PixelBuffer image, byte[] mask, InpaintWindow window, int modelSize = 512)
    {
        int side = window.Side;
        var crop = new PixelBuffer(side, side);
        var hole = new float[side * side];
        var src = image.Data;
        var dst = crop.Data;

        Parallel.For(0, side, y =>
        {
            int sy = window.Top + y;
            int cy = Math.Clamp(sy, 0, image.Height - 1);
            for (int x = 0; x < side; x++)
            {
                int sx = window.Left + x;
                int cx = Math.Clamp(sx, 0, image.Width - 1);
                int si = (cy * image.Width + cx) * 4, di = (y * side + x) * 4;
                // The picture is flattened onto white first, so see-through places are not dark holes for the model.
                int a = src[si + 3];
                dst[di] = (byte)((src[si] * a + 255 * (255 - a)) / 255);
                dst[di + 1] = (byte)((src[si + 1] * a + 255 * (255 - a)) / 255);
                dst[di + 2] = (byte)((src[si + 2] * a + 255 * (255 - a)) / 255);
                dst[di + 3] = 255;
                bool inside = sx >= 0 && sy >= 0 && sx < image.Width && sy < image.Height;
                hole[y * side + x] = inside && mask[cy * image.Width + cx] > 0 ? 1f : 0f;
            }
        });

        var resized = side == modelSize ? crop : Resampler.Resize(crop, modelSize, modelSize, ResampleMode.Smooth);
        var holeSmall = side == modelSize ? hole : Segmentation.MaskOps.Resize(hole, side, side, modelSize, modelSize);

        int plane = modelSize * modelSize;
        var tensor = new float[3 * plane];
        var mask01 = new float[plane];
        var d = resized.Data;
        for (int p = 0; p < plane; p++)
        {
            int i = p * 4;
            tensor[p] = d[i + 2] / 255f;
            tensor[plane + p] = d[i + 1] / 255f;
            tensor[2 * plane + p] = d[i] / 255f;
            mask01[p] = holeSmall[p] >= 0.25f ? 1f : 0f;            // a little generous, so no sliver of the object is left
        }
        return (tensor, mask01);
    }

    /// <summary>
    /// A picture of the same size as the original that holds only the filled part: the model's answer where the (widened) selection was,
    /// with a soft edge, and nothing anywhere else. <paramref name="output"/> is the model's picture (size x size, planar RGB, values 0..1).
    /// </summary>
    public static PixelBuffer Compose(int imageWidth, int imageHeight, byte[] mask, PixelRect area, float[] output, InpaintWindow window, int modelSize = 512, int feather = 3)
    {
        // The model's answer as a picture the size of the window.
        int plane = modelSize * modelSize;
        var answer = new PixelBuffer(modelSize, modelSize);
        var a = answer.Data;
        for (int p = 0; p < plane; p++)
        {
            int i = p * 4;
            a[i + 2] = ColorMath.ClampByte(output[p] * 255f);
            a[i + 1] = ColorMath.ClampByte(output[plane + p] * 255f);
            a[i] = ColorMath.ClampByte(output[2 * plane + p] * 255f);
            a[i + 3] = 255;
        }
        if (window.Side != modelSize) answer = Resampler.Resize(answer, window.Side, window.Side, ResampleMode.Bicubic);

        // Soft edge: the widened mask, blurred a little, decides how much of the answer shows.
        var region = area.Inflate(feather + 2).Intersect(new PixelBuffer(imageWidth, imageHeight).Bounds);
        var soft = Soften(mask, imageWidth, imageHeight, region, feather);

        var result = new PixelBuffer(imageWidth, imageHeight);
        var dst = result.Data;
        var src = answer.Data;
        Parallel.For(region.Y, region.Bottom, y =>
        {
            int wy = y - window.Top;
            if (wy < 0 || wy >= window.Side) return;
            for (int x = region.X; x < region.Right; x++)
            {
                int wx = x - window.Left;
                if (wx < 0 || wx >= window.Side) continue;
                byte alpha = soft[y * imageWidth + x];
                if (alpha == 0) continue;
                int si = (wy * window.Side + wx) * 4, di = (y * imageWidth + x) * 4;
                dst[di] = src[si]; dst[di + 1] = src[si + 1]; dst[di + 2] = src[si + 2]; dst[di + 3] = alpha;
            }
        });
        return result;
    }

    /// <summary>The mask with a soft edge (a box blur of the given radius, twice), only inside <paramref name="region"/>.</summary>
    private static byte[] Soften(byte[] mask, int width, int height, PixelRect region, int radius)
    {
        var soft = new byte[mask.Length];
        if (radius <= 0)
        {
            for (int y = region.Y; y < region.Bottom; y++) Array.Copy(mask, y * width + region.X, soft, y * width + region.X, region.Width);
            return soft;
        }
        var a = new float[mask.Length];
        var b = new float[mask.Length];
        for (int y = region.Y; y < region.Bottom; y++)
            for (int x = region.X; x < region.Right; x++) a[y * width + x] = mask[y * width + x] / 255f;

        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = region.Y; y < region.Bottom; y++)
                for (int x = region.X; x < region.Right; x++)
                {
                    float sum = 0; int n = 0;
                    for (int k = Math.Max(region.X, x - radius); k <= Math.Min(region.Right - 1, x + radius); k++) { sum += a[y * width + k]; n++; }
                    b[y * width + x] = sum / n;
                }
            for (int y = region.Y; y < region.Bottom; y++)
                for (int x = region.X; x < region.Right; x++)
                {
                    float sum = 0; int n = 0;
                    for (int k = Math.Max(region.Y, y - radius); k <= Math.Min(region.Bottom - 1, y + radius); k++) { sum += b[k * width + x]; n++; }
                    a[y * width + x] = sum / n;
                }
        }
        for (int y = region.Y; y < region.Bottom; y++)
            for (int x = region.X; x < region.Right; x++)
            {
                // Inside the original mask the answer must show completely; the blur only softens the outside edge.
                float v = Math.Max(a[y * width + x], mask[y * width + x] / 255f * (a[y * width + x] > 0.5f ? 1f : 0f));
                soft[y * width + x] = (byte)Math.Clamp((int)Math.Round(v * 255f), 0, 255);
            }
        return soft;
    }
}
