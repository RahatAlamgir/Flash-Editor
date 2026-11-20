using System.Windows;
using System.Windows.Data;
using FlashEditor.App.Rendering;
using FlashEditor.App.Services;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;
using FlashEditor.Core.Transforms;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>File > Export For: pick a ready-made size and how the picture fits it, see a preview, and export (this page or all pages).</summary>
public partial class ExportForDialog : Window
{
    private readonly PixelBuffer _small;       // a small copy of the picture for the live preview
    private readonly double _smallScale;
    private bool _ready;

    public ExportForDialog(PixelBuffer picture, int pageCount, ImageFileFormat format, int quality)
    {
        InitializeComponent();

        var view = new ListCollectionView(ExportPresets.All.ToList());
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ExportPreset.Group)));
        PresetBox.ItemsSource = view;
        PresetBox.SelectedItem = ExportPresets.All.First(p => p.Name == "Instagram post (square)");

        FormatBox.ItemsSource = new[]
        {
            new FormatItem(ImageFileFormat.Png, "PNG (sharp, can be see-through)"),
            new FormatItem(ImageFileFormat.Jpeg, "JPG (small photos)"),
            new FormatItem(ImageFileFormat.Webp, "WebP (small, modern)"),
        };
        FormatBox.DisplayMemberPath = nameof(FormatItem.Text);
        FormatBox.SelectedIndex = format == ImageFileFormat.Jpeg ? 1 : format == ImageFileFormat.Webp ? 2 : 0;
        QualitySlider.Value = Math.Clamp(quality, 10, 100);
        AllPagesBox.Visibility = pageCount > 1 ? Visibility.Visible : Visibility.Collapsed;

        _smallScale = Math.Min(1.0, 400.0 / Math.Max(picture.Width, picture.Height));
        _small = _smallScale < 1
            ? Resampler.Resize(picture, Math.Max(1, (int)(picture.Width * _smallScale)), Math.Max(1, (int)(picture.Height * _smallScale)), ResampleMode.Smooth)
            : picture;
        _ready = true;
        Update();
    }

    public ExportForResult? Result { get; private set; }

    private ExportPreset Preset => (ExportPreset)PresetBox.SelectedItem;

    private ExportFit Fit => FillOption.IsChecked == true ? ExportFit.Fill : StretchOption.IsChecked == true ? ExportFit.Stretch : ExportFit.Fit;

    private sealed record FormatItem(ImageFileFormat Format, string Text);

    private ImageFileFormat Format => ((FormatItem)FormatBox.SelectedItem).Format;

    private bool Transparent => TransparentOption.IsChecked == true && Format != ImageFileFormat.Jpeg;

    private void OnChanged(object sender, RoutedEventArgs e) => Update();

    private void Update()
    {
        if (!_ready || PresetBox.SelectedItem == null || FormatBox.SelectedItem == null) return;
        QualityText.Text = ((int)QualitySlider.Value).ToString();
        bool lossy = Format is ImageFileFormat.Jpeg or ImageFileFormat.Webp;
        QualitySlider.IsEnabled = lossy;
        TransparentOption.IsEnabled = Format != ImageFileFormat.Jpeg;
        if (Format == ImageFileFormat.Jpeg && TransparentOption.IsChecked == true) WhiteOption.IsChecked = true;

        var preset = Preset;
        SizeText.Text = $"{preset.Width} x {preset.Height} px";

        // The preview: the same fitting, made on a small picture and a small preset of the same shape.
        double previewScale = 240.0 / Math.Max(preset.Width, preset.Height);
        var small = new ExportPreset(preset.Group, preset.Name, Math.Max(1, (int)(preset.Width * previewScale)), Math.Max(1, (int)(preset.Height * previewScale)));
        PreviewImage.Source = BitmapConverter.ToFrozenBitmap(ExportPresets.Compose(_small, small, Fit, Transparent ? null : ColorRgba.White));
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Result = new ExportForResult(Preset, Fit, Format, (int)QualitySlider.Value, Transparent, AllPagesBox.Visibility == Visibility.Visible && AllPagesBox.IsChecked == true);
        DialogResult = true;
    }
}
