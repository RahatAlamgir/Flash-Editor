using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlashEditor.App.Rendering;
using FlashEditor.App.Services;
using FlashEditor.App.Views.Controls;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Sheets;
using Microsoft.Win32;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>
/// Split an asset sheet: shows the pieces found (numbered boxes, updated as settings change) and asks
/// whether to add them as layers and / or save them as PNG files.
/// </summary>
public partial class SplitSheetDialog : Window
{
    private const double StageWidth = 558, StageHeight = 458;

    private readonly PixelBuffer _sheet;
    private readonly double _scale;
    private readonly DispatcherTimer _debounce;
    private IReadOnlyList<SheetPiece> _pieces = Array.Empty<SheetPiece>();
    private readonly byte[]? _selection;
    private int _version;

    /// <param name="selectionMask">The current selection: its separate areas become the pieces (null = find items automatically).</param>
    public SplitSheetDialog(PixelBuffer sheet, string baseName, string? folder, byte[]? selectionMask)
    {
        InitializeComponent();
        _sheet = sheet;
        _selection = selectionMask;
        IntroText.Text = selectionMask != null
            ? "Using your selection: every separate selected area becomes one piece. (Select with the Magic Wand, Quick Select, Lasso, Pen... Shift adds.)"
            : "Nothing is selected, so items are found automatically (transparent or plain background). For full control, select the items first with any selection tool, then split.";
        ToleranceSlider.IsEnabled = selectionMask == null;
        _scale = Math.Min(1, Math.Min(StageWidth / sheet.Width, StageHeight / sheet.Height));
        Stage.Width = sheet.Width * _scale;
        Stage.Height = sheet.Height * _scale;
        SheetImage.Source = BitmapConverter.ToFrozenBitmap(sheet);
        NameBox.Text = baseName;
        FolderBox.Text = folder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = RefreshAsync(); };
        foreach (var slider in new[] { GapSlider, MinSizeSlider, ToleranceSlider, PaddingSlider })
            DependencyPropertyDescriptor.FromProperty(NumberSlider.ValueProperty, typeof(NumberSlider))
                .AddValueChanged(slider, (_, _) => Schedule());
        Loaded += (_, _) => _ = RefreshAsync();
    }

    public SplitSheetResult? Result { get; private set; }

    private SplitSettings Settings => new(
        Gap: (int)GapSlider.Value,
        MinSize: (int)MinSizeSlider.Value,
        Tolerance: (int)ToleranceSlider.Value,
        RemoveBackground: RemoveBackgroundBox.IsChecked == true,
        Padding: (int)PaddingSlider.Value);

    private void OnSettingChanged(object sender, RoutedEventArgs e) => Schedule();

    private void Schedule()
    {
        if (!IsLoaded) return;
        _version++;
        CountText.Text = "Looking for pieces...";
        _debounce.Stop();
        _debounce.Start();
    }

    private async Task RefreshAsync()
    {
        int version = ++_version;
        var settings = Settings;
        var selection = _selection;
        var pieces = await Task.Run(() => SheetSplitter.Split(_sheet, settings, selection));
        if (version != _version) return; // settings changed meanwhile
        _pieces = pieces;
        CountText.Text = pieces.Count switch
        {
            0 => "No pieces found. Try a lower tolerance or smaller minimum size.",
            1 => "1 piece found",
            _ => $"{pieces.Count} pieces found",
        };
        DrawBoxes();
    }

    private void DrawBoxes()
    {
        BoxCanvas.Children.Clear();
        var stroke = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xFF));
        for (int i = 0; i < _pieces.Count; i++)
        {
            var b = _pieces[i].Bounds;
            var box = new Rectangle
            {
                Width = Math.Max(2, b.Width * _scale),
                Height = Math.Max(2, b.Height * _scale),
                Stroke = stroke,
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(Color.FromArgb(30, 0x4F, 0xC3, 0xFF)),
            };
            Canvas.SetLeft(box, b.X * _scale);
            Canvas.SetTop(box, b.Y * _scale);
            BoxCanvas.Children.Add(box);

            var label = new Border
            {
                Background = stroke,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 0, 4, 0),
                Child = new TextBlock { Text = (i + 1).ToString(), Foreground = Brushes.Black, FontSize = 11, FontWeight = FontWeights.SemiBold },
            };
            Canvas.SetLeft(label, b.X * _scale);
            Canvas.SetTop(label, b.Y * _scale);
            BoxCanvas.Children.Add(label);
        }
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Folder for the pieces", InitialDirectory = Directory.Exists(FolderBox.Text) ? FolderBox.Text : null };
        if (dialog.ShowDialog(this) == true) FolderBox.Text = dialog.FolderName;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        bool asLayers = AsLayersBox.IsChecked == true, saveFiles = SaveFilesBox.IsChecked == true;
        string? error = _pieces.Count == 0 ? "No pieces were found."
            : !asLayers && !saveFiles ? "Choose at least one: add as layers, or save as PNG files."
            : saveFiles && string.IsNullOrWhiteSpace(FolderBox.Text) ? "Choose a folder for the PNG files."
            : saveFiles && string.IsNullOrWhiteSpace(NameBox.Text) ? "Enter a file name."
            : saveFiles && NameBox.Text.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 ? "The file name contains characters Windows does not allow."
            : null;
        if (error != null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }
        Result = new SplitSheetResult(_pieces, asLayers, asLayers && HideOriginalBox.IsChecked == true,
            saveFiles ? FolderBox.Text.Trim() : null, NameBox.Text.Trim());
        DialogResult = true;
    }
}
