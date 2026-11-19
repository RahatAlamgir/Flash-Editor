using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>The diagram that was chosen in Insert > SmartArt: which layout, the words and the colors.</summary>
public sealed record SmartArtChoice(string LayoutId, string Name, IReadOnlyList<string> Items, string Scheme);

/// <summary>Insert > SmartArt: pick a diagram from the pictures, type your words (one per line), choose colors.</summary>
public partial class SmartArtDialog : Window
{
    private readonly Dictionary<string, IReadOnlyList<(SmartArt.LayoutInfo Layout, ImageSource Preview)>> _previews = new();
    private readonly Dictionary<string, Border> _cards = new();
    private SmartArt.LayoutInfo _selected = SmartArt.Layouts[0];
    private string _lastSample = "";
    private readonly bool _ready;

    public SmartArtChoice? Result { get; private set; }

    public SmartArtDialog()
    {
        InitializeComponent();
        foreach (var (name, _) in SmartArt.Schemes) Scheme.Items.Add(name);
        Categories.Items.Add("All diagrams");
        foreach (var c in SmartArt.Categories) Categories.Items.Add(c);
        _ready = true;
        Scheme.SelectedIndex = 0;                 // fills the grid
        Categories.SelectedIndex = 0;
        Select(SmartArt.Layouts[0]);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !Words.IsKeyboardFocused) { Close(); e.Handled = true; } };
    }

    private string SchemeName => (string)(Scheme.SelectedItem ?? SmartArt.Schemes[0].Name);

    private void OnSchemeChanged(object sender, SelectionChangedEventArgs e) { if (_ready) Refill(); }
    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e) { if (_ready) Refill(); }

    private IReadOnlyList<(SmartArt.LayoutInfo Layout, ImageSource Preview)> Previews()
    {
        string scheme = SchemeName;
        if (!_previews.TryGetValue(scheme, out var list))
        {
            list = SmartArt.Layouts.Select(l => (l, Preview(l, scheme))).ToList();
            _previews[scheme] = list;
        }
        return list;
    }

    private static ImageSource Preview(SmartArt.LayoutInfo layout, string scheme)
    {
        const int size = 150;
        var page = new PixelBuffer(size, size);
        page.Fill(new ColorRgba(238, 238, 242));
        var drawn = ObjectRendering.Renderer.Render(SmartArt.Build(layout.Id, layout.Sample, scheme, size, size, size * 0.88), size, size);
        FlashEditor.Core.Painting.Compositor.DrawOver(page, drawn, 0, 0);
        return BitmapConverter.ToFrozenBitmap(page);
    }

    private void Refill()
    {
        Grid.Children.Clear();
        _cards.Clear();
        string? category = Categories.SelectedIndex > 0 ? (string)Categories.SelectedItem : null;
        foreach (var (layout, preview) in Previews().Where(p => category == null || p.Layout.Category == category))
        {
            var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(new Image { Source = preview, Width = 112, Height = 112, Stretch = Stretch.Uniform });
            panel.Children.Add(new TextBlock { Text = layout.Name, FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0), MaxWidth = 128, TextTrimming = TextTrimming.CharacterEllipsis });
            var card = new Border { Width = 138, Height = 152, Margin = new Thickness(3), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(2), Padding = new Thickness(4), Cursor = Cursors.Hand, Background = Brushes.Transparent, Child = panel, ToolTip = layout.Name };
            var captured = layout;
            card.MouseLeftButtonUp += (_, _) => Select(captured);
            card.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) OnInsert(this, new RoutedEventArgs()); };
            _cards[layout.Id] = card;
            Grid.Children.Add(card);
        }
        MarkSelected();
    }

    private void Select(SmartArt.LayoutInfo layout)
    {
        // New sample words only while the words are still the ones that came with the last layout.
        bool untouched = Words.Text.Trim() == _lastSample.Trim() || Words.Text.Trim().Length == 0;
        _selected = layout;
        if (untouched)
        {
            _lastSample = string.Join("\r\n", layout.Sample);
            Words.Text = _lastSample;
        }
        WordsTitle.Text = layout.Min == layout.Max ? $"Your words (one per line, {layout.Max} needed)" : $"Your words (one per line, {layout.Min} to {layout.Max})";
        Hint.Text = $"{layout.Name}: the text and shapes become a group. Ungroup it (Ctrl+Shift+G) to move or recolor single parts.";
        MarkSelected();
    }

    private void MarkSelected()
    {
        foreach (var (id, card) in _cards)
            card.BorderBrush = id == _selected.Id ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("DividerBrush");
    }

    private void OnInsert(object sender, RoutedEventArgs e)
    {
        var items = Words.Text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (items.Count == 0) items = _selected.Sample.ToList();
        Result = new SmartArtChoice(_selected.Id, _selected.Name, items, SchemeName);
        DialogResult = true;
    }
}
