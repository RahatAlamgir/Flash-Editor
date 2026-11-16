using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Effects.Adjustments;

/// <summary>
/// Changes the color of an area (e.g. a dress) to a chosen color while keeping its shading.
/// <para>
/// Real fabric is never one flat color: folds are darker, highlights lighter, and the color itself
/// varies. So instead of painting one color, every pixel keeps its own light/dark difference from
/// the area's average (<see cref="ReferenceLightness"/>) and its relative color strength
/// (<see cref="ReferenceSaturation"/>); only the hue is replaced and the overall tone is moved to
/// the chosen color.
/// </para>
/// </summary>
public sealed class RecolorEffect : ImageEffect
{
    public RecolorEffect(double referenceLightness, double referenceSaturation)
    {
        ReferenceLightness = Math.Clamp(referenceLightness, 0.01, 0.99);
        ReferenceSaturation = Math.Clamp(referenceSaturation, 0, 1);
    }

    /// <summary>Average lightness (0..1) of the area being recolored.</summary>
    public double ReferenceLightness { get; }

    /// <summary>Average saturation (0..1) of the area being recolored.</summary>
    public double ReferenceSaturation { get; }

    public override string Name => "Recolor";

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new[]
    {
        new EffectParameter("hue", "Hue", 0, 360, 0, 1, "°"),
        new EffectParameter("sat", "Saturation", 0, 100, 50, 1, "%"),
        new EffectParameter("light", "Lightness", 0, 100, 50, 1, "%"),
        new EffectParameter("shading", "Keep shading", 0, 100, 100, 1, "%"),
        new EffectParameter("strength", "Strength", 0, 100, 100, 1, "%"),
    };

    /// <summary>Sets hue / saturation / lightness from a target color.</summary>
    public static void SetTarget(EffectValues values, ColorRgba color)
    {
        ColorMath.RgbToHsl(color.R / 255.0, color.G / 255.0, color.B / 255.0, out double h, out double s, out double l);
        values["hue"] = Math.Round(h);
        values["sat"] = Math.Round(s * 100);
        values["light"] = Math.Round(l * 100);
    }

    public override PixelBuffer Apply(PixelBuffer source, EffectValues values, CancellationToken cancellation = default)
    {
        double hue = values["hue"];
        double targetS = values["sat"] / 100;
        double targetL = Math.Clamp(values["light"] / 100, 0, 1);
        double shading = values["shading"] / 100;
        double strength = values["strength"] / 100;
        double refL = ReferenceLightness, refS = ReferenceSaturation;

        var result = source.Clone();
        var d = result.Data;
        int stride = result.Stride;
        Parallel.For(0, result.Height, new ParallelOptions { CancellationToken = cancellation }, y =>
        {
            int end = (y + 1) * stride;
            for (int i = y * stride; i < end; i += 4)
            {
                if (d[i + 3] == 0) continue;
                double r = d[i + 2] / 255.0, g = d[i + 1] / 255.0, b = d[i] / 255.0;
                ColorMath.RgbToHsl(r, g, b, out _, out double s, out double l);

                // Lightness: move the average to the target, scaling toward black or white so
                // shadows and highlights stay in proportion (and never clip harshly).
                double moved = targetL >= refL
                    ? l + (1 - l) * (targetL - refL) / (1 - refL)
                    : l * targetL / refL;
                double newL = targetL + (moved - targetL) * shading;

                // Saturation: keep each pixel's strength relative to the area's average.
                double newS = refS < 0.04 ? targetS : Math.Clamp(targetS * s / refS, 0, 1);
                newS = targetS + (newS - targetS) * shading;

                ColorMath.HslToRgb(hue, Math.Clamp(newS, 0, 1), Math.Clamp(newL, 0, 1), out double nr, out double ng, out double nb);
                d[i + 2] = ColorMath.ClampByte((r + (nr - r) * strength) * 255);
                d[i + 1] = ColorMath.ClampByte((g + (ng - g) * strength) * 255);
                d[i] = ColorMath.ClampByte((b + (nb - b) * strength) * 255);
            }
        });
        return result;
    }

    /// <summary>
    /// Average color, lightness and saturation of the pixels inside <paramref name="selection"/>
    /// (all visible pixels when null), weighted by selection amount and opacity.
    /// </summary>
    public static (ColorRgba Average, double Lightness, double Saturation) Measure(PixelBuffer image, Selection? selection)
    {
        double wSum = 0, rSum = 0, gSum = 0, bSum = 0, lSum = 0, sSum = 0;
        var area = selection?.Bounds.Intersect(image.Bounds) ?? image.Bounds;
        for (int y = area.Y; y < area.Bottom; y++)
        {
            for (int x = area.X; x < area.Right; x++)
            {
                int i = image.IndexOf(x, y);
                double w = image.Data[i + 3] / 255.0 * (selection?.Coverage(x, y) ?? 1);
                if (w <= 0) continue;
                double r = image.Data[i + 2] / 255.0, g = image.Data[i + 1] / 255.0, b = image.Data[i] / 255.0;
                ColorMath.RgbToHsl(r, g, b, out _, out double s, out double l);
                wSum += w; rSum += r * w; gSum += g * w; bSum += b * w; lSum += l * w; sSum += s * w;
            }
        }
        if (wSum <= 0) return (ColorRgba.Black, 0.5, 0);
        var average = new ColorRgba(ColorMath.ClampByte(rSum / wSum * 255), ColorMath.ClampByte(gSum / wSum * 255), ColorMath.ClampByte(bSum / wSum * 255));
        return (average, lSum / wSum, sSum / wSum);
    }
}
