using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.App.Services;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>What was picked in Insert > Icons: an icon (a symbol of the icon font) or a ready-made badge.</summary>
public sealed record IconChoice(bool IsBadge, string Id, string Name, string Glyph = "");

/// <summary>Insert > Icons and Badges: a grid of pictures with a search box. Click one to insert it.</summary>
public partial class IconPickerDialog : Window
{
    private static readonly Lazy<IReadOnlyList<(Badges.BadgeInfo Info, ImageSource Preview)>> BadgePreviews = new(MakeBadgePreviews);

    private readonly bool _ready;

    public IconChoice? Result { get; private set; }

    public IconPickerDialog(bool startWithBadges)
    {
        InitializeComponent();
        Categories.Items.Add("All icons");
        foreach (var c in IconCatalog.Categories) Categories.Items.Add(c);
        Categories.SelectedIndex = 0;
        _ready = true;
        if (startWithBadges) BadgesTab.IsChecked = true; else Refill();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Loaded += (_, _) => Search.Focus();
    }

    private bool ShowingBadges => BadgesTab.IsChecked == true;

    private void OnKindChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        Categories.Visibility = ShowingBadges ? Visibility.Collapsed : Visibility.Visible;
        Refill();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) { if (_ready) Refill(); }
    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e) { if (_ready) Refill(); }

    private void Refill()
    {
        Grid.Children.Clear();
        string query = Search.Text.Trim();
        bool Matches(string name, string category = "") => query.Length == 0
            || query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w => name.Contains(w, StringComparison.OrdinalIgnoreCase) || category.Contains(w, StringComparison.OrdinalIgnoreCase));

        if (ShowingBadges)
        {
            foreach (var (info, preview) in BadgePreviews.Value.Where(b => Matches(b.Info.Name)))
                Grid.Children.Add(BadgeButton(info, preview));
            Hint.Text = "Click a badge to put it in the middle of your picture. It is a group: ungroup it (Ctrl+Shift+G) to change the colors and words.";
            return;
        }

        string? category = Categories.SelectedIndex > 0 ? (string)Categories.SelectedItem : null;
        int count = 0;
        foreach (var icon in IconCatalog.All.Where(i => (category == null || i.Category == category) && Matches(i.Name, i.Category)))
        {
            Grid.Children.Add(IconButton(icon));
            count++;
        }
        Hint.Text = count == 0 ? "Nothing matches. Try another word." : "Click an icon to put it in the middle of your picture. It is text: change its color, size, outline and shadow in Properties.";
    }

    private Button IconButton(IconItem icon)
    {
        var button = new Button
        {
            Width = 62, Height = 62, Margin = new Thickness(3), Padding = new Thickness(0), ToolTip = icon.Name, Style = (Style)FindResource("IconButton"),
            Content = new TextBlock
            {
                Text = icon.Glyph, FontFamily = new FontFamily(IconCatalog.FontFamily), FontSize = 28, Foreground = (Brush)FindResource("TextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        button.Click += (_, _) => Pick(new IconChoice(false, icon.Name, icon.Name, icon.Glyph));
        return button;
    }

    private Button BadgeButton(Badges.BadgeInfo info, ImageSource preview)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new Image { Source = preview, Width = 90, Height = 90, Stretch = Stretch.Uniform });
        panel.Children.Add(new TextBlock { Text = info.Name, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0), Foreground = (Brush)FindResource("SubtleTextBrush") });
        var button = new Button { Width = 112, Height = 124, Margin = new Thickness(3), Padding = new Thickness(0), Content = panel, ToolTip = info.Name, Style = (Style)FindResource("IconButton") };
        button.Click += (_, _) => Pick(new IconChoice(true, info.Id, info.Name));
        return button;
    }

    private void Pick(IconChoice choice)
    {
        Result = choice;
        DialogResult = true;
    }

    /// <summary>Draws each badge once with the real renderer, on a light checker-free gray, for the grid.</summary>
    private static IReadOnlyList<(Badges.BadgeInfo, ImageSource)> MakeBadgePreviews()
    {
        const int size = 180;
        var list = new List<(Badges.BadgeInfo, ImageSource)>();
        foreach (var info in Badges.All)
        {
            var page = new PixelBuffer(size, size);
            page.Fill(new ColorRgba(238, 238, 242));
            var drawn = ObjectRendering.Renderer.Render(Badges.Build(info.Id, size, size, size * 0.86), size, size);
            FlashEditor.Core.Painting.Compositor.DrawOver(page, drawn, 0, 0);
            list.Add((info, BitmapConverter.ToFrozenBitmap(page)));
        }
        return list;
    }
}
