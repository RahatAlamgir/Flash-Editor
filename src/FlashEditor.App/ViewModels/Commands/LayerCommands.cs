using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.History;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Transforms;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>Layer menu and Layers panel buttons.</summary>
public sealed class LayerCommands
{
    private readonly MainViewModel _main;

    public LayerCommands(MainViewModel main)
    {
        _main = main;
        NewLayerCommand = new RelayCommand(NewLayer);
        DuplicateCommand = new RelayCommand(Duplicate, () => Doc.ActiveLayer != null);
        DeleteCommand = new RelayCommand(Delete, () => Doc.ActiveLayer is { IsLocked: false } && Doc.Layers.Count > 1);
        ToggleLockCommand = new RelayCommand(ToggleLock, () => Doc.ActiveLayer != null);
        SelectAllLayersCommand = new RelayCommand(_main.SelectAllLayers, () => Doc.Layers.Count > 0);
        LockSelectedCommand = new RelayCommand(() => SetLockOfSelected(true), () => _main.SelectedLayers.Any(l => !l.IsLocked));
        UnlockSelectedCommand = new RelayCommand(() => SetLockOfSelected(false), () => _main.SelectedLayers.Any(l => l.IsLocked));
        CropToSelectionCommand = new RelayCommand(CropToSelection, () => Doc.Selection != null && Doc.ActiveLayer is { IsLocked: false });
        CropObjectCommand = new RelayCommand(CropObject, () => Doc.ActiveLayer is { IsLocked: false });
        RotateRightCommand = new RelayCommand(() => RotateLayer(90), () => Doc.ActiveLayer is { IsLocked: false, IsAdjustment: false });
        RotateLeftCommand = new RelayCommand(() => RotateLayer(-90), () => Doc.ActiveLayer is { IsLocked: false, IsAdjustment: false });
        SplitCommand = new RelayCommand(Split, () => Doc.Selection != null && Doc.ActiveLayer is { IsLocked: false, IsAdjustment: false });
        GroupCommand = new RelayCommand(Group, () => _main.SelectedLayers.Count >= 2 && _main.SelectedLayers.All(l => !l.IsLocked));
        UngroupCommand = new RelayCommand(Ungroup, () => Doc.ActiveLayer is { Object: GroupObject, IsLocked: false });
        MoveUpCommand = new RelayCommand(() => Move(+1), () => Doc.ActiveLayer != null && Doc.ActiveLayerIndex < Doc.Layers.Count - 1);
        MoveDownCommand = new RelayCommand(() => Move(-1), () => Doc.ActiveLayerIndex > 0);
        BringToFrontCommand = new RelayCommand(() => MoveTo(Doc.Layers.Count - 1, "Bring to Front"),
            () => Doc.ActiveLayer != null && Doc.ActiveLayerIndex < Doc.Layers.Count - 1);
        SendToBackCommand = new RelayCommand(() => MoveTo(0, "Send to Back"), () => Doc.ActiveLayerIndex > 0);
        ConvertToPixelsCommand = new RelayCommand(() => { if (Doc.ActiveLayer is { } l) _main.ConvertToPixels(l, announce: true); },
            () => Doc.ActiveLayer?.Object != null);
        MergeSelectedCommand = new RelayCommand(MergeSelected, () => MergeCandidates().Count >= 2);
        AddGradientBackgroundCommand = new RelayCommand(AddGradientBackground);
        AddAdjustmentLayerCommand = new RelayCommand(AddAdjustmentLayer);
        AddLevelsLayerCommand = new RelayCommand(AddLevelsLayer);
        AddCurvesLayerCommand = new RelayCommand(AddCurvesLayer);
        // Ctrl+E: with several layers selected it merges them all, otherwise the active layer goes into the one below it.
        MergeUpCommand = new RelayCommand(MergeUp, () => Doc.ActiveLayerIndex >= 0 && Doc.ActiveLayerIndex < Doc.Layers.Count - 1);
        MergeDownCommand = new RelayCommand(
            () => { if (_main.SelectedLayers.Count >= 2) MergeSelected(); else MergeDown(); },
            () => _main.SelectedLayers.Count >= 2
                ? MergeCandidates().Count >= 2
                : Doc.ActiveLayerIndex > 0 && !Doc.ActiveLayer!.IsLocked && !Doc.Layers[Doc.ActiveLayerIndex - 1].IsLocked);
        FlattenCommand = new RelayCommand(Flatten, () => Doc.Layers.Count > 1);
        FlipLayerHorizontalCommand = new RelayCommand(() => FlipLayer(true), () => Doc.ActiveLayer != null);
        FlipLayerVerticalCommand = new RelayCommand(() => FlipLayer(false), () => Doc.ActiveLayer != null);
        RenameCommand = new RelayCommand(p =>
        {
            if (p is LayerViewModel vm) vm.IsEditingName = true;
            else if (_main.Document.ActiveLayer is { } active) active.IsEditingName = true;
        });
    }

