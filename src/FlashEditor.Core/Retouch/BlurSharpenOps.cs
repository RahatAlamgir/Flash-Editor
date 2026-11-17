using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Retouch;

/// <summary>Blur tool: softens the pixels under the brush.</summary>
public sealed class BlurOp : IRetouchOp
{
    private readonly int _radius;

    /// <param name="radius">How far the blur reaches (1..8 pixels).</param>
    public BlurOp(int radius) => _radius = Math.Clamp(radius, 1, 8);

    public void Apply(PixelBuffer original, byte[] dst, int x, int y, double strength)
    {
        if (!LocalMean.At(original, x, y, _radius, 1, out var r, out var g, out var b, out var a)) return;
        int i = original.IndexOf(x, y);
        var blurred = new[] { ColorMath.ClampByte(b), ColorMath.ClampByte(g), ColorMath.ClampByte(r), ColorMath.ClampByte(a) };
        BlendMath.Lerp(dst, i, original.Data, i, blurred, 0, strength);
    }
}

/// <summary>Sharpen tool: makes edges and detail under the brush crisper (an unsharp mask).</summary>
public sealed class SharpenOp : IRetouchOp
{
    private readonly int _radius;
    private readonly double _amount;

    public SharpenOp(int radius = 2, double amount = 1.6) { _radius = Math.Clamp(radius, 1, 8); _amount = amount; }

    public void Apply(PixelBuffer original, byte[] dst, int x, int y, double strength)
    {
        int i = original.IndexOf(x, y);
        var o = original.Data;
        if (o[i + 3] == 0 || !LocalMean.At(original, x, y, _radius, 1, out var mr, out var mg, out var mb, out _)) return;
        double k = _amount * strength;
        dst[i] = ColorMath.ClampByte(o[i] + (o[i] - mb) * k);
        dst[i + 1] = ColorMath.ClampByte(o[i + 1] + (o[i + 1] - mg) * k);
        dst[i + 2] = ColorMath.ClampByte(o[i + 2] + (o[i + 2] - mr) * k);
    }
}
