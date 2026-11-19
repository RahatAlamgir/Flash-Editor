using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using FlashEditor.App.ViewModels;
using FlashEditor.Core.IO;
using FlashEditor.Core.IO.Project;

namespace FlashEditor.App.Views;

/// <summary>
/// Main window. Layout and menus are in XAML; this file only handles single-key shortcuts,
/// drag &amp; drop and the "save changes?" question on close.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>Drags of the project tabs (top) and the page tabs (bottom).</summary>
    public Controls.TabDragger ProjectDragger { get; }
    public Controls.TabDragger PageDragger { get; }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        Services.WindowPlacement.Restore(this); // full screen on the first run, afterwards as the user left it
        _viewModel = viewModel;
        DataContext = viewModel;
        CanvasArea.Attach(viewModel);

        // Drag a project tab (top) or a page tab (bottom) left or right to change its place.
        ProjectDragger = new Controls.TabDragger(ProjectTabs, (item, index) => _viewModel.MoveProject((ProjectGroup)item, index));
        PageDragger = new Controls.TabDragger(PageTabs, (item, index) => _viewModel.MovePage((DocumentViewModel)item, index));

        Services.IconTheme.Style = Services.AppSettings.Current.IconStyle;
        BuildShapeMenu();
        Services.Shortcuts.Register(viewModel);
        Services.Shortcuts.Apply(this);
        Services.Shortcuts.Changed += (_, _) => Services.Shortcuts.Apply(this);
        Ribbon.FileRequested += (_, _) => FileDrawer.Open();
        Ribbon.ExtraSearchRoot = ("File", FileDrawer);
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseDown += OnAnyMouseDown;
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
    /// <summary>A choice in the menu of a page card (under the canvas).</summary>
    private void OnPageMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string action } item && item.DataContext is DocumentViewModel page)
            _viewModel.Pages.Run(page, action);
    }

    /// <summary>Click on a page card: Ctrl adds or removes it from the picked pages, Shift picks a range.</summary>
    private void OnPageCardDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DocumentViewModel page } || e.ClickCount != 1) return;
        // The "..." button and the rename box do their own thing.
        for (var d = e.OriginalSource as DependencyObject; d != null && d is not Border { Name: "Card" }; d = System.Windows.Media.VisualTreeHelper.GetParent(d))
            if (d is Button or TextBox) return;
        _viewModel.Pages.Click(page, Keyboard.Modifiers);
    }

    /// <summary>The "+" in the gap before a page card: a new empty page right there.</summary>
    private void OnInsertPageBefore(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DocumentViewModel page })
        {
            _viewModel.Pages.AddBlank(page.Project.Pages.IndexOf(page));
            Keyboard.Focus(null);
        }
        e.Handled = true;
    }

    private bool _pagesFolded;

    /// <summary>The arrow in the middle: slides the page strip away or back.</summary>
    private void OnTogglePages(object sender, RoutedEventArgs e)
    {
        _pagesFolded = !_pagesFolded;
        if (_pagesFolded) ReleasePages();
        PagesToggle.Content = _pagesFolded ? "" : "";
        PagesToggle.ToolTip = _pagesFolded ? "Show the pages" : "Hide the pages";
        var animation = new System.Windows.Media.Animation.DoubleAnimation(_pagesFolded ? 0 : 128, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut },
        };
        PagesBar.BeginAnimation(HeightProperty, animation);
    }

    /// <summary>The keyboard goes back to the canvas and only the open page stays picked, so Delete works on the selected object again.</summary>
    private void ReleasePages()
    {
        if (_viewModel.Document is { } doc) _viewModel.Pages.Click(doc, ModifierKeys.None);
        Keyboard.ClearFocus();
        Focus();
    }

    private static DependencyObject? NextParent(DependencyObject d) =>
        d is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D ? System.Windows.Media.VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

    /// <summary>A click anywhere outside the page strip (canvas, an object, panels) ends the page selection.</summary>
    private void OnAnyMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!PagesHaveFocus) return;
        for (var d = e.OriginalSource as DependencyObject; d != null; d = NextParent(d))
            if (ReferenceEquals(d, PagesBar) || ReferenceEquals(d, PageGrid) || d is System.Windows.Controls.Primitives.Popup or System.Windows.Controls.ContextMenu) return;
        ReleasePages();
    }

    private void OnPastePages(object sender, RoutedEventArgs e) => _viewModel.Pages.Paste();

    /// <summary>True while the keyboard belongs to the page row or the grid view: Ctrl+C / X / V / D / A and Delete then work on pages.</summary>
    private bool PagesHaveFocus => PagesBar.IsKeyboardFocusWithin || PageGrid.IsKeyboardFocusWithin;

    private bool HandlePageKeys(Key key, ModifierKeys modifiers)
    {
        var pages = _viewModel.Pages;
        if (modifiers == ModifierKeys.Control)
        {
            switch (key)
            {
                case Key.C: pages.Copy(); return true;
                case Key.X: pages.Copy(); pages.Delete(); return true;
                case Key.V: pages.Paste(); return true;
                case Key.D: pages.Duplicate(); return true;
                case Key.A: pages.SelectAll(); return true;
            }
        }
        if (modifiers == ModifierKeys.None && key is Key.Delete or Key.Back) { pages.Delete(); return true; }
        return false;
    }

    /// <summary>The "..." button on a page card opens the same menu as a right-click.</summary>
    private void OnPageMenuButton(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FrameworkElement card } && card.ContextMenu is { } menu)
        {
            menu.PlacementTarget = card;
            menu.IsOpen = true;
        }
        e.Handled = true;
    }

    private static bool IsTyping => Keyboard.FocusedElement is TextBoxBase or ComboBox { IsEditable: true };

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (FileDrawer.IsOpen) return; // the drawer handles Esc itself; no tool shortcuts while it is open
        if (IsTyping) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        // With the keyboard on the page row (after a click on a page): the same keys work on the picked pages.
        if (PagesHaveFocus && HandlePageKeys(key, modifiers))
        {
            e.Handled = true;
            return;
        }

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
        foreach (var path in files.Where(CanOpenDropped))
        {
            // Ctrl = add as a layer to the current picture; otherwise each file opens in its own tab (projects always open).
            if (e.KeyStates.HasFlag(DragDropKeyStates.ControlKey) && !ProjectFile.IsProject(path)) _viewModel.File.PlacePath(path);
            else _viewModel.File.OpenPath(path);
        }
    }

    private static string? GetDroppedImage(DragEventArgs e)
        => e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files && CanOpenDropped(files[0])
            ? files[0]
            : null;

    private static bool CanOpenDropped(string path) => ImageFileFormats.CanOpen(path) || ProjectFile.IsProject(path) || Core.IO.Psd.PsdFile.IsPsd(path) || Core.IO.Svg.SvgFile.IsSvg(path);

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_viewModel.ConfirmCloseAll())
        {
            e.Cancel = true;
            return;
        }
        Services.WindowPlacement.Save(this); // here, while the window still knows its size (RestoreBounds is empty after Closed)
    }


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
        if (sender is not TextBox { DataContext: IRenamable document } box) return;
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
        if (sender is not TextBox { DataContext: IRenamable { IsRenaming: true } document } box) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        document.IsRenaming = false;
    }
}
