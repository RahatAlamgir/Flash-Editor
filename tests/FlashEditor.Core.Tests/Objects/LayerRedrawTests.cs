using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

/// <summary>
/// Dragging an object redraws only what changed (or just shifts the old drawing). These tests make sure the result is
/// always exactly what a full redraw would give, and that buffers held by undo steps are never changed behind their back.
/// </summary>
public class LayerRedrawTests
{
    /// <summary>Paints the object's (unrotated) box in a color that depends on the object, and counts how often it draws.</summary>
    private sealed class CountingRenderer : IObjectRenderer
    {
        public int Draws;

        public PixelBuffer Render(LayerObject obj, int canvasWidth, int canvasHeight)
        {
            var canvas = new PixelBuffer(canvasWidth, canvasHeight);
            RenderInto(canvas, obj);
            return canvas;
        }

        public (double Width, double Height) MeasureText(TextObject text) => (10, 10);

        public PixelRect DrawingBounds(LayerObject obj) => obj.Box.Bounds(2);

        public PixelRect PaintedArea(LayerObject obj, int w, int h) => DrawingBounds(obj).Intersect(new PixelRect(0, 0, w, h));

        public void RenderInto(PixelBuffer canvas, LayerObject obj)
        {
            Draws++;
            var area = PaintedArea(obj, canvas.Width, canvas.Height);
            var color = obj is ShapeObject { Fill: { } fill } ? fill : ColorRgba.Black;
            for (int y = area.Y; y < area.Bottom; y++)
                for (int x = area.X; x < area.Right; x++)
                    canvas.SetPixel(x, y, color);
        }
    }

    private static readonly ColorRgba Red = new(255, 0, 0), Blue = new(0, 0, 255);

    private static ShapeObject Box(double x, double y, double w = 20, double h = 10, ColorRgba? fill = null)
        => new(new ObjectBox(x, y, w, h), ShapeKind.Rectangle, fill ?? Red, null, 0);

    private static (Layer Layer, CountingRenderer Renderer) NewLayer(LayerObject first)
    {
        var renderer = new CountingRenderer();
        ObjectRendering.Renderer = renderer;
        var layer = new Layer("L", 200, 150);
        layer.SetObject(first);
        return (layer, renderer);
    }

    private static void AssertSameAsFullRedraw(Layer layer)
        => Assert.True(layer.Pixels.ContentEquals(Layer.RenderState(layer.Object!, layer.Width, layer.Height).Pixels));

    [Fact]
    public void Moving_by_whole_pixels_shifts_the_drawing_without_drawing_it_again()
    {
        var (layer, renderer) = NewLayer(Box(20, 20));
        int draws = renderer.Draws;

        layer.SetObject(Box(60, 50));   // a plain move, fully on the canvas

        Assert.Equal(draws, renderer.Draws);
        AssertSameAsFullRedraw(layer);
        Assert.Equal(0, layer.Pixels.GetPixel(25, 25).A);   // the old place is empty again
        Assert.Equal(255, layer.Pixels.GetPixel(65, 55).A);
    }

    [Fact]
    public void Overlapping_old_and_new_place_still_gives_the_right_picture()
    {
        var (layer, _) = NewLayer(Box(20, 20, 40, 30));
        layer.SetObject(Box(25, 23, 40, 30)); // moves by 5, 3: the two places overlap
        AssertSameAsFullRedraw(layer);
    }

    [Fact]
    public void Any_other_change_is_drawn_for_real_and_leaves_no_leftovers()
    {
        var (layer, renderer) = NewLayer(Box(20, 20));

        int draws = renderer.Draws;
        layer.SetObject(Box(20, 20, fill: Blue));            // new color
        Assert.True(renderer.Draws > draws);
        AssertSameAsFullRedraw(layer);

        layer.SetObject(Box(70.5, 40));                      // half a pixel: not an exact shift
        AssertSameAsFullRedraw(layer);

        layer.SetObject(Box(70.5, 40, 50, 30, Blue));        // resized
        AssertSameAsFullRedraw(layer);
        Assert.Equal(0, layer.Pixels.GetPixel(25, 25).A);    // nothing left behind
    }

    [Fact]
    public void Moving_partly_or_fully_off_the_canvas_and_back_never_loses_pixels()
    {
        var (layer, _) = NewLayer(Box(20, 20, 40, 30));
        foreach (var (x, y) in new[] { (-30.0, 20.0), (170.0, 130.0), (-200.0, -200.0), (60.0, 60.0), (190.0, 20.0), (20.0, 20.0) })
        {
            layer.SetObject(Box(x, y, 40, 30));
            AssertSameAsFullRedraw(layer);
        }
        Assert.Equal(255, layer.Pixels.GetPixel(30, 30).A); // back where it started: whole again, not cut off
    }

    [Fact]
    public void A_buffer_kept_for_undo_is_never_changed_in_place()
    {
        var (layer, _) = NewLayer(Box(20, 20));
        var saved = layer.SaveState();                        // e.g. an undo step keeps this
        var savedCopy = saved.Pixels.Clone();

        layer.SetObject(Box(100, 80));                        // must draw on a fresh buffer
        layer.SetObject(Box(110, 90));                        // and may now work in place on that one

        Assert.True(saved.Pixels.ContentEquals(savedCopy));   // the kept buffer is exactly as it was
        Assert.NotSame(saved.Pixels, layer.Pixels);
        AssertSameAsFullRedraw(layer);

        layer.RestoreState(saved);                            // undo
        Assert.Equal(255, layer.Pixels.GetPixel(25, 25).A);
        layer.SetObject(Box(100, 80));                        // after a restore the buffer is shared again: must not touch it
        Assert.True(saved.Pixels.ContentEquals(savedCopy));
    }

    [Fact]
    public void Only_the_changed_area_is_reported_to_the_screen()
    {
        var (layer, _) = NewLayer(Box(20, 20));
        PixelRect reported = PixelRect.Empty;
        layer.PixelsChanged += (_, rect) => reported = reported.Union(rect);

        layer.SetObject(Box(60, 50));

        Assert.False(reported.IsEmpty);
        Assert.True(reported.Area < 200 * 150 / 4, $"reported {reported}");
    }
}
