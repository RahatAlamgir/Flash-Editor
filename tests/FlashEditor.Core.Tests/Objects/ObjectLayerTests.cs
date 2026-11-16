using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

/// <summary>Editable objects on layers: drawing, undo, and following canvas changes.</summary>
public class ObjectLayerTests
{
    public ObjectLayerTests() => FakeObjectRenderer.Install();

    private static ShapeObject Square(double x, double y, double size = 10)
        => new(new ObjectBox(x, y, size, size), ShapeKind.Rectangle, ColorRgba.Black, null, 0);

    [Fact]
    public void Setting_an_object_draws_it_and_converting_keeps_the_pixels()
    {
        var layer = new Layer("L", 50, 50);
        layer.SetObject(Square(5, 5));
        Assert.True(layer.IsObjectLayer);
        Assert.Equal(255, layer.Pixels.GetPixel(8, 8).A);
        Assert.Equal(0, layer.Pixels.GetPixel(30, 30).A);

        layer.SetObject(null); // convert to pixels
        Assert.False(layer.IsObjectLayer);
        Assert.Equal(255, layer.Pixels.GetPixel(8, 8).A);
    }

    [Fact]
    public void Object_change_undo_redo_and_merging()
    {
        var doc = Document.FromImage(new PixelBuffer(50, 50));
        var layer = doc.Layers[0];
        layer.SetObject(Square(0, 0));

        doc.History.Push(ObjectChangeAction.Apply("Fill", layer, ((ShapeObject)layer.Object!) with { Fill = ColorRgba.White }, "Fill")!);
        doc.History.Push(ObjectChangeAction.Apply("Fill", layer, ((ShapeObject)layer.Object!) with { Fill = ColorRgba.Black }, "Fill")!);
        doc.History.Push(ObjectChangeAction.Apply("Move", layer, ObjectTransforms.Offset(layer.Object!, 20, 0))!);
        Assert.Equal(2, doc.History.Position); // the two fill changes merged

        doc.History.Undo();
        Assert.Equal(0, layer.Object!.Box.X);
        Assert.Equal(255, layer.Pixels.GetPixel(5, 5).A);
        doc.History.Redo();
        Assert.Equal(20, layer.Object!.Box.X);
        Assert.Equal(0, layer.Pixels.GetPixel(5, 5).A);
    }

    [Fact]
    public void Convert_to_pixels_can_be_undone()
    {
        var layer = new Layer("L", 30, 30);
        layer.SetObject(Square(1, 1));
        var action = ObjectChangeAction.Apply("Convert to Pixels", layer, null)!;
        Assert.False(layer.IsObjectLayer);
        action.Undo();
        Assert.True(layer.IsObjectLayer);
    }

    [Fact]
    public void Canvas_size_and_crop_move_objects_and_keep_them_editable()
    {
        var doc = Document.FromImage(new PixelBuffer(40, 40));
        var layer = new Layer("Shape", 40, 40);
        layer.SetObject(Square(20, 20));
        doc.SetLayers(new[] { doc.Layers[0], layer }, layer);

        var action = CanvasChangeAction.Reframe("Crop", doc, 20, 20, -15, -15);
        Assert.True(layer.IsObjectLayer);
        Assert.Equal(5, layer.Object!.Box.X);
        Assert.Equal(255, layer.Pixels.GetPixel(10, 10).A);

        action.Undo();
        Assert.Equal(20, layer.Object!.Box.X);
        Assert.Equal(40, layer.Width);
    }

    [Fact]
    public void Resizing_the_image_scales_objects()
    {
        var doc = Document.FromImage(new PixelBuffer(40, 40));
        var layer = new Layer("Shape", 40, 40);
        layer.SetObject(Square(10, 10, 10));
        doc.SetLayers(new[] { doc.Layers[0], layer }, layer);

        CanvasChangeAction.Apply("Image Size", doc, p => FlashEditor.Core.Transforms.Resampler.Resize(p, 80, 80),
            (o, w, h) => ObjectTransforms.Scale(o, 2, 2));
        Assert.Equal(new ObjectBox(20, 20, 20, 20), layer.Object!.Box);
        Assert.Equal(80, layer.Width);
    }

    [Fact]
    public void Box_math_handles_rotation()
    {
        var box = new ObjectBox(0, 0, 100, 20, 90); // center (50,10), rotated to stand upright
        Assert.True(box.Contains(new PointD(50, 50)));
        Assert.False(box.Contains(new PointD(90, 10)));
        var local = box.ToLocal(box.ToCanvas(30, 5));
        Assert.Equal(30, local.X, 6);
        Assert.Equal(5, local.Y, 6);
    }

    [Fact]
    public void Flipping_and_rotating_the_image_moves_objects_along()
    {
        var obj = Square(0, 0, 10); // center (5,5)
        var flipped = ObjectTransforms.FlipHorizontal(obj, 100);
        Assert.Equal(95, flipped.Box.Center.X, 6);
        Assert.True(flipped.FlipX);

        // 90° clockwise on a 100 x 50 canvas -> 50 x 100: top-left goes to top-right.
        var rotated = ObjectTransforms.Rotate(obj, 90, 100, 50, 50, 100);
        Assert.Equal(45, rotated.Box.Center.X, 6);
        Assert.Equal(5, rotated.Box.Center.Y, 6);
        Assert.Equal(90, rotated.Box.Rotation, 6);
    }

    [Fact]
    public void Text_box_fits_its_text()
    {
        var text = new TextObject(new ObjectBox(0, 0, 1, 1), "Hello", "Arial", 30, ColorRgba.Black);
        var fitted = ObjectRendering.FitTextBox(text);
        Assert.Equal(50, fitted.Box.Width);
        Assert.Equal(30, fitted.Box.Height);
    }

    [Fact]
    public void Duplicate_keeps_the_object()
    {
        var layer = new Layer("L", 20, 20);
        layer.SetObject(Square(2, 2));
        var copy = layer.Duplicate("copy");
        Assert.Equal(layer.Object, copy.Object);
    }
}
