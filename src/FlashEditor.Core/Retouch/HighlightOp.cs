using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Retouch;

/// <summary>Tints pixels towards a color. Spot Healing shows the area being painted with it until the mouse is released.</summary>
public sealed class HighlightOp : IRetouchOp
{
    private readonly ColorRgba _color;
    private readonly double _amount;

    public HighlightOp(ColorRgba color, double amount) { _color = color; _amount = amount; }

    public void Apply(PixelBuffer original, byte[] dst, int x, int y, double strength)
    {
        int i = original.IndexOf(x, y);
        double t = strength * _amount;
        dst[i] = ColorMath.ClampByte(dst[i] + (_color.B - dst[i]) * t);
        dst[i + 1] = ColorMath.ClampByte(dst[i + 1] + (_color.G - dst[i + 1]) * t);
        dst[i + 2] = ColorMath.ClampByte(dst[i + 2] + (_color.R - dst[i + 2]) * t);
    }
}
