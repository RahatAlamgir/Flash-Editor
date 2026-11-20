using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

public class ThreeDAndShadowSizeTests
{
    /// <summary>A solid red square from (x, y) with the given side on a transparent buffer.</summary>
    private static PixelBuffer Square(int size, int x, int y, int side, ColorRgba? color = null)
    {
        var p = new PixelBuffer(size, size);
        for (int py = y; py < y + side; py++) for (int px = x; px < x + side; px++) p.SetPixel(px, py, color ?? new ColorRgba(200, 40, 40));
        return p;
    }

    private static int VisibleCount(PixelBuffer p)
    {
        int n = 0;
        for (int y = 0; y < p.Height; y++) for (int x = 0; x < p.Width; x++) if (p.GetPixel(x, y).A > 8) n++;
        return n;
    }

    // ---- shadow size

    [Fact]
    public void A_bigger_shadow_covers_more_and_a_smaller_one_less()
    {
        var src = Square(200, 80, 80, 40);
        var black = new ColorRgba(0, 0, 0, 255);
        int normal = VisibleCount(ShadowFx.Apply(src, black, 0, 0, 0));
        int big = VisibleCount(ShadowFx.Apply(src, black, 0, 0, 0, size: 2.0, centerX: 100, centerY: 100));
        int small = VisibleCount(ShadowFx.Apply(src, black, 0, 0, 0, size: 0.5, centerX: 100, centerY: 100));
        Assert.True(big > normal * 3);          // twice as wide and tall: about four times the area
        Assert.True(small <= normal);           // the object itself still covers its own place
        // the shadow is made around the object's middle: a big one sticks out on every side
        var bigShadow = ShadowFx.Apply(src, black, 0, 0, 0, 2.0, 100, 100);
        Assert.True(bigShadow.GetPixel(70, 100).A > 200 && bigShadow.GetPixel(130, 100).A > 200 && bigShadow.GetPixel(100, 70).A > 200);
    }

    [Fact]
    public void A_smaller_shadow_hides_behind_the_object_and_size_one_changes_nothing()
    {
        var src = Square(200, 80, 80, 40);
        var black = new ColorRgba(0, 0, 0, 255);
        var small = ShadowFx.Apply(src, black, 6, 6, 0, 0.5, 100, 100);
        Assert.Equal(new ColorRgba(200, 40, 40), small.GetPixel(100, 100));       // the object is on top
        var same = ShadowFx.Apply(src, black, 6, 6, 0, 1.0, 100, 100);
        var plain = ShadowFx.Apply(src, black, 6, 6, 0);
        Assert.Equal(plain.Data, same.Data);
    }

    [Fact]
    public void The_shadow_reaches_further_when_it_is_bigger()
    {
        var shadow = new ShadowStyle(ColorRgba.Black, 4, 45, 10);
        Assert.True(shadow.ExtentFor(100, 100) == shadow.Extent);
        Assert.True((shadow with { Size = 2 }).ExtentFor(100, 100) > shadow.Extent + 40);
        Assert.Equal(shadow.Extent, (shadow with { Size = 0.5 }).ExtentFor(100, 100));
    }

    [Fact]
    public void Presets_keep_the_size_of_the_shadow()
    {
        var current = ShadowStyle.Default with { Size = 1.7 };
        Assert.Equal(1.7, ShadowPreset.Hard.CreateStyle(current).Size);
        Assert.Equal(1.0, ShadowPreset.Hard.CreateStyle(null).Size);
    }

    // ---- 3D

    [Fact]
    public void The_side_runs_in_the_chosen_direction()
    {
        var src = Square(200, 60, 60, 40);
        var right = ThreeDFx.Apply(src, 30, 0, new ColorRgba(0, 0, 255), 0, 4);
        Assert.True(right.GetPixel(120, 80).B > 150 && right.GetPixel(120, 80).R < 30);   // right of the face: the (shaded) side color
        Assert.True(right.GetPixel(120, 80).A == 255);
        Assert.Equal(0, right.GetPixel(60 - 10, 80).A);                              // nothing on the other side
        Assert.Equal(0, right.GetPixel(80, 60 + 40 + 10).A);                         // nothing below

        var down = ThreeDFx.Apply(src, 0, 30, new ColorRgba(0, 0, 255), 0, 4);
        Assert.True(down.GetPixel(80, 120).A == 255);
        Assert.Equal(0, down.GetPixel(120, 80).A);
    }

    [Fact]
    public void The_side_is_as_deep_as_the_depth_and_no_deeper()
    {
        var src = Square(300, 60, 60, 40);
        foreach (int depth in new[] { 1, 5, 17, 40, 64, 100 })
        {
            var r = ThreeDFx.Apply(src, depth, 0, new ColorRgba(0, 0, 255), 0, 4);
            Assert.True(r.GetPixel(60 + 40 + depth - 1, 80).A == 255, $"depth {depth}: the last column of the side is missing");
            Assert.Equal(0, r.GetPixel(60 + 40 + depth + 1, 80).A);
        }
    }

