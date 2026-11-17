using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>One object inside a group, with the layer name and opacity it had before grouping.</summary>
public sealed record GroupMember(LayerObject Object, string Name, double Opacity);

/// <summary>
/// Several objects that move, resize and rotate together (Ctrl+G). The members keep their positions
/// as they were when grouped (<see cref="OriginalBox"/> is the area they covered then); the group's
/// <see cref="LayerObject.Box"/> says where that area is now. Ungrouping turns the members back
/// into separate layers at their current place and size.
/// </summary>
public sealed record GroupObject(
    ObjectBox Box,
    IReadOnlyList<GroupMember> Members,
    ObjectBox OriginalBox,
    bool FlipX = false,
    bool FlipY = false) : LayerObject(Box, FlipX, FlipY)
{
    public override string KindName => "Group";
    public override string SuggestedLayerName => $"Group ({Members.Count})";

    /// <summary>Groups objects (bottom to top) without moving them.</summary>
    public static GroupObject Create(IReadOnlyList<GroupMember> members)
    {
        if (members.Count == 0) throw new ArgumentException("A group needs at least one object.");
        double l = double.MaxValue, t = double.MaxValue, r = double.MinValue, b = double.MinValue;
        foreach (var m in members)
        {
            foreach (var c in m.Object.Box.Corners())
            {
                l = Math.Min(l, c.X); t = Math.Min(t, c.Y);
                r = Math.Max(r, c.X); b = Math.Max(b, c.Y);
            }
        }
        var box = new ObjectBox(l, t, Math.Max(1, r - l), Math.Max(1, b - t));
        return new GroupObject(box, members, box);
    }

    /// <summary>Horizontal / vertical stretch of the group compared to when it was made.</summary>
    public (double Sx, double Sy) Scale => (Box.Width / OriginalBox.Width, Box.Height / OriginalBox.Height);

    /// <summary>Where a point that was at <paramref name="original"/> (when grouped) is now on the canvas.</summary>
    public PointD MapPoint(PointD original)
    {
        var (sx, sy) = Scale;
        var c0 = OriginalBox.Center;
        double x = (original.X - c0.X) * sx * (FlipX ? -1 : 1);
        double y = (original.Y - c0.Y) * sy * (FlipY ? -1 : 1);
        return Box.ToCanvas(x, y);
    }

    /// <summary>The members as they look now (moved, scaled, rotated and flipped with the group).</summary>
    public IReadOnlyList<GroupMember> CurrentMembers()
    {
        var result = new List<GroupMember>(Members.Count);
        for (int i = 0; i < Members.Count; i++) result.Add(CurrentMember(i));
        return result;
    }

    /// <summary>One member as it looks now on the canvas (see <see cref="CurrentMembers"/>).</summary>
    public GroupMember CurrentMember(int index)
    {
        var (sx, sy) = Scale;
        bool mirrored = FlipX != FlipY;
        var m = Members[index];
        var child = ObjectTransforms.Scale(m.Object, sx, sy);
        var center = MapPoint(m.Object.Box.Center);
        double rotation = (mirrored ? -m.Object.Box.Rotation : m.Object.Box.Rotation) + Box.Rotation;
        var box = ObjectBox.FromCenter(center, child.Box.Width, child.Box.Height, ObjectBox.NormalizeAngle(rotation));
        child = child with { Box = box, FlipX = child.FlipX != FlipX, FlipY = child.FlipY != FlipY };
        return m with { Object = child };
    }

    /// <summary>
    /// The group after one part was changed (<paramref name="part"/> is given as it looks on the canvas, e.g. moved
    /// or recolored). The group is re-fitted around its parts, so it grows or shrinks with them; the parts of
    /// the group keep their look, and the group's own shadow is kept.
    /// </summary>
    public GroupObject WithPart(int index, LayerObject part)
    {
        if (index < 0 || index >= Members.Count) return this;
        var current = CurrentMembers().ToList();
        current[index] = current[index] with { Object = part };
        return Create(current) with { Shadow = Shadow };
    }

    /// <summary>The group without one of its parts, or null when that was its only part.</summary>
    public GroupObject? WithoutPart(int index)
    {
        if (index < 0 || index >= Members.Count || Members.Count < 2) return null;
        var current = CurrentMembers().ToList();
        current.RemoveAt(index);
        return Create(current) with { Shadow = Shadow };
    }
}
