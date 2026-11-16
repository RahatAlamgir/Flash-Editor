using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Tests.Painting;

public class PaintingTests
{
    [Fact]
    public void Flood_fill_stops_at_different_colors()
    {
        var image = TestImages.Solid(10, 10, ColorRgba.White);
        for (int y = 0; y < 10; y++) image.SetPixel(5, y, ColorRgba.Black); // wall

        FloodFill.Fill(image, 1, 1, new ColorRgba(255, 0, 0), tolerance: 10, contiguous: true, selection: null);

        Assert.Equal(new ColorRgba(255, 0, 0), image.GetPixel(0, 9));
        Assert.Equal(ColorRgba.Black, image.GetPixel(5, 5));
        Assert.Equal(ColorRgba.White, image.GetPixel(8, 8));
    }

    [Fact]
    public void Non_contiguous_fill_reaches_everywhere()
    {
        var image = TestImages.Solid(10, 10, ColorRgba.White);
        for (int y = 0; y < 10; y++) image.SetPixel(5, y, ColorRgba.Black);

        FloodFill.Fill(image, 1, 1, new ColorRgba(255, 0, 0), 10, contiguous: false, selection: null);
        Assert.Equal(new ColorRgba(255, 0, 0), image.GetPixel(8, 8));
    }

    [Fact]
    public void Fill_respects_selection()
    {
        var image = TestImages.Solid(10, 10, ColorRgba.White);
        var selection = Selection.FromRectangle(new PixelRect(0, 0, 3, 3), 10, 10);
        FloodFill.Fill(image, 1, 1, ColorRgba.Black, 10, true, selection);
        Assert.Equal(ColorRgba.Black, image.GetPixel(2, 2));
        Assert.Equal(ColorRgba.White, image.GetPixel(4, 4));
    }

    [Fact]
    public void Overlapping_dabs_do_not_build_up_opacity()
    {
        var image = TestImages.Solid(40, 40, ColorRgba.White);
        var stroke = new BrushStroke(image, new BrushSettings(10, 1.0, 0.5), ColorRgba.Black, erase: false, selection: null);

        // Go back and forth over the same spot many times.
        for (int i = 0; i < 10; i++)
        {
            stroke.AddPoint(new PointD(10, 20));
            stroke.AddPoint(new PointD(30, 20));
        }
        var p = image.GetPixel(20, 20);
        Assert.InRange(p.R, 125, 130); // 50% black over white ≈ 128
    }

    [Fact]
    public void Eraser_makes_pixels_transparent()
    {
        var image = TestImages.Solid(20, 20, ColorRgba.White);
        var stroke = new BrushStroke(image, new BrushSettings(8, 1.0, 1.0), ColorRgba.Black, erase: true, selection: null);
        stroke.AddPoint(new PointD(10, 10));
        Assert.Equal(0, image.GetPixel(10, 10).A);
        Assert.Equal(255, image.GetPixel(0, 0).A);
        Assert.False(stroke.TotalDirty.IsEmpty);
    }

    [Fact]
    public void Restore_stroke_paints_the_original_back_only_under_the_brush()
    {
        var original = TestImages.Solid(40, 20, new ColorRgba(200, 30, 30));      // before background removal
        var cutOut = new PixelBuffer(40, 20);                                       // everything removed
        var stroke = new BrushStroke(cutOut, new BrushSettings(8, 1.0, 1.0), original, selection: null);
        stroke.AddPoint(new PointD(10, 10));

        Assert.Equal(new ColorRgba(200, 30, 30), cutOut.GetPixel(10, 10)); // restored, full color and alpha
        Assert.Equal(0, cutOut.GetPixel(30, 10).A);                        // away from the brush: still removed
    }

    [Fact]
    public void Soft_restore_brings_back_partial_transparency()
    {
        var original = TestImages.Solid(20, 20, ColorRgba.Black);
        var cutOut = new PixelBuffer(20, 20);
        var stroke = new BrushStroke(cutOut, new BrushSettings(10, 1.0, 0.5), original, selection: null);
        stroke.AddPoint(new PointD(10, 10));
        Assert.InRange(cutOut.GetPixel(10, 10).A, 120, 135); // 50% opacity
    }

    [Fact]
    public void Draw_over_blends_with_opacity()
    {
        var target = TestImages.Solid(2, 2, ColorRgba.White);
        var source = TestImages.Solid(2, 2, ColorRgba.Black);
        Compositor.DrawOver(target, source, 0, 0, opacity: 0.5);
        Assert.InRange(target.GetPixel(0, 0).R, 126, 129);
    }
}
