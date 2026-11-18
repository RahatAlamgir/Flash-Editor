using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FlashEditor.App.Views.Controls;
using FlashEditor.Core.Layout;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>What the Photo Grid dialog gave back.</summary>
public sealed record PhotoGridSettings(CollageLayout Layout, double Gap, double Padding, double CornerRadius);

/// <summary>Insert > Photo Grid: pick a collage layout; it is added as empty frames to fill with pictures.</summary>
public partial class PhotoGridDialog : Window
{
    private readonly double _canvasWidth, _canvasHeight;

    public PhotoGridDialog(double canvasWidth, double canvasHeight)
    {
        InitializeComponent();
        _canvasWidth = canvasWidth;
        _canvasHeight = canvasHeight;
        LayoutBox.ItemsSource = CollageLayout.All.Select(l => l.Name).ToList();
        LayoutBox.SelectedIndex = 4;
        foreach (var slider in new[] { GapSlider, PaddingSlider, CornerSlider })
            DependencyPropertyDescriptor.FromProperty(NumberSlider.ValueProperty, typeof(NumberSlider)).AddValueChanged(slider, (_, _) => DrawPreview());
        Loaded += (_, _) => DrawPreview();
    }

    public PhotoGridSettings? Result { get; private set; }

    private CollageLayout Layout => CollageLayout.All[Math.Max(0, LayoutBox.SelectedIndex)];

    private void OnChanged(object sender, SelectionChangedEventArgs e) => DrawPreview();

    // A small picture of the frames on a canvas of the same shape.
    private void DrawPreview()
    {
        if (Preview == null) return;
        Preview.Children.Clear();
        double scale = Math.Min(Preview.Width / _canvasWidth, Preview.Height / _canvasHeight);
        double ox = (Preview.Width - _canvasWidth * scale) / 2, oy = (Preview.Height - _canvasHeight * scale) / 2;
        var paper = new Rectangle { Width = _canvasWidth * scale, Height = _canvasHeight * scale, Fill = Brushes.White };
        Canvas.SetLeft(paper, ox); Canvas.SetTop(paper, oy);
        Preview.Children.Add(paper);
        foreach (var (x, y, w, h) in Layout.Place(_canvasWidth, _canvasHeight, PaddingSlider.Value, GapSlider.Value))
        {
            var frame = new Rectangle
            {
                Width = Math.Max(1, w * scale), Height = Math.Max(1, h * scale),
                RadiusX = CornerSlider.Value * scale, RadiusY = CornerSlider.Value * scale,
                Fill = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
            };
            Canvas.SetLeft(frame, ox + x * scale); Canvas.SetTop(frame, oy + y * scale);
            Preview.Children.Add(frame);
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        Result = new PhotoGridSettings(Layout, GapSlider.Value, PaddingSlider.Value, CornerSlider.Value);
        DialogResult = true;
    }
}
