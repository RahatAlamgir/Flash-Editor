using FlashEditor.App.Tools.Selecting;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Selections;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>Edit menu: undo/redo, clipboard, fill/delete and selection commands.</summary>
public sealed class EditCommands
{
    private readonly MainViewModel _main;

    public EditCommands(MainViewModel main)
    {
        _main = main;
        UndoCommand = new RelayCommand(Undo, () => Group.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => Group.CanRedo);
        CopyCommand = new RelayCommand(() => Copy(cut: false), () => Doc.ActiveLayer != null);
        CutCommand = new RelayCommand(() => Copy(cut: true), () => Doc.ActiveLayer != null);
        PasteCommand = new RelayCommand(Paste, () => _main.Clipboard.HasImage);
        DeleteCommand = new RelayCommand(Delete, () => Doc.Selection != null);
        FillCommand = new RelayCommand(() => Fill(_main.Colors.Foreground), () => Doc.ActiveLayer != null);
        FillWithColorCommand = new RelayCommand(FillWithChosenColor, () => Doc.ActiveLayer != null);
        // Ctrl+A: with the Select / Move tool it selects every layer (like in Canva); with the other tools it selects all pixels.
        SelectAllCommand = new RelayCommand(() =>
        {
            if (_main.Tools.ActiveTool is Tools.Selecting.MoveTool) _main.SelectAllLayers();
            else Doc.Selection = Selection.All(Doc.Width, Doc.Height);
        });
        DeselectCommand = new RelayCommand(() => Doc.Selection = null, () => Doc.Selection != null);
        InvertSelectionCommand = new RelayCommand(InvertSelection);
        SelectLayerContentCommand = new RelayCommand(SelectLayerContent, () => Doc.ActiveLayer != null);
    }

    private ProjectGroup Group => _main.Document.Project;

    /// <summary>Undo takes back the newest step of the whole project, whichever page it was made in (that page is shown); Redo does it again.</summary>
    private void Undo()
    {
        _main.Tools.ResetActiveTool();
        Group.Undo();
    }

    private void Redo()
    {
        _main.Tools.ResetActiveTool();
        Group.Redo();
    }

    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand CutCommand { get; }
    public RelayCommand PasteCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand FillCommand { get; }

    /// <summary>Asks which color to use (a color window), then fills the selection (or the whole layer) with it.</summary>
    public RelayCommand FillWithColorCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectCommand { get; }
    public RelayCommand InvertSelectionCommand { get; }
    public RelayCommand SelectLayerContentCommand { get; }

    private Document Doc => _main.Document.Model;
    private HistoryManager History => Doc.History;

    /// <summary>Copies the selected pixels (or the whole layer) of the active layer.</summary>
    private void Copy(bool cut)
    {
        if (TryCopyObjects(cut)) return;
        // Copying only reads pixels: it works on locked layers and keeps objects editable.
        var layer = Doc.ActiveLayer;
        if (layer == null || (cut ? !_main.TryGetEditableLayer(out layer) : !_main.TryGetVisibleLayer(out layer))) return;
        var selection = Doc.Selection ?? Selection.All(Doc.Width, Doc.Height);
        var pixels = SelectionPixels.Extract(layer.Pixels, selection);
        _main.Clipboard.Copy(pixels, selection.Bounds.X, selection.Bounds.Y);

        if (cut)
        {
            var before = layer.Pixels.Clone();
            var dirty = SelectionPixels.Clear(layer.Pixels, selection);
            layer.NotifyPixelsChanged(dirty);
            _main.Push(PixelChangeAction.Create("Cut", layer, before, dirty));
        }
        _main.SetStatus(cut ? "Cut to clipboard" : $"Copied {pixels.Width} x {pixels.Height} px");
    }

    /// <summary>
    /// With the Select / Move tool and no pixel selection, Copy / Cut take the selected objects as they are (still editable), so they can be
    /// pasted into any picture or project. Returns false when this does not apply (the normal pixel copy then runs).
    /// </summary>
    private bool TryCopyObjects(bool cut)
    {
        if (_main.Tools.ActiveTool is not MoveTool || Doc.Selection != null || _main.ActivePartIndex != null) return false;
        var layers = _main.SelectedLayers;
        if (layers.Count == 0 || layers.Any(l => l.Object == null || l.IsAdjustment)) return false;

        var bounds = PixelRect.Empty;
        foreach (var layer in layers) bounds = bounds.Union(layer.VisibleBounds());
        if (bounds.IsEmpty) return false;
        var picture = SelectionPixels.Extract(layers[^1].Pixels, Selection.FromRectangle(bounds, Doc.Width, Doc.Height) ?? Selection.All(Doc.Width, Doc.Height));
        _main.Clipboard.CopyObjects(layers.Select(l => new Services.CopiedObject(l.Object!, l.Name, l.Opacity)), picture, bounds.X, bounds.Y);

        if (cut)
        {
            if (layers.Count >= Doc.Layers.Count) { _main.SetStatus("Copied. The picture needs at least one layer, so they were not cut."); return true; }
            if (layers.Any(l => l.IsLocked)) { _main.SetStatus("Copied. A locked layer cannot be cut."); return true; }
            var doc = Doc;
            var cutLayers = layers.ToHashSet();
            _main.Push(LayerStructureAction.Record("Cut", doc, () =>
            {
                var rest = doc.Layers.Where(l => !cutLayers.Contains(l)).ToList();
                doc.SetLayers(rest, rest[^1]);
            }));
            _main.SetStatus(layers.Count == 1 ? "Cut. Paste it here or into another picture or project." : $"Cut {layers.Count} objects. Paste them here or into another picture or project.");
            return true;
        }
        _main.SetStatus(layers.Count == 1 ? "Copied. Paste it here or into another picture or project." : $"Copied {layers.Count} objects. Paste them here or into another picture or project.");
        return true;
    }

