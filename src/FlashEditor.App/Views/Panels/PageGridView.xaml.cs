using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlashEditor.App.ViewModels;

namespace FlashEditor.App.Views.Panels;

/// <summary>The grid view of all pages: drag to reorder, double-click to open.</summary>
public partial class PageGridView : UserControl
{
    private const string DragFormat = "FlashEditor.Page";
    private Point _downAt;
    private DocumentViewModel? _pressed;
    private bool _dragging;

    public PageGridView() => InitializeComponent();

    private MainViewModel? Main => DataContext as MainViewModel;

    private static DocumentViewModel? PageOf(object sender) => (sender as FrameworkElement)?.DataContext as DocumentViewModel;

    private void OnCardDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = PageOf(sender);
        _downAt = e.GetPosition(this);
        _dragging = false;
        if (_pressed == null || Main == null) return;
        if (e.ClickCount == 2) Main.Pages.Run(_pressed, "open");
        else Main.Pages.Click(_pressed, Keyboard.Modifiers);
    }

    private void OnCardMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pressed == null || _dragging) return;
        var now = e.GetPosition(this);
        if (Math.Abs(now.X - _downAt.X) < 8 && Math.Abs(now.Y - _downAt.Y) < 8) return;
        _dragging = true;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(DragFormat, _pressed), DragDropEffects.Move);
        _pressed = null;
    }

    private void OnCardUp(object sender, MouseButtonEventArgs e)
    {
        _pressed = null;
        _dragging = false;
    }

    private void OnCardDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCardDrop(object sender, DragEventArgs e)
    {
        if (Main is not { } main || e.Data.GetData(DragFormat) is not DocumentViewModel dragged || PageOf(sender) is not { } target) return;
        int index = target.Project.Pages.IndexOf(target);
        if (index >= 0 && !ReferenceEquals(dragged, target)) main.MovePage(dragged, index);
        e.Handled = true;
    }

    private void OnCardMenu(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string action } item && item.DataContext is DocumentViewModel page && Main is { } main)
            main.Pages.Run(page, action, askNameInDialog: true);
    }
}
