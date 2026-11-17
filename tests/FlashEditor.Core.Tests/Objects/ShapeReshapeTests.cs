using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

/// <summary>Corner points of straight-sided shapes, and where they are on the canvas (for dragging them).</summary>
public class ShapeReshapeTests
{
    private static ShapeObject Shape(ShapeKind kind, ObjectBox? box = null)
        => new(box ?? new ObjectBox(100, 100, 200, 120), kind, ColorRgba.Black, null, 0);

    [Fact]
    public void Straight_sided_shapes_have_corner_points_and_curved_ones_do_not()
    {
        foreach (var kind in Enum.GetValues<ShapeKind>())
        {
            var vertices = ShapeVertices.For(Shape(kind));
            Assert.Equal(ShapeVertices.IsPolygonal(kind), vertices != null);
            if (vertices == null) continue;

            Assert.True(vertices.Count >= 3, $"{kind} has {vertices.Count} points");
            Assert.All(vertices, v =>
            {
                Assert.InRange(v.X, -1e-9, 1 + 1e-9);   // every point lies inside the box
                Assert.InRange(v.Y, -1e-9, 1 + 1e-9);
            });
        }
    }

    [Theory]
    [InlineData(ShapeKind.Ellipse), InlineData(ShapeKind.Heart), InlineData(ShapeKind.Ring), InlineData(ShapeKind.Cloud),
     InlineData(ShapeKind.SpeechBubble), InlineData(ShapeKind.Crescent), InlineData(ShapeKind.Pie), InlineData(ShapeKind.Line),
     InlineData(ShapeKind.Shield), InlineData(ShapeKind.OvalBubble), InlineData(ShapeKind.Teardrop), InlineData(ShapeKind.HalfCircle)]
    public void Curved_shapes_have_no_corner_points_of_their_own(ShapeKind kind)
    {
        Assert.False(Shape(kind).HasStraightSides);
        Assert.Equal(kind != ShapeKind.Line, Shape(kind).CanReshape); // curved shapes are reshaped by the four corners of the box
        Assert.Null(ShapeVertices.For(Shape(kind)));
    }

    [Fact]
    public void A_rectangle_is_its_four_corners_and_a_trapezoid_is_wider_at_the_bottom()
    {
        Assert.Equal(new[] { new PointD(0, 0), new PointD(1, 0), new PointD(1, 1), new PointD(0, 1) }, ShapeVertices.For(Shape(ShapeKind.Rectangle)));

        var trapezoid = ShapeVertices.For(Shape(ShapeKind.Trapezoid))!;
        Assert.True(trapezoid[1].X - trapezoid[0].X < trapezoid[2].X - trapezoid[3].X);    // top edge shorter than the bottom edge
    }

    [Theory]
    [InlineData(ShapeKind.Pentagon, 5), InlineData(ShapeKind.Hexagon, 6), InlineData(ShapeKind.Octagon, 8),
     InlineData(ShapeKind.Sparkle, 8), InlineData(ShapeKind.Burst, 32), InlineData(ShapeKind.Triangle, 3), InlineData(ShapeKind.Diamond, 4)]
    public void The_regular_shapes_have_the_right_number_of_corners(ShapeKind kind, int expected)
        => Assert.Equal(expected, ShapeVertices.For(Shape(kind))!.Count);

    [Fact]
    public void Star_and_polygon_follow_the_points_setting()
    {
        Assert.Equal(10, ShapeVertices.For(Shape(ShapeKind.Star) with { Points = 5 })!.Count);
        Assert.Equal(14, ShapeVertices.For(Shape(ShapeKind.Star) with { Points = 7 })!.Count);
        Assert.Equal(9, ShapeVertices.For(Shape(ShapeKind.Polygon) with { Points = 9 })!.Count);
    }

    [Fact]
    public void The_arrow_head_keeps_its_size_for_the_box_it_has_now()
    {
        var wide = ShapeVertices.For(Shape(ShapeKind.Arrow, new ObjectBox(0, 0, 400, 100)))!;
        var narrow = ShapeVertices.For(Shape(ShapeKind.Arrow, new ObjectBox(0, 0, 100, 100)))!;
        Assert.Equal(1 - 90.0 / 400, wide[1].X, 6);       // head = min(45% of the width, 90% of the height) = 90 px of 400
        Assert.Equal(1 - 45.0 / 100, narrow[1].X, 6);     // head = 45 px of 100
    }

