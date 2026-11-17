using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

/// <summary>Finding the picture's own pixel under a point of the canvas (so a brush can paint on the picture itself).</summary>
public class ImageObjectMappingTests
{
    private static ImageObject Picture(ObjectBox box, bool flipX = false, bool flipY = false)
        => new(box, new PixelBuffer(100, 50), "p", flipX, flipY);

    private static void AssertPoint(PointD expected, PointD actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
    }

    [Fact]
    public void A_picture_shown_at_its_own_size_maps_one_to_one()
    {
        var p = Picture(new ObjectBox(200, 100, 100, 50));
        AssertPoint(new PointD(0, 0), p.ToSource(new PointD(200, 100)));
        AssertPoint(new PointD(30, 20), p.ToSource(new PointD(230, 120)));
        AssertPoint(new PointD(100, 50), p.ToSource(new PointD(300, 150)));
        Assert.Equal(1.0, p.SourcePixelsPerCanvasPixel, 6);
    }

    [Fact]
    public void Resizing_changes_the_scale()
    {
        var p = Picture(new ObjectBox(0, 0, 400, 200));                 // 4 times larger on the canvas
        AssertPoint(new PointD(50, 25), p.ToSource(new PointD(200, 100)));
        Assert.Equal(0.25, p.SourcePixelsPerCanvasPixel, 6);

        var small = Picture(new ObjectBox(0, 0, 50, 25));                // half size: each canvas pixel covers 2 picture pixels
        Assert.Equal(2.0, small.SourcePixelsPerCanvasPixel, 6);
    }

    [Fact]
    public void A_moved_picture_maps_relative_to_where_it_is_now()
    {
        var p = Picture(new ObjectBox(500, 300, 100, 50));
        AssertPoint(new PointD(10, 10), p.ToSource(new PointD(510, 310)));
        AssertPoint(new PointD(-10, -10), p.ToSource(new PointD(490, 290)));    // outside the picture: negative, still well defined
    }

    [Fact]
    public void A_rotated_picture_maps_back_to_upright_pixels()
    {
        // Turned 90 degrees clockwise around its center (150, 125): the picture's right edge now points down.
        var p = Picture(new ObjectBox(100, 100, 100, 50, 90));
        var center = p.Box.Center;
        AssertPoint(new PointD(50, 25), p.ToSource(center));
        // The picture's top-left corner (0,0) is now at the top-right of the turned picture.
        AssertPoint(new PointD(0, 0), p.ToSource(p.Box.ToCanvas(-50, -25)));
        AssertPoint(new PointD(100, 0), p.ToSource(p.Box.ToCanvas(50, -25)));
        AssertPoint(new PointD(100, 50), p.ToSource(p.Box.ToCanvas(50, 25)));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void A_mirrored_picture_maps_back_mirrored_pixels(bool flipX, bool flipY)
    {
        var p = Picture(new ObjectBox(0, 0, 100, 50), flipX, flipY);
        // The picture's left-top pixel is drawn at the opposite side when mirrored.
        var screenTopLeft = new PointD(flipX ? 100 : 0, flipY ? 50 : 0);
        AssertPoint(new PointD(0, 0), p.ToSource(screenTopLeft));
        AssertPoint(new PointD(100, 50), p.ToSource(new PointD(flipX ? 0 : 100, flipY ? 0 : 50)));
    }

    [Fact]
    public void Rotated_mirrored_and_resized_together_still_round_trip_through_the_renderer_transform()
    {
        // The renderer draws a picture point (u, v) at: center + Rotate(flip * (point - size/2)). ToSource must undo exactly that.
        var p = new ImageObject(new ObjectBox(40, 60, 300, 120, 33), new PixelBuffer(60, 24), "p", FlipX: true, FlipY: false);
        double a = p.Box.Rotation * Math.PI / 180;
        foreach (var (u, v) in new[] { (0.0, 0.0), (60.0, 24.0), (13.5, 7.25), (59, 1) })
        {
            double lx = (u / 60 * 300 - 150) * (p.FlipX ? -1 : 1), ly = (v / 24 * 120 - 60) * (p.FlipY ? -1 : 1);
            var canvas = new PointD(p.Box.Center.X + lx * Math.Cos(a) - ly * Math.Sin(a), p.Box.Center.Y + lx * Math.Sin(a) + ly * Math.Cos(a));
            AssertPoint(new PointD(u, v), p.ToSource(canvas));
        }
    }

    [Fact]
    public void Editing_the_pixels_makes_a_new_object_that_keeps_the_original_picture()
    {
        var original = new PixelBuffer(100, 50);
        var edited = original.Clone();
        var p = Picture(new ObjectBox(0, 0, 100, 50));

        var next = p.WithEditedSource(edited, original);
        Assert.NotEqual(p, next);                        // different, so undo / redo and redrawing treat it as a change
        Assert.Same(edited, next.Source);
        Assert.Same(original, next.Original);
        Assert.Equal(p.Revision + 1, next.Revision);
        Assert.Equal(p.Box, next.Box);
    }

    [Fact]
    public void Moving_a_picture_keeps_its_original()
    {
        var original = new PixelBuffer(100, 50);
        var p = Picture(new ObjectBox(0, 0, 100, 50)) with { Original = original };
        var moved = (ImageObject)ObjectTransforms.Offset(p, 40, 25);
        var scaled = (ImageObject)ObjectTransforms.Scale(p, 2, 2);
        Assert.Same(original, moved.Original);
        Assert.Same(original, scaled.Original);
    }
}

/// <summary>Clicking a picture only counts where the picture has visible pixels.</summary>
public class ImageObjectClickTests
{
    private static ImageObject CutOut(ObjectBox box)
    {
        // 100 x 50 picture: only the left half is solid, the right half has been removed (transparent).
        var source = new PixelBuffer(100, 50);
        for (int y = 0; y < 50; y++)
            for (int x = 0; x < 50; x++) source.SetPixel(x, y, new ColorRgba(10, 20, 30, 255));
        return new ImageObject(box, source);
    }

    [Fact]
    public void Solid_parts_can_be_clicked_and_removed_parts_cannot()
    {
        var picture = CutOut(new ObjectBox(0, 0, 100, 50));
        Assert.True(picture.HasContentAt(new PointD(20, 25)));
        Assert.False(picture.HasContentAt(new PointD(80, 25)));       // removed part: click goes through
        Assert.False(picture.HasContentAt(new PointD(-20, 25)));      // outside the picture
    }

    [Fact]
    public void A_small_margin_makes_thin_edges_easier_to_hit()
    {
        var picture = CutOut(new ObjectBox(0, 0, 100, 50));
        Assert.False(picture.HasContentAt(new PointD(53, 25), radius: 0));
        Assert.True(picture.HasContentAt(new PointD(53, 25), radius: 4));
    }

    [Fact]
    public void It_works_for_moved_resized_and_rotated_pictures()
    {
        var picture = CutOut(new ObjectBox(300, 200, 400, 200, 90));    // 4 times larger, turned a quarter
        // The solid left half of the picture is now the top half (turned 90 degrees clockwise): above the center.
        var center = picture.Box.Center;
        Assert.True(picture.HasContentAt(new PointD(center.X, center.Y - 60)));
        Assert.False(picture.HasContentAt(new PointD(center.X, center.Y + 60)));
    }
}
