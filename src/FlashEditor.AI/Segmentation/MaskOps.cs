using FlashEditor.Core.Imaging;
using FlashEditor.Core.Transforms;

namespace FlashEditor.AI.Segmentation;

/// <summary>
/// Pure helper math around the model: image -> input tensor, raw output -> 0..1 mask, mask resizing
/// and the user's edge adjustments. No AI runtime needed, so all of it is unit-tested.
/// </summary>
public static class MaskOps
{
    /// <summary>
    /// Resizes the image to size x size and returns it as planar RGB floats (NCHW), normalized as
    /// (value / 255 - mean) / std per channel.
    /// </summary>
    public static float[] ToInputTensor(PixelBuffer image, int size, float[] mean, float[] std)
    {
        var resized = image.Width == size && image.Height == size
            ? image
            : Resampler.Resize(image, size, size, ResampleMode.Smooth);
        int plane = size * size;
        var tensor = new float[3 * plane];
        var d = resized.Data;
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                int p = y * size + x, i = p * 4;
                tensor[p] = (d[i + 2] / 255f - mean[0]) / std[0];             // R
                tensor[plane + p] = (d[i + 1] / 255f - mean[1]) / std[1];     // G
                tensor[2 * plane + p] = (d[i] / 255f - mean[2]) / std[2];     // B
            }
        });
        return tensor;
    }

    /// <summary>Stretches raw model output to 0..1 (as rembg does).</summary>
    public static float[] Normalize(ReadOnlySpan<float> raw)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (float v in raw) { if (v < min) min = v; if (v > max) max = v; }
        var result = new float[raw.Length];
        float range = max - min;
        if (range < 1e-6f) return result; // flat output: nothing found
        for (int i = 0; i < raw.Length; i++) result[i] = (raw[i] - min) / range;
        return result;
    }

    /// <summary>Bilinear resize of a single-channel mask.</summary>
    public static float[] Resize(float[] mask, int width, int height, int newWidth, int newHeight)
    {
        var result = new float[newWidth * newHeight];
        double sx = (double)width / newWidth, sy = (double)height / newHeight;
        Parallel.For(0, newHeight, y =>
        {
            double fy = Math.Clamp((y + 0.5) * sy - 0.5, 0, height - 1);
            int y0 = (int)fy, y1 = Math.Min(y0 + 1, height - 1);
            float ty = (float)(fy - y0);
            for (int x = 0; x < newWidth; x++)
            {
                double fx = Math.Clamp((x + 0.5) * sx - 0.5, 0, width - 1);
                int x0 = (int)fx, x1 = Math.Min(x0 + 1, width - 1);
                float tx = (float)(fx - x0);
                float top = mask[y0 * width + x0] * (1 - tx) + mask[y0 * width + x1] * tx;
                float bottom = mask[y1 * width + x0] * (1 - tx) + mask[y1 * width + x1] * tx;
                result[y * newWidth + x] = top * (1 - ty) + bottom * ty;
            }
        });
        return result;
    }

    /// <summary>
    /// Applies the user's edge settings to a 0..1 mask (in place).
    /// <paramref name="sharpness"/> 0 = model's soft edges .. 1 = hard cut;
    /// <paramref name="shift"/> -1 = shrink (remove more) .. +1 = grow (keep more).
    /// </summary>
    public static void Adjust(float[] mask, double sharpness, double shift)
    {
        double width = 1 - 0.96 * Math.Clamp(sharpness, 0, 1);
        double center = 0.5 - 0.4 * Math.Clamp(shift, -1, 1);
        // Keep the whole soft band inside 0..1: whatever the AI is sure about stays fully kept (1) or fully
        // removed (0). Shrink / grow only moves the edge; it never makes the subject itself see-through.
        double lo0 = Math.Clamp(center - width / 2, 0, 1 - width);
        float lo = (float)lo0, range = (float)width;
        Parallel.For(0, mask.Length, i => mask[i] = Math.Clamp((mask[i] - lo) / range, 0f, 1f));
    }

    /// <summary>Multiplies the image's alpha by the mask (0 = remove, 1 = keep).</summary>
    public static PixelBuffer ApplyAsAlpha(PixelBuffer image, float[] mask)
    {
        var result = image.Clone();
        var d = result.Data;
        Parallel.For(0, result.Height, y =>
        {
            int row = y * result.Width;
            for (int x = 0; x < result.Width; x++)
            {
                int i = (row + x) * 4;
                d[i + 3] = ColorMath.ClampByte(d[i + 3] * mask[row + x]);
            }
        });
        return result;
    }
}
