using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Rendering;

/// <summary>
/// Uses WPF's own anti-aliased vector renderer to turn shapes, text and images into pixels.
/// Everything is drawn in image coordinates; only <paramref name="bounds"/> is rendered.
/// </summary>
public static class WpfRasterizer
{
    /// <param name="highQualityImages">Use high-quality scaling for images drawn with DrawImage.</param>
    public static PixelBuffer? Render(PixelRect bounds, Action<DrawingContext> draw, bool highQualityImages = false)
    {
        if (bounds.IsEmpty) return null;

        var visual = new DrawingVisual();
        if (highQualityImages) RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
            draw(dc);
            dc.Pop();
        }

        var target = new RenderTargetBitmap(bounds.Width, bounds.Height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var data = new byte[bounds.Width * bounds.Height * 4];
        target.CopyPixels(data, bounds.Width * 4, 0);
        return BitmapConverter.FromPremultiplied(data, bounds.Width, bounds.Height);
    }
}