    [Fact]
    public void A_diagonal_side_is_solid_with_no_gaps()
    {
        var src = Square(300, 60, 60, 30);
        var r = ThreeDFx.Apply(src, 40, 40, new ColorRgba(0, 0, 255), 0, 4);        // about 56 px deep to the bottom right
        for (int k = 0; k <= 36; k++) Assert.True(r.GetPixel(60 + 15 + k, 60 + 15 + k).A == 255, $"hole at step {k}");
    }

    [Fact]
    public void The_face_stays_on_top_and_the_side_gets_darker_with_depth()
    {
        var src = Square(300, 60, 60, 40, new ColorRgba(255, 255, 255));
        var r = ThreeDFx.Apply(src, 60, 0, new ColorRgba(200, 200, 200), 0, 4);
        Assert.Equal(new ColorRgba(255, 255, 255), r.GetPixel(80, 80));              // the face is untouched without a bevel
        Assert.True(r.GetPixel(105, 80).R > r.GetPixel(155, 80).R);                  // near the face lighter than far away
    }

    [Fact]
    public void Without_a_color_the_side_is_the_objects_own_color_but_darker()
    {
        var src = Square(300, 60, 60, 40, new ColorRgba(200, 100, 50));
        var r = ThreeDFx.Apply(src, 30, 0, null, 0, 4);
        var side = r.GetPixel(115, 80);
        Assert.True(side.R < 200 && side.R > 60);
        Assert.True(side.R > side.G && side.G > side.B);                              // still reddish
    }

    [Fact]
    public void A_bevel_lights_the_top_left_edge_and_shades_the_bottom_right_edge()
    {
        var src = Square(200, 60, 60, 80, new ColorRgba(128, 128, 128));
        var r = ThreeDFx.Apply(src, 0, 0, null, 1.0, 8);
        Assert.True(r.GetPixel(100, 61).R > 150);                                     // top edge lighter
        Assert.True(r.GetPixel(61, 100).R > 150);                                     // left edge lighter
        Assert.True(r.GetPixel(100, 138).R < 110);                                    // bottom edge darker
        Assert.True(r.GetPixel(138, 100).R < 110);                                    // right edge darker
        Assert.Equal(128, r.GetPixel(100, 100).R);                                    // the middle of the face is unchanged
        Assert.Equal(src.GetPixel(100, 100).A, r.GetPixel(100, 100).A);
    }

    [Fact]
    public void Nothing_changes_without_depth_and_bevel()
    {
        var src = Square(100, 30, 30, 30);
        var r = ThreeDFx.Apply(src, 0, 0, null, 0, 4);
        Assert.Equal(src.Data, r.Data);
    }

    // ---- the style and the transforms

    [Fact]
    public void The_style_has_its_direction_and_reach()
    {
        var t = new ThreeDStyle(Depth: 30, Angle: 90);
        Assert.Equal(0, t.OffsetX, 6);
        Assert.Equal(30, t.OffsetY, 6);
        Assert.True(t.Extent >= 30);
        Assert.Equal(60, t.Scaled(2).Depth);
        Assert.Equal(180, t.Rotated(90).Angle);
    }

    [Fact]
    public void Resizing_and_flipping_an_object_carries_the_3d_look_along()
    {
        ShapeObject shape = new(new ObjectBox(10, 10, 100, 100), ShapeKind.Rectangle, new ColorRgba(1, 2, 3), null, 0) { ThreeD = new ThreeDStyle(20, 45, null, 0.5), Shadow = ShadowStyle.Default with { Size = 1.5 } };
        var bigger = (ShapeObject)ObjectTransforms.Scale(shape, 2, 2);
        Assert.Equal(40, bigger.ThreeD!.Depth, 6);
        Assert.Equal(1.5, bigger.Shadow!.Size);

        var flipped = ObjectTransforms.FlipHorizontal(shape, 400);
        Assert.Equal(135, flipped.ThreeD!.Angle, 6);
        var up = ObjectTransforms.FlipVertical(shape, 400);
        Assert.Equal(-45, up.ThreeD!.Angle, 6);
        var turned = ObjectTransforms.Rotate(shape, 90, 400, 400, 400, 400);
        Assert.Equal(135, turned.ThreeD!.Angle, 6);
    }

    [Fact]
    public void Presets_make_a_style_and_keep_the_chosen_color()
    {
        var current = new ThreeDStyle(10, 0, new ColorRgba(9, 9, 9), 0);
        var made = ThreeDPreset.Pop.CreateStyle(current);
        Assert.Equal(ThreeDPreset.Pop.Depth, made.Depth);
        Assert.Equal(new ColorRgba(9, 9, 9), made.Color);
        Assert.Null(ThreeDPreset.Block.CreateStyle(null).Color);
        Assert.Equal(5, ThreeDPreset.All.Count);
    }
}