    public RelayCommand NewLayerCommand { get; }
    public RelayCommand DuplicateCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand MergeDownCommand { get; }
    public RelayCommand MergeUpCommand { get; }

    /// <summary>Merges every selected (visible, unlocked) layer into one new layer.</summary>
    public RelayCommand MergeSelectedCommand { get; }
    public RelayCommand AddGradientBackgroundCommand { get; }
    public RelayCommand AddAdjustmentLayerCommand { get; }
    public RelayCommand AddLevelsLayerCommand { get; }
    public RelayCommand AddCurvesLayerCommand { get; }
    public RelayCommand ToggleLockCommand { get; }

    /// <summary>Selects every layer (the Select all button of the Layers panel).</summary>
    public RelayCommand SelectAllLayersCommand { get; }

    /// <summary>Locks every selected layer that is not locked yet.</summary>
    public RelayCommand LockSelectedCommand { get; }

    /// <summary>Unlocks every selected layer that is locked.</summary>
    public RelayCommand UnlockSelectedCommand { get; }
    public RelayCommand GroupCommand { get; }
    public RelayCommand CropToSelectionCommand { get; }
    public RelayCommand CropObjectCommand { get; }
    public RelayCommand RotateRightCommand { get; }
    public RelayCommand RotateLeftCommand { get; }
    public RelayCommand SplitCommand { get; }
    public RelayCommand UngroupCommand { get; }
    public RelayCommand BringToFrontCommand { get; }
    public RelayCommand SendToBackCommand { get; }
    public RelayCommand ConvertToPixelsCommand { get; }
    public RelayCommand FlattenCommand { get; }
    public RelayCommand FlipLayerHorizontalCommand { get; }
    public RelayCommand FlipLayerVerticalCommand { get; }
    public RelayCommand RenameCommand { get; }

    private Document Doc => _main.Document.Model;

    /// <summary>
    /// A layer without a picture that changes the colors of everything below it (brightness, contrast, saturation, hue). Nothing in the
    /// pictures changes, so it can be tuned or deleted at any time.
    /// </summary>
    private void AddAdjustmentLayer()
    {
        var layer = new Layer("Adjustment", Doc.Width, Doc.Height) { Adjustment = new AdjustmentStyle(Brightness: 0, Contrast: 15, Saturation: 15) };
        InsertAboveActive("Add Adjustment Layer", layer);
        _main.SetStatus("Added an adjustment layer. Use the sliders in the Layers panel; it changes every layer below it.");
    }

    /// <summary>A Levels adjustment layer: set where black and white are and move the middle tones (with sliders), without changing the pictures.</summary>
    private void AddLevelsLayer()
    {
        InsertAboveActive("Add Levels Layer", new Layer("Levels", Doc.Width, Doc.Height) { Adjustment = AdjustmentStyle.NewLevels });
        _main.SetStatus("Added a Levels layer. Move the black and white points and the middle tones in the Layers panel; it changes every layer below it.");
    }

    /// <summary>A Curves adjustment layer: bend the tone curve of all colors or of one color channel.</summary>
    private void AddCurvesLayer()
    {
        InsertAboveActive("Add Curves Layer", new Layer("Curves", Doc.Width, Doc.Height) { Adjustment = AdjustmentStyle.NewCurves });
        _main.SetStatus("Added a Curves layer. Click the curve in the Layers panel to add a point and drag it; it changes every layer below it.");
    }

