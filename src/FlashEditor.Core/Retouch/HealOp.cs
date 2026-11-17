using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Retouch;

/// <summary>
/// Healing: copies the texture (fine detail) from the copied place, but keeps the light and color of the place being painted,
/// so the patch melts into its surroundings instead of showing as a pasted piece.
/// </summary>
public sealed class HealOp : IRetouchOp
{
    private readonly int _dx, _dy, _radius, _step;
    private Func<int, int, bool>? _isPainted;

    /// <param name="radius">
    /// How far around a pixel "the surrounding light and color" is looked at (pixels). Should be a bit more than the radius of the
    /// painted area, so the surroundings reach out of the blemish.
    /// </param>
    public HealOp(int dx, int dy, int radius = 8)
    {
        _dx = dx; _dy = dy;
        _radius = Math.Max(2, radius);
        _step = Math.Max(1, _radius / 6);
    }

    /// <summary>The painted pixels (the blemish itself) are left out when the surrounding tone is measured.</summary>
    public void Attach(Func<int, int, bool> isPainted) => _isPainted = isPainted;

    public void Apply(PixelBuffer original, byte[] dst, int x, int y, double strength)
    {
        int sx = x + _dx, sy = y + _dy;
        if (sx < 0 || sy < 0 || sx >= original.Width || sy >= original.Height) return;

        int i = original.IndexOf(x, y), j = original.IndexOf(sx, sy);
        var o = original.Data;
        if (o[j + 3] < 8) return;                                  // nothing there to copy

        double r = o[j + 2], g = o[j + 1], b = o[j];
        if (LocalMean.At(original, x, y, _radius, _step, out var tr, out var tg, out var tb, out _, _isPainted)
            && LocalMean.At(original, sx, sy, _radius, _step, out var lr, out var lg, out var lb, out _))
        {
            r += tr - lr; g += tg - lg; b += tb - lb;              // texture from the source, tone from the surroundings of the target
        }
        var healed = new[] { ColorMath.ClampByte(b), ColorMath.ClampByte(g), ColorMath.ClampByte(r), o[j + 3] };
        BlendMath.Lerp(dst, i, o, i, healed, 0, strength);
    }
}
