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

    // ------------------------------------------------------------------ search ("tell me what you want to do")

    /// <summary>A command the search can find.</summary>
    public sealed record SearchItem(string Title, string Where, string Glyph, string Extra, Action Run, Func<bool> CanRun)
    {
        public double Opacity => CanRun() ? 1 : 0.4;
    }

    /// <summary>Another part of the window to search too (the File drawer), named for the "where" column.</summary>
    public (string Name, DependencyObject Root)? ExtraSearchRoot { get; set; }

    private List<SearchItem> BuildSearchIndex()
    {
        var items = new List<SearchItem>();
        foreach (TabItem tab in Tabs.Items) Walk(tab.Content as DependencyObject, tab.Header as string ?? "", "", DataContext, items);
        if (ExtraSearchRoot is { } extra) Walk(extra.Root, extra.Name, "", DataContext, items);
        return items;
    }

    private static string LabelOf(ContentControl c)
        => (c.Content as string) ?? System.Windows.Automation.AutomationProperties.GetName(c) ?? "";

    private static void Walk(DependencyObject? d, string tab, string group, object? context, List<SearchItem> items)
    {
        if (d == null) return;
        if (d is HeaderedContentControl { Header: string groupName }) group = groupName;
        if (d is ToggleButton toggle && LabelOf(toggle) is { Length: > 0 } toggleLabel)
        {
            items.Add(new SearchItem(toggleLabel, tab, toggle.Tag as string ?? "", (toggle.ToolTip as string ?? "") + " " + group, () => toggle.IsChecked = toggle.IsChecked != true, () => true));
        }
        else if (d is Button button && LabelOf(button) is { Length: > 0 } label)
        {
            string clean = label.Replace(" \u25BE", "").Trim();
            string glyph = button.Tag as string ?? "";
            string tip = (button.ToolTip as string ?? "") + " " + group + " " + System.Windows.Automation.AutomationProperties.GetHelpText(button);
            if (button.Command is { } command)
            {
                var parameter = button.CommandParameter;
                items.Add(new SearchItem(clean, tab, glyph, tip, () => command.Execute(parameter), () => command.CanExecute(parameter)));
            }
            else if (button.ContextMenu is { } menu)
            {
                menu.DataContext ??= context;
                foreach (var item in menu.Items.OfType<MenuItem>()) AddMenuItem(item, clean + " " + group, tab, glyph, items);
            }
        }
        foreach (var child in LogicalTreeHelper.GetChildren(d).OfType<DependencyObject>()) Walk(child, tab, group, context, items);
    }

    private static void AddMenuItem(MenuItem item, string parent, string tab, string glyph, List<SearchItem> items)
    {
        if (item.Header is not string header) return;
        header = header.Replace("...", "").Trim();
        if (item.Command is { } command)
        {
            var parameter = item.CommandParameter;
            items.Add(new SearchItem($"{header} ({parent.Split(' ')[0]})", tab, glyph, item.ToolTip as string ?? "", () => command.Execute(parameter), () => command.CanExecute(parameter)));
        }
        foreach (var sub in item.Items.OfType<MenuItem>()) AddMenuItem(sub, parent, tab, glyph, items);
    }

    /// <summary>The commands that match what was typed (every word has to appear in the name, the tab or the tip), best first.</summary>
    public IReadOnlyList<SearchItem> Search(string query, int max = 9)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return Array.Empty<SearchItem>();
        return BuildSearchIndex()
            .Select(i => (Item: i, Hay: $"{i.Title} {i.Where} {i.Extra}"))
            .Where(x => words.All(w => x.Hay.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Item.Title.StartsWith(words[0], StringComparison.OrdinalIgnoreCase) ? 0 : x.Item.Title.Contains(words[0], StringComparison.OrdinalIgnoreCase) ? 1 : 2)
            .ThenBy(x => x.Item.Title.Length)
            .Select(x => x.Item)
            .GroupBy(i => (i.Title, i.Where)).Select(g => g.First())
            .Take(max).ToList();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var results = Search(SearchBox.Text);
        SearchResults.ItemsSource = results;
        SearchPopup.IsOpen = results.Count > 0;
        if (results.Count > 0) SearchResults.SelectedIndex = 0;
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down when SearchPopup.IsOpen:
                SearchResults.SelectedIndex = Math.Min(SearchResults.Items.Count - 1, SearchResults.SelectedIndex + 1);
                e.Handled = true;
                break;
            case Key.Up when SearchPopup.IsOpen:
                SearchResults.SelectedIndex = Math.Max(0, SearchResults.SelectedIndex - 1);
                e.Handled = true;
                break;
            case Key.Enter:
                if (SearchResults.SelectedItem is SearchItem item) RunSearchItem(item);
                e.Handled = true;
                break;
            case Key.Escape:
                SearchBox.Clear();
                SearchPopup.IsOpen = false;
                Keyboard.ClearFocus();
                e.Handled = true;
                break;
        }
    }

    private void OnSearchResultClicked(object sender, MouseButtonEventArgs e)
    {
        var row = FindParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (row?.DataContext is SearchItem item) RunSearchItem(item);
    }

    private void RunSearchItem(SearchItem item)
    {
        SearchPopup.IsOpen = false;
        SearchBox.Clear();
        Keyboard.ClearFocus();
        if (item.CanRun()) item.Run();
    }

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
