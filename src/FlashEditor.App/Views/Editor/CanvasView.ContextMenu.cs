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
        void Add(string header, ICommand command, string? gesture = null, bool enabled = true, string? icon = null)
            => menu.Items.Add(new MenuItem
            {
                Header = header,
                Command = command,
                InputGestureText = gesture ?? "",
                IsEnabled = enabled && command.CanExecute(null),
                Icon = Controls.MenuIcons.Of(Viewport, icon),
            });
        void Separator() => menu.Items.Add(new Separator());

        bool locked = layer?.IsLocked == true;
        Add(locked ? "Unlock" : "Lock", _main.Layers.ToggleLockCommand, "Ctrl+L", icon: locked ? "Unlock" : "Lock");
        if (count > 1) Add($"Group {count} Layers", _main.Layers.GroupCommand, "Ctrl+G", icon: "Group");
        if (count > 1) Add($"Merge {count} Layers", _main.Layers.MergeSelectedCommand, "Ctrl+E", icon: "Merge");
        // Several layers line up with each other (one layer aligns to the canvas in the Properties panel).
        if (count > 1)
        {
            var align = new MenuItem { Header = "Align", Icon = IconOf("Align") };
            void AddAlign(string header, ICommand command, string parameter, string icon)
                => align.Items.Add(new MenuItem { Header = header, Command = command, CommandParameter = parameter, IsEnabled = command.CanExecute(parameter), Icon = IconOf(icon) });
            AddAlign("Left", _main.Align.AlignCommand, "Left", "AlignLeft");
            AddAlign("Center", _main.Align.AlignCommand, "CenterHorizontally", "AlignCenter");
            AddAlign("Right", _main.Align.AlignCommand, "Right", "AlignRight");
            align.Items.Add(new Separator());
            AddAlign("Top", _main.Align.AlignCommand, "Top", "AlignTop");
            AddAlign("Middle", _main.Align.AlignCommand, "CenterVertically", "AlignMiddle");
            AddAlign("Bottom", _main.Align.AlignCommand, "Bottom", "AlignBottom");
            if (count > 2)
            {
                align.Items.Add(new Separator());
                AddAlign("Distribute Horizontally", _main.Align.DistributeCommand, "Horizontally", "AlignCenter");
                AddAlign("Distribute Vertically", _main.Align.DistributeCommand, "Vertically", "AlignMiddle");
            }
            align.IsEnabled = _main.Align.AlignCommand.CanExecute(null);
            menu.Items.Add(align);
        }
        if (obj is GroupObject) Add("Ungroup", _main.Layers.UngroupCommand, "Ctrl+Shift+G", icon: "Ungroup");
        Separator();
        if (obj is TextObject && !locked) Add("Edit Text...", _main.Properties.EditTextCommand, icon: "EditText");
        Add(hasSelection ? "Cut Selection" : "Cut", _main.Edit.CutCommand, "Ctrl+X", icon: "Cut");
        Add(hasSelection ? "Copy Selection" : "Copy", _main.Edit.CopyCommand, "Ctrl+C", icon: "Copy");
        Add("Paste", _main.Edit.PasteCommand, "Ctrl+V", icon: "Paste");
        Add("Duplicate", _main.Layers.DuplicateCommand, "Ctrl+D", icon: "Duplicate");
        if (hasSelection) Add("Split to New Layer", _main.Layers.SplitCommand, icon: "Split");
        if (hasSelection) Add("Delete Selected Pixels", _main.Edit.DeleteCommand, "Delete", icon: "Delete");
        Separator();
        Add("Bring to Front", _main.Layers.BringToFrontCommand, "Ctrl+Shift+]", icon: "Front");
        Add("Bring Forward", _main.Layers.MoveUpCommand, "Ctrl+]", icon: "Up");
        Add("Send Backward", _main.Layers.MoveDownCommand, "Ctrl+[", icon: "Down");
        Add("Send to Back", _main.Layers.SendToBackCommand, "Ctrl+Shift+[", icon: "Back");
        Separator();
        if (hasSelection) Add("Crop Layer to Selection", _main.Layers.CropToSelectionCommand, icon: "Crop");
        Add("Rotate 90° Right", _main.Layers.RotateRightCommand, icon: "RotateRight");
        Add("Rotate 90° Left", _main.Layers.RotateLeftCommand, icon: "RotateLeft");
        Add("Flip Horizontal", _main.Layers.FlipLayerHorizontalCommand, icon: "FlipH");
        Add("Flip Vertical", _main.Layers.FlipLayerVerticalCommand, icon: "FlipV");
        if (obj != null) Add("Reset Rotation", _main.Properties.ResetRotationCommand, icon: "ResetRotation");
        Separator();
        Add(hasSelection ? "Recolor Selection..." : "Recolor...", _main.Effects.RecolorCommand, "Ctrl+Shift+R", icon: "Recolor");
        Add("Remove Background (AI)", _main.Effects.RemoveBackgroundCommand, "Ctrl+Alt+B", icon: "RemoveBg");
        Add("Select Subject (AI)", _main.Effects.SelectSubjectCommand, icon: "SelectSubject");
        Add(hasSelection ? "Fill Selection (AI)" : "Select an area first to Fill (AI)", _main.Inpaint.FillSelectionCommand, "Shift+F5", icon: "FillAi");
        if (hasSelection) Add("Deselect", _main.Edit.DeselectCommand, "Ctrl+Shift+D", icon: "Deselect");

        menu.IsOpen = true;
    }

    /// <summary>A small icon (from Themes/Icons.xaml) for a menu item, or null.</summary>
    private object? IconOf(string? key)
    {
        if (key == null || TryFindResource("Icon." + key) is not System.Windows.Media.Geometry geometry) return null;
        var path = new System.Windows.Shapes.Path { Data = geometry, Style = (System.Windows.Style)FindResource("IconPath") };
        return new System.Windows.Controls.Viewbox { Width = 15, Height = 15, Child = path };
    }
}
