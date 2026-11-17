using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Tests.Objects;

/// <summary>Editing one part inside a group (Canva style).</summary>
public class GroupPartEditTests
{
    public GroupPartEditTests() => FakeObjectRenderer.Install();

    private static ShapeObject Square(double x, double y, double size = 10)
        => new(new ObjectBox(x, y, size, size), ShapeKind.Rectangle, ColorRgba.Black, null, 0);

    private static GroupObject ThreeSquares() => GroupObject.Create(new[]
    {
        new GroupMember(Square(0, 0), "A", 1),
        new GroupMember(Square(30, 10), "B", 0.5),
        new GroupMember(Square(60, 0), "C", 1),
    });

    [Fact]
    public void Changing_a_part_changes_only_that_part_and_refits_the_group()
    {
        var group = ThreeSquares();
        var moved = group.CurrentMember(1).Object with { Box = new ObjectBox(30, 100, 10, 10) };
        var edited = group.WithPart(1, moved);

        var parts = edited.CurrentMembers();
        Assert.Equal(0, parts[0].Object.Box.Y, 6);
        Assert.Equal(100, parts[1].Object.Box.Y, 6);
        Assert.Equal(60, parts[2].Object.Box.X, 6);
        Assert.Equal(new[] { "A", "B", "C" }, parts.Select(p => p.Name));
        Assert.Equal(0.5, parts[1].Opacity);                 // the part keeps its opacity
        Assert.Equal(new ObjectBox(0, 0, 70, 110), edited.Box); // group grew down to the moved part
    }

    [Fact]
    public void Editing_a_part_of_a_moved_and_resized_group_keeps_everything_where_it_is_on_the_canvas()
    {
        var group = ThreeSquares();
        group = group with { Box = new ObjectBox(100, 50, 140, 40) };  // moved, and stretched 2x horizontally / 2x vertically
        var before = group.CurrentMembers();

        var edited = group.WithPart(0, before[0].Object with { Shadow = ShadowStyle.Default });
        var after = edited.CurrentMembers();

        for (int i = 1; i < 3; i++)
        {
            Assert.Equal(before[i].Object.Box.X, after[i].Object.Box.X, 6);
            Assert.Equal(before[i].Object.Box.Y, after[i].Object.Box.Y, 6);
            Assert.Equal(before[i].Object.Box.Width, after[i].Object.Box.Width, 6);
        }
        Assert.NotNull(after[0].Object.Shadow);
        Assert.Null(after[1].Object.Shadow);
    }

    [Fact]
    public void Group_shadow_survives_editing_a_part()
    {
        var group = ThreeSquares() with { Shadow = ShadowStyle.Default };
        var edited = group.WithPart(2, group.CurrentMember(2).Object with { FlipX = true });
        Assert.Equal(ShadowStyle.Default, edited.Shadow);
    }

    [Fact]
    public void Removing_a_part_keeps_the_others_in_place_and_the_last_part_cannot_be_removed()
    {
        var group = ThreeSquares();
        var smaller = group.WithoutPart(1)!;
        Assert.Equal(new[] { "A", "C" }, smaller.Members.Select(m => m.Name));
        Assert.Equal(60, smaller.CurrentMember(1).Object.Box.X, 6);

        var single = GroupObject.Create(new[] { new GroupMember(Square(0, 0), "Only", 1) });
        Assert.Null(single.WithoutPart(0));
    }

    [Fact]
    public void Edit_target_reads_and_writes_a_part_or_the_whole_layer_object()
    {
        var layer = new Layer("G", 200, 100);
        layer.SetObject(ThreeSquares());

        var whole = new EditTarget(layer);
        Assert.IsType<GroupObject>(whole.Object);

        var part = new EditTarget(layer, 2);
        Assert.Equal(60, part.Object!.Box.X, 6);
        var changed = part.Object with { Box = part.Object.Box.Offset(5, 0) };
        Assert.Equal(65, ((GroupObject)part.WithObject(changed)!).CurrentMember(2).Object.Box.X, 6);

        Assert.Null(new EditTarget(layer, 9).Object);        // a part that does not exist
    }

    [Fact]
    public void Part_edit_can_be_undone_through_the_normal_object_history()
    {
        var doc = Document.FromImage(new PixelBuffer(200, 100));
        var layer = doc.Layers[0];
        layer.SetObject(ThreeSquares());

        var target = new EditTarget(layer, 0);
        var edited = target.WithObject(target.Object! with { Box = new ObjectBox(0, 50, 10, 10) });
        doc.History.Push(ObjectChangeAction.Apply("Move", layer, edited)!);
        Assert.Equal(50, new EditTarget(layer, 0).Object!.Box.Y, 6);

        doc.History.Undo();
        Assert.Equal(0, new EditTarget(layer, 0).Object!.Box.Y, 6);
    }
}
