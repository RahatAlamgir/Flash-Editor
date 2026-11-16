using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FlashEditor.App.ViewModels;

namespace FlashEditor.App.Views.Panels;

/// <summary>Layer list with visibility, thumbnails, inline rename, opacity and layer buttons.</summary>
public partial class LayersPanel : UserControl
{
    private MainViewModel? _main;
    private bool _syncing;

    public LayersPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_main != null) _main.SelectedLayersChanged -= OnMainSelectionChanged;
            _main = DataContext as MainViewModel;
            if (_main != null) _main.SelectedLayersChanged += OnMainSelectionChanged;
        };
    }

    /// <summary>Ctrl / Shift + click in the list selects several layers (same as Shift+click on the canvas).</summary>
    private void OnLayerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _main == null || LayerList.SelectedItems.Count == 0) return;
        // Only clicks / keys in this list count. The list also changes when the active layer changes
        // elsewhere (canvas Shift+click, undo); reacting to that would drop the other selected layers.
        if (!LayerList.IsMouseOver && !LayerList.IsKeyboardFocusWithin)
        {
            Dispatcher.BeginInvoke(() => OnMainSelectionChanged(null, EventArgs.Empty));
            return;
        }
        var layers = LayerList.SelectedItems.OfType<LayerViewModel>().Select(l => l.Model).ToList();
        var active = e.AddedItems.OfType<LayerViewModel>().LastOrDefault()?.Model ?? _main.Document.Model.ActiveLayer;
        _syncing = true;
        try { _main.SetSelection(layers, active); }
        finally { _syncing = false; }
        // The active-layer binding may have reset the list to one row: show all selected rows again.
        Dispatcher.BeginInvoke(() => OnMainSelectionChanged(null, EventArgs.Empty));
    }

    /// <summary>Shows the canvas selection (Shift+click there) as selected rows here.</summary>
    private void OnMainSelectionChanged(object? sender, EventArgs e)
    {
        if (_syncing || _main == null) return;
        var wanted = _main.SelectedLayers;
        _syncing = true;
        try
        {
            if (wanted.Count <= 1) return; // single selection follows the SelectedItem binding
            LayerList.SelectedItems.Clear();
            foreach (var vm in _main.Document.LayersTopDown)
                if (wanted.Contains(vm.Model)) LayerList.SelectedItems.Add(vm);
        }
        finally { _syncing = false; }
    }

    /// <summary>Double-click a layer name to rename it.</summary>
    private void OnNameMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: LayerViewModel layer })
        {
            layer.IsEditingName = true;
            e.Handled = true;
        }
    }

    private void OnEditorVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox box && box.IsVisible)
        {
            box.Focus();
            box.SelectAll();
        }
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: LayerViewModel layer } box) return;
        if (e.Key == Key.Enter)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            layer.IsEditingName = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget(); // undo typing
            layer.IsEditingName = false;
            e.Handled = true;
        }
    }

    private void OnEditorLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: LayerViewModel layer } box || !layer.IsEditingName) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        layer.IsEditingName = false;
    }
}
