using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Retouch;

/// <summary>Clone Stamp: the pixel becomes the pixel at a fixed distance away (the place being copied from).</summary>
public sealed class CloneOp : IRetouchOp
{
    private readonly int _dx, _dy;

    /// <param name="dx">How far right the copied place is from the painted pixel (negative = left).</param>
    /// <param name="dy">How far down the copied place is.</param>
    public CloneOp(int dx, int dy) { _dx = dx; _dy = dy; }

    public void Apply(PixelBuffer original, byte[] dst, int x, int y, double strength)
    {
        int sx = x + _dx, sy = y + _dy;
        if (sx < 0 || sy < 0 || sx >= original.Width || sy >= original.Height) return;
        int i = original.IndexOf(x, y);
        BlendMath.Lerp(dst, i, original.Data, i, original.Data, original.IndexOf(sx, sy), strength);
    }
}
