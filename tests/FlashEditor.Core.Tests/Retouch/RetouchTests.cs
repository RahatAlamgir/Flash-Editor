using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Retouch;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Tests.Retouch;

public class RetouchTests
{
    private static PixelBuffer Solid(int w, int h, ColorRgba c)
    {
        var p = new PixelBuffer(w, h);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) p.SetPixel(x, y, c);
        return p;
    }

    private static PixelBuffer Run(PixelBuffer source, IRetouchOp op, double strength = 1.0, int? onlyX = null)
    {
        var result = source.Clone();
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
                if (onlyX == null || x == onlyX) op.Apply(source, result.Data, x, y, strength);
        return result;
    }

    [Fact]
    public void Clone_copies_the_pixel_from_the_chosen_distance()
    {
        var p = Solid(20, 1, new ColorRgba(0, 0, 255));
        for (int x = 10; x < 20; x++) p.SetPixel(x, 0, new ColorRgba(255, 0, 0));
        var r = Run(p, new CloneOp(10, 0), 1.0, onlyX: 2);
        Assert.Equal(new ColorRgba(255, 0, 0), r.GetPixel(2, 0));      // copied from x=12
        Assert.Equal(new ColorRgba(0, 0, 255), r.GetPixel(3, 0));      // others untouched
    }

    [Fact]
    public void Clone_half_strength_mixes_and_outside_the_picture_changes_nothing()
    {
        var p = Solid(20, 1, new ColorRgba(0, 0, 0));
        for (int x = 10; x < 20; x++) p.SetPixel(x, 0, new ColorRgba(200, 200, 200));
        Assert.InRange(Run(p, new CloneOp(10, 0), 0.5, 2).GetPixel(2, 0).R, 98, 102);
        Assert.Equal(new ColorRgba(200, 200, 200), Run(p, new CloneOp(10, 0), 1.0, 15).GetPixel(15, 0));   // x+10 is off the picture
    }

    [Fact]
    public void Heal_brings_the_texture_but_keeps_the_tone_of_the_target()
    {
        // left half: dark gray flat. right half: bright gray with one darker speck.
        var p = Solid(60, 21, new ColorRgba(60, 60, 60));
        for (int y = 0; y < 21; y++) for (int x = 30; x < 60; x++) p.SetPixel(x, y, new ColorRgba(180, 180, 180));
        p.SetPixel(45, 10, new ColorRgba(120, 120, 120));               // detail in the bright half

        var cloned = Run(p, new CloneOp(30, 0), 1.0, onlyX: 14).GetPixel(14, 10);
        var healed = Run(p, new HealOp(30, 0, 8), 1.0, onlyX: 14).GetPixel(14, 10);
        var speck = Run(p, new HealOp(30, 0, 8), 1.0, onlyX: 15).GetPixel(15, 10);
        Assert.True(cloned.R >= 115);                                     // clone drags the bright tone along
        Assert.InRange(healed.R, 55, 65);                                 // heal stays dark like the target
        Assert.True(speck.R < 20);                                        // and the speck from the source is still darker than its surroundings
    }

    [Fact]
    public void Dodge_lightens_and_burn_darkens_and_alpha_is_kept()
    {
        var p = Solid(3, 1, new ColorRgba(100, 100, 100, 200));
        var dodged = Run(p, new ToneOp(false, ToneRange.Midtones), 0.5).GetPixel(1, 0);
        var burned = Run(p, new ToneOp(true, ToneRange.Midtones), 0.5).GetPixel(1, 0);
        Assert.True(dodged.R > 100);
        Assert.True(burned.R < 100);
        Assert.Equal(200, dodged.A);
        Assert.Equal(200, burned.A);
    }

    [Fact]
    public void The_range_decides_which_pixels_change_most()
    {
        var bright = Solid(1, 1, new ColorRgba(225, 225, 225));
        int Change(PixelBuffer p, ToneRange r) => Math.Abs(Run(p, new ToneOp(true, r), 0.8).GetPixel(0, 0).R - p.GetPixel(0, 0).R);
        Assert.True(Change(bright, ToneRange.Highlights) > Change(bright, ToneRange.Shadows));
        Assert.True(Change(bright, ToneRange.Highlights) > Change(bright, ToneRange.Midtones));
    }

    [Fact]
    public void Blur_softens_an_edge_and_sharpen_makes_it_steeper()
    {
        var p = Solid(20, 5, new ColorRgba(50, 50, 50));
        for (int y = 0; y < 5; y++) for (int x = 10; x < 20; x++) p.SetPixel(x, y, new ColorRgba(200, 200, 200));

        var blurred = Run(p, new BlurOp(3), 1.0);
        Assert.InRange(blurred.GetPixel(9, 2).R, 60, 190);                // the dark side of the edge got lighter
        Assert.InRange(blurred.GetPixel(10, 2).R, 60, 190);               // and the bright side darker
        Assert.Equal(50, blurred.GetPixel(2, 2).R);                       // flat areas stay the same

        var sharp = Run(p, new SharpenOp(2, 1.6), 1.0);                   // sharpening the crisp edge exaggerates it
        int before = p.GetPixel(10, 2).R - p.GetPixel(9, 2).R;
        int after = sharp.GetPixel(10, 2).R - sharp.GetPixel(9, 2).R;
        Assert.True(after > before);
    }

    [Fact]
    public void Spot_heal_finds_a_clean_place_and_not_the_blemish()
    {
        var p = new PixelBuffer(200, 200);
        for (int y = 0; y < 200; y++) for (int x = 0; x < 200; x++) p.SetPixel(x, y, new ColorRgba((byte)(100 + x / 4), 120, 120));   // smooth gradient
        var blemish = new PixelRect(95, 95, 10, 10);
        for (int y = blemish.Y; y < blemish.Bottom; y++) for (int x = blemish.X; x < blemish.Right; x++) p.SetPixel(x, y, new ColorRgba(0, 0, 0));

        var offset = SpotHeal.FindOffset(p, blemish);
        Assert.NotNull(offset);
        var source = new PixelRect(blemish.X + offset!.Value.Dx, blemish.Y + offset.Value.Dy, 10, 10);
        Assert.True(source.Intersect(blemish).IsEmpty);
        Assert.True(source.Intersect(p.Bounds) == source);

        var heal = new HealOp(offset.Value.Dx, offset.Value.Dy, 12);
        heal.Attach((x, y) => blemish.Contains(x, y));
        var healed = Run(p, heal, 1.0);
        Assert.InRange(healed.GetPixel(100, 100).R, 110, 140);            // the black spot is gone and the gradient tone is back
    }

    [Fact]
    public void Spot_heal_gives_up_when_there_is_no_room()
    {
        var p = Solid(30, 30, new ColorRgba(1, 2, 3));
        Assert.Null(SpotHeal.FindOffset(p, new PixelRect(0, 0, 30, 30)));
        Assert.Null(SpotHeal.FindOffset(p, PixelRect.Empty));
    }

    [Fact]
    public void A_patch_changes_only_the_outlined_area()
    {
        var p = Solid(40, 40, new ColorRgba(0, 0, 255));
        for (int y = 0; y < 40; y++) for (int x = 20; x < 40; x++) p.SetPixel(x, y, new ColorRgba(255, 0, 0));
        var region = Selection.FromRectangle(new PixelRect(5, 5, 10, 10), 40, 40)!;
        var target = p.Clone();
        RetouchRegion.Apply(target, p, new CloneOp(20, 0), region, null, region.Bounds.Inflate(3));
        Assert.Equal(new ColorRgba(255, 0, 0), target.GetPixel(10, 10));  // inside: now red
        Assert.Equal(new ColorRgba(0, 0, 255), target.GetPixel(2, 2));    // outside: unchanged
        Assert.Equal(new ColorRgba(0, 0, 255), target.GetPixel(16, 10));  // just outside the area: unchanged
    }

    [Fact]
    public void A_stroke_with_an_operation_works_and_can_be_redone_with_another()
    {
        var p = Solid(60, 60, new ColorRgba(100, 100, 100));
        var stroke = new BrushStroke(p, new BrushSettings(20, 1.0, 1.0), new ToneOp(false, ToneRange.Midtones), null);
        stroke.AddPoint(new PointD(30, 30));
        Assert.True(p.GetPixel(30, 30).R > 100);
        Assert.Equal(100, p.GetPixel(2, 2).R);                            // far from the brush

        stroke.Reapply(new ToneOp(true, ToneRange.Midtones));
        Assert.True(p.GetPixel(30, 30).R < 100);                          // redone from the untouched pixels, not on top of the first result
    }
}
