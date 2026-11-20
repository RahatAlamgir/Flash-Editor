using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

public class ObjectWarpTests
{
    private static ImageObject Picture() => new(new ObjectBox(100, 100, 200, 100), new PixelBuffer(4, 2));
    private static TextObject Text() => new(new ObjectBox(10, 10, 80, 30), "Hi", "Segoe UI", 20, new ColorRgba(0, 0, 0), false, false);
    private static ShapeObject Circle() => new(new ObjectBox(0, 0, 100, 100), ShapeKind.Ellipse, new ColorRgba(1, 2, 3), null, 0);
    private static ShapeObject Square() => new(new ObjectBox(0, 0, 100, 100), ShapeKind.Rectangle, new ColorRgba(1, 2, 3), null, 0);

    [Fact]
    public void Every_object_except_a_line_can_be_reshaped()
    {
        Assert.True(Picture().CanReshape);
        Assert.True(Text().CanReshape);
        Assert.True(Circle().CanReshape);
        Assert.True(Square().CanReshape);
        Assert.False((Square() with { Shape = ShapeKind.Line }).CanReshape);
    }

    [Fact]
    public void A_picture_starts_with_its_four_box_corners_and_a_drag_makes_it_custom()
    {
        var pic = Picture();
        Assert.Equal(4, pic.ReshapePoints!.Count);
        Assert.False(pic.HasCustomOutline);

        var pts = pic.ReshapePoints!.ToList();
        pts[1] = new PointD(1.5, -0.25);                       // top-right corner pulled outward
        var warped = pic.WithReshapePoints(pts);
        Assert.True(warped.HasCustomOutline);
        Assert.Equal(new PointD(1.5, -0.25), warped.ReshapePoints![1]);
        Assert.False(warped.WithoutReshape().HasCustomOutline);
    }

    [Fact]
    public void A_straight_shape_keeps_using_its_own_corners_a_curved_one_uses_the_box_corners()
    {
        var square = (ShapeObject)Square().WithReshapePoints(new[] { new PointD(-0.2, 0), new PointD(1, 0), new PointD(1, 1), new PointD(0, 1) });
        Assert.NotNull(square.Vertices);
        Assert.Null(square.Warp);

        var circle = (ShapeObject)Circle().WithReshapePoints(new[] { new PointD(0, 0), new PointD(1.4, 0), new PointD(1, 1), new PointD(0, 1) });
        Assert.Null(circle.Vertices);
        Assert.NotNull(circle.Warp);
        Assert.True(circle.HasCustomOutline);
        Assert.False(circle.WithoutReshape().HasCustomOutline);
    }

    [Fact]
    public void Corners_dragged_outward_grow_the_area_the_object_covers()
    {
        var pic = Picture();
        var plain = pic.VisualBounds();
        Assert.Equal(new PixelRect(100, 100, 200, 100), plain);

        var pts = pic.ReshapePoints!.ToList();
        pts[1] = new PointD(1.5, -1);                          // 100 px right of the box, 100 px above it
        var wide = pic.WithReshapePoints(pts).VisualBounds();
        Assert.Equal(0, wide.Y);
        Assert.Equal(400, wide.X + wide.Width);

        var square = (ShapeObject)Square().WithReshapePoints(new[] { new PointD(0, 0), new PointD(2, 0), new PointD(1, 1), new PointD(0, 1) });
        Assert.Equal(200, square.VisualBounds().X + square.VisualBounds().Width);
    }

    [Fact]
    public void Reshape_points_follow_the_object_when_it_is_turned_and_mirrored()
    {
        var pic = Picture() with { FlipX = true, Box = new ObjectBox(100, 100, 200, 100, 30) };
        var target = pic.FromUnit(new PointD(1.3, -0.4));
        var back = pic.ToUnit(target);
        Assert.InRange(back.X, 1.3 - 1e-9, 1.3 + 1e-9);
        Assert.InRange(back.Y, -0.4 - 1e-9, -0.4 + 1e-9);
    }

