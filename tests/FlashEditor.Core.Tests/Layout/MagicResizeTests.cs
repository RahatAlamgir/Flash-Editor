using FlashEditor.Core.Imaging;
using FlashEditor.Core.Layout;
using FlashEditor.Core.Objects;
using Xunit;

namespace FlashEditor.Core.Tests.Layout;

public class MagicResizeTests
{
    [Fact]
    public void An_object_keeps_its_proportions_and_its_relative_place()
    {
        var shape = new ShapeObject(new ObjectBox(100, 100, 100, 100), ShapeKind.Ellipse, new ColorRgba(1, 2, 3), null, 0);
        var mapped = MagicResize.MapObject(shape, 1000, 500, 500, 1000);        // 0.5 across, 2 down: the smaller change (0.5) sizes it
        Assert.Equal((50, 50), (mapped.Box.Width, mapped.Box.Height));
        Assert.Equal(75, mapped.Box.Center.X, 6);                               // 150 * 0.5
        Assert.Equal(300, mapped.Box.Center.Y, 6);                              // 150 * 2
    }

    [Fact]
    public void Text_gets_smaller_with_the_canvas()
    {
        var text = new TextObject(new ObjectBox(100, 100, 300, 60), "Hello", "Segoe UI", 40, ColorRgba.Black);
        var mapped = (TextObject)MagicResize.MapObject(text, 1000, 500, 500, 250);
        Assert.Equal(20, mapped.FontSize, 6);
    }

    [Fact]
    public void A_background_shape_stretches_over_the_new_canvas()
    {
        var background = new ShapeObject(new ObjectBox(0, 0, 1000, 500), ShapeKind.Rectangle, new ColorRgba(9, 9, 9), null, 0);
        Assert.True(MagicResize.IsBackground(background, 1000, 500));
        var mapped = MagicResize.MapObject(background, 1000, 500, 500, 1000);
        Assert.Equal((0, 0, 500, 1000), (mapped.Box.X, mapped.Box.Y, mapped.Box.Width, mapped.Box.Height));
    }

    [Fact]
    public void A_background_picture_covers_the_new_canvas_without_being_squashed()
    {
        var picture = new ImageObject(new ObjectBox(0, 0, 1000, 500), new PixelBuffer(10, 5), "Photo");
        var mapped = MagicResize.MapObject(picture, 1000, 500, 500, 1000);
        Assert.Equal(2, mapped.Box.Width / mapped.Box.Height, 6);               // still 2 : 1
        Assert.True(mapped.Box.Width >= 500 && mapped.Box.Height >= 1000);
        Assert.Equal((250, 500), (mapped.Box.Center.X, mapped.Box.Center.Y));
    }

    [Fact]
    public void A_small_object_is_not_taken_for_a_background()
        => Assert.False(MagicResize.IsBackground(new ShapeObject(new ObjectBox(10, 10, 300, 200), ShapeKind.Rectangle, null, null, 0), 1000, 500));

    [Fact]
    public void A_solid_pixel_layer_covers_the_new_canvas()
    {
        var p = new PixelBuffer(100, 50);
        p.Fill(new ColorRgba(10, 20, 30));
        var mapped = MagicResize.MapPixels(p, 50, 100);
        Assert.Equal((50, 100), (mapped.Width, mapped.Height));
        Assert.Equal(255, mapped.GetPixel(0, 0).A);
        Assert.Equal(255, mapped.GetPixel(49, 99).A);
    }

    [Fact]
    public void Painted_pixels_move_with_the_relative_place_of_their_middle()
    {
        var p = new PixelBuffer(200, 100);
        for (int y = 20; y < 40; y++) for (int x = 20; x < 60; x++) p.SetPixel(x, y, new ColorRgba(255, 0, 0));
        var mapped = MagicResize.MapPixels(p, 100, 200);                          // sx 0.5, sy 2: size by 0.5, middle (40,30) -> (20,60)
        Assert.Equal(255, mapped.GetPixel(20, 60).A);
        Assert.Equal(0, mapped.GetPixel(80, 150).A);
        var bounds = mapped.VisibleBounds();
        Assert.InRange(bounds.Width, 18, 22);
        Assert.InRange(bounds.Height, 8, 12);
    }
}
