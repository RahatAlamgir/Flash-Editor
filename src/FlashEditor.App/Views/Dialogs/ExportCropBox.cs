using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>
/// The crop box on the Export preview: drag on the picture to draw it, drag inside it to move it,
/// drag a corner to resize it. An aspect ratio (square, 16:9...) keeps its shape.
/// Works in image pixels; the preview is a scaled-down copy.
/// </summary>
internal sealed class ExportCropBox
{
    private const double PreviewSize = 378;
    private const double CornerGrab = 10; // screen pixels

    private enum Drag { None, New, Move, Corner }

    private readonly ExportDialog _view;
    private readonly int _imageWidth, _imageHeight;
    private readonly double _scale;
    private double _ratio;
    private Drag _drag;
    private Point _dragStart;
    private PixelRect _dragStartRect;
    private int _cornerX, _cornerY; // fixed corner while resizing (image pixels)

    public ExportCropBox(ExportDialog view, int imageWidth, int imageHeight)
    {
        _view = view;
        _imageWidth = imageWidth;
        _imageHeight = imageHeight;
        _scale = Math.Min(PreviewSize / imageWidth, PreviewSize / imageHeight);
        _view.CropStageElement.Width = imageWidth * _scale;
        _view.CropStageElement.Height = imageHeight * _scale;
        Rect = Full;
        Draw();
    }

    public event EventHandler? Changed;

    /// <summary>Area to keep, in image pixels.</summary>
    public PixelRect Rect { get; private set; }

    public bool IsCropped => Rect != Full;

    private PixelRect Full => new(0, 0, _imageWidth, _imageHeight);

    public void Reset() => SetRect(Full);

    /// <summary>Width : height to keep (0 = free). Fits the largest box of that shape, centered.</summary>
    public void SetAspect(double ratio)
    {
        _ratio = ratio;
        if (ratio <= 0) return;
        int w = _imageWidth, h = _imageHeight;
        if (w / (double)h > ratio) w = (int)Math.Round(h * ratio); else h = (int)Math.Round(w / ratio);
        SetRect(new PixelRect((_imageWidth - w) / 2, (_imageHeight - h) / 2, Math.Max(1, w), Math.Max(1, h)));
    }

    public void MouseDown(MouseButtonEventArgs e)
    {
        var p = e.GetPosition(_view.CropCanvasElement);
        _dragStart = p;
        _dragStartRect = Rect;

        var corner = NearCorner(p);
        if (corner != null)
        {
            _drag = Drag.Corner;
            (_cornerX, _cornerY) = corner.Value;
        }
        else if (Contains(Rect, ToImage(p)) && IsCropped)
        {
            _drag = Drag.Move;
        }
        else
        {
            _drag = Drag.New;
            var start = ToImage(p);
            (_cornerX, _cornerY) = ((int)start.X, (int)start.Y);
        }
        _view.CropCanvasElement.CaptureMouse();
    }

    public void MouseMove(MouseEventArgs e)
    {
        var p = e.GetPosition(_view.CropCanvasElement);
        if (_drag == Drag.None)
        {
            _view.CropCanvasElement.Cursor = NearCorner(p) != null ? Cursors.SizeNWSE
                : Contains(Rect, ToImage(p)) && IsCropped ? Cursors.SizeAll : Cursors.Cross;
            return;
        }

        var img = ToImage(p);
        if (_drag == Drag.Move)
        {
            int dx = (int)Math.Round((p.X - _dragStart.X) / _scale), dy = (int)Math.Round((p.Y - _dragStart.Y) / _scale);
            int x = Math.Clamp(_dragStartRect.X + dx, 0, _imageWidth - _dragStartRect.Width);
            int y = Math.Clamp(_dragStartRect.Y + dy, 0, _imageHeight - _dragStartRect.Height);
            SetRect(_dragStartRect with { X = x, Y = y });
        }
        else
        {
            SetRect(FromCorner(_cornerX, _cornerY, (int)Math.Round(img.X), (int)Math.Round(img.Y)));
        }
    }

    public void MouseUp(MouseButtonEventArgs e)
    {
        _drag = Drag.None;
        _view.CropCanvasElement.ReleaseMouseCapture();
        if (Rect.Width < 2 || Rect.Height < 2) Reset(); // a click without dragging: no crop
    }

    /// <summary>Box from a fixed corner to the mouse, kept inside the image and in the chosen shape.</summary>
    private PixelRect FromCorner(int fx, int fy, int mx, int my)
    {
        mx = Math.Clamp(mx, 0, _imageWidth);
        my = Math.Clamp(my, 0, _imageHeight);
        int w = Math.Abs(mx - fx), h = Math.Abs(my - fy);
        if (_ratio > 0)
        {
            if (w / Math.Max(1.0, h) > _ratio) w = (int)Math.Round(h * _ratio); else h = (int)Math.Round(w / _ratio);
        }
        int x = mx >= fx ? fx : fx - w;
        int y = my >= fy ? fy : fy - h;
        return new PixelRect(x, y, Math.Max(1, w), Math.Max(1, h)).Intersect(Full);
    }

    private (int, int)? NearCorner(Point p)
    {
        if (!IsCropped) return null;
        var r = Rect;
        var corners = new[] { (r.X, r.Y, r.Right, r.Bottom), (r.Right, r.Y, r.X, r.Bottom), (r.Right, r.Bottom, r.X, r.Y), (r.X, r.Bottom, r.Right, r.Y) };
        foreach (var (cx, cy, fx, fy) in corners)
            if (Math.Abs(cx * _scale - p.X) <= CornerGrab && Math.Abs(cy * _scale - p.Y) <= CornerGrab) return (fx, fy);
        return null;
    }

    private Point ToImage(Point preview) => new(preview.X / _scale, preview.Y / _scale);

    private static bool Contains(PixelRect r, Point p) => p.X >= r.X && p.Y >= r.Y && p.X <= r.Right && p.Y <= r.Bottom;

    private void SetRect(PixelRect rect)
    {
        if (rect.IsEmpty || rect == Rect) return;
        Rect = rect;
        Draw();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Draw()
    {
        double x = Rect.X * _scale, y = Rect.Y * _scale, w = Rect.Width * _scale, h = Rect.Height * _scale;
        var shade = new GeometryGroup { FillRule = FillRule.EvenOdd };
        shade.Children.Add(new RectangleGeometry(new Rect(0, 0, _imageWidth * _scale, _imageHeight * _scale)));
        shade.Children.Add(new RectangleGeometry(new Rect(x, y, w, h)));
        _view.CropShadeElement.Data = IsCropped ? shade : null;

        var box = _view.CropBoxElement;
        box.Visibility = IsCropped ? Visibility.Visible : Visibility.Collapsed;
        Canvas.SetLeft(box, x);
        Canvas.SetTop(box, y);
        box.Width = w;
        box.Height = h;

        var thumbs = new GeometryGroup();
        if (IsCropped)
            foreach (var (cx, cy) in new[] { (x, y), (x + w, y), (x + w, y + h), (x, y + h) })
                thumbs.Children.Add(new RectangleGeometry(new Rect(cx - 4, cy - 4, 8, 8)));
        _view.CropThumbsElement.Data = thumbs;
    }
}