    /// <summary>A rectangle covering the whole picture with a gradient, placed behind everything (change its colors in the Properties panel).</summary>
    private void AddGradientBackground()
    {
        var look = GradientPreset.Ocean;
        var rectangle = new ShapeObject(new ObjectBox(0, 0, Doc.Width, Doc.Height), ShapeKind.Rectangle, look.Colors[0], null, 0) { Gradient = look.CreateStyle() };
        var layer = _main.AddObjectLayer(rectangle, "Add Gradient Background");
        layer.Name = "Gradient background";
        if (Doc.ActiveLayerIndex > 0) SendToBackCommand.Execute(null);
        _main.SetStatus("Added a gradient background. Change its colors in the Properties panel.");
    }

    private void NewLayer()
    {
        var layer = new Layer(Doc.NextLayerName(), Doc.Width, Doc.Height);
        InsertAboveActive("New Layer", layer);
    }

    /// <summary>
    /// Ctrl+D: copies the selected layers (all of them when several are selected) and selects the copies.
    /// The copies go above the top-most selected layer. Copies of editable objects are shifted a little so they
    /// can be told apart from the original.
    /// </summary>
    private void Duplicate()
    {
        var sources = _main.SelectedLayers.ToList(); // bottom to top
        if (sources.Count == 0) return;

        var copies = sources.Select(source =>
        {
            var copy = source.Duplicate(source.Name + " copy");
            if (copy.Object is { } obj) copy.SetObject(ObjectTransforms.Offset(obj, 20, 20));
            return copy;
        }).ToList();

        int top = sources.Max(layer => Doc.Layers.IndexOf(layer));
        _main.Push(LayerStructureAction.Record(sources.Count > 1 ? "Duplicate Layers" : "Duplicate Layer", Doc, () =>
        {
            var layers = Doc.Layers.ToList();
            layers.InsertRange(top + 1, copies);
            Doc.SetLayers(layers, copies[^1]);
        }));
        _main.SetSelection(copies, copies[^1]);
    }

    private void InsertAboveActive(string actionName, Layer layer)
    {
        _main.Push(LayerStructureAction.Record(actionName, Doc, () =>
        {
            var layers = Doc.Layers.ToList();
            layers.Insert(Doc.ActiveLayerIndex + 1, layer);
            Doc.SetLayers(layers, layer);
        }));
    }

    private void Delete()
    {
        // Several selected: delete them all (locked ones stay; at least one layer must remain).
        var selected = _main.SelectedLayers.Where(l => !l.IsLocked).ToList();
        if (selected.Count > 1)
        {
            if (selected.Count >= Doc.Layers.Count) selected.RemoveAt(0);
            _main.Push(LayerStructureAction.Record("Delete Layers", Doc, () =>
            {
                var rest = Doc.Layers.Where(l => !selected.Contains(l)).ToList();
                Doc.SetLayers(rest, rest.LastOrDefault());
            }));
            return;
        }
        if (Doc.ActiveLayer is not { } active || Doc.Layers.Count <= 1) return;
        int index = Doc.ActiveLayerIndex;
        _main.Push(LayerStructureAction.Record("Delete Layer", Doc, () =>
        {
            var layers = Doc.Layers.Where(l => l != active).ToList();
            Doc.SetLayers(layers, layers[Math.Max(0, index - 1)]);
        }));
    }

    /// <param name="direction">+1 = up (towards the top), -1 = down.</param>
    private void Move(int direction)
    {
        if (Doc.ActiveLayer is not { } active) return;
        int index = Doc.ActiveLayerIndex, target = index + direction;
        if (target < 0 || target >= Doc.Layers.Count) return;

        _main.Push(LayerStructureAction.Record(direction > 0 ? "Move Layer Up" : "Move Layer Down", Doc, () =>
        {
            var layers = Doc.Layers.ToList();
            (layers[index], layers[target]) = (layers[target], layers[index]);
            Doc.SetLayers(layers, active);
        }));
    }

