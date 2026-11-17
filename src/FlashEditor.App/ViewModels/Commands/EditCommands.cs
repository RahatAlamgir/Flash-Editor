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
        UndoCommand = new RelayCommand(() => { _main.Tools.ResetActiveTool(); History.Undo(); }, () => History.CanUndo);
        RedoCommand = new RelayCommand(() => { _main.Tools.ResetActiveTool(); History.Redo(); }, () => History.CanRedo);
        CopyCommand = new RelayCommand(() => Copy(cut: false), () => Doc.ActiveLayer != null);
        CutCommand = new RelayCommand(() => Copy(cut: true), () => Doc.ActiveLayer != null);
        PasteCommand = new RelayCommand(Paste, () => _main.Clipboard.HasImage);
        DeleteCommand = new RelayCommand(Delete, () => Doc.Selection != null);
        FillCommand = new RelayCommand(Fill, () => Doc.ActiveLayer != null);
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

    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand CutCommand { get; }
    public RelayCommand PasteCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand FillCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectCommand { get; }
    public RelayCommand InvertSelectionCommand { get; }
    public RelayCommand SelectLayerContentCommand { get; }

    private Document Doc => _main.Document.Model;
    private HistoryManager History => Doc.History;

    /// <summary>Copies the selected pixels (or the whole layer) of the active layer.</summary>
    private void Copy(bool cut)
    {
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

    /// <summary>Pastes as a new, resizable image layer, selects it, and switches to the Move tool.</summary>
    private void Paste()
    {
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
    private void Fill()
    {
        if (!_main.TryGetEditableLayer(out var layer)) return;
        var selection = Doc.Selection ?? Selection.All(Doc.Width, Doc.Height);
        var before = layer.Pixels.Clone();
        var dirty = SelectionPixels.Fill(layer.Pixels, selection, _main.Colors.Foreground);
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
