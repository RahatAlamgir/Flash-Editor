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
        reach = (int)Math.Ceiling(EffectReach(obj));
        var canvasBounds = new PixelRect(0, 0, canvasWidth, canvasHeight);
        return obj.VisualBounds(MarginFor(obj) + reach).Intersect(canvasBounds.Inflate(reach));
    }

    public PixelRect DrawingBounds(LayerObject obj)
    {
        int reach = (int)Math.Ceiling(EffectReach(obj));
        return obj.VisualBounds(MarginFor(obj) + reach);
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
        }, highQualityImages: (obj is ImageObject or GroupObject || obj.Warp != null));

        // The 3D look comes first (the shadow is then cast by the object together with its side).
        if (pixels != null && obj.ThreeD is { } threeD)
            pixels = ThreeDFx.Apply(pixels, threeD.OffsetX, threeD.OffsetY, threeD.Color, threeD.Bevel, BevelSize(obj, 1));
        if (pixels != null && shadow != null)
        {
            var center = obj.Box.Center;
            pixels = ShadowFx.Apply(pixels, shadow.Color, shadow.OffsetX, shadow.OffsetY, shadow.Blur, shadow.Size, center.X - area.X, center.Y - area.Y);
        }
        if (pixels != null) canvas.CopyFrom(pixels, area.X, area.Y);
    }

    /// <summary>How far the shadow and the 3D side can reach beyond the object, in canvas pixels.</summary>
    private static double EffectReach(LayerObject obj)
        => (obj.Shadow?.ExtentFor(obj.Box.Width, obj.Box.Height) ?? 0) + (obj.ThreeD?.Extent ?? 0);

    /// <summary>Width of the lit and shaded bevel edges: a few percent of the object's smaller side.</summary>
    private static double BevelSize(LayerObject obj, double scale)
        => Math.Clamp(Math.Min(obj.Box.Width, obj.Box.Height) * 0.05, 3, 14) * scale;

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
        TextObject { OutlineWidth: > 0, OutlineColor: not null } t => t.OutlineWidth + 2,
        GroupObject g => 2 + g.Members.Max(m => MarginFor(m.Object) + EffectReach(m.Object)) * Math.Max(g.Scale.Sx, g.Scale.Sy),
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
        if (obj.Warp is { Count: 4 } corners)
        {
            DrawWarped(dc, obj, toCanvas, shadowScale, corners);
            return;
        }

        var b = obj.Box;
        switch (obj)
        {
            case ShapeObject shape:
            {
                var geometry = ShapeGeometryFactory.Build(shape, b.Width, b.Height);
                Brush? fill = !shape.CanFill || shape.Fill is not { } f ? null
                    : shape.Gradient is { } gradient ? GradientBrushFor(gradient, b.Width, b.Height)
                    : Frozen(new SolidColorBrush(BitmapConverter.ToWpf(f)));
                Pen? pen = null;
                var strokeColor = shape.Stroke ?? (shape.IsLineLike ? shape.Fill : null);
                if (strokeColor is { } sc && shape.StrokeWidth > 0)
                {
                    Brush strokeBrush = shape.GradientBorder && shape.Gradient is { } borderGradient
                        ? GradientBrushFor(borderGradient, b.Width, b.Height)
                        : Frozen(new SolidColorBrush(BitmapConverter.ToWpf(sc)));
                    pen = new Pen(strokeBrush, shape.StrokeWidth)
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
                if (text.Gradient is { } textGradient) formatted.SetForegroundBrush(GradientBrushFor(textGradient, naturalW, naturalH));
                dc.PushTransform(new ScaleTransform(b.Width / naturalW, b.Height / naturalH));
                double originX = text.Align switch
                {
                    TextAlign.Center => naturalW / 2,
                    TextAlign.Right => naturalW - 1,
                    _ => 1,
                };
                if (text.OutlineWidth > 0 && text.OutlineColor is { } outlineColor)
                {
                    // The outline is drawn first, twice as wide, so that only its outer half shows once the letters are drawn on top.
                    var pen = new Pen(Frozen(new SolidColorBrush(BitmapConverter.ToWpf(outlineColor))), text.OutlineWidth * 2) { LineJoin = PenLineJoin.Round };
                    pen.Freeze();
                    dc.DrawGeometry(null, pen, formatted.BuildGeometry(new Point(originX, 1)));
                }
                dc.DrawText(formatted, new Point(originX, 1));
                dc.Pop();
                break;
            }
            case ImageObject image:
                dc.DrawImage(ToBitmap(image.Source), new Rect(0, 0, b.Width, b.Height));
                if (image.Gradient is { } overlay)
                {
                    // A see-through gradient laid over the picture; only where the picture itself is visible (a cut-out stays cut out).
                    dc.PushOpacityMask(new ImageBrush(ToBitmap(image.Source)) { Stretch = Stretch.Fill });
                    dc.DrawRectangle(GradientBrushFor(overlay, b.Width, b.Height), null, new Rect(0, 0, b.Width, b.Height));
                    dc.Pop();
                }
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
                    if (member.Object.Shadow != null || member.Object.ThreeD != null) DrawWithShadow(dc, member.Object, memberToCanvas, childShadowScale);
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
    /// Draws an object whose corners were dragged (pictures, text, groups, curved shapes): the object is drawn normally on a bitmap
    /// of its own, which is then stretched onto the four corner points. Corners may lie outside the box.
    /// </summary>
    private static void DrawWarped(DrawingContext dc, LayerObject obj, Matrix toCanvas, double shadowScale, IReadOnlyList<PointD> unitCorners)
    {
        var plain = obj with { Warp = null, Shadow = null };
        double w = Math.Max(1, obj.Box.Width), h = Math.Max(1, obj.Box.Height);
        var quad = unitCorners.Select(p => new PointD(p.X * w, p.Y * h)).ToArray();
        double minX = quad.Min(p => p.X), maxX = quad.Max(p => p.X), minY = quad.Min(p => p.Y), maxY = quad.Max(p => p.Y);

        double determinant = Math.Abs(new Matrix(toCanvas.M11, toCanvas.M12, toCanvas.M21, toCanvas.M22, 0, 0).Determinant);
        if (determinant < 1e-9)
        {
            Draw(dc, plain, toCanvas, shadowScale);
            return;
        }

        // Pixels per unit of the object's own space: as sharp as it is on the canvas, more when the corners pulled it bigger.
        double margin = MarginFor(plain);
        double stretch = Math.Min(4, Math.Max(1, Math.Max((maxX - minX) / w, (maxY - minY) / h)));
        double res = Math.Clamp(Math.Sqrt(determinant) * stretch, 0.05, 16);
        res = Math.Min(res, 2500 / (Math.Max(Math.Max(w, h), Math.Max(maxX - minX, maxY - minY)) + 2 * margin));
        int pad = (int)Math.Ceiling(margin * res) + 1;

        var source = WpfRasterizer.Render(new PixelRect(0, 0, (int)Math.Ceiling(w * res) + 2 * pad, (int)Math.Ceiling(h * res) + 2 * pad), c =>
        {
            c.PushTransform(new MatrixTransform(new Matrix(res, 0, 0, res, pad, pad)));
            Draw(c, plain, toCanvas, shadowScale);
            c.Pop();
        }, highQualityImages: true);
        if (source == null) return;

        double originX = Math.Floor((minX - margin) * res), originY = Math.Floor((minY - margin) * res);
        int width = Math.Max(1, (int)Math.Ceiling((maxX + margin) * res - originX));
        int height = Math.Max(1, (int)Math.Ceiling((maxY + margin) * res - originY));
        var warp = QuadWarp.Create(quad.Select(p => new PointD(p.X * res - originX, p.Y * res - originY)).ToArray());
        if (warp == null) return;

        var warped = warp.Apply(source, pad, pad, w * res, h * res, width, height);
        dc.DrawImage(BitmapConverter.ToFrozenBitmap(warped), new Rect(originX / res, originY / res, width / res, height / res));
    }

    /// <summary>
    /// Draws a part of a group together with its own shadow. The part is drawn alone on a small bitmap (in its own
    /// space, so rotation and group stretching do not matter), the shadow is made from that, and the result is
    /// drawn into the group. The shadow keeps pointing the same way on the canvas, however the part is turned.
    /// </summary>
    private static void DrawWithShadow(DrawingContext dc, LayerObject obj, Matrix toCanvas, double shadowScale)
    {
        var shadow = obj.Shadow?.Scaled(shadowScale);
        var threeD = obj.ThreeD?.Scaled(shadowScale);
        var plain = obj with { Shadow = null, ThreeD = null };

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
        double perCanvasPixel = res / Math.Sqrt(Math.Abs(determinant));      // bitmap pixels per canvas pixel (extents are given on the canvas)
        double reach = ((shadow?.ExtentFor(obj.Box.Width, obj.Box.Height) ?? 0) + (threeD?.Extent ?? 0)) * perCanvasPixel;
        int pad = (int)Math.Ceiling(MarginFor(plain) * res + reach);
        int width = (int)Math.Ceiling(obj.Box.Width * res) + 2 * pad;
        int height = (int)Math.Ceiling(obj.Box.Height * res) + 2 * pad;

        var drawn = WpfRasterizer.Render(new PixelRect(0, 0, width, height), c =>
        {
            c.PushTransform(new MatrixTransform(new Matrix(res, 0, 0, res, pad, pad)));
            Draw(c, plain, toCanvas, shadowScale);
            c.Pop();
        }, highQualityImages: true);
        if (drawn == null) return;

        // Directions are given on the canvas; the bitmap is in the part's own (turned) space.
        linear.Invert();
        if (threeD != null)
        {
            var side = linear.Transform(new Vector(threeD.OffsetX, threeD.OffsetY)) * res;
            drawn = ThreeDFx.Apply(drawn, side.X, side.Y, threeD.Color, threeD.Bevel, BevelSize(obj, res));
        }
        if (shadow != null)
        {
            var shift = linear.Transform(new Vector(shadow.OffsetX, shadow.OffsetY)) * res;
            drawn = ShadowFx.Apply(drawn, shadow.Color, shift.X, shift.Y, shadow.Blur * perCanvasPixel, shadow.Size,
                pad + obj.Box.Width * res / 2, pad + obj.Box.Height * res / 2);
        }

        dc.DrawImage(BitmapConverter.ToFrozenBitmap(drawn), new Rect(-pad / res, -pad / res, width / res, height / res));
    }

    /// <summary>
    /// A gradient laid over a <paramref name="width"/> x <paramref name="height"/> area at the origin. A straight gradient is stretched
    /// so its ends touch the corners of the area whatever the angle (the way CSS does it); a radial one grows from the middle to the edges.
    /// </summary>
    private static Brush GradientBrushFor(GradientStyle gradient, double width, double height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var stops = StopsOf(gradient);

        Brush brush;
        if (gradient.Radial)
        {
            brush = new RadialGradientBrush(stops)
            {
                MappingMode = BrushMappingMode.Absolute,
                Center = new Point(width / 2, height / 2),
                GradientOrigin = new Point(width / 2, height / 2),
                RadiusX = width / 2,
                RadiusY = height / 2,
            };
        }
        else
        {
            double angle = gradient.Angle * Math.PI / 180, dx = Math.Cos(angle), dy = Math.Sin(angle);
            double half = (Math.Abs(width * dx) + Math.Abs(height * dy)) / 2;       // reaches the corner that is furthest along the direction
            var center = new Point(width / 2, height / 2);
            brush = new LinearGradientBrush(stops, 0)
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(center.X - dx * half, center.Y - dy * half),
                EndPoint = new Point(center.X + dx * half, center.Y + dy * half),
            };
        }
        return Frozen(brush);
    }

    /// <summary>The colors of a gradient as WPF gradient stops (shared with the small previews in the Properties panel).</summary>
    public static GradientStopCollection StopsOf(GradientStyle gradient)
    {
        var stops = new GradientStopCollection();
        foreach (var stop in gradient.Sorted) stops.Add(new System.Windows.Media.GradientStop(BitmapConverter.ToWpf(stop.Color), Math.Clamp(stop.Position, 0, 1)));
        return stops;
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
        // Space between the lines: 1 = the font's own spacing.
        if (Math.Abs(text.LineSpacing - 1) > 0.001)
            formatted.LineHeight = Math.Max(1, new FontFamily(text.FontFamily).LineSpacing * Math.Max(1, text.FontSize) * text.LineSpacing);
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
