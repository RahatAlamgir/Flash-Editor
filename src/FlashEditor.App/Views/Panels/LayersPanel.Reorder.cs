using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.App.ViewModels;
using FlashEditor.Core.Documents;

namespace FlashEditor.App.Views.Panels;

/// <summary>Drag a layer (or all selected layers) up or down in the list to change its place in the stack.</summary>
public partial class LayersPanel
{
    private const string DragFormat = "FlashEditor.Layers";

    /// <summary>How far (in pixels) the pointer must move up or down with the button held before a layer is dragged: about a third of a row.</summary>
    private static double DragDistance => Math.Max(14, SystemParameters.MinimumVerticalDragDistance * 3);

    private Point _dragStart;
    private List<Layer>? _dragLayers; // the layers that will be dragged once the mouse has moved far enough

    private void OnListMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragLayers = null;
        if (_main == null || e.ChangedButton != MouseButton.Left || Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.OriginalSource is not DependencyObject source) return;

        // Only the row itself starts a drag: not the eye / lock / arrow buttons, the name box, or a group's part rows.
        if (FindAncestor<ToggleButton>(source) != null || FindAncestor<TextBox>(source) != null) return;
        if (FindAncestor<FrameworkElement>(source, element => element.DataContext is GroupChildViewModel) != null) return;
        if (FindAncestor<ListBoxItem>(source) is not { DataContext: LayerViewModel row }) return;

        // Dragging one of several selected layers takes them all along.
        var selected = _main.SelectedLayers;
        _dragLayers = selected.Count > 1 && selected.Contains(row.Model) ? selected.ToList() : new List<Layer> { row.Model };
        _dragStart = LayerList.PointToScreen(e.GetPosition(LayerList));      // screen position: the panel may move on screen while it is clicked
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragLayers == null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragLayers = null;
            return;
        }
        // A click is not a drag: the pointer has to travel a good way up or down first (a slightly shaky click or a touchpad tap must never
        // move a layer). While we wait, the list does not see the movement either, so it cannot turn the click into a range selection.
        // Measured on the screen, not inside the list: when a click changes what the Properties panel shows, the panel can change height and
        // the list moves under the pointer. That made a plain click look like a long drag and moved the layer.
        var screen = LayerList.PointToScreen(e.GetPosition(LayerList));
        if (Math.Abs(screen.Y - _dragStart.Y) < DragDistance)
        {
            e.Handled = true;
            return;
        }

        var layers = _dragLayers;
        _dragLayers = null;
        try { DragDrop.DoDragDrop(LayerList, new DataObject(DragFormat, layers), DragDropEffects.Move); }
        finally { DropLine.Visibility = Visibility.Collapsed; }
    }

    private void OnListMouseUp(object sender, MouseButtonEventArgs e) => _dragLayers = null;

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DragFormat)) return; // not ours (a file dropped on the window): let the window handle it
        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var position = e.GetPosition(LayerList);
        var (_, y) = FindGap(position);
        DropLine.Margin = new Thickness(4, Math.Max(0, y - 1.5), 4, 0);
        DropLine.Visibility = Visibility.Visible;
        AutoScroll(position);
    }

    private void OnListDragLeave(object sender, DragEventArgs e)
    {
        var position = e.GetPosition(LayerList);
        bool outside = position.X < 0 || position.Y < 0 || position.X > LayerList.ActualWidth || position.Y > LayerList.ActualHeight;
        if (outside) DropLine.Visibility = Visibility.Collapsed;
    }

    private void OnListDrop(object sender, DragEventArgs e)
    {
        DropLine.Visibility = Visibility.Collapsed;
        if (_main == null || e.Data.GetData(DragFormat) is not List<Layer> layers) return;
        e.Handled = true;

        var (gap, _) = FindGap(e.GetPosition(LayerList));
        _main.Layers.MoveLayersToGap(layers, gap);
    }

    /// <summary>
    /// The gap in the list the mouse is closest to (0 = above the first row, Count = below the last row) and the
    /// vertical position of that gap in list coordinates (rows can have different heights, e.g. open groups).
    /// </summary>
    private (int Gap, double Y) FindGap(Point position)
    {
        // Only the rows that are on screen exist (the list creates rows as they scroll into view), so the gap is worked out from those:
        // the area below the last row you can see is the gap after that row, never the end of the whole list.
        var rows = new List<LayerOrder.RowSpan>();
        for (int i = 0; i < LayerList.Items.Count; i++)
        {
            if (LayerList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement row) continue;
            var bounds = row.TransformToAncestor(LayerList).TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));
            rows.Add(new LayerOrder.RowSpan(i, bounds.Top, bounds.Bottom));
        }
        int gap = LayerOrder.GapAt(rows, position.Y, LayerList.Items.Count);
        var at = rows.FirstOrDefault(r => r.Index == gap);
        double y = at != default ? at.Top : rows.Count > 0 ? rows.Where(r => r.Index < gap).Select(r => r.Bottom).DefaultIfEmpty(0).Max() : 0;
        return (gap, y);
    }

    /// <summary>Scrolls the list while a layer is dragged near its top or bottom edge.</summary>
    private void AutoScroll(Point position)
    {
        const double Edge = 28, Step = 14;
        if (FindDescendant<ScrollViewer>(LayerList) is not { } scroller) return;
        if (position.Y < Edge) scroller.ScrollToVerticalOffset(scroller.VerticalOffset - Step);
        else if (position.Y > LayerList.ActualHeight - Edge) scroller.ScrollToVerticalOffset(scroller.VerticalOffset + Step);
    }

    private static T? FindAncestor<T>(DependencyObject? start, Func<T, bool>? matches = null) where T : DependencyObject
    {
        for (var element = start; element != null; element = element is Visual or System.Windows.Media.Media3D.Visual3D
                                                                 ? VisualTreeHelper.GetParent(element)
                                                                 : LogicalTreeHelper.GetParent(element))
            if (element is T found && (matches == null || matches(found))) return found;
        return null;
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) return found;
            if (FindDescendant<T>(child) is { } deeper) return deeper;
        }
        return null;
    }
}
