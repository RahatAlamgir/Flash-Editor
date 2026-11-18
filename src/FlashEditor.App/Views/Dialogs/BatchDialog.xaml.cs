using System.IO;
using System.Windows;
using System.Windows.Controls;
using FlashEditor.Core.IO;
using Microsoft.Win32;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>File > Batch Convert: pick a folder of pictures, the new format and size, and where to save the results.</summary>
public partial class BatchDialog : Window
{
    private static readonly ImageFileFormat[] Formats =
    {
        ImageFileFormat.Png, ImageFileFormat.Jpeg, ImageFileFormat.Webp, ImageFileFormat.Bmp, ImageFileFormat.Tiff, ImageFileFormat.Gif,
    };

    public BatchDialog()
    {
        InitializeComponent();
        FormatBox.ItemsSource = new[] { "PNG", "JPG", "WEBP", "BMP", "TIFF", "GIF" };
        FormatBox.SelectedIndex = 1;
        Loaded += (_, _) => InputBox.Focus();
    }

    public BatchOptions? Result { get; private set; }

    private ImageFileFormat Format => Formats[Math.Max(0, FormatBox.SelectedIndex)];

    private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
        => QualityRow.Visibility = ImageFileFormats.HasQuality(Format) ? Visibility.Visible : Visibility.Collapsed;

    private static string? PickFolder(string title, string current)
    {
        var dialog = new OpenFolderDialog { Title = title };
        if (Directory.Exists(current)) dialog.InitialDirectory = current;
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    private void OnBrowseInput(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Folder with the pictures", InputBox.Text) is not { } folder) return;
        InputBox.Text = folder;
        if (string.IsNullOrWhiteSpace(OutputBox.Text)) OutputBox.Text = Path.Combine(folder, "converted");
    }

    private void OnBrowseOutput(object sender, RoutedEventArgs e)
    {
        if (PickFolder("Folder for the new pictures", OutputBox.Text) is { } folder) OutputBox.Text = folder;
    }

    private void OnInputChanged(object sender, RoutedEventArgs e)
    {
        if (CountText == null) return;
        string input = InputBox.Text.Trim();
        if (!Directory.Exists(input)) { CountText.Text = ""; return; }
        var options = new BatchOptions(input, string.IsNullOrWhiteSpace(OutputBox.Text) ? Path.Combine(input, "converted") : OutputBox.Text, Format,
            IncludeSubfolders: SubfoldersBox.IsChecked == true);
        int count = BatchConverter.FindPictures(options).Count;
        CountText.Text = count == 0 ? "No pictures found in this folder." : $"{count} picture{(count == 1 ? "" : "s")} found.";
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        string input = InputBox.Text.Trim(), output = OutputBox.Text.Trim();
        if (!Directory.Exists(input)) { Error("Choose the folder with the pictures."); return; }
        if (output.Length == 0) { Error("Choose where to save the new pictures."); return; }
        if (!int.TryParse(MaxWidthBox.Text.Trim(), out int w) || !int.TryParse(MaxHeightBox.Text.Trim(), out int h) || w < 0 || h < 0)
        {
            Error("The largest size must be a number (0 = keep the size).");
            return;
        }

        var options = new BatchOptions(input, output, Format, (int)Math.Round(QualitySlider.Value), w, h, SubfoldersBox.IsChecked == true);
        if (BatchConverter.FindPictures(options).Count == 0) { Error("There are no pictures to convert in that folder."); return; }
        Result = options;
        DialogResult = true;
    }

    private void Error(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
