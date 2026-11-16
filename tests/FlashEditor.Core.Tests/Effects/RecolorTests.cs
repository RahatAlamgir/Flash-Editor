using FlashEditor.Core.Effects.Adjustments;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Tests.Effects;

/// <summary>Recolor: new color, but folds (dark) and highlights (light) are kept.</summary>
public class RecolorTests
{
    /// <summary>A "red dress": shadow, mid tone and highlight of the same red.</summary>
    private static PixelBuffer RedFabric()
    {
        var image = new PixelBuffer(3, 1);
        image.SetPixel(0, 0, new ColorRgba(110, 20, 25));   // fold (dark)
        image.SetPixel(1, 0, new ColorRgba(200, 40, 45));   // main color
        image.SetPixel(2, 0, new ColorRgba(240, 120, 125)); // highlight
        return image;
    }

    private static double Lightness(ColorRgba c)
    {
        ColorMath.RgbToHsl(c.R / 255.0, c.G / 255.0, c.B / 255.0, out _, out _, out double l);
        return l;
    }

    [Fact]
    public void Recolor_to_blue_keeps_dark_and_light_parts()
    {
        var image = RedFabric();
        var (_, l, s) = RecolorEffect.Measure(image, null);
        var effect = new RecolorEffect(l, s);
        var values = effect.CreateDefaultValues();
        RecolorEffect.SetTarget(values, new ColorRgba(30, 80, 200)); // blue
        var result = effect.Apply(image, values);

        var fold = result.GetPixel(0, 0);
        var main = result.GetPixel(1, 0);
        var highlight = result.GetPixel(2, 0);
        foreach (var p in new[] { fold, main, highlight }) Assert.True(p.B > p.R && p.B > p.G, $"{p} should be blue");
        Assert.True(Lightness(fold) < Lightness(main) && Lightness(main) < Lightness(highlight), "shading order is kept");
        Assert.True(Lightness(highlight) - Lightness(fold) > 0.2, "folds and highlights are still clearly different");
    }

    [Fact]
    public void Zero_shading_gives_one_flat_color()
    {
        var image = RedFabric();
        var (_, l, s) = RecolorEffect.Measure(image, null);
        var effect = new RecolorEffect(l, s);
        var values = effect.CreateDefaultValues();
        RecolorEffect.SetTarget(values, new ColorRgba(30, 160, 60));
        values["shading"] = 0;
        var result = effect.Apply(image, values);
        Assert.Equal(result.GetPixel(0, 0), result.GetPixel(2, 0));
    }

    [Fact]
    public void Measure_uses_only_the_selection()
    {
        var image = new PixelBuffer(2, 1);
        image.SetPixel(0, 0, ColorRgba.Black);
        image.SetPixel(1, 0, ColorRgba.White);
        var selection = Selection.FromRectangle(new PixelRect(1, 0, 1, 1), 2, 1);
        var (average, lightness, _) = RecolorEffect.Measure(image, selection);
        Assert.Equal(ColorRgba.White, average);
        Assert.Equal(1, lightness, 3);
    }
}
