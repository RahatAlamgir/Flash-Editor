using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Project;
using FlashEditor.Core.Objects;
using Xunit;

namespace FlashEditor.Core.Tests.Objects;

public class GradientTests
{
    [Fact]
    public void A_second_color_is_suggested_that_differs_from_the_first()
    {
        var blue = GradientStyle.Starting(new ColorRgba(59, 130, 246));
        Assert.Equal(new ColorRgba(59, 130, 246), blue.From);
        Assert.NotEqual(blue.From, blue.To);
        Assert.Equal(255, blue.To.A);
    }

    [Fact]
    public void Grey_and_black_fade_to_a_lighter_or_darker_color_instead_of_a_hue()
    {
        var black = GradientStyle.Starting(new ColorRgba(0, 0, 0));
        Assert.True(black.To.R > 150 && black.To.R == black.To.G && black.To.G == black.To.B);
        var white = GradientStyle.Starting(new ColorRgba(255, 255, 255));
        Assert.True(white.To.R < 120);
    }

    [Fact]
    public void Reversing_swaps_the_colors_and_keeps_the_rest()
    {
        var g = GradientStyle.Two(new ColorRgba(1, 2, 3), new ColorRgba(4, 5, 6), 30, true).Reversed();
        Assert.Equal(new ColorRgba(4, 5, 6), g.From);
        Assert.Equal(new ColorRgba(1, 2, 3), g.To);
        Assert.Equal(30, g.Angle);
        Assert.True(g.Radial);
    }

    [Fact]
    public void Every_preset_makes_a_style_with_its_own_values()
    {
        Assert.True(GradientPreset.All.Count >= 6);
        foreach (var preset in GradientPreset.All)
        {
            var style = preset.CreateStyle();
            Assert.Equal(preset.Colors.Count, style.Stops.Count);
            Assert.Equal(preset.Colors[0], style.From);
            Assert.Equal(preset.Colors[^1], style.To);
            Assert.Equal(preset.Radial, style.Radial);
        }
        Assert.Equal(GradientPreset.All.Count, GradientPreset.All.Select(p => p.Name).Distinct().Count());
    }

