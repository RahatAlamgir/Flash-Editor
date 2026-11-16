using FlashEditor.Core.Effects;
using FlashEditor.Core.Effects.Adjustments;
using FlashEditor.Core.Effects.Background;
using FlashEditor.Core.Effects.Filters;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Tests.Effects;

public class EffectTests
{
    private static PixelBuffer Apply(ImageEffect effect, PixelBuffer image, Action<EffectValues>? configure = null)
    {
        var values = effect.CreateDefaultValues();
        configure?.Invoke(values);
        return effect.Apply(image, values);
    }

    [Fact]
    public void Invert_twice_is_identity()
    {
        var image = TestImages.Numbered(8, 8);
        var effect = new InvertEffect();
        Assert.True(image.ContentEquals(Apply(effect, Apply(effect, image))));
    }

    [Fact]
    public void Grayscale_makes_channels_equal()
    {
        var result = Apply(new GrayscaleEffect(), TestImages.Solid(2, 2, new ColorRgba(200, 50, 10)));
        var p = result.GetPixel(0, 0);
        Assert.Equal(p.R, p.G);
        Assert.Equal(p.G, p.B);
    }

    [Fact]
    public void Sepia_gives_warm_tone()
    {
        var p = Apply(new SepiaEffect(), TestImages.Solid(1, 1, new ColorRgba(100, 100, 100))).GetPixel(0, 0);
        Assert.True(p.R > p.G && p.G > p.B);
    }

    [Fact]
    public void Brightness_increases_values()
    {
        var p = Apply(new BrightnessContrastEffect(), TestImages.Solid(1, 1, new ColorRgba(100, 100, 100)),
            v => v["brightness"] = 50).GetPixel(0, 0);
        Assert.True(p.R > 100);
    }

    [Fact]
    public void Default_hue_saturation_changes_nothing()
    {
        var image = TestImages.Solid(3, 3, new ColorRgba(30, 160, 90));
        var p = Apply(new HueSaturationEffect(), image).GetPixel(1, 1);
        Assert.InRange(p.R, 29, 31);
        Assert.InRange(p.G, 159, 161);
        Assert.InRange(p.B, 89, 91);
    }

    [Fact]
    public void Blur_keeps_a_solid_image_unchanged()
    {
        var color = new ColorRgba(10, 200, 90);
        var result = Apply(new GaussianBlurEffect(), TestImages.Solid(30, 30, color), v => v["radius"] = 5);
        Assert.Equal(color, result.GetPixel(15, 15));
        Assert.Equal(color, result.GetPixel(0, 0));
    }

    [Fact]
    public void Blur_softens_an_edge()
    {
        var image = TestImages.Solid(20, 1, ColorRgba.Black);
        for (int x = 10; x < 20; x++) image.SetPixel(x, 0, ColorRgba.White);
        var result = Apply(new GaussianBlurEffect(), image, v => v["radius"] = 2);
        Assert.InRange(result.GetPixel(10, 0).R, 60, 200);
    }

    [Fact]
    public void Pixelate_makes_cells_uniform()
    {
        var result = Apply(new PixelateEffect(), TestImages.Numbered(8, 8), v => v["size"] = 4);
        Assert.Equal(result.GetPixel(0, 0), result.GetPixel(3, 3));
        Assert.NotEqual(result.GetPixel(0, 0), result.GetPixel(4, 4));
    }

    [Fact]
    public void Noise_is_deterministic_per_instance()
    {
        var effect = new NoiseEffect();
        var image = TestImages.Solid(16, 16, new ColorRgba(128, 128, 128));
        Assert.True(Apply(effect, image).ContentEquals(Apply(effect, image)));
        Assert.False(image.ContentEquals(Apply(effect, image)));
    }

    [Fact]
    public void Effect_only_changes_selected_area()
    {
        var image = TestImages.Solid(10, 10, ColorRgba.White);
        var selection = Selection.FromRectangle(new PixelRect(0, 0, 5, 10), 10, 10);
        var effect = new InvertEffect();
        var result = EffectApplier.Apply(image, effect, effect.CreateDefaultValues(), selection);
        Assert.Equal(ColorRgba.Black, result.GetPixel(2, 5));
        Assert.Equal(ColorRgba.White, result.GetPixel(7, 5));
    }

    [Fact]
    public void Background_removal_clears_plain_background_and_keeps_subject()
    {
        var image = TestImages.SubjectOnWhite(60, 20, new ColorRgba(200, 30, 30));
        var result = Apply(new BackgroundRemovalEffect(), image, v => v["feather"] = 0);

        Assert.Equal(0, result.GetPixel(0, 0).A);    // corner (background) removed
        Assert.Equal(0, result.GetPixel(15, 30).A);  // background left of the subject removed
        Assert.Equal(255, result.GetPixel(30, 30).A); // subject kept
    }

    [Fact]
    public void Background_detection_finds_the_border_color()
    {
        var colors = BackgroundRemovalEffect.DetectBorderColors(TestImages.SubjectOnWhite(40, 10, ColorRgba.Black));
        var (r, g, b) = colors[0];
        Assert.True(r > 250 && g > 250 && b > 250);
    }
}
