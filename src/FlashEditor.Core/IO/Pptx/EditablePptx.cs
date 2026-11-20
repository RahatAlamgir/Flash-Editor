using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.Core.IO.Pptx;

/// <summary>
/// Turns pages into PowerPoint slides you can edit: text becomes text boxes, simple shapes become PowerPoint shapes, placed pictures stay
/// pictures. Anything PowerPoint cannot draw the same way (shadows, glow, 3D, charts, QR codes, painted layers...) is placed as a picture
/// at the right spot, so the slide always looks like the page. A page with adjustment layers or blend modes becomes one picture.
/// </summary>
public static class EditablePptx
{
    public sealed record Options(int Quality = 90, ColorRgba? Matte = null);

    public static byte[] Build(IReadOnlyList<Document> pages, Options? options = null)
    {
        if (pages.Count == 0) throw new ArgumentException("A presentation needs at least one slide.", nameof(pages));
        options ??= new Options();
        var slides = pages.Select(p => BuildSlide(p, options)).ToList();
        return PptxWriter.Build(slides, pages[0].Width, pages[0].Height);
    }

    public static void Save(IReadOnlyList<Document> pages, string path, Options? options = null)
    {
        string temp = path + ".tmp";
        File.WriteAllBytes(temp, Build(pages, options));
        File.Move(temp, path, overwrite: true);
    }

    // ------------------------------------------------------------------ one page

    public static PptxSlide BuildSlide(Document page, Options options)
    {
        var slide = new PptxSlide();
        var ctx = new Context(slide, page.Width, page.Height, options);

        // Looks that PowerPoint cannot combine the way this program does: keep the page as it is, as one picture.
        if (page.Layers.Any(l => l.IsVisible && l.Opacity > 0 && (l.IsAdjustment || l.BlendMode != BlendMode.Normal)))
        {
            ctx.Picture(Painting.Compositor.Flatten(page), "Page", 0, 0, 1);
            return slide;
        }

        // The paper color: slides are white; a visible background layer is just the first picture.
        foreach (var layer in page.Layers)
        {
            if (!layer.IsVisible || layer.Opacity <= 0) continue;
            if (layer.Object is { } obj && layer.EffectiveMask == null) ctx.Emit(obj, layer.Opacity, layer.Name, () => layer.Pixels);
            else if (layer.EffectiveMask == null && Uniform(layer.Pixels) is { } paper) ctx.Fill(paper, layer.Name, layer.Opacity);
            else ctx.Picture(layer.MaskedPixels(), layer.Name, 0, 0, layer.Opacity);
        }
        return slide;
    }

    /// <summary>The color when every pixel of the buffer is the same (an empty or flat background layer), otherwise null.</summary>
    private static ColorRgba? Uniform(PixelBuffer p)
    {
        var d = p.Data;
        if (d.Length < 4) return null;
        for (int i = 4; i < d.Length; i += 4)
            if (d[i] != d[0] || d[i + 1] != d[1] || d[i + 2] != d[2] || d[i + 3] != d[3]) return null;
        var c = p.GetPixel(0, 0);
        return c.A == 0 ? null : c;   // a fully see-through layer shows nothing, it is simply skipped below
    }

    // ------------------------------------------------------------------ objects

    private sealed class Context
    {
        private readonly PptxSlide _slide;
        private readonly int _width, _height;
        private readonly Options _options;

        public Context(PptxSlide slide, int width, int height, Options options)
        {
            _slide = slide; _width = width; _height = height; _options = options;
        }

        /// <summary>True when the object has none of the effects PowerPoint shapes cannot show the same way.</summary>
        private static bool Plain(LayerObject o)
            => o.Shadow == null && o.Glow == null && o.InnerShadow == null && o.Outline == null && o.Gradient == null && o.ThreeD == null
               && o.Warp == null && o.Tone == null && o.BlurAmount <= 0;

        /// <summary>A flat color over the whole page: a rectangle you can recolor in PowerPoint.</summary>
        public void Fill(ColorRgba color, string name, double opacity)
            => _slide.AddShape("rect", name, 0, 0, _width, _height, color, null, 0, 0, false, false, opacity);

        public void Emit(LayerObject obj, double opacity, string name, Func<PixelBuffer> render)
        {
            if (obj is GroupObject group && Plain(group))
            {
                foreach (var member in group.CurrentMembers())
                    Emit(member.Object, opacity * member.Opacity, member.Name, () => Rendered(member.Object));
                return;
            }
            if (Plain(obj))
            {
                switch (obj)
                {
                    case TextObject text when text.Arc == 0 && text.OutlineWidth <= 0 && !text.FlipX && !text.FlipY && text.Text.Trim().Length > 0:
                        Text(text, opacity, name);
                        return;
                    case ShapeObject shape when TryShape(shape, opacity, name):
                        return;
                    case ImageObject image:
                        Image(image, opacity);
                        return;
                }
            }
            Picture(render(), name, 0, 0, opacity);
        }

        private PixelBuffer Rendered(LayerObject obj)
        {
            try { return Layer.RenderState(obj, _width, _height).Pixels; }
            catch (Exception) { return new PixelBuffer(_width, _height); } // no renderer available: nothing to show for this part
        }

        // ---- text

        private void Text(TextObject t, double opacity, string name)
        {
            string align = t.Align switch { TextAlign.Center => "ctr", TextAlign.Right => "r", _ => "l" };
            var b = t.Box;
            _slide.AddText(t.Text, name, b.X, b.Y, b.Width, b.Height, t.FontFamily, t.FontSize, t.Color, t.Bold, t.Italic, align,
                b.Rotation, opacity, t.LineSpacing, t.LetterSpacing);
        }

        // ---- shapes

