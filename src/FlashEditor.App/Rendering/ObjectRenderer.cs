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
        RenderInto(canvas, obj);
        return canvas;
    }

    /// <summary>
    /// The part of the object (box, border, shadow) that is drawn, in canvas pixels. A shadow can reach onto the canvas
    /// from an object that is partly outside it, so such an object is drawn a little beyond the canvas edge.
    /// </summary>
    private static PixelRect DrawArea(LayerObject obj, int canvasWidth, int canvasHeight, out int reach)
    {
        var shadow = obj.Shadow;
        reach = shadow == null ? 0 : (int)Math.Ceiling(shadow.Extent);
        var canvasBounds = new PixelRect(0, 0, canvasWidth, canvasHeight);
        return obj.Box.Bounds(MarginFor(obj) + reach).Intersect(canvasBounds.Inflate(reach));
    }

    public PixelRect DrawingBounds(LayerObject obj)
    {
        int reach = obj.Shadow == null ? 0 : (int)Math.Ceiling(obj.Shadow.Extent);
        return obj.Box.Bounds(MarginFor(obj) + reach);
    }

    public PixelRect PaintedArea(LayerObject obj, int canvasWidth, int canvasHeight)
        => DrawArea(obj, canvasWidth, canvasHeight, out _).Intersect(new PixelRect(0, 0, canvasWidth, canvasHeight));

    public void RenderInto(PixelBuffer canvas, LayerObject obj)
    {
        var shadow = obj.Shadow;
        var area = DrawArea(obj, canvas.Width, canvas.Height, out _);
        if (area.IsEmpty) return;

        var matrix = LocalToCanvas(obj);
        var pixels = WpfRasterizer.Render(area, dc =>
        {
            dc.PushTransform(new MatrixTransform(matrix));
            Draw(dc, obj, matrix, shadowScale: 1); // the object's own shadow is added below, from its pixels
            dc.Pop();
        }, highQualityImages: obj is ImageObject or GroupObject);

        if (pixels != null && shadow != null)
            pixels = ShadowFx.Apply(pixels, shadow.Color, shadow.OffsetX, shadow.OffsetY, shadow.Blur);
        if (pixels != null) canvas.CopyFrom(pixels, area.X, area.Y);
    }

    public (double Width, double Height) MeasureText(TextObject text)
    {
        var formatted = CreateText(text);
        return (Math.Ceiling(formatted.WidthIncludingTrailingWhitespace) + 2, Math.Ceiling(formatted.Height) + 2);
    }

    /// <summary>
    /// Extra room around the box for borders that stick out (also inside groups). The object's own shadow is not
    /// counted (it is added afterwards); the shadows of the parts of a group are.
    /// </summary>
    private static double MarginFor(LayerObject obj) => obj switch
    {
        ShapeObject { Stroke: not null } s => s.StrokeWidth / 2 + 2,
        GroupObject g => 2 + g.Members.Max(m => MarginFor(m.Object) + (m.Object.Shadow?.Extent ?? 0)) * Math.Max(g.Scale.Sx, g.Scale.Sy),
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

    /// <param name="toCanvas">Maps the object's own space (0,0 = its top-left) to the canvas, including every group around it.</param>
    /// <param name="shadowScale">How much the groups around the object were stretched (shadows grow with them).</param>
    private static void Draw(DrawingContext dc, LayerObject obj, Matrix toCanvas, double shadowScale)
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
                double childShadowScale = shadowScale * Math.Sqrt(sx * sy);
                dc.PushTransform(new MatrixTransform(toBox));
                foreach (var member in group.Members)
                {
                    var local = LocalToCanvas(member.Object);
                    var memberToCanvas = local * toBox * toCanvas;
                    dc.PushOpacity(member.Opacity);
                    dc.PushTransform(new MatrixTransform(local));
                    if (member.Object.Shadow != null) DrawWithShadow(dc, member.Object, memberToCanvas, childShadowScale);
                    else Draw(dc, member.Object, memberToCanvas, childShadowScale);
                    dc.Pop();
                    dc.Pop();
                }
                dc.Pop();
                break;
            }
        }
    }

    /// <summary>
    /// Draws a part of a group together with its own shadow. The part is drawn alone on a small bitmap (in its own
    /// space, so rotation and group stretching do not matter), the shadow is made from that, and the result is
    /// drawn into the group. The shadow keeps pointing the same way on the canvas, however the part is turned.
    /// </summary>
    private static void DrawWithShadow(DrawingContext dc, LayerObject obj, Matrix toCanvas, double shadowScale)
    {
        var shadow = obj.Shadow!.Scaled(shadowScale);
        var plain = obj with { Shadow = null };

        var linear = new Matrix(toCanvas.M11, toCanvas.M12, toCanvas.M21, toCanvas.M22, 0, 0);
        double determinant = linear.Determinant;
        if (Math.Abs(determinant) < 1e-9)
        {
            Draw(dc, plain, toCanvas, shadowScale);
            return;
        }

        // Pixels of the bitmap per unit of the part's own space = how big the part is on the canvas.
        double box = Math.Max(1, Math.Max(obj.Box.Width, obj.Box.Height));
        double res = Math.Clamp(Math.Sqrt(Math.Abs(determinant)), 0.05, 16);
        res = Math.Min(res, 3000 / box); // keep the bitmap a sane size
        int pad = (int)Math.Ceiling(MarginFor(plain) * res + shadow.Extent);
        int width = (int)Math.Ceiling(obj.Box.Width * res) + 2 * pad;
        int height = (int)Math.Ceiling(obj.Box.Height * res) + 2 * pad;

        var drawn = WpfRasterizer.Render(new PixelRect(0, 0, width, height), c =>
        {
            c.PushTransform(new MatrixTransform(new Matrix(res, 0, 0, res, pad, pad)));
            Draw(c, plain, toCanvas, shadowScale);
            c.Pop();
        }, highQualityImages: true);
        if (drawn == null) return;

        // The shadow's direction is given on the canvas; the bitmap is in the part's own (turned) space.
        linear.Invert();
        var shift = linear.Transform(new Vector(shadow.OffsetX, shadow.OffsetY)) * res;
        var shadowed = ShadowFx.Apply(drawn, shadow.Color, shift.X, shift.Y, shadow.Blur * res / Math.Sqrt(Math.Abs(determinant)));

        dc.DrawImage(BitmapConverter.ToFrozenBitmap(shadowed), new Rect(-pad / res, -pad / res, width / res, height / res));
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

    /// <summary>
    /// Forgets the screen copy kept for a picture. Pictures normally never change, but the Refine brush edits the pixels of the
    /// picture it is painting on, so the copy must be made again.
    /// </summary>
    public static void InvalidateBitmap(PixelBuffer source) => BitmapCache.Remove(source);

    private static BitmapSource ToBitmap(PixelBuffer source)
        => BitmapCache.GetValue(source, BitmapConverter.ToFrozenBitmap);

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
