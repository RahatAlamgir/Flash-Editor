using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FlashEditor.App.Tools.Navigation;
using FlashEditor.App.Tools.Painting;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Views.Editor;

/// <summary>Right-click menu on the canvas: selects what is under the mouse and offers quick actions.</summary>
public partial class CanvasView
{
    /// <summary>Tools that use the right mouse button themselves (zoom out, pick background color).</summary>
    private bool ToolUsesRightButton => ActiveTool is ZoomTool or EyedropperTool;

    private void ShowQuickMenu(PointD imagePoint)
    {
        if (_main == null) return;
        // Right-clicking one of several selected layers keeps them all selected (to group them).
        var hit = _main.FindLayerAt(imagePoint, 3 / _zoom);
        if (hit == null || !_main.SelectedLayers.Contains(hit)) _main.SelectLayerAt(imagePoint, 3 / _zoom);
        else _main.Document.Model.ActiveLayer = hit;
        int count = _main.SelectedLayers.Count;

        var doc = _main.Document.Model;
        var layer = doc.ActiveLayer;
        var obj = layer?.Object;
        bool hasSelection = doc.Selection != null;

        var menu = new ContextMenu { PlacementTarget = Viewport, Placement = PlacementMode.MousePoint };
        void Add(string header, ICommand command, string? gesture = null, bool enabled = true)
            => menu.Items.Add(new MenuItem
            {
                Header = header,
                Command = command,
                InputGestureText = gesture ?? "",
                IsEnabled = enabled && command.CanExecute(null),
            });
        void Separator() => menu.Items.Add(new Separator());

        bool locked = layer?.IsLocked == true;
        Add(locked ? "Unlock" : "Lock", _main.Layers.ToggleLockCommand, "Ctrl+L");
        if (count > 1) Add($"Group {count} Layers", _main.Layers.GroupCommand, "Ctrl+G");
        if (count > 1) Add($"Merge {count} Layers", _main.Layers.MergeSelectedCommand, "Ctrl+E");
        // Align: one layer lines up with the canvas, several line up with each other.
        {
            var align = new MenuItem { Header = count > 1 ? "Align" : "Align to Canvas" };
            void AddAlign(string header, ICommand command, string parameter)
                => align.Items.Add(new MenuItem { Header = header, Command = command, CommandParameter = parameter, IsEnabled = command.CanExecute(parameter) });
            AddAlign("Left", _main.Align.AlignCommand, "Left");
            AddAlign("Center", _main.Align.AlignCommand, "CenterHorizontally");
            AddAlign("Right", _main.Align.AlignCommand, "Right");
            align.Items.Add(new Separator());
            AddAlign("Top", _main.Align.AlignCommand, "Top");
            AddAlign("Middle", _main.Align.AlignCommand, "CenterVertically");
            AddAlign("Bottom", _main.Align.AlignCommand, "Bottom");
            if (count > 2)
            {
                align.Items.Add(new Separator());
                AddAlign("Distribute Horizontally", _main.Align.DistributeCommand, "Horizontally");
                AddAlign("Distribute Vertically", _main.Align.DistributeCommand, "Vertically");
            }
            align.IsEnabled = _main.Align.AlignCommand.CanExecute(null) || layer != null && !layer.IsLocked;
            menu.Items.Add(align);
        }
        if (obj is GroupObject) Add("Ungroup", _main.Layers.UngroupCommand, "Ctrl+Shift+G");
        Separator();
        if (obj is TextObject && !locked) Add("Edit Text...", _main.Properties.EditTextCommand);
        Add(hasSelection ? "Cut Selection" : "Cut", _main.Edit.CutCommand, "Ctrl+X");
        Add(hasSelection ? "Copy Selection" : "Copy", _main.Edit.CopyCommand, "Ctrl+C");
        Add("Paste", _main.Edit.PasteCommand, "Ctrl+V");
        Add("Duplicate", _main.Layers.DuplicateCommand, "Ctrl+D");
        Add(hasSelection ? "Delete Selected Pixels" : count > 1 ? $"Delete {count} Layers" : "Delete Layer",
            hasSelection ? _main.Edit.DeleteCommand : _main.Layers.DeleteCommand, "Delete");
        Separator();
        Add("Bring to Front", _main.Layers.BringToFrontCommand, "Ctrl+Shift+]");
        Add("Bring Forward", _main.Layers.MoveUpCommand, "Ctrl+]");
        Add("Send Backward", _main.Layers.MoveDownCommand, "Ctrl+[");
        Add("Send to Back", _main.Layers.SendToBackCommand, "Ctrl+Shift+[");
        Separator();
        Add("Crop This Object...", _main.Layers.CropObjectCommand);
        if (hasSelection) Add("Crop Layer to Selection", _main.Layers.CropToSelectionCommand);
        Add("Flip Horizontal", _main.Layers.FlipLayerHorizontalCommand);
        Add("Flip Vertical", _main.Layers.FlipLayerVerticalCommand);
        if (obj != null) Add("Reset Rotation", _main.Properties.ResetRotationCommand);
        if (obj is ImageObject) Add("Original Size", _main.Properties.OriginalSizeCommand);
        Separator();
        Add(hasSelection ? "Recolor Selection..." : "Recolor...", _main.Effects.RecolorCommand, "Ctrl+Shift+R");
        Add("Remove Background (AI)", _main.Effects.RemoveBackgroundCommand, "Ctrl+Alt+B");
        Add("Select Subject (AI)", _main.Effects.SelectSubjectCommand);
        Add(hasSelection ? "Remove / Fill Selection (AI)" : "Remove / Fill (AI): select an area first", _main.Inpaint.FillSelectionCommand, "Shift+F5");
        Add("Split Asset Sheet into Pieces...", _main.Image.SplitSheetCommand);
        Add("Select Layer Content", _main.Edit.SelectLayerContentCommand);
        if (hasSelection) Add("Deselect", _main.Edit.DeselectCommand, "Ctrl+Shift+D");
        if (obj != null)
        {
            Separator();
            Add("Convert to Pixels", _main.Properties.ConvertToPixelsCommand);
        }

        menu.IsOpen = true;
    }
}
