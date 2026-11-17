using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.History;

/// <summary>
/// Undo step for an in-place pixel edit on one layer. Only the changed rectangle is stored
/// (before and after), which keeps memory low for brush strokes and small edits.
/// </summary>
public sealed class PixelChangeAction : IHistoryAction
{
    private readonly Layer _layer;
    private readonly PixelRect _rect;
    private readonly PixelBuffer _before;
    private readonly PixelBuffer _after;

    private PixelChangeAction(string name, Layer layer, PixelRect rect, PixelBuffer before, PixelBuffer after)
    {
        Name = name;
        _layer = layer;
        _rect = rect;
        _before = before;
        _after = after;
    }

    public string Name { get; }

    public long SizeInBytes => (long)_before.Data.Length + _after.Data.Length;

    /// <summary>
    /// Builds the action by comparing <paramref name="fullBefore"/> (a full copy of the layer taken before
    /// editing) with the layer's current pixels inside <paramref name="dirty"/>.
    /// Returns null when nothing actually changed.
    /// </summary>
    public static PixelChangeAction? Create(string name, Layer layer, PixelBuffer fullBefore, PixelRect dirty)
    {
        dirty = dirty.Intersect(layer.Pixels.Bounds);
        if (dirty.IsEmpty) return null;
        var before = fullBefore.Crop(dirty);
        var after = layer.Pixels.Crop(dirty);
        return before.ContentEquals(after) ? null : new PixelChangeAction(name, layer, dirty, before, after);
    }

    public void Undo() => Apply(_before);
    public void Redo() => Apply(_after);

    private void Apply(PixelBuffer source)
    {
        _layer.Pixels.CopyFrom(source, _rect.X, _rect.Y);
        _layer.NotifyPixelsChanged(_rect);
    }
}
