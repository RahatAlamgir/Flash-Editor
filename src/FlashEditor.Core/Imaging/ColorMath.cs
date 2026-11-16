namespace FlashEditor.Core.Imaging;

/// <summary>Color-space conversions (RGB, HSV, HSL). All channels use 0..1 doubles except hue (0..360).</summary>
public static class ColorMath
{
    public static byte ClampByte(double v) => v <= 0 ? (byte)0 : v >= 255 ? (byte)255 : (byte)(v + 0.5);

    public static byte ClampByte(int v) => v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)v;

    public static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double d = max - min;
        v = max;
        s = max <= 0 ? 0 : d / max;
        h = d <= 0 ? 0 : HueFrom(r, g, b, max, d);
    }

    public static void HsvToRgb(double h, double s, double v, out double r, out double g, out double b)
    {
        h = ((h % 360) + 360) % 360 / 60.0;
        int i = (int)Math.Floor(h);
        double f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
        (r, g, b) = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
    }

    public static void RgbToHsl(double r, double g, double b, out double h, out double s, out double l)
    {
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double d = max - min;
        l = (max + min) / 2;
        if (d <= 0) { h = 0; s = 0; return; }
        s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        h = HueFrom(r, g, b, max, d);
    }

    public static void HslToRgb(double h, double s, double l, out double r, out double g, out double b)
    {
        if (s <= 0) { r = g = b = l; return; }
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p = 2 * l - q;
        double hk = (((h % 360) + 360) % 360) / 360.0;
        r = HueToChannel(p, q, hk + 1.0 / 3);
        g = HueToChannel(p, q, hk);
        b = HueToChannel(p, q, hk - 1.0 / 3);
    }

    public static ColorRgba FromHsv(double h, double s, double v, byte alpha = 255)
    {
        HsvToRgb(h, s, v, out double r, out double g, out double b);
        return new ColorRgba(ClampByte(r * 255), ClampByte(g * 255), ClampByte(b * 255), alpha);
    }

    private static double HueFrom(double r, double g, double b, double max, double d)
    {
        double h;
        if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
        else if (max == g) h = (b - r) / d + 2;
        else h = (r - g) / d + 4;
        return h * 60;
    }

    private static double HueToChannel(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 0.5) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
