using System.Runtime.CompilerServices;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Documents;

/// <summary>Which settings an adjustment layer shows: brightness / contrast / colors, Levels, or Curves.</summary>
public enum AdjustmentKind { Basic, Levels, Curves }

/// <summary>
/// The settings of an adjustment layer: a layer that has no picture of its own but changes the colors of every layer below it.
/// Nothing is ever changed in the pictures themselves, so it can be tuned or removed at any time.
/// </summary>
/// <param name="Brightness">-100 (darker) .. 100 (lighter).</param>
/// <param name="Contrast">-100 (flatter) .. 100 (stronger).</param>
/// <param name="Saturation">-100 (grey) .. 100 (more vivid).</param>
/// <param name="Hue">-180 .. 180 degrees around the color wheel.</param>
/// <param name="Kind">Which controls the layer shows. All settings work whatever the kind is.</param>
/// <param name="LevelsBlack">Levels: this brightness (0..254) and everything darker becomes black.</param>
/// <param name="LevelsWhite">Levels: this brightness (1..255) and everything lighter becomes white.</param>
/// <param name="LevelsGamma">Levels: the middle tones. 1 = unchanged, above 1 lighter, below 1 darker.</param>
/// <param name="OutputBlack">Levels: black becomes this brightness (0..254), which lifts the shadows.</param>
/// <param name="OutputWhite">Levels: white becomes this brightness (1..255), which dims the highlights.</param>
/// <param name="Curve">Curves: the tone curve of all colors together (null = straight line).</param>
/// <param name="CurveRed">Curves: an extra curve for the red channel only.</param>
/// <param name="CurveGreen">Curves: an extra curve for the green channel only.</param>
/// <param name="CurveBlue">Curves: an extra curve for the blue channel only.</param>
public sealed record AdjustmentStyle(
    double Brightness = 0, double Contrast = 0, double Saturation = 0, double Hue = 0,
    AdjustmentKind Kind = AdjustmentKind.Basic,
    double LevelsBlack = 0, double LevelsWhite = 255, double LevelsGamma = 1, double OutputBlack = 0, double OutputWhite = 255,
    IReadOnlyList<CurvePoint>? Curve = null, IReadOnlyList<CurvePoint>? CurveRed = null,
    IReadOnlyList<CurvePoint>? CurveGreen = null, IReadOnlyList<CurvePoint>? CurveBlue = null)
{
    public static AdjustmentStyle Neutral { get; } = new();

    /// <summary>A Levels adjustment layer that starts unchanged.</summary>
    public static AdjustmentStyle NewLevels { get; } = new(Kind: AdjustmentKind.Levels);

    /// <summary>A Curves adjustment layer that starts unchanged.</summary>
    public static AdjustmentStyle NewCurves { get; } = new(Kind: AdjustmentKind.Curves);

    private bool LevelsAreNeutral => LevelsBlack <= 0 && LevelsWhite >= 255 && Math.Abs(LevelsGamma - 1) < 1e-9 && OutputBlack <= 0 && OutputWhite >= 255;
    private bool CurvesAreNeutral => CurveMath.IsStraight(Curve) && CurveMath.IsStraight(CurveRed) && CurveMath.IsStraight(CurveGreen) && CurveMath.IsStraight(CurveBlue);

    /// <summary>True when applying it would change nothing.</summary>
    public bool IsNeutral => Brightness == 0 && Contrast == 0 && Saturation == 0 && Hue == 0 && LevelsAreNeutral && CurvesAreNeutral;

    /// <summary>The tables for Levels and Curves, worked out once per style (null when there are none).</summary>
    private sealed class ToneTables
    {
        public byte[] Red = null!, Green = null!, Blue = null!;
    }

    private static readonly ConditionalWeakTable<AdjustmentStyle, ToneTables?> TableCache = new();

    private ToneTables? Tones
    {
        get
        {
            if (LevelsAreNeutral && CurvesAreNeutral) return null;
            return TableCache.GetValue(this, style =>
            {
                var levels = CurveMath.LevelsTable(style.LevelsBlack, style.LevelsWhite, style.LevelsGamma, style.OutputBlack, style.OutputWhite);
                var master = CurveMath.BuildTable(style.Curve);
                byte[] Channel(IReadOnlyList<CurvePoint>? own)
                {
                    var channel = CurveMath.BuildTable(own);
                    var table = new byte[256];
                    for (int i = 0; i < 256; i++) table[i] = channel[master[levels[i]]];       // Levels first, then the all-colors curve, then the channel's own curve
                    return table;
                }
                return new ToneTables { Red = Channel(style.CurveRed), Green = Channel(style.CurveGreen), Blue = Channel(style.CurveBlue) };
            });
        }
    }

    /// <summary>The adjusted color of one pixel (channels 0..255), mixed with the original by <paramref name="strength"/> (0..1).</summary>
    public void Apply(ref byte b, ref byte g, ref byte r, double strength = 1)
    {
        if (IsNeutral || strength <= 0) return;
        Apply(ref b, ref g, ref r, strength, Tones);
    }

    private void Apply(ref byte b, ref byte g, ref byte r, double strength, ToneTables? tones)
    {
        double rr = r / 255.0, gg = g / 255.0, bb = b / 255.0;
        double or = rr, og = gg, ob = bb;

        if (Hue != 0 || Saturation != 0)
        {
            ColorMath.RgbToHsv(rr, gg, bb, out double h, out double s, out double v);
            if (Hue != 0) h += Hue;
            if (Saturation != 0) s = Math.Clamp(Saturation > 0 ? s + (1 - s) * Saturation / 100 * Math.Min(1, s * 2 + 0.2) : s * (1 + Saturation / 100), 0, 1);
            ColorMath.HsvToRgb(h, s, v, out rr, out gg, out bb);
        }

        if (Contrast != 0)
        {
            double k = Contrast > 0 ? 1 + Contrast / 100 * 1.5 : 1 + Contrast / 100;      // up to 2.5 times steeper, down to flat grey
            rr = (rr - 0.5) * k + 0.5; gg = (gg - 0.5) * k + 0.5; bb = (bb - 0.5) * k + 0.5;
        }
        if (Brightness != 0)
        {
            double add = Brightness / 100 * 0.5;
            rr += add; gg += add; bb += add;
        }
        if (tones != null)
        {
            rr = tones.Red[ColorMath.ClampByte(rr * 255)] / 255.0;
            gg = tones.Green[ColorMath.ClampByte(gg * 255)] / 255.0;
            bb = tones.Blue[ColorMath.ClampByte(bb * 255)] / 255.0;
        }

        r = ColorMath.ClampByte((or + (rr - or) * strength) * 255);
        g = ColorMath.ClampByte((og + (gg - og) * strength) * 255);
        b = ColorMath.ClampByte((ob + (bb - ob) * strength) * 255);
    }

    /// <summary>Adjusts the pixels inside <paramref name="region"/> of a picture in place (transparent pixels stay as they are).</summary>
    public void Apply(PixelBuffer target, PixelRect region, double strength = 1)
    {
        region = region.Intersect(target.Bounds);
        if (IsNeutral || strength <= 0 || region.IsEmpty) return;
        var data = target.Data;
        var tones = Tones;
        Parallel.For(region.Y, region.Bottom, y =>
        {
            int i = target.IndexOf(region.X, y);
            for (int x = 0; x < region.Width; x++, i += 4)
            {
                if (data[i + 3] == 0) continue;
                Apply(ref data[i], ref data[i + 1], ref data[i + 2], strength, tones);
            }
        });
    }
}
