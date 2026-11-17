using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Retouch;

/// <summary>Which part of the picture's brightness Dodge/Burn works on.</summary>
public enum ToneRange { Shadows, Midtones, Highlights }

/// <summary>Dodge (lighten) and Burn (darken), mostly inside the chosen range of brightness.</summary>
public sealed class ToneOp : IRetouchOp
{
    private readonly bool _burn;
    private readonly ToneRange _range;

    public ToneOp(bool burn, ToneRange range) { _burn = burn; _range = range; }

    public void Apply(PixelBuffer original, byte[] dst, int x, int y, double strength)
    {
        int i = original.IndexOf(x, y);
        double b = dst[i], g = dst[i + 1], r = dst[i + 2];
        double light = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
        double w = _range switch
        {
            ToneRange.Shadows => (1 - light) * (1 - light),
            ToneRange.Highlights => light * light,
            _ => 4 * light * (1 - light),
        };
        double k = strength * (0.15 + 0.85 * w);                    // a little effect everywhere, most inside the range
        if (_burn) { r -= r * k; g -= g * k; b -= b * k; }
        else { r += (255 - r) * k; g += (255 - g) * k; b += (255 - b) * k; }
        dst[i] = ColorMath.ClampByte(b); dst[i + 1] = ColorMath.ClampByte(g); dst[i + 2] = ColorMath.ClampByte(r);
    }
}
