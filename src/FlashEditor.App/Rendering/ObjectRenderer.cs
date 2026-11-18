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

        // Color and blur of the object itself come first, so shadows and the other looks are made from the adjusted object.
        if (pixels != null && obj.Tone is { IsNeutral: false } tone) tone.Apply(pixels, pixels.Bounds);
        if (pixels != null && obj.BlurAmount >= 0.3) pixels = FlashEditor.Core.Effects.Filters.Blur.Gaussian(pixels, obj.BlurAmount);

        // The 3D look comes next (the shadow is then cast by the object together with its side).
        if (pixels != null && obj.ThreeD is { } threeD)
            pixels = ThreeDFx.Apply(pixels, threeD.OffsetX, threeD.OffsetY, threeD.Color, threeD.Bevel, BevelSize(obj, 1));
        // Then the looks that follow the outline: a shadow inside the edges, and a line around the outside.
        if (pixels != null && obj.InnerShadow is { } inner) pixels = SurfaceFx.InnerShadow(pixels, inner.Color, inner.OffsetX, inner.OffsetY, inner.Blur);
        if (pixels != null && obj.Outline is { } line) pixels = SurfaceFx.Outline(pixels, line.Color, line.Width);
        var shape = pixels;                                  // what the glow is made from: the object with its outline, without the shadow
        if (pixels != null && shadow != null)
        {
            var center = obj.Box.Center;
            pixels = ShadowFx.Apply(pixels, shadow.Color, shadow.OffsetX, shadow.OffsetY, shadow.Blur, shadow.Size, center.X - area.X, center.Y - area.Y);
        }
        if (pixels != null && shape != null && obj.Glow is { } glow)
            pixels = SurfaceFx.Under(pixels, SurfaceFx.GlowLayer(shape, glow.Color, glow.Size, glow.Intensity));
        if (pixels != null) canvas.CopyFrom(pixels, area.X, area.Y);
    }

    /// <summary>How far the shadow and the 3D side can reach beyond the object, in canvas pixels.</summary>
    private static double EffectReach(LayerObject obj)
        => (obj.Shadow?.ExtentFor(obj.Box.Width, obj.Box.Height) ?? 0) + (obj.ThreeD?.Extent ?? 0) + (obj.Glow?.Extent ?? 0) + (obj.Outline?.Extent ?? 0) + obj.BlurAmount * 3;

    /// <summary>Width of the lit and shaded bevel edges: a few percent of the object's smaller side.</summary>
    private static double BevelSize(LayerObject obj, double scale)
        => Math.Clamp(Math.Min(obj.Box.Width, obj.Box.Height) * 0.05, 3, 14) * scale;

    public (double Width, double Height) MeasureText(TextObject text)
    {
        if (Math.Abs(text.Arc) >= 0.5) return ArcText.Measure(text);
        if (SpacedText.Applies(text)) { var spaced = SpacedText.Build(text); return (spaced.Width, spaced.Height); }
        var formatted = CreateText(text);
        return (Math.Ceiling(formatted.WidthIncludingTrailingWhitespace) + 2, Math.Ceiling(formatted.Height) + 2);
    }

    /// <summary>
    /// Extra room around the box for borders that stick out (also inside groups). The object's own shadow is not
    /// counted (it is added afterwards); the shadows of the parts of a group are.
    /// </summary>
    private static double MarginFor(LayerObject obj) => obj switch
    {
        ShapeObject { IsLineLike: true } l => LineCurves.Reach(l.Box.Width, l.Bend, l.BendStyle) + (l.ArrowStart || l.ArrowEnd ? ArrowLength(l) / 2 : l.StrokeWidth / 2) + 2,
        ShapeObject { Shape: ShapeKind.Path, Closed: false } p when p.ArrowStart || p.ArrowEnd => ArrowLength(p) + 2 + p.StrokeWidth / 2,
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
    /// <summary>How long an arrow head is: it grows with the line width.</summary>
    private static double ArrowLength(ShapeObject line) => Math.Max(10, line.StrokeWidth * 4);

    /// <summary>Filled triangles at the ends of a line or an open curve (drawn in the shape's own space), turned to follow the line at its ends.</summary>
    private static void DrawArrowHeads(DrawingContext dc, ShapeObject shape, ObjectBox box, Brush brush)
    {
        double w = Math.Max(1, box.Width), h = Math.Max(1, box.Height);
        PointD a, b, outA, outB;
        if (shape.Shape == ShapeKind.Path && shape.Nodes is { Count: >= 2 } nodes)
        {
            a = new PointD(nodes[0].Point.X * w, nodes[0].Point.Y * h);
            b = new PointD(nodes[^1].Point.X * w, nodes[^1].Point.Y * h);
            (outA, outB) = CurvePath.EndDirections(nodes.Select(n => n.Mapped(p => new PointD(p.X * w, p.Y * h))).ToList());
        }
        else
        {
            var (s0, c1, c2, e0) = LineCurves.Controls(w, h, shape.Bend, shape.BendStyle);
            a = s0; b = e0;
            (outA, outB) = LineCurves.TipDirections(s0, c1, c2, e0);
        }

        double len = ArrowLength(shape), half = len * 0.5, reach = shape.StrokeWidth / 2;
        StreamGeometry Head(PointD tip, PointD outward)
        {
            // The tip sits a little beyond the end of the line; the base is back along the line, with the wings to both sides.
            var tipPoint = new Point(tip.X + outward.X * reach, tip.Y + outward.Y * reach);
            var back = new Point(tipPoint.X - outward.X * len, tipPoint.Y - outward.Y * len);
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(tipPoint, true, true);
                ctx.LineTo(new Point(back.X - outward.Y * half, back.Y + outward.X * half), true, false);
                ctx.LineTo(new Point(back.X + outward.Y * half, back.Y - outward.X * half), true, false);
            }
            g.Freeze();
            return g;
        }
        if (shape.ArrowEnd) dc.DrawGeometry(brush, null, Head(b, outB));
        if (shape.ArrowStart) dc.DrawGeometry(brush, null, Head(a, outA));
    }

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
                    // Dashes are counted in line widths: a dash is 3 wide with 2 of gap; a dot is round with a gap of 1.6.
                    var cap = shape.Dash == StrokeStyle.Dotted || shape.IsLineLike || shape.Shape == ShapeKind.Path ? PenLineCap.Round : PenLineCap.Flat;
                    pen = new Pen(strokeBrush, shape.StrokeWidth)
                    {
                        LineJoin = PenLineJoin.Round,
                        StartLineCap = cap,
                        EndLineCap = cap,
                        DashCap = shape.Dash == StrokeStyle.Dotted ? PenLineCap.Round : PenLineCap.Flat,
                    };
                    // Length and gap of the dashes in pixels (0 = automatic); the pen counts in line widths. A round dot is as wide as the line,
                    // so for dots the gap is measured between the edges of the dots.
                    double lineWidth = Math.Max(0.1, shape.StrokeWidth);
                    if (shape.Dash == StrokeStyle.Dashed)
                        pen.DashStyle = new DashStyle(new double[] { shape.DashLength > 0 ? shape.DashLength / lineWidth : 3, shape.DashGap > 0 ? shape.DashGap / lineWidth : 2 }, 0);
                    else if (shape.Dash == StrokeStyle.Dotted)
                        pen.DashStyle = new DashStyle(new double[] { 0.01, (shape.DashGap > 0 ? shape.DashGap / lineWidth : 1.2) + 1 }, 0);
                    pen.Freeze();
                }
                if (shape.Picture is { } picture && !shape.IsLineLike)
                {
                    // A picture in the frame: the fill underneath, the picture clipped to the outline, then the border on top.
                    dc.DrawGeometry(fill, null, geometry);
                    var (px, py, pw, ph) = picture.Place(b.Width, b.Height);
                    dc.PushClip(geometry);
                    dc.DrawImage(ToBitmap(picture.Image), new Rect(px, py, pw, ph));
                    dc.Pop();
                    if (pen != null) dc.DrawGeometry(null, pen, geometry);
                }
                else dc.DrawGeometry(fill, pen, geometry);
                if ((shape.IsLineLike || shape is { Shape: ShapeKind.Path, Closed: false }) && (shape.ArrowStart || shape.ArrowEnd) && strokeColor is { } arrowColor)
                    DrawArrowHeads(dc, shape, b, Frozen(new SolidColorBrush(BitmapConverter.ToWpf(arrowColor))));
                break;
            }
            case QrCodeObject qr:
                QrRenderer.Draw(dc, qr, b.Width, b.Height);
                break;
            case ChartObject chart:
                ChartRenderer.Draw(dc, chart, b.Width, b.Height);
                break;
            case BarcodeObject barcode:
                BarcodeRenderer.Draw(dc, barcode, b.Width, b.Height);
                break;
            case TextObject { Arc: not 0 } curved when Math.Abs(curved.Arc) >= 0.5:
            {
                // Text on a curve: every letter is placed along a circle (one line; the line breaks become spaces).
                var letters = ArcText.Build(curved);
                var area = letters.Bounds;
                if (area.IsEmpty) break;
                var (arcW, arcH) = ArcText.Measure(curved);
                Brush fillBrush = curved.Gradient is { } curvedGradient
                    ? GradientBrushFor(curvedGradient, arcW, arcH)
                    : Frozen(new SolidColorBrush(BitmapConverter.ToWpf(curved.Color)));
                // The box may have been resized: stretch the curved text to fill it.
                dc.PushTransform(new ScaleTransform(b.Width / arcW, b.Height / arcH));
                dc.PushTransform(new TranslateTransform(-area.X + 1 + curved.OutlineWidth, -area.Y + 1 + curved.OutlineWidth));
                Pen? arcPen = null;
                if (curved.OutlineWidth > 0 && curved.OutlineColor is { } arcOutline)
                {
                    arcPen = new Pen(Frozen(new SolidColorBrush(BitmapConverter.ToWpf(arcOutline))), curved.OutlineWidth * 2) { LineJoin = PenLineJoin.Round };
                    arcPen.Freeze();
                    dc.DrawGeometry(null, arcPen, letters);
                }
                dc.DrawGeometry(fillBrush, null, letters);
                dc.Pop();
                dc.Pop();
                break;
            }
            case TextObject spacedText when SpacedText.Applies(spacedText):
            {
                // Extra space between letters and / or words: the letters are placed one by one and drawn as outlines.
                var (letters, spacedW, spacedH) = SpacedText.Build(spacedText);
                Brush spacedFill = spacedText.Gradient is { } spacedGradient
                    ? GradientBrushFor(spacedGradient, spacedW, spacedH)
                    : Frozen(new SolidColorBrush(BitmapConverter.ToWpf(spacedText.Color)));
                dc.PushTransform(new ScaleTransform(b.Width / spacedW, b.Height / spacedH));
                dc.PushTransform(new TranslateTransform(1, 1));
                if (spacedText.OutlineWidth > 0 && spacedText.OutlineColor is { } spacedOutline)
                {
                    var spacedPen = new Pen(Frozen(new SolidColorBrush(BitmapConverter.ToWpf(spacedOutline))), spacedText.OutlineWidth * 2) { LineJoin = PenLineJoin.Round };
                    spacedPen.Freeze();
                    dc.DrawGeometry(null, spacedPen, letters);
                }
                dc.DrawGeometry(spacedFill, null, letters);
                dc.Pop();
                dc.Pop();
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
                    if (member.Object.Shadow != null || member.Object.ThreeD != null || member.Object.Glow != null || member.Object.InnerShadow != null || member.Object.Outline != null)
                        DrawWithShadow(dc, member.Object, memberToCanvas, childShadowScale);
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
        var glow = obj.Glow?.Scaled(shadowScale);
        var inner = obj.InnerShadow?.Scaled(shadowScale);
        var outline = obj.Outline?.Scaled(shadowScale);
        var plain = obj with { Shadow = null, ThreeD = null, Glow = null, InnerShadow = null, Outline = null };

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
        double reach = ((shadow?.ExtentFor(obj.Box.Width, obj.Box.Height) ?? 0) + (threeD?.Extent ?? 0) + (glow?.Extent ?? 0) + (outline?.Extent ?? 0)) * perCanvasPixel;
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
        if (inner != null)
        {
            var shift = linear.Transform(new Vector(inner.OffsetX, inner.OffsetY)) * res;
            drawn = SurfaceFx.InnerShadow(drawn, inner.Color, shift.X, shift.Y, inner.Blur * perCanvasPixel);
        }
        if (outline != null) drawn = SurfaceFx.Outline(drawn, outline.Color, outline.Width * perCanvasPixel);
        var shape = drawn;
        if (shadow != null)
        {
            var shift = linear.Transform(new Vector(shadow.OffsetX, shadow.OffsetY)) * res;
            drawn = ShadowFx.Apply(drawn, shadow.Color, shift.X, shift.Y, shadow.Blur * perCanvasPixel, shadow.Size,
                pad + obj.Box.Width * res / 2, pad + obj.Box.Height * res / 2);
        }
        if (glow != null) drawn = SurfaceFx.Under(drawn, SurfaceFx.GlowLayer(shape, glow.Color, glow.Size * perCanvasPixel, glow.Intensity));

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
