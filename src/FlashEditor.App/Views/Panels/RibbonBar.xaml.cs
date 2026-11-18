using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace FlashEditor.App.Views.Panels;

/// <summary>
/// The ribbon at the top of the window: tabs (File, Edit, Select, Image ...) with groups of buttons under them.
/// The layout is in RibbonBar.xaml; this file opens the dropdown buttons and hides / shows the ribbon.
/// </summary>
public partial class RibbonBar : UserControl
{
    public RibbonBar() => InitializeComponent();

    /// <summary>The File button was pressed: the main window opens the File drawer.</summary>
    public event EventHandler? FileRequested;

    private void OnFileClicked(object sender, RoutedEventArgs e) => FileRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The menu under Insert > Shape (filled by the main window, which has the pictures of the shapes).</summary>
    public ContextMenu ShapeMenu => (ContextMenu)Resources["ShapeMenu"];

    /// <summary>Fills the row of quick shape buttons in the Insert tab (the main window knows the shapes and their pictures).</summary>
    public Action<Panel>? FillQuickShapes { get; set; }

    private void OnQuickShapesLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Panel { Children.Count: 0 } panel) FillQuickShapes?.Invoke(panel);
    }

    private bool IsCollapsed => Equals(Tabs.Tag, "collapsed");

    private void SetCollapsed(bool collapsed)
    {
        Tabs.Tag = collapsed ? "collapsed" : null;
        CollapseGlyph.Text = collapsed ? "" : "";
    }

    private void OnCollapseClicked(object sender, RoutedEventArgs e) => SetCollapsed(!IsCollapsed);

    /// <summary>Clicking a tab while the ribbon is hidden brings it back.</summary>
    private void OnTabClicked(object sender, MouseButtonEventArgs e)
    {
        var item = FindParent<TabItem>((e.OriginalSource ?? e.Source) as DependencyObject);
        if (item == null) return;
        item.IsSelected = true;
        if (IsCollapsed) SetCollapsed(false);
    }

    private void OnTabDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<TabItem>(e.OriginalSource as DependencyObject) != null) SetCollapsed(!IsCollapsed);
    }

    /// <summary>Opens the menu of a dropdown button right under it.</summary>
    private void OnDropDown(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.DataContext = DataContext;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnExit(object sender, RoutedEventArgs e) => Window.GetWindow(this)?.Close();

    private static T? FindParent<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return null;
    }
}