    /// <summary>Keeps only the selected part of the active layer (any selection shape); the rest of the picture stays.</summary>
    private void CropToSelection()
    {
        if (Doc.Selection is not { } selection || Doc.ActiveLayer is not { } layer) return;
        var before = layer.SaveState();
        layer.RestoreState(LayerCrop.ToSelection(layer, selection));
        _main.Push(new LayerContentAction("Crop Layer to Selection", layer, before, layer.SaveState()));
        Doc.Selection = null;
        _main.SelectTool<Tools.Selecting.MoveTool>();
        _main.SetStatus($"Kept only the selected part of \"{layer.Name}\". It is now a movable, resizable picture.");
    }

    /// <summary>Opens the Crop tool for just the selected object.</summary>
    private void CropObject()
    {
        _main.SelectTool<Tools.Transform.CropTool>();
        _main.Tools.Get<Tools.Transform.CropTool>().CropActiveLayer();
    }

    /// <summary>Puts the selected layers into one group layer (where the top one was).</summary>
    private void Group()
    {
        var layers = _main.SelectedLayers.Where(l => l.IsVisible).ToList(); // bottom to top
        var members = new List<GroupMember>();
        foreach (var layer in layers)
        {
            var obj = layer.Object ?? AsImage(layer);
            if (obj != null) members.Add(new GroupMember(obj, layer.Name, layer.Opacity));
        }
        if (members.Count < 2)
        {
            _main.SetStatus("Select at least two visible, non-empty layers to group (Shift+click on the canvas, or Ctrl+click in the Layers panel).");
            return;
        }

        var group = new Layer($"Group ({members.Count})", Doc.Width, Doc.Height);
        group.SetObject(GroupObject.Create(members));
        int top = Doc.Layers.IndexOf(layers[^1]);
        _main.Push(LayerStructureAction.Record("Group", Doc, () =>
        {
            var list = Doc.Layers.ToList();
            list.Insert(top + 1, group);
            list.RemoveAll(l => layers.Contains(l));
            Doc.SetLayers(list, group);
        }));
        _main.SelectOnly(group);
        _main.SetStatus($"Grouped {members.Count} layers. They now move, resize and rotate together. Ctrl+Shift+G ungroups.");
    }

    /// <summary>A painted layer as an image object (its visible area), or null when it is empty.</summary>
    private static LayerObject? AsImage(Layer layer)
    {
        var bounds = layer.VisibleBounds();
        if (bounds.IsEmpty) return null;
        var (content, x, y) = layer.GetFullContent();
        return new ImageObject(new ObjectBox(bounds.X, bounds.Y, bounds.Width, bounds.Height), content.Crop(bounds.Offset(-x, -y)), layer.Name);
    }

    /// <summary>Turns a group back into separate layers, exactly where its parts are now.</summary>
    private void Ungroup()
    {
        if (Doc.ActiveLayer is not { Object: GroupObject group } groupLayer) return;
        var parts = group.CurrentMembers().Select(m =>
        {
            var layer = new Layer(m.Name, Doc.Width, Doc.Height) { Opacity = m.Opacity };
            layer.SetObject(m.Object);
            return layer;
        }).ToList();

        _main.Push(LayerStructureAction.Record("Ungroup", Doc, () =>
        {
            var list = Doc.Layers.ToList();
            int index = list.IndexOf(groupLayer);
            list.RemoveAt(index);
            list.InsertRange(index, parts);
            Doc.SetLayers(list, parts[^1]);
        }));
        _main.SetSelection(parts, parts[^1]);
        _main.SetStatus($"Ungrouped into {parts.Count} layers (still selected together).");
    }

    private void ToggleLock()
    {
        if (_main.Document.ActiveLayer is not { } layer) return;
        layer.IsLocked = !layer.IsLocked; // recorded as an undo step by the layer view model
        _main.SetStatus(layer.IsLocked ? $"Locked \"{layer.Name}\"." : $"Unlocked \"{layer.Name}\".");
    }

