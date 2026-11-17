using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

/// <summary>Drop shadows: the shadow picture itself and how shadows follow object transforms.</summary>
public class ShadowTests
{
    private static PixelBuffer RedSquareOnTransparent(int size = 40, int square = 10, int at = 10)
    {
        var buffer = new PixelBuffer(size, size);
        for (int y = at; y < at + square; y++)
            for (int x = at; x < at + square; x++)
                buffer.SetPixel(x, y, new ColorRgba(255, 0, 0));
        return buffer;
    }

    [Fact]
    public void Sharp_shadow_is_the_shape_shifted_and_tinted()
    {
        var result = ShadowFx.Apply(RedSquareOnTransparent(), new ColorRgba(0, 0, 255, 200), 5, 5, blur: 0);

        var onObject = result.GetPixel(12, 12);           // object stays on top, unchanged
        Assert.Equal(new ColorRgba(255, 0, 0), onObject);

        var inShadow = result.GetPixel(22, 22);           // shifted square covers 15..24, the object only 10..19
        Assert.Equal(200, inShadow.A);
        Assert.Equal(255, inShadow.B);
        Assert.Equal(0, inShadow.R);

        Assert.Equal(0, result.GetPixel(30, 30).A);       // nothing beyond the shifted shape
        Assert.Equal(0, result.GetPixel(11, 3).A);        // and nothing on the side it was shifted away from
    }

    [Fact]
    public void Blurred_shadow_fades_out_and_spreads_beyond_the_shape()
    {
        var result = ShadowFx.Apply(RedSquareOnTransparent(), ColorRgba.Black, 0, 0, blur: 8);

        int centerAlpha = result.GetPixel(15, 15).A;
        int edgeOutside = result.GetPixel(7, 15).A;        // 3 px outside the left edge
        int farOutside = result.GetPixel(0, 15).A;
        Assert.Equal(255, centerAlpha);
        Assert.InRange(edgeOutside, 1, 254);               // soft: partly transparent next to the shape
        Assert.True(farOutside < edgeOutside);
    }

    [Fact]
    public void Shadow_strength_comes_from_the_color_alpha()
    {
        var result = ShadowFx.Apply(RedSquareOnTransparent(), new ColorRgba(0, 0, 0, 100), 15, 0, blur: 0);
        Assert.InRange((int)result.GetPixel(30, 15).A, 99, 101);
    }

    [Fact]
    public void Scaling_an_object_scales_its_shadow()
    {
        var shape = new ShapeObject(new ObjectBox(0, 0, 10, 10), ShapeKind.Rectangle, ColorRgba.Black, null, 0)
        {
            Shadow = new ShadowStyle(ColorRgba.Black, Distance: 10, Angle: 30, Blur: 6),
        };
        var scaled = ObjectTransforms.Scale(shape, 2, 2);
        Assert.Equal(20, scaled.Shadow!.Distance, 6);
        Assert.Equal(12, scaled.Shadow.Blur, 6);
        Assert.Equal(30, scaled.Shadow.Angle);
    }

    [Fact]
    public void Flipping_and_rotating_the_picture_turns_the_shadow_direction()
    {
        var shape = new ShapeObject(new ObjectBox(0, 0, 10, 10), ShapeKind.Rectangle, ColorRgba.Black, null, 0)
        {
            Shadow = new ShadowStyle(ColorRgba.Black, 10, Angle: 45, Blur: 0),   // down and to the right
        };

        Assert.Equal(135, ObjectTransforms.FlipHorizontal(shape, 100).Shadow!.Angle, 6);  // down and to the left
        Assert.Equal(-45, ObjectTransforms.FlipVertical(shape, 100).Shadow!.Angle, 6);    // up and to the right
        Assert.Equal(135, ObjectTransforms.Rotate(shape, 90, 100, 100, 100, 100).Shadow!.Angle, 6);
    }

    [Fact]
    public void There_are_ten_different_presets_with_sensible_values()
    {
        Assert.Equal(new[] { "Soft", "Hard", "Lifted", "Subtle", "Contact", "Sticker", "Long", "Glow", "Neon", "Left" }, ShadowPreset.All.Select(p => p.Name));
        Assert.Equal(ShadowPreset.All.Count, ShadowPreset.All.Select(p => p.Name).Distinct().Count());
        Assert.Equal(ShadowPreset.All.Count, ShadowPreset.All.Select(p => p.CreateStyle()).Distinct().Count()); // every preset looks different

        var hard = ShadowPreset.Hard.CreateStyle();
        Assert.Equal(0, hard.Blur);                                           // hard = no blur
        Assert.True(ShadowPreset.Lifted.CreateStyle().Blur > ShadowPreset.Soft.CreateStyle().Blur);
        Assert.True(ShadowPreset.Lifted.CreateStyle().Distance > ShadowPreset.Soft.CreateStyle().Distance);
        Assert.All(ShadowPreset.All, p => Assert.InRange(p.CreateStyle().Color.A, 40, 255)); // visible, never invisible
    }

    [Fact]
    public void A_preset_with_its_own_color_always_uses_it()
    {
        var purple = new ShadowStyle(new ColorRgba(120, 40, 200, 255), 3, 10, 2);
        var glow = ShadowPreset.Glow.CreateStyle(purple);
        Assert.Equal(ShadowPreset.Glow.Color!.Value.R, glow.Color.R);
        Assert.Equal(ShadowPreset.Glow.Color.Value.B, glow.Color.B);
        Assert.Equal(ShadowPreset.Glow.Opacity, glow.Color.A);
        Assert.Equal(0, glow.Distance); // a glow is not shifted
        Assert.True(glow.Blur > 20);
    }

    [Fact]
    public void A_preset_keeps_the_color_of_an_existing_shadow()
    {
        var purple = new ShadowStyle(new ColorRgba(120, 40, 200, 255), 3, 10, 2);
        var result = ShadowPreset.Soft.CreateStyle(purple);

        Assert.Equal((byte)120, result.Color.R);
        Assert.Equal((byte)200, result.Color.B);
        Assert.Equal(ShadowPreset.Soft.Opacity, result.Color.A);              // strength comes from the preset
        Assert.Equal(ShadowPreset.Soft.Blur, result.Blur);
        Assert.Equal(ColorRgba.Black.R, ShadowPreset.Soft.CreateStyle().Color.R); // no shadow yet: black
    }

    [Fact]
    public void Shadow_is_part_of_the_object_so_undo_can_compare_it()
    {
        var plain = new ShapeObject(new ObjectBox(0, 0, 10, 10), ShapeKind.Rectangle, ColorRgba.Black, null, 0);
        var shadowed = plain with { Shadow = ShadowStyle.Default };
        Assert.NotEqual(plain, shadowed);
        Assert.Equal(shadowed, plain with { Shadow = ShadowStyle.Default });
    }
}
