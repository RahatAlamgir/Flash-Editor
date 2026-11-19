using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.App.Services;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>What was picked in Insert > Icons / Badges / Graphics: an icon (a symbol of the icon font), a ready-made badge or a graphic.</summary>
public sealed record IconChoice(bool IsBadge, string Id, string Name, string Glyph = "", bool IsGraphic = false);

/// <summary>Insert > Icons, Badges and Graphics: a grid of pictures with categories and a search box. Click one to insert it.</summary>
public partial class IconPickerDialog : Window
{
    private static readonly Lazy<IReadOnlyList<(Badges.BadgeInfo Info, ImageSource Preview)>> BadgePreviews = new(MakeBadgePreviews);
    private static readonly Lazy<IReadOnlyList<(Graphics.GraphicInfo Info, ImageSource Preview)>> GraphicPreviews = new(MakeGraphicPreviews);

    private enum Kind { Icons, Badges, Graphics }

    private readonly bool _ready;
    private bool _loadingCategories;

    public IconChoice? Result { get; private set; }

    /// <param name="start">"Icons", "Badges" or "Graphics": which page opens first.</param>
    public IconPickerDialog(string start)
    {
        InitializeComponent();
        _ready = true;
        switch (start)
        {
            case "Badges": BadgesTab.IsChecked = true; break;
            case "Graphics": GraphicsTab.IsChecked = true; break;
            default: IconsTab.IsChecked = true; break;
        }
        OnKindChanged(this, new RoutedEventArgs());
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Loaded += (_, _) => Search.Focus();
    }

    private Kind Current => BadgesTab.IsChecked == true ? Kind.Badges : GraphicsTab.IsChecked == true ? Kind.Graphics : Kind.Icons;

    private void OnKindChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _loadingCategories = true;
        Categories.Items.Clear();
        var (all, names) = Current switch
        {
            Kind.Badges => ("All badges", Badges.Categories),
            Kind.Graphics => ("All graphics", Graphics.Categories),
            _ => ("All icons", IconCatalog.Categories),
        };
        Categories.Items.Add(all);
        foreach (var c in names) Categories.Items.Add(c);
        Categories.SelectedIndex = 0;
        _loadingCategories = false;
        Refill();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) { if (_ready) Refill(); }
    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e) { if (_ready && !_loadingCategories) Refill(); }

    private void Refill()
    {
        Grid.Children.Clear();
        string query = Search.Text.Trim();
        bool Matches(string name, string category = "") => query.Length == 0
            || query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => name.Contains(w, StringComparison.OrdinalIgnoreCase) || category.Contains(w, StringComparison.OrdinalIgnoreCase));
        string? category = Categories.SelectedIndex > 0 ? (string)Categories.SelectedItem : null;
        int count = 0;

        switch (Current)
        {
            case Kind.Badges:
                foreach (var (info, preview) in BadgePreviews.Value.Where(b => (category == null || b.Info.Category == category) && Matches(b.Info.Name, b.Info.Category)))
                {
                    Grid.Children.Add(PreviewButton(info.Name, preview, new IconChoice(true, info.Id, info.Name)));
                    count++;
                }
                Hint.Text = count == 0 ? "Nothing matches. Try another word." : "Click a badge to put it in the middle of your picture. It is a group: ungroup it (Ctrl+Shift+G) to change the colors and words.";
                break;
            case Kind.Graphics:
                foreach (var (info, preview) in GraphicPreviews.Value.Where(g => (category == null || g.Info.Category == category) && Matches(g.Info.Name, g.Info.Category)))
                {
                    Grid.Children.Add(PreviewButton(info.Name, preview, new IconChoice(false, info.Id, info.Name, IsGraphic: true)));
                    count++;
                }
                Hint.Text = count == 0 ? "Nothing matches. Try another word." : "Click a graphic to put it in the middle of your picture. It is a group of shapes: ungroup it (Ctrl+Shift+G) to recolor or change its parts.";
                break;
            default:
                foreach (var icon in IconCatalog.All.Where(i => (category == null || i.Category == category) && Matches(i.Name, i.Category)))
                {
                    Grid.Children.Add(IconButton(icon));
                    count++;
                }
                Hint.Text = count == 0 ? "Nothing matches. Try another word." : "Click an icon to put it in the middle of your picture. It is text: change its color, size, outline and shadow in Properties.";
                break;
        }
    }

    private Button IconButton(IconItem icon)
    {
        var button = new Button
        {
            Width = 62, Height = 62, Margin = new Thickness(3), Padding = new Thickness(0), ToolTip = icon.Name, Style = (Style)FindResource("IconButton"),
            Content = new TextBlock
            {
                Text = icon.Glyph, FontFamily = new FontFamily(IconCatalog.FontFamily), FontSize = 28,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        ((TextBlock)button.Content).SetResourceReference(TextBlock.ForegroundProperty, "Icon." + CategoryColor(icon.Category));
        button.Click += (_, _) => Pick(new IconChoice(false, icon.Name, icon.Name, icon.Glyph));
        return button;
    }

    private static string CategoryColor(string category) => category switch
    {
        "Arrows" => "Blue", "Symbols" => "Amber", "Communication" => "Green", "Media" => "Pink", "Objects" => "Orange",
        "Places and travel" => "Teal", "Weather and nature" => "Sky", _ => "Purple",
    };

    private Button PreviewButton(string name, ImageSource preview, IconChoice choice)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new Image { Source = preview, Width = 90, Height = 90, Stretch = Stretch.Uniform });
        panel.Children.Add(new TextBlock { Text = name, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0), Foreground = (Brush)FindResource("SubtleTextBrush"), MaxWidth = 104, TextTrimming = TextTrimming.CharacterEllipsis });
        var button = new Button { Width = 112, Height = 124, Margin = new Thickness(3), Padding = new Thickness(0), Content = panel, ToolTip = name, Style = (Style)FindResource("IconButton") };
        button.Click += (_, _) => Pick(choice);
        return button;
    }

    private void Pick(IconChoice choice)
    {
        Result = choice;
        DialogResult = true;
    }

    private static ImageSource Preview(GroupObject group, int size)
    {
        var page = new PixelBuffer(size, size);
        page.Fill(new ColorRgba(238, 238, 242));
        var drawn = ObjectRendering.Renderer.Render(group, size, size);
        FlashEditor.Core.Painting.Compositor.DrawOver(page, drawn, 0, 0);
        return BitmapConverter.ToFrozenBitmap(page);
    }

    /// <summary>Draws each badge once with the real renderer, for the grid.</summary>
    private static IReadOnlyList<(Badges.BadgeInfo, ImageSource)> MakeBadgePreviews()
        => Badges.All.Select(info => (info, Preview(Badges.Build(info.Id, 150, 150, 150 * 0.86), 150))).ToList();

    private static IReadOnlyList<(Graphics.GraphicInfo, ImageSource)> MakeGraphicPreviews()
        => Graphics.All.Select(info => (info, Preview(Graphics.Build(info.Id, 150, 150, 150 * 0.8), 150))).ToList();
}