    /// <summary>
    /// Locks (or unlocks) all selected layers that are not already so. One undo step for all of them.
    /// </summary>
    private void SetLockOfSelected(bool locked)
    {
        var changing = _main.SelectedLayers.Where(l => l.IsLocked != locked).ToList();
        if (changing.Count == 0) return;

        foreach (var layer in changing) layer.IsLocked = locked;
        _main.Push(new CompositeAction(locked ? "Lock Layers" : "Unlock Layers",
            changing.Select(l => (IHistoryAction)new LayerPropertyAction(l, LayerProperty.Lock, !locked, locked))));

        string verb = locked ? "Locked" : "Unlocked";
        _main.SetStatus(changing.Count == 1 ? $"{verb} \"{changing[0].Name}\"." : $"{verb} {changing.Count} layers.");
    }

    /// <summary>
    /// Drag and drop in the Layers panel: moves the layers into the gap number <paramref name="gap"/> of the list
    /// (0 = above the top layer, Count = below the bottom one). One undo step; locked layers can be reordered too.
    /// </summary>
    public void MoveLayersToGap(IReadOnlyCollection<Layer> moved, int gap)
    {
        var order = LayerOrder.MoveToGap(Doc.Layers.ToList(), moved, gap);
        if (order == null) return;
        var active = Doc.ActiveLayer;
        _main.Push(LayerStructureAction.Record(moved.Count > 1 ? "Move Layers" : "Move Layer", Doc, () => Doc.SetLayers(order, active)));
    }

    /// <summary>Moves the active layer to the given position in the stack (0 = bottom).</summary>
    private void MoveTo(int target, string name)
    {
        if (Doc.ActiveLayer is not { } active) return;
        _main.Push(LayerStructureAction.Record(name, Doc, () =>
        {
            var layers = Doc.Layers.Where(l => l != active).ToList();
            layers.Insert(Math.Clamp(target, 0, layers.Count), active);
            Doc.SetLayers(layers, active);
        }));
    }

    /// <summary>Paints the active layer onto the layer below it, then removes it.</summary>
    /// <summary>Merges the selected layer with the one above it (the same as selecting that one and merging it down).</summary>
    private void MergeUp()
    {
        int index = Doc.ActiveLayerIndex;
        if (index < 0 || index >= Doc.Layers.Count - 1) return;
        Doc.ActiveLayer = Doc.Layers[index + 1];
        MergeDown();
    }

    private void MergeDown()
    {
        int index = Doc.ActiveLayerIndex;
        if (index <= 0) return;
        var upper = Doc.Layers[index];
        var lower = Doc.Layers[index - 1];
        _main.ConvertToPixels(lower, announce: false); // merging paints into the lower layer's pixels

        var before = lower.Pixels.Clone();
        if (upper.IsVisible) Compositor.DrawLayer(lower.Pixels, upper);
        lower.NotifyPixelsChanged();
        var pixelStep = PixelChangeAction.Create("Merge", lower, before, lower.Pixels.Bounds);

        var structureStep = LayerStructureAction.Record("Merge", Doc, () =>
            Doc.SetLayers(Doc.Layers.Where(l => l != upper).ToList(), lower));

        _main.Push(new CompositeAction("Merge Down", new IHistoryAction?[] { pixelStep, structureStep }));
    }

    /// <summary>The selected layers that can be merged: visible and unlocked, bottom to top.</summary>
    private List<Layer> MergeCandidates()
        => Doc.Layers.Where(l => _main.SelectedLayers.Contains(l) && l.IsVisible && !l.IsLocked).ToList();

