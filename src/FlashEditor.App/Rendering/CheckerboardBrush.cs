using System.Windows;
using System.Windows.Media;

namespace FlashEditor.App.Rendering;

/// <summary>The gray checkerboard shown behind transparent pixels.</summary>
public static class CheckerboardBrush
{
    /// <param name="cellSize">Size of one square in the brush's coordinate space.</param>
    public static DrawingBrush Create(double cellSize)
    {
        var light = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
        var dark = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(light, null, new RectangleGeometry(new Rect(0, 0, 2, 2))));
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
        group.Children.Add(new GeometryDrawing(dark, null, new RectangleGeometry(new Rect(1, 1, 1, 1))));

        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, cellSize * 2, cellSize * 2),
            Stretch = Stretch.Fill,
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetCachingHint(brush, CachingHint.Cache);
        return brush;
    }
}
