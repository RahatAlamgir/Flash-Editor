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
        ("Full HD (1920 x 1080)", 1920, 1080),
        ("HD (1280 x 720)", 1280, 720),
        ("Square post (1080 x 1080)", 1080, 1080),
        ("Story (1080 x 1920)", 1080, 1920),
        ("A4 at 300 dpi (2480 x 3508)", 2480, 3508),
        ("Icon (256 x 256)", 256, 256),
        ("Small (800 x 600)", 800, 600),
    };

    private readonly ColorRgba _backgroundColor;

    public NewImageDialog(ColorRgba backgroundColor)
    {
        InitializeComponent();
        _backgroundColor = backgroundColor;
        PresetBox.ItemsSource = Presets.Select(p => p.Name).ToList();
        PresetBox.SelectedIndex = 2;
        Loaded += (_, _) => WidthBox.Focus();
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
        Result = new NewImageSettings(w, h, background);
        DialogResult = true;
    }

    private static bool TryReadSize(string text, out int value)
        => int.TryParse(text.Trim(), out value) && value is >= 1 and <= ImageDecoder.MaxDimension;
}
