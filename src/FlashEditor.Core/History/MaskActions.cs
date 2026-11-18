using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.History;

/// <summary>Adding, removing, inverting or switching on / off a layer mask: the whole mask before and after is kept.</summary>
public sealed class MaskStateAction : IHistoryAction
{
    private readonly Layer _layer;
    private readonly LayerMask? _before, _after;
    private readonly bool _beforeEnabled, _afterEnabled;

    private MaskStateAction(string name, Layer layer, LayerMask? before, bool beforeEnabled, LayerMask? after, bool afterEnabled)
    {
        Name = name;
        _layer = layer;
        _before = before;
        _beforeEnabled = beforeEnabled;
        _after = after;
        _afterEnabled = afterEnabled;
    }

    public string Name { get; }

    public long SizeInBytes => (_before?.ByteSize ?? 0) + (_after?.ByteSize ?? 0);

    /// <summary>Makes the change in <paramref name="change"/> (which edits the layer's mask) and returns the undo step.</summary>
    public static MaskStateAction Record(string name, Layer layer, Action change)
    {
        var before = layer.Mask?.Clone();
        bool beforeEnabled = layer.MaskEnabled;
        change();
        var after = layer.Mask?.Clone();
        return new MaskStateAction(name, layer, before, beforeEnabled, after, layer.MaskEnabled);
    }

    public void Undo() => Set(_before, _beforeEnabled);
    public void Redo() => Set(_after, _afterEnabled);

    private void Set(LayerMask? mask, bool enabled)
    {
        _layer.SetMask(mask?.Clone());
        _layer.MaskEnabled = enabled;
    }
}

/// <summary>Painting on a layer mask: only the changed rectangle is kept.</summary>
public sealed class MaskPaintAction : IHistoryAction
{
    private readonly Layer _layer;
    private readonly PixelRect _region;
    private readonly byte[] _before, _after;

    public MaskPaintAction(string name, Layer layer, PixelRect region, byte[] before, byte[] after)
    {
        Name = name;
        _layer = layer;
        _region = region;
        _before = before;
        _after = after;
    }

    public string Name { get; }

    public long SizeInBytes => _before.Length + _after.Length;

    public void Undo() => Paste(_before);
    public void Redo() => Paste(_after);

    private void Paste(byte[] data)
    {
        if (_layer.Mask is not { } mask) return;
        mask.PasteRegion(_region, data);
        _layer.NotifyMaskChanged(_region);
    }
}