    [Theory]
    [InlineData(0, false, false), InlineData(30, false, false), InlineData(90, true, false), InlineData(-40, false, true), InlineData(155, true, true)]
    public void Points_convert_between_the_canvas_and_the_shape_and_back(double rotation, bool flipX, bool flipY)
    {
        var shape = new ShapeObject(new ObjectBox(40, 60, 300, 120, rotation), ShapeKind.Rectangle, null, null, 0, FlipX: flipX, FlipY: flipY);
        foreach (var unit in new[] { new PointD(0, 0), new PointD(1, 0), new PointD(1, 1), new PointD(0, 1), new PointD(0.25, 0.8), new PointD(0.5, 0.5) })
        {
            var canvas = shape.FromUnit(unit);
            var back = shape.ToUnit(canvas);
            Assert.Equal(unit.X, back.X, 6);
            Assert.Equal(unit.Y, back.Y, 6);
        }
    }

    [Fact]
    public void An_unrotated_shape_maps_points_directly()
    {
        var shape = Shape(ShapeKind.Rectangle, new ObjectBox(100, 50, 200, 100));
        Assert.Equal(new PointD(100, 50), shape.FromUnit(new PointD(0, 0)));
        Assert.Equal(new PointD(300, 150), shape.FromUnit(new PointD(1, 1)));
        Assert.Equal(0.75, shape.ToUnit(new PointD(250, 100)).X, 9);
        Assert.Equal(0.5, shape.ToUnit(new PointD(250, 100)).Y, 9);
    }

    [Fact]
    public void A_mirrored_shape_has_its_first_corner_on_the_other_side()
    {
        var shape = Shape(ShapeKind.Rectangle, new ObjectBox(100, 50, 200, 100)) with { FlipX = true };
        Assert.Equal(new PointD(300, 50), shape.FromUnit(new PointD(0, 0)));     // the left corner is drawn on the right
    }

    [Fact]
    public void Dragging_a_corner_makes_a_custom_outline_and_turns_off_settings_that_no_longer_apply()
    {
        var rounded = Shape(ShapeKind.Rectangle) with { CornerRadius = 12 };
        Assert.True(rounded.HasCorners);
        Assert.False(rounded.HasCustomOutline);

        var custom = rounded with { Vertices = new[] { new PointD(0.2, 0), new PointD(0.8, 0), new PointD(1, 1), new PointD(0, 1) } };
        Assert.True(custom.HasCustomOutline);
        Assert.False(custom.HasCorners);
        Assert.NotEqual(rounded, custom);
        Assert.Equal(4, custom.CurrentVertices!.Count);

        var star = Shape(ShapeKind.Star);
        Assert.True(star.HasPoints);
        Assert.False((star with { Vertices = ShapeVertices.For(star) }).HasPoints);
    }

    [Fact]
    public void Resizing_or_scaling_a_reshaped_shape_keeps_its_custom_corners()
    {
        var vertices = new[] { new PointD(0.2, 0), new PointD(0.8, 0), new PointD(1, 1), new PointD(0, 1) };
        var custom = Shape(ShapeKind.Rectangle) with { Vertices = vertices };

        Assert.Same(vertices, ((ShapeObject)ObjectTransforms.Scale(custom, 2, 2)).Vertices);
        Assert.Same(vertices, ((ShapeObject)ObjectTransforms.Offset(custom, 30, 40)).Vertices);
        Assert.Same(vertices, ((ShapeObject)ObjectTransforms.FlipHorizontal(custom, 800)).Vertices);
    }

    [Fact]
    public void Shapes_have_readable_names()
    {
        Assert.Equal("Double arrow", ShapeNames.Of(ShapeKind.DoubleArrow));
        Assert.Equal("X mark", ShapeNames.Of(ShapeKind.XMark));
        Assert.Equal("Oval bubble", ShapeNames.Of(ShapeKind.OvalBubble));
        Assert.Equal("Pentagon", ShapeNames.Of(ShapeKind.Pentagon));
    }
}
