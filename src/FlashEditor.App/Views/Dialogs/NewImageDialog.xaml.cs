using System.Windows;
using System.Windows.Controls;
using FlashEditor.App.Services;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>Size and background for a new blank image.</summary>
public partial class NewImageDialog : Window
{
    private static readonly (string Name, int W, int H)[] Presets =
    {
        ("Custom", 0, 0),
        ("Full HD / presentation (1920 x 1080)", 1920, 1080),
        ("HD (1280 x 720)", 1280, 720),
        ("YouTube thumbnail (1280 x 720)", 1280, 720),
        ("Instagram post, square (1080 x 1080)", 1080, 1080),
        ("Instagram post, portrait (1080 x 1350)", 1080, 1350),
        ("Instagram / TikTok story (1080 x 1920)", 1080, 1920),
        ("Facebook post (1200 x 630)", 1200, 630),
        ("Facebook cover (820 x 312)", 820, 312),
        ("X / Twitter header (1500 x 500)", 1500, 500),
        ("LinkedIn banner (1584 x 396)", 1584, 396),
        ("Pinterest pin (1000 x 1500)", 1000, 1500),
        ("A4 at 300 dpi (2480 x 3508)", 2480, 3508),
        ("A5 at 300 dpi (1748 x 2480)", 1748, 2480),
        ("US Letter at 300 dpi (2550 x 3300)", 2550, 3300),
        ("Business card at 300 dpi (1050 x 600)", 1050, 600),
        ("Poster 18 x 24 in at 150 dpi (2700 x 3600)", 2700, 3600),
        ("Icon (256 x 256)", 256, 256),
        ("Small (800 x 600)", 800, 600),
    };

    private readonly ColorRgba _backgroundColor;

    private readonly bool _askName;

    /// <param name="askName">Show a box for the project's name (used by the Project Manager).</param>
    public NewImageDialog(ColorRgba backgroundColor, bool askName = false)
    {
        InitializeComponent();
        _askName = askName;
        if (askName)
        {
            Title = "New Project";
            NameRow.Visibility = Visibility.Visible;
            Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
        }
        _backgroundColor = backgroundColor;
        PresetBox.ItemsSource = Presets.Select(p => p.Name).ToList();
        PresetBox.SelectedIndex = 2;
        TemplateBox.ItemsSource = DocumentTemplates.Names;
        TemplateBox.SelectedIndex = 0;
        if (!askName) Loaded += (_, _) => WidthBox.Focus();
    }

    public NewImageSettings? Result { get; private set; }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        var preset = Presets[Math.Max(0, PresetBox.SelectedIndex)];
        if (preset.W == 0) return;
        WidthBox.Text = preset.W.ToString();
        HeightBox.Text = preset.H.ToString();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!TryReadSize(WidthBox.Text, out int w) || !TryReadSize(HeightBox.Text, out int h))
        {
            ErrorText.Text = $"Enter a width and height between 1 and {ImageDecoder.MaxDimension}.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var background = TransparentOption.IsChecked == true ? ColorRgba.Transparent
            : BackgroundColorOption.IsChecked == true ? _backgroundColor
            : ColorRgba.White;
        string? template = TemplateBox.SelectedItem as string;
        Result = new NewImageSettings(w, h, background, template == DocumentTemplates.Blank ? null : template, _askName ? NameBox.Text.Trim() : null);
        DialogResult = true;
    }

    private static bool TryReadSize(string text, out int value)
        => int.TryParse(text.Trim(), out value) && value is >= 1 and <= ImageDecoder.MaxDimension;
}