    [Fact]
    public void A_gradient_survives_saving_and_opening_a_project()
    {
        var shape = new ShapeObject(new ObjectBox(5, 5, 40, 30), ShapeKind.Ellipse, new ColorRgba(255, 0, 0), null, 0)
        { Gradient = GradientStyle.Two(new ColorRgba(255, 0, 0), new ColorRgba(0, 0, 255), -45, true) };
        var text = new TextObject(new ObjectBox(0, 0, 50, 20), "Hi", "Segoe UI", 16, new ColorRgba(0, 0, 0))
        { Gradient = GradientStyle.Two(new ColorRgba(1, 2, 3), new ColorRgba(9, 8, 7), 180) };

        var doc = new Document(60, 50);
        var one = new Layer("Shape", 60, 50);
        one.RestoreState(new LayerState(new PixelBuffer(60, 50), null, 0, 0, shape));
        var two = new Layer("Text", 60, 50);
        two.RestoreState(new LayerState(new PixelBuffer(60, 50), null, 0, 0, text));
        doc.SetLayers(new List<Layer> { one, two }, two);

        var path = Path.Combine(Path.GetTempPath(), "gradient-" + Guid.NewGuid().ToString("N") + ".flash");
        try
        {
            ProjectWriter.Save(doc, path);
            var back = ProjectReader.Open(path);
            Assert.Equal(shape.Gradient, back.Layers[0].Object!.Gradient);
            Assert.Equal(text.Gradient, back.Layers[1].Object!.Gradient);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Turning_or_resizing_an_object_keeps_its_gradient()
    {
        var shape = new ShapeObject(new ObjectBox(10, 10, 100, 100), ShapeKind.Rectangle, new ColorRgba(1, 2, 3), null, 0)
        { Gradient = GradientStyle.Two(new ColorRgba(1, 2, 3), new ColorRgba(4, 5, 6)) };
        Assert.Equal(shape.Gradient, ObjectTransforms.Scale(shape, 2, 2).Gradient);
        Assert.Equal(shape.Gradient, ObjectTransforms.FlipHorizontal(shape, 400).Gradient);
        Assert.Equal(shape.Gradient, ObjectTransforms.Rotate(shape, 90, 400, 400, 400, 400).Gradient);
    }

    [Fact]
    public void A_gradient_can_have_many_colors_spread_evenly()
    {
        var g = GradientStyle.Evenly(new[] { new ColorRgba(255, 0, 0), new ColorRgba(0, 255, 0), new ColorRgba(0, 0, 255) });
        Assert.Equal(new[] { 0.0, 0.5, 1.0 }, g.Stops.Select(s => s.Position).ToArray());
        Assert.Equal(new ColorRgba(0, 255, 0), g.ColorAt(0.5));
        var quarter = g.ColorAt(0.25);                                  // halfway between red and green
        Assert.True(quarter.R is > 120 and < 135 && quarter.G is > 120 and < 135);
        Assert.True(GradientPreset.Rainbow.Colors.Count >= 4);
    }

    [Fact]
    public void Colors_can_be_added_removed_and_moved_up_to_the_limits()
    {
        var g = GradientStyle.Two(new ColorRgba(255, 0, 0), new ColorRgba(0, 0, 255));
        var three = g.WithStopAdded();
        Assert.Equal(3, three.Stops.Count);
        Assert.Equal(0.5, three.Stops[2].Position);                     // goes into the biggest gap
        var mixed = three.Stops[2].Color;
        Assert.True(mixed.R > 100 && mixed.B > 100);                    // it starts as the color the gradient had there, so nothing jumps

        Assert.Equal(2, three.WithStopRemoved(2).Stops.Count);
        Assert.Same(g, g.WithStopRemoved(0));                           // never fewer than two
        var many = g;
        for (int i = 0; i < 20; i++) many = many.WithStopAdded();
        Assert.Equal(GradientStyle.MaxStops, many.Stops.Count);

        var moved = three.WithStop(2, s => s with { Position = 0.8 });
        Assert.Equal(0.8, moved.Stops[2].Position);
        Assert.Equal(3, moved.Sorted.Count);
        Assert.Equal(moved.Stops[2].Color, moved.Sorted[1].Color);      // the moved stop is now second along the way
    }

    [Fact]
    public void Gradients_with_the_same_colors_are_equal_and_reversing_twice_gives_the_original()
    {
        var a = GradientStyle.Evenly(new[] { new ColorRgba(1, 1, 1), new ColorRgba(2, 2, 2), new ColorRgba(3, 3, 3) }, 10);
        var b = GradientStyle.Evenly(new[] { new ColorRgba(1, 1, 1), new ColorRgba(2, 2, 2), new ColorRgba(3, 3, 3) }, 10);
        Assert.Equal(a, b);
        Assert.NotEqual(a, a with { Angle = 11 });
        Assert.Equal(a, a.Reversed().Reversed());
        Assert.Equal(new ColorRgba(3, 3, 3), a.Reversed().From);
    }

    [Fact]
    public void Several_colors_survive_saving_and_opening_a_project()
    {
        var gradient = GradientPreset.Rainbow.CreateStyle();
        var shape = new ShapeObject(new ObjectBox(5, 5, 40, 30), ShapeKind.Rectangle, new ColorRgba(255, 0, 0), null, 0) { Gradient = gradient };
        var doc = new Document(60, 50);
        var layer = new Layer("Shape", 60, 50);
        layer.RestoreState(new LayerState(new PixelBuffer(60, 50), null, 0, 0, shape));
        doc.SetLayers(new List<Layer> { layer }, layer);
        var path = Path.Combine(Path.GetTempPath(), "gradient-multi-" + Guid.NewGuid().ToString("N") + ".flash");
        try
        {
            ProjectWriter.Save(doc, path);
            var back = ProjectReader.Open(path).Layers[0].Object!.Gradient!;
            Assert.Equal(gradient, back);
            Assert.Equal(5, back.Stops.Count);
        }
        finally { File.Delete(path); }
    }
}
