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
            if (_main != null)
            {
                _main.SelectedLayersChanged -= OnMainSelectionChanged;
                _main.ActivePartChanged -= OnActivePartChanged;
            }
            _main = DataContext as MainViewModel;
            if (_main != null)
            {
                _main.SelectedLayersChanged += OnMainSelectionChanged;
                _main.ActivePartChanged += OnActivePartChanged;
            }
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

    private void OnActivePartChanged(object? sender, EventArgs e) => UpdatePartHighlight();

    /// <summary>Click a part under a group to edit it inside the group; double-click its name to rename it.</summary>
    private void OnPartMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_main == null || sender is not FrameworkElement { DataContext: GroupChildViewModel part }) return;
        if (IsInsideTextBox(e.OriginalSource as DependencyObject)) return; // typing a new name: leave the box alone

        e.Handled = true; // we select the group and its part ourselves (the list would only select the group)
        if (e.ClickCount >= 2)
        {
            part.IsEditingName = true;
            return;
        }
        _main.SelectPart(part.GroupLayer, part.Index);
        UpdatePartHighlight();
        _main.Canvas?.RefreshTransformFrame();
    }

    private static bool IsInsideTextBox(DependencyObject? element)
    {
        for (; element != null; element = System.Windows.Media.VisualTreeHelper.GetParent(element))
            if (element is TextBox) return true;
        return false;
    }

    /// <summary>Highlights the row of the part that is being edited (if any).</summary>
    private void UpdatePartHighlight()
    {
        if (_main == null) return;
        int? selected = _main.ActivePartIndex;
        var layer = _main.Document.Model.ActiveLayer;
        foreach (var vm in _main.Document.LayersTopDown)
            foreach (var child in vm.Children)
                child.IsSelected = selected != null && ReferenceEquals(child.GroupLayer, layer) && child.Index == selected;
    }

    /// <summary>Double-click a layer name to rename it.</summary>
    private void OnNameMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: IEditableName layer })
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
        if (sender is not TextBox { DataContext: IEditableName layer } box) return;
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
        if (sender is not TextBox { DataContext: IEditableName layer } box || !layer.IsEditingName) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        layer.IsEditingName = false;
    }
}
