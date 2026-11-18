using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FlashEditor.App.ViewModels;
using FlashEditor.App.Views.Controls;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Views.Panels;

/// <summary>Right-click a layer in the list: quick layer options.</summary>
public partial class LayersPanel
{
    private void OnListRightClick(object sender, MouseButtonEventArgs e)
    {
        if (_main == null || FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: LayerViewModel row }) return;
        e.Handled = true;
        ShowLayerMenu(row);
    }

    /// <summary>Selects the layer (unless it is one of several selected) and opens its quick menu at the mouse.</summary>
    public void ShowLayerMenu(LayerViewModel row)
    {
        if (_main == null) return;

        // Right-clicking one of several selected layers keeps them all selected; any other layer becomes the selection.
        if (!_main.SelectedLayers.Contains(row.Model)) _main.SetSelection(new[] { row.Model }, row.Model);
        else _main.Document.Model.ActiveLayer = row.Model;
        int count = _main.SelectedLayers.Count;
        var layer = row.Model;

        var menu = new ContextMenu { PlacementTarget = LayerList, Placement = PlacementMode.MousePoint };
        void Add(string header, ICommand command, string? gesture = null, string? icon = null, object? parameter = null)
            => menu.Items.Add(new MenuItem
            {
                Header = header,
                Command = command,
                CommandParameter = parameter,
                InputGestureText = gesture ?? "",
                IsEnabled = command.CanExecute(parameter),
                Icon = MenuIcons.Of(this, icon),
            });
        void Separator() => menu.Items.Add(new Separator());

        var visibility = new MenuItem { Header = row.IsVisible ? "Hide" : "Show", Icon = MenuIcons.Of(this, "Eye") };
        visibility.Click += (_, _) => row.IsVisible = !row.IsVisible;
        menu.Items.Add(visibility);
        Add(layer.IsLocked ? "Unlock" : "Lock", _main.Layers.ToggleLockCommand, "Ctrl+L", layer.IsLocked ? "Unlock" : "Lock");
        Add("Rename", _main.Layers.RenameCommand, "Double-click", "EditText", row);
        Separator();
        Add("Duplicate", _main.Layers.DuplicateCommand, "Ctrl+D", "Duplicate");
        if (count > 1) Add($"Group {count} Layers", _main.Layers.GroupCommand, "Ctrl+G", "Group");
        if (layer.Object is GroupObject) Add("Ungroup", _main.Layers.UngroupCommand, "Ctrl+Shift+G", "Ungroup");
        if (count > 1) Add($"Merge {count} Layers", _main.Layers.MergeSelectedCommand, "Ctrl+E", "Merge");
        else
        {
            Add("Merge Up", _main.Layers.MergeUpCommand, icon: "Up");
            Add("Merge Down", _main.Layers.MergeDownCommand, "Ctrl+E", "Merge");
        }
        Separator();
        Add("Bring to Front", _main.Layers.BringToFrontCommand, "Ctrl+Shift+]", "Front");
        Add("Move Up", _main.Layers.MoveUpCommand, "Ctrl+]", "Up");
        Add("Move Down", _main.Layers.MoveDownCommand, "Ctrl+[", "Down");
        Add("Send to Back", _main.Layers.SendToBackCommand, "Ctrl+Shift+[", "Back");
        Separator();
        Add("Clip to Layer Below", _main.Mask.ClipToBelowCommand, icon: "Group");
        if (layer.HasMask) Add("Delete Layer Mask", _main.Mask.DeleteCommand, icon: "Deselect");
        else Add("Add Layer Mask", _main.Mask.RevealAllCommand, icon: "MaskBrush");
        Separator();
        Add(count > 1 ? $"Delete {count} Layers" : "Delete Layer", _main.Layers.DeleteCommand, "Delete", "Delete");

        menu.IsOpen = true;
    }
}
