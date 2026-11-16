using System.Windows;
using FlashEditor.App.Services;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>Resize the whole image (change its resolution up or down).</summary>
public partial class ImageSizeDialog : Window
{
    public ImageSizeDialog(int width, int height)
    {
        InitializeComponent();
        CurrentSizeText.Text = $"Current size: {width} x {height} px ({width * (long)height / 1e6:0.##} megapixels)";
        Editor.Initialize(width, height);
        Loaded += (_, _) => Editor.FocusWidth();
    }

    public ImageSizeSettings? Result { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!Editor.TryGetSize(out int w, out int h, out string error))
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        Result = new ImageSizeSettings(w, h, Editor.Mode);
        DialogResult = true;
    }
}
