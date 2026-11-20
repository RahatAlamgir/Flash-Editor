using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.Core.Layout;

namespace FlashEditor.App.Views.Controls;

/// <summary>
/// Lets the tabs of an <see cref="ItemsControl"/> (a horizontal strip) be dragged left and right with the mouse. The tab follows the pointer:
/// it changes place with a neighbour as soon as the pointer passes that neighbour's middle. A click that wiggles a little is still a click:
/// nothing moves until the pointer has travelled a good way, measured on the screen.
/// </summary>
public sealed class TabDragger
{
    private const double MinDragDistance = 10;       // pixels the pointer must travel before a click becomes a drag

    private readonly ItemsControl _strip;
    private readonly Action<object, int> _move;
    private object? _item;                            // the tab that was pressed
    private Point _startScreen;
    private bool _dragging;

    /// <param name="strip">The strip of tabs (its items are the tabs).</param>
    /// <param name="move">Moves an item to a new index.</param>
    public TabDragger(ItemsControl strip, Action<object, int> move)
    {
        _strip = strip;
        _move = move;
        strip.PreviewMouseLeftButtonDown += OnDown;
        strip.PreviewMouseMove += OnMove;
        strip.PreviewMouseLeftButtonUp += OnUp;
        strip.LostMouseCapture += (_, _) => Reset();
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        Reset();
        if (e.ClickCount != 1 || e.OriginalSource is not DependencyObject source) return;
        // The close button and the rename box are not for dragging.
        if (Ancestor<ButtonBase>(source) != null || Ancestor<TextBoxBase>(source) != null) return;
        var container = ItemsControl.ContainerFromElement(_strip, source);
        if (container == null) return;
        _item = _strip.ItemContainerGenerator.ItemFromContainer(container);
        _startScreen = _strip.PointToScreen(e.GetPosition(_strip));
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_item == null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            Reset();
            return;
        }

        var screen = _strip.PointToScreen(e.GetPosition(_strip));
        if (!_dragging)
        {
            if (Math.Abs(screen.X - _startScreen.X) < MinDragDistance) return;
            _dragging = true;
            _strip.CaptureMouse();
            _strip.Cursor = Cursors.SizeWE;
        }

        Follow(e.GetPosition(_strip).X);
        e.Handled = true;
    }

    /// <summary>Moves the tab that is being dragged to where the pointer (at <paramref name="x"/> in the strip) is.</summary>
    private void Follow(double x)
    {
        var spans = new List<TabSpan>();
        for (int i = 0; i < _strip.Items.Count; i++)
        {
            if (_strip.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement tab) { spans.Add(new TabSpan(double.MaxValue, double.MaxValue)); continue; }
            var bounds = tab.TransformToAncestor(_strip).TransformBounds(new Rect(0, 0, tab.ActualWidth, tab.ActualHeight));
            spans.Add(new TabSpan(bounds.Left, bounds.Right));
        }
        int current = _item == null ? -1 : _strip.Items.IndexOf(_item);
        int target = TabOrder.IndexAfterMove(current, TabOrder.GapAt(spans, x));
        if (current >= 0 && target != current) _move(_item!, target);
    }



    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging) e.Handled = true;       // the drop is not also a click on a tab
        Reset();
    }

    private void Reset()
    {
        _item = null;
        if (_dragging)
        {
            _dragging = false;
            _strip.Cursor = null;
            if (_strip.IsMouseCaptured) _strip.ReleaseMouseCapture();
        }
    }

    private static T? Ancestor<T>(DependencyObject? start) where T : DependencyObject
    {
        for (var element = start; element != null; element = element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
            if (element is T found) return found;
        return null;
    }
}
