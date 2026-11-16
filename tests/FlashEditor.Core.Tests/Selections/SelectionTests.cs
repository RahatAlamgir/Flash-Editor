using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Selections;

namespace FlashEditor.Core.Tests.Selections;

public class SelectionTests
{
    [Fact]
    public void Rectangle_selection_has_exact_bounds()
    {
        var selection = Selection.FromRectangle(new PixelRect(2, 3, 4, 5), 20, 20)!;
        Assert.Equal(new PixelRect(2, 3, 4, 5), selection.Bounds);
        Assert.True(selection.IsSelected(2, 3));
        Assert.True(selection.IsSelected(5, 7));
        Assert.False(selection.IsSelected(6, 7));
    }

    [Fact]
    public void Selection_outside_canvas_is_null()
        => Assert.Null(Selection.FromRectangle(new PixelRect(50, 50, 5, 5), 20, 20));

    [Fact]
    public void Triangle_polygon_fills_inside_only()
    {
        var triangle = new[] { new PointD(0, 0), new PointD(10, 0), new PointD(0, 10) };
        var selection = Selection.FromPolygon(triangle, 10, 10)!;
        Assert.True(selection.IsSelected(1, 1));
        Assert.False(selection.IsSelected(8, 8));
    }

    [Fact]
    public void Invert_swaps_selected_and_unselected()
    {
        var selection = Selection.FromRectangle(new PixelRect(0, 0, 5, 10), 10, 10)!.Invert()!;
        Assert.False(selection.IsSelected(0, 0));
        Assert.True(selection.IsSelected(5, 0));
        Assert.Equal(new PixelRect(5, 0, 5, 10), selection.Bounds);
    }

    [Fact]
    public void Offset_moves_the_mask()
    {
        var moved = Selection.FromRectangle(new PixelRect(0, 0, 2, 2), 10, 10)!.Offset(3, 4)!;
        Assert.Equal(new PixelRect(3, 4, 2, 2), moved.Bounds);
    }

    [Fact]
    public void Extract_and_clear_selected_pixels()
    {
        var image = TestImages.Solid(6, 6, ColorRgba.Black);
        var selection = Selection.FromRectangle(new PixelRect(1, 1, 2, 2), 6, 6)!;

        var copy = SelectionPixels.Extract(image, selection);
        Assert.Equal((2, 2), (copy.Width, copy.Height));
        Assert.Equal(ColorRgba.Black, copy.GetPixel(0, 0));

        SelectionPixels.Clear(image, selection);
        Assert.Equal(0, image.GetPixel(1, 1).A);
        Assert.Equal(255, image.GetPixel(0, 0).A);
    }
}
