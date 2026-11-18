using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Project;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Objects;
using Xunit;

namespace FlashEditor.Core.Tests.Objects;

public class SurfaceFxTests
{
    /// <summary>A red square of 40 x 40 in the middle of a 120 x 120 empty picture.</summary>
    private static PixelBuffer Square()
    {
        var p = new PixelBuffer(120, 120);
        for (int y = 40; y < 80; y++) for (int x = 40; x < 80; x++) p.SetPixel(x, y, new ColorRgba(255, 0, 0));
        return p;
    }

    [Fact]
    public void An_outline_surrounds_the_object_at_the_chosen_width_and_not_further()
    {
        var result = SurfaceFx.Outline(Square(), new ColorRgba(0, 0, 255), 6);
        Assert.Equal(new ColorRgba(255, 0, 0), result.GetPixel(60, 60));           // the object is untouched
        var near = result.GetPixel(60, 36);                                         // 4 pixels above the square: inside the line
        Assert.Equal(255, near.A);
        Assert.Equal(new ColorRgba(0, 0, 255), new ColorRgba(near.R, near.G, near.B));
        Assert.Equal(0, result.GetPixel(60, 30).A);                                 // 10 pixels away: outside the line
    }

    [Fact]
    public void An_outline_follows_the_real_shape_so_corners_are_round()
    {
        var result = SurfaceFx.Outline(Square(), new ColorRgba(0, 0, 255), 10);
        // Diagonally out of a corner: the straight distance to the corner is 14, more than the width, so the line does not reach there.
        Assert.Equal(0, result.GetPixel(32, 32).A);
        Assert.True(result.GetPixel(40, 31).A > 0);                                 // straight above the corner it does
    }

    [Fact]
    public void A_glow_fades_with_the_distance_from_the_object()
    {
        var glow = SurfaceFx.GlowLayer(Square(), new ColorRgba(255, 255, 0), 12, 100);
        int near = glow.GetPixel(60, 38).A, farther = glow.GetPixel(60, 28).A, far = glow.GetPixel(60, 2).A;
        Assert.True(near > farther, $"{near} > {farther}");
        Assert.True(farther > far);
        Assert.Equal(0, far);
        Assert.Equal(new ColorRgba(255, 255, 0), new ColorRgba(glow.GetPixel(60, 38).R, glow.GetPixel(60, 38).G, glow.GetPixel(60, 38).B));
    }

    [Fact]
    public void A_stronger_glow_is_brighter_at_the_same_distance()
    {
        var weak = SurfaceFx.GlowLayer(Square(), new ColorRgba(255, 255, 0), 12, 40).GetPixel(60, 30).A;
        var strong = SurfaceFx.GlowLayer(Square(), new ColorRgba(255, 255, 0), 12, 200).GetPixel(60, 30).A;
        Assert.True(strong > weak);
    }

    [Fact]
    public void A_glow_laid_under_an_object_keeps_the_object_on_top()
    {
        var shape = Square();
        var result = SurfaceFx.Under(shape, SurfaceFx.GlowLayer(shape, new ColorRgba(255, 255, 0), 12, 100));
        Assert.Equal(new ColorRgba(255, 0, 0), result.GetPixel(60, 60));
        Assert.True(result.GetPixel(60, 36).A > 0);
    }

    [Fact]
    public void An_inner_shadow_darkens_only_the_edges_it_falls_on()
    {
        // Shadow shifted down and right: it shows on the top and left edges inside the square.
        var result = SurfaceFx.InnerShadow(Square(), new ColorRgba(0, 0, 0), 8, 8, 6);
        Assert.True(result.GetPixel(41, 60).R < 150, "left edge is darkened");
        Assert.True(result.GetPixel(60, 41).R < 150, "top edge is darkened");
        Assert.True(result.GetPixel(78, 60).R > 200, "right edge stays light");
        Assert.Equal(255, result.GetPixel(60, 60).R);                                  // the middle is untouched
        Assert.Equal(0, result.GetPixel(10, 10).A);                                    // nothing appears outside the object
        Assert.Equal(255, result.GetPixel(41, 60).A);                                  // the object keeps its transparency
    }

    [Fact]
    public void Effects_on_an_empty_picture_change_nothing()
    {
        var empty = new PixelBuffer(30, 30);
        Assert.True(SurfaceFx.Outline(empty, ColorRgba.Black, 5).ContentEquals(empty));
        Assert.True(SurfaceFx.InnerShadow(empty, ColorRgba.Black, 3, 3, 4).ContentEquals(empty));
        Assert.Equal(0, SurfaceFx.GlowLayer(empty, ColorRgba.Black, 5, 100).VisibleBounds().Width);
    }

    [Fact]
    public void The_new_looks_are_part_of_a_saved_look_and_follow_when_the_object_is_scaled()
    {
        var shape = new ShapeObject(new ObjectBox(10, 10, 40, 30), ShapeKind.Rectangle, new ColorRgba(1, 2, 3), null, 0)
        {
            Glow = GlowStyle.Default with { Size = 10 },
            Outline = new OutlineStyle(new ColorRgba(9, 9, 9), 4),
            InnerShadow = InnerShadowStyle.Default with { Distance = 6, Blur = 8 },
        };
        var look = ObjectStyle.From(shape);
        var other = look.ApplyTo(new ShapeObject(new ObjectBox(0, 0, 10, 10), ShapeKind.Ellipse, new ColorRgba(5, 5, 5), null, 0));
        Assert.Equal(shape.Glow, other.Glow);
        Assert.Equal(shape.Outline, other.Outline);
        Assert.Equal(shape.InnerShadow, other.InnerShadow);

        var doubled = ObjectTransforms.Scale(shape, 2, 2);
        Assert.Equal(20, doubled.Glow!.Size, 6);
        Assert.Equal(8, doubled.Outline!.Width, 6);
        Assert.Equal(12, doubled.InnerShadow!.Distance, 6);
    }

    [Fact]
    public void Glow_inner_shadow_and_outline_are_saved_with_the_project()
    {
        var layer = new Layer("Shape", 80, 60);
        var obj = new ShapeObject(new ObjectBox(10, 10, 40, 30), ShapeKind.Ellipse, new ColorRgba(1, 2, 3), null, 0)
        {
            Glow = new GlowStyle(new ColorRgba(1, 2, 3, 4), 11, 120),
            InnerShadow = new InnerShadowStyle(new ColorRgba(5, 6, 7, 8), 3, 90, 5),
            Outline = new OutlineStyle(new ColorRgba(9, 8, 7, 6), 2),
        };
        layer.RestoreState(new LayerState(layer.Pixels, null, 0, 0, obj));      // no drawing needed here: only the saved settings matter
        var doc = new Document(80, 60);
        doc.SetLayers(new[] { layer }, layer);
        string path = Path.Combine(Path.GetTempPath(), "flash-surface-" + Guid.NewGuid().ToString("N") + ".flash");
        try
        {
            ProjectWriter.Save(doc, path);
            var loaded = ProjectReader.Open(path).Layers[0].Object!;
            Assert.Equal(new GlowStyle(new ColorRgba(1, 2, 3, 4), 11, 120), loaded.Glow);
            Assert.Equal(new InnerShadowStyle(new ColorRgba(5, 6, 7, 8), 3, 90, 5), loaded.InnerShadow);
            Assert.Equal(new OutlineStyle(new ColorRgba(9, 8, 7, 6), 2), loaded.Outline);
        }
        finally { File.Delete(path); }
    }
}