        private static int Percent(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * 100000);

        private bool TryShape(ShapeObject s, double opacity, string name)
        {
            if (s.Picture != null || s.Vertices != null || s.GradientBorder || s.Nodes != null || s.Bend != 0) return false;
            var b = s.Box;
            double min = Math.Max(1e-6, Math.Min(b.Width, b.Height));
            string? preset = null;
            var adjust = new List<(string, int)>();
            switch (s.Shape)
            {
                case ShapeKind.Rectangle:
                    if (s.CornerRadius > 0.5) { preset = "roundRect"; adjust.Add(("adj", (int)Math.Round(Math.Clamp(s.CornerRadius / min, 0, 0.5) * 100000))); }
                    else preset = "rect";
                    break;
                case ShapeKind.Ellipse: preset = "ellipse"; break;
                case ShapeKind.Triangle: preset = "triangle"; break;
                case ShapeKind.RightTriangle: preset = "rtTriangle"; break;
                case ShapeKind.Diamond: preset = "diamond"; break;
                case ShapeKind.Trapezoid: preset = "trapezoid"; break;
                case ShapeKind.Parallelogram: preset = "parallelogram"; break;
                case ShapeKind.Heart: preset = "heart"; break;
                case ShapeKind.Arrow: preset = "rightArrow"; break;
                case ShapeKind.DoubleArrow: preset = "leftRightArrow"; break;
                case ShapeKind.Chevron: preset = "chevron"; break;
                case ShapeKind.Lightning: preset = "lightningBolt"; break;
                case ShapeKind.Cloud: preset = "cloud"; break;
                case ShapeKind.Teardrop: preset = "teardrop"; break;
                case ShapeKind.Crescent: preset = "moon"; break;
                case ShapeKind.Pentagon: preset = "pentagon"; break;
                case ShapeKind.Hexagon: preset = "hexagon"; break;
                case ShapeKind.Octagon: preset = "octagon"; break;
                case ShapeKind.XMark: preset = "mathMultiply"; break;
                case ShapeKind.Cross: preset = "plus"; adjust.Add(("adj", Percent((1 - s.Inner) / 2))); break;
                case ShapeKind.Frame: preset = "frame"; adjust.Add(("adj1", Percent((1 - s.Inner) / 2))); break;
                case ShapeKind.Ring: preset = "donut"; adjust.Add(("adj", Percent((1 - s.Inner) / 2))); break;
                case ShapeKind.SpeechBubble: preset = s.CornerRadius > 0.5 ? "wedgeRoundRectCallout" : "wedgeRectCallout"; break;
                case ShapeKind.OvalBubble: preset = "wedgeEllipseCallout"; break;
                case ShapeKind.Star:
                    if (s.Points is 4 or 5 or 6 or 7 or 8 or 10 or 12 or 16 or 24 or 32) preset = "star" + s.Points;
                    break;
                case ShapeKind.Polygon:
                    preset = s.Points switch { 5 => "pentagon", 6 => "hexagon", 7 => "heptagon", 8 => "octagon", 10 => "decagon", 12 => "dodecagon", _ => null };
                    break;
                case ShapeKind.Line:
                    _slide.AddShape("line", name, b.X, b.Y + b.Height / 2, b.Width, 0, null, s.Stroke ?? s.Fill, Math.Max(1, s.StrokeWidth),
                        b.Rotation, false, false, opacity, DashName(s.Dash), s.ArrowStart, s.ArrowEnd);
                    return true;
            }
            if (preset == null) return false;
            _slide.AddShape(preset, name, b.X, b.Y, b.Width, b.Height, s.Fill, s.Stroke, s.StrokeWidth, b.Rotation, s.FlipX, s.FlipY, opacity,
                DashName(s.Dash), false, false, adjust);
            return true;
        }

        private static string DashName(StrokeStyle d) => d switch { StrokeStyle.Dashed => "dash", StrokeStyle.Dotted => "sysDot", _ => "solid" };

        // ---- pictures

        private void Image(ImageObject image, double opacity)
        {
            bool hasAlpha = HasTransparency(image.Source);
            byte[] data = hasAlpha
                ? ImageEncoder.Encode(image.Source, ImageFileFormat.Png, 100, null)
                : ImageEncoder.Encode(image.Source, ImageFileFormat.Jpeg, _options.Quality, _options.Matte ?? ColorRgba.White);
            var b = image.Box;
            _slide.AddPicture(data, hasAlpha, image.Name, b.X, b.Y, b.Width, b.Height, b.Rotation, image.FlipX, image.FlipY, opacity);
        }

        private static bool HasTransparency(PixelBuffer p)
        {
            var d = p.Data;
            for (int i = 3; i < d.Length; i += 4) if (d[i] < 255) return true;
            return false;
        }

        /// <summary>Places what is painted in <paramref name="pixels"/> (a canvas-sized buffer) as one picture of just the covered area.</summary>
        public void Picture(PixelBuffer pixels, string name, double offsetX, double offsetY, double opacity)
        {
            var area = pixels.VisibleBounds().Intersect(pixels.Bounds);
            if (area.IsEmpty) return;
            var part = area.Width == pixels.Width && area.Height == pixels.Height ? pixels : pixels.Crop(area);
            bool alpha = HasTransparency(part);
            byte[] data = alpha
                ? ImageEncoder.Encode(part, ImageFileFormat.Png, 100, null)
                : ImageEncoder.Encode(part, ImageFileFormat.Jpeg, _options.Quality, _options.Matte ?? ColorRgba.White);
            _slide.AddPicture(data, alpha, name, offsetX + area.X, offsetY + area.Y, area.Width, area.Height, 0, false, false, opacity);
        }
    }
}