    /// <summary>Pastes objects that were copied as objects: the same shapes, text and pictures, in the same places, on new layers.</summary>
    private void PasteObjects(IReadOnlyList<Services.CopiedObject> items)
    {
        var doc = Doc;
        // Pasted into the picture they came from: slightly shifted, so they are not hidden behind the originals.
        bool sameSpot = items.Any(i => doc.Layers.Any(l => Equals(l.Object, i.Object)));
        var created = new List<Layer>();
        foreach (var item in items)
        {
            var obj = sameSpot ? ObjectTransforms.Offset(item.Object, 24, 24) : item.Object;
            var layer = new Layer(_main.UniqueLayerName(item.Name), doc.Width, doc.Height);
            layer.SetObject(obj);
            layer.Opacity = item.Opacity;
            created.Add(layer);
        }
        _main.Push(LayerStructureAction.Record("Paste", doc, () =>
        {
            var list = doc.Layers.ToList();
            list.AddRange(created);
            doc.SetLayers(list, created[^1]);
        }));
        doc.Selection = null;
        _main.SetSelection(created, created[^1]);
        _main.SelectTool<MoveTool>();
        _main.SetStatus(created.Count == 1 ? "Pasted. It is still fully editable." : $"Pasted {created.Count} objects. They are still fully editable.");
    }

    /// <summary>Pastes as a new, resizable image layer, selects it, and switches to the Move tool.</summary>
    private void Paste()
    {
        if (_main.Clipboard.GetObjects() is { Count: > 0 } objects)
        {
            PasteObjects(objects);
            return;
        }
        var content = _main.Clipboard.Get();
        if (content == null) return;
        var pixels = content.Pixels;

        ImageObject image;
        if (content.HasOrigin && content.OriginX + pixels.Width <= Doc.Width && content.OriginY + pixels.Height <= Doc.Height)
            image = new ImageObject(new ObjectBox(content.OriginX, content.OriginY, pixels.Width, pixels.Height), pixels, "Pasted");
        else
            image = ImageObject.CenteredOn(pixels, Doc.Width, Doc.Height, "Pasted");

        _main.AddObjectLayer(image, "Paste");
        Doc.Selection = null;
        _main.SelectTool<MoveTool>();
        _main.SetStatus("Pasted as a new layer. Drag to move it, handles to resize or rotate.");
    }

    /// <summary>Selects every visible pixel of the active layer.</summary>
    private void SelectLayerContent()
    {
        if (Doc.ActiveLayer is not { } layer) return;
        var mask = new byte[Doc.Width * Doc.Height];
        var data = layer.Pixels.Data;
        for (int i = 0; i < mask.Length; i++) mask[i] = data[i * 4 + 3];
        Doc.Selection = Selection.FromMask(mask, Doc.Width, Doc.Height);
        _main.SetStatus(Doc.Selection == null ? "The layer is empty." : $"Selected the content of \"{layer.Name}\".");
    }

    /// <summary>Clears the selected pixels to transparent.</summary>
    private void Delete()
    {
        if (Doc.Selection is not { } selection || !_main.TryGetEditableLayer(out var layer)) return;
        var before = layer.Pixels.Clone();
        var dirty = SelectionPixels.Clear(layer.Pixels, selection);
        layer.NotifyPixelsChanged(dirty);
        _main.Push(PixelChangeAction.Create("Delete", layer, before, dirty));
    }

    /// <summary>Fills the selection (or the whole layer) with the foreground color.</summary>
    private void FillWithChosenColor()
    {
        var color = _main.Dialogs.PickColor(_main.Colors.Foreground, "Fill with which color?");
        if (color is { } c) Fill(c with { A = 255 });
    }

    private void Fill(ColorRgba color)
    {
        if (!_main.TryGetEditableLayer(out var layer)) return;
        var selection = Doc.Selection ?? Selection.All(Doc.Width, Doc.Height);
        var before = layer.Pixels.Clone();
        var dirty = SelectionPixels.Fill(layer.Pixels, selection, color);
        layer.NotifyPixelsChanged(dirty);
        _main.Push(PixelChangeAction.Create("Fill", layer, before, dirty));
    }

    private void InvertSelection()
    {
        Doc.Selection = Doc.Selection == null
            ? Selection.All(Doc.Width, Doc.Height)
            : Doc.Selection.Invert();
    }
}
