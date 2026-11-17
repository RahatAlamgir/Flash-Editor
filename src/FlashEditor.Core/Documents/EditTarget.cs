using FlashEditor.Core.Objects;

namespace FlashEditor.Core.Documents;

/// <summary>
/// The object being edited: the object of a layer, or one part inside a group layer (Canva style).
/// Editing code works with <see cref="Object"/> (always as it looks on the canvas) and hands the changed
/// object to <see cref="WithObject"/>, which gives the new object for the whole layer.
/// </summary>
/// <param name="Part">Index of the part in the group's members (bottom to top), or null for the layer's own object.</param>
public readonly record struct EditTarget(Layer? Layer, int? Part = null)
{
    public bool IsPart => Part != null;

    /// <summary>The object as it is on the canvas now, or null (nothing selected / the part no longer exists).</summary>
    public LayerObject? Object
    {
        get
        {
            if (Layer?.Object is not { } top) return null;
            if (Part is not { } index) return top;
            return top is GroupObject group && index >= 0 && index < group.Members.Count ? group.CurrentMember(index).Object : null;
        }
    }

    /// <summary>The object the layer should get after <paramref name="changed"/> replaced <see cref="Object"/>.</summary>
    public LayerObject? WithObject(LayerObject changed)
    {
        if (Layer?.Object is not { } top) return null;
        if (Part is not { } index) return changed;
        return top is GroupObject group ? group.WithPart(index, changed) : null;
    }
}
