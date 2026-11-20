using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests.Objects;

public class TemperatureAdjustmentTests
{
    private static (byte R, byte G, byte B) Run(AdjustmentStyle style, byte r, byte g, byte b)
    {
        style.Apply(ref b, ref g, ref r);
        return (r, g, b);
    }

    [Fact]
    public void Warm_adds_red_and_removes_blue_and_cool_does_the_opposite()
    {
        var warm = Run(new AdjustmentStyle(Temperature: 100), 128, 128, 128);
        Assert.True(warm.R > 128 && warm.B < 128);
        Assert.Equal(128, warm.G);

        var cool = Run(new AdjustmentStyle(Temperature: -100), 128, 128, 128);
        Assert.True(cool.R < 128 && cool.B > 128);
    }

    [Fact]
    public void Tint_moves_green_against_magenta()
    {
        var magenta = Run(new AdjustmentStyle(Tint: 100), 128, 128, 128);
        Assert.True(magenta.G < 128 && magenta.R > 128 && magenta.B > 128);
        var green = Run(new AdjustmentStyle(Tint: -100), 128, 128, 128);
        Assert.True(green.G > 128);
    }

    [Fact]
    public void Temperature_and_tint_count_as_a_change()
    {
        Assert.True(AdjustmentStyle.Neutral.IsNeutral);
        Assert.False(new AdjustmentStyle(Temperature: 5).IsNeutral);
        Assert.False(new AdjustmentStyle(Tint: -5).IsNeutral);
    }

    [Fact]
    public void Temperature_matches_the_Temperature_effect_in_the_Adjust_menu()
    {
        var effect = new Core.Effects.Adjustments.TemperatureEffect();
        var values = effect.CreateDefaultValues();
        values["temperature"] = 50;
        var picture = new PixelBuffer(1, 1);
        picture.SetPixel(0, 0, new ColorRgba(100, 120, 140));
        var result = effect.Apply(picture, values).GetPixel(0, 0);

        var viaStyle = Run(new AdjustmentStyle(Temperature: 50), 100, 120, 140);
        Assert.InRange(Math.Abs(result.R - viaStyle.R), 0, 1);
        Assert.InRange(Math.Abs(result.B - viaStyle.B), 0, 1);
    }
}
