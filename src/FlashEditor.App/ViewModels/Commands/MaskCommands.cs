using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>
/// Layer > Layer Mask: hide or show parts of a layer without erasing them. A mask can be made from nothing (all shown / all hidden) or from
/// the current selection, changed with the Mask Brush, switched off, turned around, applied to the pixels or deleted.
/// </summary>
public sealed class MaskCommands
{
    private readonly MainViewModel _main;

    public MaskCommands(MainViewModel main)
    {
        _main = main;
        RevealAllCommand = new RelayCommand(() => Add(reveal: true), () => CanAdd);
        HideAllCommand = new RelayCommand(() => Add(reveal: false), () => CanAdd);
        FromSelectionCommand = new RelayCommand(() => FromSelection(revealSelected: true), () => Layer != null && !Layer.IsAdjustment && HasSelection);
        HideSelectionCommand = new RelayCommand(() => FromSelection(revealSelected: false), () => Layer != null && !Layer.IsAdjustment && HasSelection);
        InvertCommand = new RelayCommand(Invert, () => Layer?.HasMask == true);
        ToggleCommand = new RelayCommand(Toggle, () => Layer?.HasMask == true);
        ApplyCommand = new RelayCommand(Apply, () => Layer?.HasMask == true);
        DeleteCommand = new RelayCommand(Delete, () => Layer?.HasMask == true);
        ClipToBelowCommand = new RelayCommand(ClipToBelow, () => CanClip);
    }

    public RelayCommand RevealAllCommand { get; }
    public RelayCommand HideAllCommand { get; }
    public RelayCommand FromSelectionCommand { get; }
    public RelayCommand HideSelectionCommand { get; }
    public RelayCommand InvertCommand { get; }
    public RelayCommand ToggleCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand DeleteCommand { get; }

    /// <summary>Clip to Layer Below: the layer shows only where the layer under it has something (a picture inside a shape or text).</summary>
    public RelayCommand ClipToBelowCommand { get; }

    private Layer? Layer => _main.Document.Model.ActiveLayer;
    private bool HasSelection => _main.Document.Model.Selection is { IsEmpty: false };
    private bool CanClip
    {
        get
        {
            var doc = _main.Document.Model;
            int index = doc.ActiveLayerIndex;
            return Layer is { IsAdjustment: false } && index > 0 && !doc.Layers[index - 1].IsAdjustment;
        }
    }

    /// <summary>
    /// Makes a mask from the shape of the layer below (where it has color, and how see-through it is there), so the layer shows only inside
    /// that shape. It is a snapshot: if the layer below is moved or changed later, change the mask with the Mask Brush or clip again.
    /// </summary>
    private void ClipToBelow()
    {
        var doc = _main.Document.Model;
        var layer = Layer!;
        var below = doc.Layers[doc.ActiveLayerIndex - 1];
        var shape = below.MaskedPixels();
        var clip = new LayerMask(layer.Width, layer.Height, 0);
        for (int i = 0; i < clip.Alpha.Length; i++) clip.Alpha[i] = shape.Data[i * 4 + 3];
        if (layer.Mask is { } existing)                 // an existing mask keeps working: both have to show
            for (int i = 0; i < clip.Alpha.Length; i++) clip.Alpha[i] = (byte)(clip.Alpha[i] * existing.Alpha[i] / 255);

        _main.Push(MaskStateAction.Record("Clip to Layer Below", layer, () =>
        {
            layer.SetMask(clip);
            layer.MaskEnabled = true;
        }));
        _main.SetStatus($"\"{layer.Name}\" now shows only inside \"{below.Name}\". Ctrl+Z undoes it; the layer's pixels are not changed.");
    }

    private bool CanAdd => Layer is { IsAdjustment: false, HasMask: false };

    private void Add(bool reveal)
    {
        var layer = Layer!;
        _main.Push(MaskStateAction.Record("Add Layer Mask", layer, () => layer.SetMask(new LayerMask(layer.Width, layer.Height, reveal ? (byte)255 : (byte)0))));
        _main.SetStatus(reveal
            ? "Added a mask that shows everything. Paint on it with the Mask Brush (K): black hides, white shows."
            : "Added a mask that hides everything. Paint on it with the Mask Brush (K) in white to show parts.");
        _main.Tools.ActiveTool = _main.Tools.Get<Tools.Painting.MaskBrushTool>();
    }

    /// <summary>Shows only the selected area (a new mask), or hides the selected area (also on an existing mask).</summary>
    private void FromSelection(bool revealSelected)
    {
        var layer = Layer!;
        var selection = _main.Document.Model.Selection!;
        _main.Push(MaskStateAction.Record(revealSelected ? "Mask: Show Selection Only" : "Mask: Hide Selection", layer, () =>
        {
            if (layer.Mask == null || revealSelected)
            {
                // "Show selection only" starts a fresh mask; "Hide selection" starts from a mask that shows everything
                layer.SetMask(revealSelected ? LayerMask.FromSelection(selection, true) : new LayerMask(layer.Width, layer.Height));
                if (!revealSelected) layer.Mask!.ApplySelection(selection, reveal: false);
                layer.SetMask(layer.Mask);
            }
            else
            {
                layer.Mask.ApplySelection(selection, reveal: false);
                layer.SetMask(layer.Mask);
            }
            layer.MaskEnabled = true;
        }));
        _main.SetStatus(revealSelected ? "The layer now shows only the selected area." : "Hid the selected area of the layer (its pixels are still there).");
    }

    private void Invert()
    {
        var layer = Layer!;
        _main.Push(MaskStateAction.Record("Invert Layer Mask", layer, () =>
        {
            layer.Mask!.Invert();
            layer.SetMask(layer.Mask);
        }));
    }

    private void Toggle()
    {
        var layer = Layer!;
        _main.Push(MaskStateAction.Record(layer.MaskEnabled ? "Switch Mask Off" : "Switch Mask On", layer, () => layer.MaskEnabled = !layer.MaskEnabled));
    }

    private void Delete()
    {
        var layer = Layer!;
        _main.Push(MaskStateAction.Record("Delete Layer Mask", layer, () => layer.SetMask(null)));
        _main.SetStatus("Deleted the mask. The layer shows all its pixels again.");
    }

    /// <summary>Makes the mask part of the pixels (the hidden parts become see-through for good) and removes the mask.</summary>
    private void Apply()
    {
        var layer = Layer!;
        if (layer.Mask is not { } mask) return;
        var before = layer.SaveState();
        var pixels = LayerMask.Bake(layer.Pixels, mask);
        var (content, x, y) = layer.GetFullContent();
        PixelBuffer? extended = layer.HasHiddenPixels ? LayerMask.Bake(content, mask, -x, -y) : null;
        layer.RestoreState(new LayerState(pixels, extended, x, y, null, null, IncludesMask: true));
        _main.Push(new LayerContentAction("Apply Layer Mask", layer, before, layer.SaveState()));
        _main.SetStatus("Applied the mask: the hidden parts are now transparent pixels. Ctrl+Z undoes it.");
    }
}
