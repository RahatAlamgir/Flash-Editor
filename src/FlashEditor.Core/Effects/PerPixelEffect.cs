using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Effects;

/// <summary>Changes one pixel's color channels in place. Alpha is never touched.</summary>
public delegate void PixelMapper(ref byte b, ref byte g, ref byte r);

/// <summary>
/// Base for adjustments where each output pixel depends only on the same input pixel
/// (brightness, hue, invert...). Subclasses just describe the per-pixel math.
/// </summary>
public abstract class PerPixelEffect : ImageEffect
{
    protected abstract PixelMapper CreateMapper(EffectValues values);

    public sealed override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        var mapper = CreateMapper(values);
        var result = source.Clone();
        var data = result.Data;
        int stride = result.Stride;

        Parallel.For(0, result.Height, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            int end = (y + 1) * stride;
            for (int i = y * stride; i < end; i += 4)
            {
                if (data[i + 3] == 0) continue;
                mapper(ref data[i], ref data[i + 1], ref data[i + 2]);
            }
        });
        return result;
    }

    /// <summary>Helper: builds a 256-entry lookup table.</summary>
    protected static byte[] BuildLut(Func<double, double> f)
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++) lut[i] = ColorMath.ClampByte(f(i));
        return lut;
    }
}
