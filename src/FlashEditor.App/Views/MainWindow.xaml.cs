using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FlashEditor.App.ViewModels;
using FlashEditor.Core.IO;

namespace FlashEditor.App.Views;

/// <summary>
/// Main window. Layout and menus are in XAML; this file only handles single-key shortcuts,
/// drag &amp; drop and the "save changes?" question on close.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        Services.WindowPlacement.Restore(this); // full screen on the first run, afterwards as the user left it
        _viewModel = viewModel;
        DataContext = viewModel;
        CanvasArea.Attach(viewModel);

        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += OnPreviewKeyUp;
        Drop += OnDrop;
        DragOver += OnDragOver;
        Closing += OnClosing;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsHistoryExpanded)) UpdateHistoryRow();
        };
        UpdateHistoryRow();
    }

    /// <summary>History is minimized to its header by default; opened, it gets a resizable share of the panel.</summary>
    private void UpdateHistoryRow()
    {
        bool open = _viewModel.IsHistoryExpanded;
        HistoryRow.Height = open ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        HistoryRow.MinHeight = open ? 100 : 0;
        HistorySplitter.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>True when the user is typing somewhere, so letter keys must not switch tools.</summary>
    private static bool IsTyping => Keyboard.FocusedElement is TextBoxBase or ComboBox { IsEditable: true };

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsTyping) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        // Space held = temporary hand tool.
        if (key == Key.Space)
        {
            CanvasArea.SetSpacePan(true);
            e.Handled = true;
            return;
        }

        // Ctrl / Alt shortcuts are handled by the menu key bindings.
        if (modifiers.HasFlag(ModifierKeys.Control) || modifiers.HasFlag(ModifierKeys.Alt)) return;

        // The active tool gets the first chance (brush size, arrow nudge, crop Enter/Esc...).
        if (_viewModel.Tools.ActiveTool.KeyDown(key, modifiers))
        {
            e.Handled = true;
            return;
        }

        switch (key)
        {
            case Key.X: _viewModel.Colors.Swap(); e.Handled = true; return;
            case Key.D: _viewModel.Colors.Reset(); e.Handled = true; return;
            case Key.Delete:
            case Key.Back:
                // Delete the selected pixels; without a selection, delete the selected shape / text / image
                // (or, when a part of a group is selected, just that part).
                if (_viewModel.DeleteActivePart()) { }
                else if (_viewModel.Edit.DeleteCommand.CanExecute(null)) _viewModel.Edit.DeleteCommand.Execute(null);
                else if (_viewModel.Document.Model.ActiveLayer?.Object != null && _viewModel.Layers.DeleteCommand.CanExecute(null))
                    _viewModel.Layers.DeleteCommand.Execute(null);
                e.Handled = true;
                return;
            case Key.Escape:
                _viewModel.Edit.DeselectCommand.Execute(null);
                e.Handled = true;
                return;
        }

        if (modifiers == ModifierKeys.None && _viewModel.Tools.TrySelectByShortcut(key))
            e.Handled = true;
    }

    private void OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) CanvasArea.SetSpacePan(false);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedImage(e) != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Drop images to open them (one tab each). Hold Ctrl while dropping to add them as new layers.</summary>
    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        foreach (var path in files.Where(ImageFileFormats.CanOpen))
        {
            // Ctrl = add as a layer to the current picture; otherwise each file opens in its own tab.
            if (e.KeyStates.HasFlag(DragDropKeyStates.ControlKey)) _viewModel.File.PlacePath(path);
            else _viewModel.File.OpenPath(path);
        }
    }

    private static string? GetDroppedImage(DragEventArgs e)
        => e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && ImageFileFormats.CanOpen(files[0])
            ? files[0]
            : null;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.ConfirmCloseAll())
        {
            e.Cancel = true;
            return;
        }
        Services.WindowPlacement.Save(this); // here, while the window still knows its size (RestoreBounds is empty after Closed)
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    // ------------------------------------------------------------------ renaming a tab

    private void OnTabRenameVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox box && box.IsVisible)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            box.Focus();
            box.SelectAll();
        }
    }

    private void OnTabRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: DocumentViewModel document } box) return;
        if (e.Key == Key.Enter)
        {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            document.IsRenaming = false;
            CanvasArea.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            document.IsRenaming = false; // keep the old name
            CanvasArea.Focus();
            e.Handled = true;
        }
    }

    private void OnTabRenameLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: DocumentViewModel { IsRenaming: true } document } box) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        document.IsRenaming = false;
    }
}
