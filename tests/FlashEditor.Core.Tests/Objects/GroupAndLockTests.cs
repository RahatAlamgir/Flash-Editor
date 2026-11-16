using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

public class GroupAndLockTests
{
    public GroupAndLockTests() => FakeObjectRenderer.Install();

    private static ShapeObject Square(double x, double y, double size = 10)
        => new(new ObjectBox(x, y, size, size), ShapeKind.Rectangle, ColorRgba.Black, null, 0);

    private static GroupObject TwoSquares() => GroupObject.Create(new[]
    {
        new GroupMember(Square(0, 0), "A", 1),
        new GroupMember(Square(30, 10), "B", 0.5),
    });

    [Fact]
    public void Group_box_covers_all_members()
    {
        var group = TwoSquares();
        Assert.Equal(new ObjectBox(0, 0, 40, 20), group.Box);
        Assert.Equal(group.Box, group.OriginalBox);
    }

    [Fact]
    public void Ungrouping_after_a_move_moves_the_members()
    {
        var moved = (GroupObject)ObjectTransforms.Offset(TwoSquares(), 100, 50);
        var members = moved.CurrentMembers();
        Assert.Equal(100, members[0].Object.Box.X, 6);
        Assert.Equal(130, members[1].Object.Box.X, 6);
        Assert.Equal(60, members[1].Object.Box.Y, 6);
        Assert.Equal(0.5, members[1].Opacity);
    }

    [Fact]
    public void Ungrouping_after_a_resize_scales_the_members()
    {
        var group = TwoSquares();
        var bigger = group with { Box = group.Box with { Width = 80, Height = 40 } }; // twice as big, same top-left
        var members = bigger.CurrentMembers();
        Assert.Equal(new ObjectBox(0, 0, 20, 20), members[0].Object.Box);
        Assert.Equal(60, members[1].Object.Box.X, 6);
        Assert.Equal(20, members[1].Object.Box.Width, 6);
    }

    [Fact]
    public void Ungrouping_after_a_flip_mirrors_the_members()
    {
        var flipped = TwoSquares() with { FlipX = true };
        var members = flipped.CurrentMembers();
        Assert.Equal(35, members[0].Object.Box.Center.X, 6); // the left square is now on the right
        Assert.True(members[0].Object.FlipX);
    }

    [Fact]
    public void Group_layer_renders_and_undo_works()
    {
        var layer = new Layer("G", 60, 60);
        layer.SetObject(TwoSquares());
        Assert.Equal(255, layer.Pixels.GetPixel(20, 10).A); // inside the group's box
        var action = ObjectChangeAction.Apply("Move", layer, ObjectTransforms.Offset(layer.Object!, 10, 0))!;
        action.Undo();
        Assert.Equal(0, layer.Object!.Box.X);
    }

    [Fact]
    public void Lock_is_an_undoable_layer_property()
    {
        var layer = new Layer("L", 4, 4);
        layer.IsLocked = true;
        var action = new LayerPropertyAction(layer, LayerProperty.Lock, false, true);
        Assert.Equal("Lock Layer", action.Name);
        action.Undo();
        Assert.False(layer.IsLocked);
        action.Redo();
        Assert.True(layer.IsLocked);
    }
}
