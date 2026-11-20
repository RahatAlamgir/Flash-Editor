using FlashEditor.AI.Inpainting;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Tests.Inpainting;

public class InpaintDetailTests
{
    /// <summary>A checker pattern: lots of fine detail.</summary>
    private static PixelBuffer Texture(int w, int h)
    {
        var p = new PixelBuffer(w, h);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            byte v = (byte)(((x + y) % 2 == 0) ? 150 : 110);
            p.SetPixel(x, y, new ColorRgba(v, v, v));
        }
        return p;
    }

    private static PixelBuffer Smooth(int w, int h)
    {
        var p = new PixelBuffer(w, h);
        p.Fill(new ColorRgba(130, 130, 130));
        return p;
    }

    [Fact]
    public void Detail_strength_is_high_for_texture_and_zero_for_flat_color()
    {
        var all = new PixelRect(0, 0, 64, 64);
        Assert.True(InpaintDetail.DetailStrength(Texture(64, 64), (_, _) => true, all) > 100);
        Assert.Equal(0, InpaintDetail.DetailStrength(Smooth(64, 64), (_, _) => true, all), 6);
    }

    [Fact]
    public void A_smooth_fill_next_to_texture_gets_sharpened_but_a_matching_one_is_left_alone()
    {
        Assert.Equal(0, InpaintDetail.AmountFor(10, 10.5));                 // about the same: nothing to do
        Assert.Equal(0, InpaintDetail.AmountFor(0, 10));                    // nothing to measure
        Assert.True(InpaintDetail.AmountFor(10, 20) > 0.5);                 // twice as much detail around: sharpen
        Assert.Equal(1.8, InpaintDetail.AmountFor(1, 100), 6);              // never more than a moderate amount
    }

    [Fact]
    public void Sharpening_adds_detail_only_inside_the_fill()
    {
        var original = Texture(80, 80);
        var fill = new PixelBuffer(80, 80);
        for (int y = 30; y < 50; y++) for (int x = 30; x < 50; x++)         // a fill with a soft gradient
        {
            byte v = (byte)(100 + x);
            fill.SetPixel(x, y, new ColorRgba(v, v, v));
        }
        var before = fill.Clone();
        InpaintDetail.Sharpen(fill, original, 1.5);

        var area = new PixelRect(30, 30, 20, 20);
        double detailBefore = InpaintDetail.DetailStrength(before, (x, y) => area.Contains(x, y), area);
        double detailAfter = InpaintDetail.DetailStrength(fill, (x, y) => area.Contains(x, y), area);
        Assert.True(detailAfter > detailBefore, $"{detailBefore} -> {detailAfter}");
        Assert.Equal(0, fill.GetPixel(5, 5).A);                             // nothing appears outside the fill
        Assert.Equal(0, fill.GetPixel(29, 40).A);
        Assert.Equal(255, fill.GetPixel(40, 40).A);                         // and the fill keeps its see-through edge settings
    }

    [Fact]
    public void An_amount_of_zero_changes_nothing()
    {
        var fill = new PixelBuffer(20, 20);
        fill.Fill(new ColorRgba(1, 2, 3));
        var copy = fill.Clone();
        InpaintDetail.Sharpen(fill, Texture(20, 20), 0);
        Assert.True(fill.ContentEquals(copy));
    }
}