    [Fact]
    public void Moving_resizing_or_flipping_keeps_the_reshape()
    {
        var pts = new[] { new PointD(0.1, 0), new PointD(1.2, 0), new PointD(1, 1), new PointD(0, 1) };
        var pic = (ImageObject)Picture().WithReshapePoints(pts);
        Assert.Equal(pts, ObjectTransforms.Offset(pic, 5, 5).Warp);
        Assert.Equal(pts, ObjectTransforms.Scale(pic, 2, 2).Warp);
        Assert.Equal(pts, ObjectTransforms.FlipHorizontal(pic, 800).Warp);
    }

    [Fact]
    public void Unit_square_corners_map_to_themselves()
    {
        var warp = QuadWarp.Create(LayerObject.DefaultCorners)!;
        Assert.True(warp.Unmap(0.25, 0.75, out var u, out var v));
        Assert.Equal(0.25, u, 9);
        Assert.Equal(0.75, v, 9);
    }

    [Fact]
    public void A_trapezium_quad_maps_its_corners_back_to_the_unit_square()
    {
        var quad = new[] { new PointD(20, 0), new PointD(80, 0), new PointD(100, 100), new PointD(0, 100) };
        var warp = QuadWarp.Create(quad)!;
        warp.Unmap(20, 0, out var u, out var v); Assert.Equal((0.0, 0.0), (Math.Round(u, 6), Math.Round(v, 6)));
        warp.Unmap(80, 0, out u, out v); Assert.Equal((1.0, 0.0), (Math.Round(u, 6), Math.Round(v, 6)));
        warp.Unmap(100, 100, out u, out v); Assert.Equal((1.0, 1.0), (Math.Round(u, 6), Math.Round(v, 6)));
        warp.Unmap(0, 100, out u, out v); Assert.Equal((0.0, 1.0), (Math.Round(u, 6), Math.Round(v, 6)));
        warp.Unmap(50, 50, out u, out v); Assert.Equal(0.5, u, 6);
    }

    [Fact]
    public void Four_points_on_a_line_cannot_make_a_warp()
        => Assert.Null(QuadWarp.Create(new[] { new PointD(0, 0), new PointD(1, 1), new PointD(2, 2), new PointD(3, 3) }));

    [Fact]
    public void Warping_a_solid_picture_onto_a_trapezium_cuts_off_the_corners()
    {
        var source = new PixelBuffer(40, 40);
        for (int y = 0; y < 40; y++) for (int x = 0; x < 40; x++) source.SetPixel(x, y, new ColorRgba(200, 50, 50, 255));
        var quad = new[] { new PointD(20, 0), new PointD(80, 0), new PointD(100, 100), new PointD(0, 100) };
        var result = QuadWarp.Create(quad)!.Apply(source, 0, 0, 40, 40, 100, 100);

        Assert.Equal(0, result.GetPixel(2, 2).A);               // top-left corner was cut off
        Assert.Equal(0, result.GetPixel(97, 2).A);              // so was the top-right
        Assert.Equal(255, result.GetPixel(50, 5).A);            // the middle of the narrow top edge is solid
        Assert.Equal(255, result.GetPixel(5, 97).A);            // the wide bottom is full
        Assert.Equal(200, result.GetPixel(50, 50).R);           // colors survive
    }

    [Fact]
    public void Warping_can_also_stretch_outward_beyond_the_original_size()
    {
        var source = new PixelBuffer(10, 10);
        for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++) source.SetPixel(x, y, new ColorRgba(0, 0, 255, 255));
        var quad = new[] { new PointD(0, 0), new PointD(60, 0), new PointD(60, 60), new PointD(0, 60) };
        var result = QuadWarp.Create(quad)!.Apply(source, 0, 0, 10, 10, 60, 60);
        Assert.Equal(255, result.GetPixel(55, 55).A);
    }
}
