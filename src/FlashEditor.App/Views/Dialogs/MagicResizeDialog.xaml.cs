using System.Windows;
using System.Windows.Controls;
using FlashEditor.Core.IO;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>What Magic Resize should do.</summary>
public sealed record MagicResizeSettings(int Width, int Height, bool AllPages);

/// <summary>Image > Magic Resize: pick the new size (a ready-made one or your own).</summary>
public partial class MagicResizeDialog : Window
{
    public MagicResizeDialog(int currentWidth, int currentHeight, int pageCount)
    {
        InitializeComponent();
        CurrentText.Text = $"This page is {currentWidth} x {currentHeight} px.";
        PresetBox.ItemsSource = NewImageDialog.Presets.Select(p => p.Name).ToList();
        PresetBox.SelectedIndex = 0;
        WidthBox.Text = currentWidth.ToString();
        HeightBox.Text = currentHeight.ToString();
        AllPagesBox.Visibility = pageCount > 1 ? Visibility.Visible : Visibility.Collapsed;
        AllPagesBox.Content = $"Resize every page of this project ({pageCount} pages)";
        Loaded += (_, _) => PresetBox.Focus();
    }

    public MagicResizeSettings? Result { get; private set; }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        var preset = NewImageDialog.Presets[Math.Max(0, PresetBox.SelectedIndex)];
        if (preset.W == 0) return;
        WidthBox.Text = preset.W.ToString();
        HeightBox.Text = preset.H.ToString();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WidthBox.Text.Trim(), out int w) || !int.TryParse(HeightBox.Text.Trim(), out int h)
            || w < 1 || h < 1 || w > ImageDecoder.MaxDimension || h > ImageDecoder.MaxDimension)
        {
            ErrorText.Text = $"Enter a width and height between 1 and {ImageDecoder.MaxDimension}.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        Result = new MagicResizeSettings(w, h, AllPagesBox.IsChecked == true);
        DialogResult = true;
    }
}
