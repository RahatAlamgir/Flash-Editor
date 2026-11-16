using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Rendering;

/// <summary>
/// Draws editable objects (shapes, text, placed images) with WPF's anti-aliased renderer.
/// Registered at startup as <see cref="ObjectRendering.Renderer"/>.
/// </summary>
public sealed class ObjectRenderer : IObjectRenderer
{
    // Converted images are cached per source buffer (sources never change; objects are immutable).
    private static readonly ConditionalWeakTable<PixelBuffer, BitmapSource> BitmapCache = new();

    public PixelBuffer Render(LayerObject obj, int canvasWidth, int canvasHeight)
    {
        var canvas = new PixelBuffer(canvasWidth, canvasHeight);
        var area = obj.Box.Bounds(MarginFor(obj)).Intersect(canvas.Bounds);
        if (area.IsEmpty) return canvas;

        var pixels = WpfRasterizer.Render(area, dc =>
        {
            dc.PushTransform(new MatrixTransform(LocalToCanvas(obj)));
            Draw(dc, obj);
            dc.Pop();
        }, highQualityImages: obj is ImageObject or GroupObject);

        if (pixels != null) canvas.CopyFrom(pixels, area.X, area.Y);
        return canvas;
    }

    public (double Width, double Height) MeasureText(TextObject text)
    {
        var formatted = CreateText(text);
        return (Math.Ceiling(formatted.WidthIncludingTrailingWhitespace) + 2, Math.Ceiling(formatted.Height) + 2);
    }

    /// <summary>Extra room around the box for borders that stick out (also inside groups).</summary>
    private static double MarginFor(LayerObject obj) => obj switch
    {
        ShapeObject { Stroke: not null } s => s.StrokeWidth / 2 + 2,
        GroupObject g => 2 + g.Members.Max(m => MarginFor(m.Object)) * Math.Max(g.Scale.Sx, g.Scale.Sy),
        _ => 2,
    };

    /// <summary>Maps box space (0,0 = top-left of the unrotated box) to canvas space: flip, rotate, position.</summary>
    public static Matrix LocalToCanvas(LayerObject obj)
    {
        var b = obj.Box;
        var m = Matrix.Identity;
        m.Translate(-b.Width / 2, -b.Height / 2);
        m.Scale(obj.FlipX ? -1 : 1, obj.FlipY ? -1 : 1);
        m.Rotate(b.Rotation);
        m.Translate(b.Center.X, b.Center.Y);
        return m;
    }

    private static void Draw(DrawingContext dc, LayerObject obj)
    {
        var b = obj.Box;
        switch (obj)
        {
            case ShapeObject shape:
            {
                var geometry = ShapeGeometryFactory.Build(shape, b.Width, b.Height);
                Brush? fill = shape.Fill is { } f && shape.CanFill ? Frozen(new SolidColorBrush(BitmapConverter.ToWpf(f))) : null;
                Pen? pen = null;
                var strokeColor = shape.Stroke ?? (shape.IsLineLike ? shape.Fill : null);
                if (strokeColor is { } sc && shape.StrokeWidth > 0)
                {
                    pen = new Pen(Frozen(new SolidColorBrush(BitmapConverter.ToWpf(sc))), shape.StrokeWidth)
                    {
                        LineJoin = PenLineJoin.Round,
                        StartLineCap = shape.IsLineLike ? PenLineCap.Round : PenLineCap.Flat,
                        EndLineCap = shape.IsLineLike ? PenLineCap.Round : PenLineCap.Flat,
                    };
                    pen.Freeze();
                }
                dc.DrawGeometry(fill, pen, geometry);
                break;
            }
            case TextObject text:
            {
                var formatted = CreateText(text);
                var (naturalW, naturalH) = (Math.Ceiling(formatted.WidthIncludingTrailingWhitespace) + 2, Math.Ceiling(formatted.Height) + 2);
                // The box may have been resized: stretch the text to fill it.
                dc.PushTransform(new ScaleTransform(b.Width / naturalW, b.Height / naturalH));
                double originX = text.Align switch
                {
                    TextAlign.Center => naturalW / 2,
                    TextAlign.Right => naturalW - 1,
                    _ => 1,
                };
                dc.DrawText(formatted, new Point(originX, 1));
                dc.Pop();
                break;
            }
            case ImageObject image:
                dc.DrawImage(ToBitmap(image.Source), new Rect(0, 0, b.Width, b.Height));
                break;
            case GroupObject group:
            {
                // Members are stored in the canvas positions they had when grouped:
                // map that area onto the group's current box, then draw each member as usual.
                var (sx, sy) = group.Scale;
                var toBox = Matrix.Identity;
                toBox.Translate(-group.OriginalBox.X, -group.OriginalBox.Y);
                toBox.Scale(sx, sy);
                dc.PushTransform(new MatrixTransform(toBox));
                foreach (var member in group.Members)
                {
                    dc.PushOpacity(member.Opacity);
                    dc.PushTransform(new MatrixTransform(LocalToCanvas(member.Object)));
                    Draw(dc, member.Object);
                    dc.Pop();
                    dc.Pop();
                }
                dc.Pop();
                break;
            }
        }
    }

    private static FormattedText CreateText(TextObject text)
    {
        var typeface = new Typeface(new FontFamily(text.FontFamily),
            text.Italic ? FontStyles.Italic : FontStyles.Normal,
            text.Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStretches.Normal);
        var brush = Frozen(new SolidColorBrush(BitmapConverter.ToWpf(text.Color)));
        var formatted = new FormattedText(string.IsNullOrEmpty(text.Text) ? " " : text.Text, CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight, typeface, Math.Max(1, text.FontSize), brush, pixelsPerDip: 1.0)
        {
            TextAlignment = text.Align switch
            {
                TextAlign.Center => TextAlignment.Center,
                TextAlign.Right => TextAlignment.Right,
                _ => TextAlignment.Left,
            },
        };
        return formatted;
    }

    private static BitmapSource ToBitmap(PixelBuffer source)
        => BitmapCache.GetValue(source, BitmapConverter.ToFrozenBitmap);

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
