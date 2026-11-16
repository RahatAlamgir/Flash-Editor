using System.Globalization;

namespace FlashEditor.Core.Imaging;

/// <summary>A color with straight (non-premultiplied) alpha.</summary>
public readonly record struct ColorRgba(byte R, byte G, byte B, byte A = 255)
{
    public static readonly ColorRgba Black = new(0, 0, 0);
    public static readonly ColorRgba White = new(255, 255, 255);
    public static readonly ColorRgba Transparent = new(0, 0, 0, 0);

    /// <summary>Hex string like "#FF8800" (alpha omitted when opaque) or "#FF880080".</summary>
    public string ToHex() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{R:X2}{G:X2}{B:X2}{A:X2}";

    /// <summary>Parses "#RGB", "#RRGGBB" or "#RRGGBBAA" (the # is optional).</summary>
    public static bool TryParseHex(string? text, out ColorRgba color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string hex = text.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
        if (hex.Length != 6 && hex.Length != 8) return false;
        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return false;
        color = hex.Length == 6
            ? new ColorRgba((byte)(v >> 16), (byte)(v >> 8), (byte)v)
            : new ColorRgba((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }
}
