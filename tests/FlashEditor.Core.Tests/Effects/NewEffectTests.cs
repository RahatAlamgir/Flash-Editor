using FlashEditor.Core.Effects;
using FlashEditor.Core.Effects.Adjustments;
using FlashEditor.Core.Effects.Filters;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests.Effects;

public class NewEffectTests
{
    private static PixelBuffer Apply(ImageEffect effect, PixelBuffer image, Action<EffectValues>? configure = null)
    {
        var values = effect.CreateDefaultValues();
        configure?.Invoke(values);
        return effect.Apply(image, values);
    }

    private static readonly ColorRgba Gray = new(100, 100, 100);

    [Fact]
    public void Exposure_plus_one_stop_doubles_brightness()
    {
        var p = Apply(new ExposureEffect(), TestImages.Solid(1, 1, Gray), v => v["exposure"] = 1).GetPixel(0, 0);
        Assert.InRange(p.R, 198, 202);
    }

    [Fact]
    public void Warm_temperature_adds_red_and_removes_blue()
    {
        var p = Apply(new TemperatureEffect(), TestImages.Solid(1, 1, Gray), v => v["temperature"] = 100).GetPixel(0, 0);
        Assert.True(p.R > 100 && p.B < 100);
    }

    [Fact]
    public void Vibrance_leaves_gray_unchanged()
    {
        Assert.Equal(Gray, Apply(new VibranceEffect(), TestImages.Solid(1, 1, Gray)).GetPixel(0, 0));
    }

    [Fact]
    public void Posterize_two_levels_gives_only_black_or_white_channels()
    {
        var result = Apply(new PosterizeEffect(), TestImages.Numbered(16, 16), v => v["levels"] = 2);
        for (int i = 0; i < result.Data.Length; i += 4)
            Assert.True(result.Data[i] is 0 or 255);
    }

    [Fact]
    public void Threshold_makes_pure_black_and_white()
    {
        var image = TestImages.Solid(2, 1, new ColorRgba(30, 30, 30));
        image.SetPixel(1, 0, new ColorRgba(220, 220, 220));
        var result = Apply(new ThresholdEffect(), image);
        Assert.Equal(ColorRgba.Black, result.GetPixel(0, 0));
        Assert.Equal(ColorRgba.White, result.GetPixel(1, 0));
    }

    [Fact]
    public void Auto_levels_stretches_a_flat_image()
    {
        var image = new PixelBuffer(10, 1);
        for (int x = 0; x < 10; x++) image.SetPixel(x, 0, new ColorRgba((byte)(100 + x * 5), (byte)(100 + x * 5), (byte)(100 + x * 5)));
        var result = Apply(new AutoContrastEffect(), image);
        Assert.True(result.GetPixel(0, 0).R < 20);
        Assert.True(result.GetPixel(9, 0).R > 235);
    }

    [Fact]
    public void Vignette_darkens_corners_but_not_the_center()
    {
        var result = Apply(new VignetteEffect(), TestImages.Solid(100, 100, ColorRgba.White), v => v["amount"] = 100);
        Assert.Equal(255, result.GetPixel(50, 50).R);
        Assert.True(result.GetPixel(0, 0).R < 80);
    }

    [Fact]
    public void Find_edges_on_a_flat_image_is_blank()
    {
        var result = Apply(new FindEdgesEffect(), TestImages.Solid(10, 10, Gray));
        Assert.Equal(ColorRgba.White, result.GetPixel(5, 5)); // sketch mode: white paper
    }

    [Fact]
    public void Emboss_on_a_flat_image_is_mid_gray()
    {
        var p = Apply(new EmbossEffect(), TestImages.Solid(10, 10, Gray)).GetPixel(5, 5);
        Assert.Equal(128, p.R);
    }

    [Fact]
    public void Motion_blur_keeps_a_solid_image()
    {
        Assert.Equal(Gray, Apply(new MotionBlurEffect(), TestImages.Solid(30, 30, Gray), v => v["angle"] = 45).GetPixel(15, 15));
    }

    [Fact]
    public void Reduce_noise_removes_a_single_speck()
    {
        var image = TestImages.Solid(9, 9, Gray);
        image.SetPixel(4, 4, ColorRgba.White);
        Assert.Equal(Gray, Apply(new ReduceNoiseEffect(), image).GetPixel(4, 4));
    }
}
