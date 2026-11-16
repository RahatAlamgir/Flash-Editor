using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>
/// Choose format, quality and output size before saving a copy of the image.
/// The result is encoded in the background (debounced) to show the real file size and a preview.
/// </summary>
public partial class ExportDialog : Window
{
    private static readonly ImageFileFormat[] Formats =
        { ImageFileFormat.Png, ImageFileFormat.Jpeg, ImageFileFormat.Webp, ImageFileFormat.Ico };

    private const int PreviewSize = 380;

    private readonly PixelBuffer _image;
    private readonly ColorRgba _matte;
    private readonly DispatcherTimer _debounce;
    private int _version;
    private PixelBuffer? _lastDecoded;
    private bool _ready;
    private readonly ExportCropBox _crop;

    public ExportDialog(PixelBuffer flattenedImage, ImageFileFormat format, int quality, ColorRgba matte)
    {
        InitializeComponent();
        _image = flattenedImage;
        _matte = matte;
        PreviewFrame.Background = CheckerboardBrush.Create(8);

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = RefreshAsync(); };

        SizeBox.Initialize(_image.Width, _image.Height);
        _crop = new ExportCropBox(this, _image.Width, _image.Height);
        _crop.Changed += (_, _) =>
        {
            SizeBox.SetSourceSize(_crop.Rect.Width, _crop.Rect.Height);
            ScheduleRefresh();
        };
        SourceImage.Source = BitmapConverter.ToFrozenBitmap(_image);
        AspectBox.SelectedIndex = 0;
        SizeBox.Changed += (_, _) => ScheduleRefresh();
        QualitySlider.Value = Math.Clamp(quality, 1, 100);
        FormatBox.SelectedIndex = Math.Max(0, Array.IndexOf(Formats, format));

        _ready = true;
        UpdateFormatUi();
        ScheduleRefresh();
        Closed += (_, _) => { _version++; _debounce.Stop(); };
    }

    public ExportSettings? Result { get; private set; }

    private ImageFileFormat Format => Formats[Math.Max(0, FormatBox.SelectedIndex)];

    private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateFormatUi();
        ScheduleRefresh();
    }

    private void OnQualityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityText != null) QualityText.Text = $"{(int)QualitySlider.Value}%";
        if (_ready) ScheduleRefresh();
    }

    private void OnZoomPreviewChanged(object sender, RoutedEventArgs e) => ShowPreview();

    private void UpdateFormatUi()
    {
        bool hasQuality = ImageFileFormats.HasQuality(Format);
        QualityPanel.Visibility = hasQuality ? Visibility.Visible : Visibility.Collapsed;
        NoQualityText.Visibility = hasQuality ? Visibility.Collapsed : Visibility.Visible;
        NoQualityText.Text = Format == ImageFileFormat.Ico
            ? "Icons always contain the sizes 16 to 256 px; the size below is the source the icon sizes are made from."
            : "PNG is lossless (no quality setting). To make the file smaller, reduce the size below or choose JPG / WebP.";
        SizeBox.IsEnabled = true;
    }

    private void ScheduleRefresh()
    {
        if (!_ready) return;
        _version++;
        PreviewBusy.Visibility = Visibility.Visible;
        _debounce.Stop();
        _debounce.Start();
    }

    private ExportSettings? CurrentSettings()
    {
        if (!SizeBox.TryGetSize(out int w, out int h, out _)) return null;
        return new ExportSettings(Format, (int)QualitySlider.Value, w, h, SizeBox.Mode, _matte,
            _crop.IsCropped ? _crop.Rect : null);
    }

    /// <summary>Encodes with the current settings in the background, then shows file size and preview.</summary>
    private async Task RefreshAsync()
    {
        var settings = CurrentSettings();
        if (settings == null)
        {
            ResultSizeText.Text = "Enter a valid size";
            ResultInfoText.Text = string.Empty;
            PreviewBusy.Visibility = Visibility.Collapsed;
            return;
        }

        int version = _version;
        try
        {
            var (bytes, decoded) = await Task.Run(() =>
            {
                var data = ImageExporter.Encode(_image, settings);
                // Decode what was written so the preview shows real compression artifacts.
                PixelBuffer shown;
                if (settings.Format == ImageFileFormat.Ico)
                    shown = ImageExporter.Resize(_image, settings);
                else
                    using (var stream = new MemoryStream(data)) shown = ImageDecoder.Load(stream);
                return (data.LongLength, shown);
            });
            if (version != _version) return; // settings changed meanwhile

            _lastDecoded = decoded;
            long rawBytes = (long)settings.Width * settings.Height * 4;
            ResultSizeText.Text = $"Estimated file size: {ImageExporter.FormatFileSize(bytes)}";
            ResultInfoText.Text = $"{settings.Width} x {settings.Height} px, {settings.Format}"
                + (ImageFileFormats.HasQuality(settings.Format) ? $" at {settings.Quality}% quality" : "")
                + $"  ({bytes * 100.0 / Math.Max(1, rawBytes):0.#}% of uncompressed)";
            ShowPreview();
        }
        catch (Exception ex)
        {
            if (version != _version) return;
            ResultSizeText.Text = "Could not encode with these settings";
            ResultInfoText.Text = ex.Message;
        }
        finally
        {
            if (version == _version) PreviewBusy.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowPreview()
    {
        bool showResult = ShowResultBox.IsChecked == true;
        CropStage.Visibility = showResult ? Visibility.Collapsed : Visibility.Visible;
        PreviewImage.Visibility = showResult ? Visibility.Visible : Visibility.Collapsed;
        PreviewTitle.Text = showResult ? "The saved result" : "Drag on the picture to crop (optional)";
        if (!showResult || _lastDecoded is not { } image) return;
        if (ZoomPreviewBox.IsChecked == true && (image.Width > PreviewSize || image.Height > PreviewSize))
        {
            // 1:1 crop from the center so compression artifacts are visible.
            int w = Math.Min(PreviewSize, image.Width), h = Math.Min(PreviewSize, image.Height);
            image = image.Crop(new PixelRect((image.Width - w) / 2, (image.Height - h) / 2, w, h));
            PreviewImage.Stretch = System.Windows.Media.Stretch.None;
        }
        else
        {
            PreviewImage.Stretch = System.Windows.Media.Stretch.Uniform;
        }
        PreviewImage.Source = BitmapConverter.ToFrozenBitmap(image);
    }

    // ------------------------------------------------------------------ crop box (logic in ExportCropBox)

    internal Grid CropStageElement => CropStage;
    internal Canvas CropCanvasElement => CropCanvas;
    internal System.Windows.Shapes.Path CropShadeElement => CropShade;
    internal System.Windows.Shapes.Rectangle CropBoxElement => CropBox;
    internal System.Windows.Shapes.Path CropThumbsElement => CropThumbs;

    private void OnCropMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) => _crop.MouseDown(e);
    private void OnCropMouseMove(object sender, System.Windows.Input.MouseEventArgs e) => _crop.MouseMove(e);
    private void OnCropMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e) => _crop.MouseUp(e);
    private void OnResetCrop(object sender, RoutedEventArgs e) { AspectBox.SelectedIndex = 0; _crop.Reset(); }
    private void OnAspectChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_crop != null && AspectBox.SelectedItem is Tools.Transform.CropAspect aspect) _crop.SetAspect(aspect.Ratio);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!SizeBox.TryGetSize(out _, out _, out string error))
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        Result = CurrentSettings();
        DialogResult = true;
    }
}