    /// <summary>
    /// Quick merge: paints all selected layers together into one new layer that takes the place of the top-most one. The result is made
    /// of pixels (use Group instead to keep the objects editable). Hidden and locked layers in the selection are left alone.
    /// </summary>
    private void MergeSelected()
    {
        var layers = MergeCandidates();
        if (layers.Count < 2)
        {
            _main.SetStatus("Select at least two visible, unlocked layers to merge (Ctrl+click in the Layers panel, or Shift+click / Ctrl+drag on the canvas).");
            return;
        }

        var flat = new PixelBuffer(Doc.Width, Doc.Height);
        foreach (var layer in layers) Compositor.DrawLayer(flat, layer);   // bottom to top, with each layer's opacity
        var merged = new Layer("Merged", flat);

        int top = Doc.Layers.IndexOf(layers[^1]);
        _main.Push(LayerStructureAction.Record("Merge Layers", Doc, () =>
        {
            var list = Doc.Layers.ToList();
            list.Insert(top + 1, merged);
            list.RemoveAll(l => layers.Contains(l));
            Doc.SetLayers(list, merged);
        }));
        _main.SelectOnly(merged);
        _main.SetStatus($"Merged {layers.Count} layers into one. Ctrl+Z undoes it. (Group, Ctrl+G, keeps objects editable instead.)");
    }

    /// <summary>Merges all visible layers into one "Background" layer.</summary>
    private void Flatten()
    {
        var flat = new Layer("Background", Compositor.Flatten(Doc));
        _main.Push(LayerStructureAction.Record("Flatten Image", Doc, () => Doc.SetLayers(new[] { flat }, flat)));
    }

    /// <summary>Turns the selected layer a quarter turn around its middle. A layer of plain pixels first becomes a movable picture.</summary>
    private void RotateLayer(double degrees)
    {
        var layer = Doc.ActiveLayer;
        if (layer == null || layer.IsLocked || !layer.IsVisible) return;
        if (layer.Object == null && !_main.MakePictureObject(layer, "Rotate")) return;
        if (layer.Object is not { } obj) return;
        _main.Push(ObjectChangeAction.Apply(degrees > 0 ? "Rotate 90° Right" : "Rotate 90° Left", layer,
            obj with { Box = obj.Box with { Rotation = ObjectBox.NormalizeAngle(obj.Box.Rotation + degrees) } }));
    }

    /// <summary>
    /// Split: the selected part of the layer moves onto a new layer above it (like Cut, then Paste in place). The original layer
    /// stays visible, without that part.
    /// </summary>
    private void Split()
    {
        if (Doc.Selection is not { } selection || !_main.TryGetEditableLayer(out var layer)) return;
        var piece = layer.Duplicate(layer.Name + " (split)");
        piece.ReplacePixels(layer.Pixels.Clone());
        if (selection.Invert() is { } outside) SelectionPixels.Clear(piece.Pixels, outside);   // the new layer keeps only the selected part (nothing to remove when everything is selected)

        var before = layer.Pixels.Clone();
        var dirty = SelectionPixels.Clear(layer.Pixels, selection);              // the original loses it
        layer.NotifyPixelsChanged(dirty);
        var cut = PixelChangeAction.Create("Split", layer, before, dirty);
        int index = Doc.Layers.ToList().IndexOf(layer);
        var insert = LayerStructureAction.Record("Split", Doc, () =>
        {
            var layers = Doc.Layers.ToList();
            layers.Insert(index + 1, piece);
            Doc.SetLayers(layers, piece);
        });
        _main.Push(new CompositeAction("Split", new IHistoryAction?[] { cut, insert }));
        Doc.Selection = null;
        _main.SelectOnly(piece);
        _main.SetStatus($"Split the selected part onto a new layer \"{piece.Name}\". \"{layer.Name}\" is still there without it. Ctrl+Z undoes it.");
    }

    private void FlipLayer(bool horizontal)
    {
        // Objects flip in place and stay editable.
        if (Doc.ActiveLayer is { Object: { } obj } objectLayer)
        {
            _main.Push(ObjectChangeAction.Apply(horizontal ? "Flip Horizontal" : "Flip Vertical", objectLayer,
                horizontal ? obj with { FlipX = !obj.FlipX } : obj with { FlipY = !obj.FlipY }));
            return;
        }
        if (!_main.TryGetEditableLayer(out var layer)) return;
        _main.Push(CanvasChangeAction.ApplyToLayer(horizontal ? "Flip Layer Horizontal" : "Flip Layer Vertical",
            Doc, layer, horizontal ? ImageTransforms.FlipHorizontal : ImageTransforms.FlipVertical));
    }
}
