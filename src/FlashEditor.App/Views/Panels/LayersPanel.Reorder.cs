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
        _dragStart = e.GetPosition(LayerList);
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragLayers == null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _dragLayers = null;
            return;
        }
        var position = e.GetPosition(LayerList);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

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
        double lastBottom = 0;
        for (int i = 0; i < LayerList.Items.Count; i++)
        {
            if (LayerList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement row) continue;
            var bounds = row.TransformToAncestor(LayerList).TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));
            if (position.Y < bounds.Top + bounds.Height / 2) return (i, bounds.Top);
            lastBottom = bounds.Bottom;
        }
        return (LayerList.Items.Count, lastBottom);
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
