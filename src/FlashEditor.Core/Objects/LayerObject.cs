namespace FlashEditor.Core.Objects;

/// <summary>
/// An editable object living on its own layer (Canva-style): a shape, a text or a placed image.
/// The layer remembers the object and redraws its pixels whenever the object changes, so color,
/// border, size, rotation or text can be changed at any time. Objects are immutable: every change
/// makes a new object (which makes undo simple).
/// </summary>
public abstract record LayerObject(ObjectBox Box, bool FlipX = false, bool FlipY = false)
{
    /// <summary>The drop shadow behind the object, or null for none (any kind of object can have one).</summary>
    public ShadowStyle? Shadow { get; init; }

    /// <summary>Short name for menus and the history ("Shape", "Text", "Image").</summary>
    public abstract string KindName { get; }

    /// <summary>Default layer name for a new layer holding this object.</summary>
    public virtual string SuggestedLayerName => KindName;

    /// <summary>
    /// True when corner handles should keep the width/height ratio by default (images and text).
    /// Shapes stretch freely; Shift switches the behavior either way.
    /// </summary>
    public virtual bool KeepsAspectByDefault => true;

    /// <summary>True when the object can only be resized along its width (a line).</summary>
    public virtual bool IsLineLike => false;
}
