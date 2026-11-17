using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

/// <summary>Which objects a rubber-band rectangle touches.</summary>
public class BoxSelectionTests
{
    [Theory]
    [InlineData(0, 0, 50, 50, true)]       // rectangle inside the box
    [InlineData(80, 80, 300, 300, true)]   // overlaps a corner
    [InlineData(-50, -50, 10, 10, true)]   // overlaps the other corner
    [InlineData(-300, -300, 300, 300, true)] // rectangle contains the whole box
    [InlineData(101, 0, 200, 50, false)]   // just to the right
    [InlineData(0, 101, 50, 200, false)]   // just below
    [InlineData(-200, 0, -1, 50, false)]   // just to the left
    public void Upright_box_against_rectangles(double l, double t, double r, double b, bool expected)
    {
        var box = new ObjectBox(0, 0, 100, 100);
        Assert.Equal(expected, box.IntersectsRect(l, t, r, b));
    }

    [Fact]
    public void A_rotated_box_is_not_hit_where_its_bounding_rectangle_is_but_it_is_not()
    {
        // A long thin bar turned 45 degrees: its bounding rectangle is a big square, but the corner of that square is empty.
        var bar = new ObjectBox(100, 100, 200, 10, 45);
        var c = bar.Center;
        var aabb = bar.Bounds();

        // Turned 45 degrees clockwise, the bar runs from the top-left to the bottom-right of its bounding square.
        Assert.True(bar.IntersectsRect(c.X - 2, c.Y - 2, c.X + 2, c.Y + 2));                 // on the bar itself
        Assert.True(bar.IntersectsRect(aabb.X, aabb.Y, aabb.X + 20, aabb.Y + 20));          // top-left: the end of the bar
        Assert.False(bar.IntersectsRect(aabb.Right - 20, aabb.Y, aabb.Right, aabb.Y + 20));  // top-right: empty corner of the square
        Assert.False(bar.IntersectsRect(aabb.X, aabb.Bottom - 20, aabb.X + 20, aabb.Bottom)); // bottom-left: the other empty corner
    }

    [Fact]
    public void Rotation_does_not_change_the_answer_for_a_rectangle_around_the_center()
    {
        foreach (double angle in new[] { 0, 30, 90, 137, -45 })
        {
            var box = new ObjectBox(40, 60, 80, 30, angle);
            var c = box.Center;
            Assert.True(box.IntersectsRect(c.X - 1, c.Y - 1, c.X + 1, c.Y + 1), $"angle {angle}");
            Assert.False(box.IntersectsRect(c.X + 500, c.Y + 500, c.X + 510, c.Y + 510), $"angle {angle}");
        }
    }
}
