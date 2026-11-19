using System.IO;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Barcodes;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.IO.Project;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Qr;
using FlashEditor.Core.Transforms;

namespace FlashEditor.App.Services;

/// <summary>
/// The tutorial project that is made the first time the program starts (and from View > Tutorial): a project of pages, each teaching a few tools
/// with real objects you can click, change and copy. Everything in it is made from the program's own parts, so it always matches the program.
/// </summary>
public static class DemoProject
{
    public const string FileName = "Flash Editor Tutorial.flash";

    private const int W = 1280, H = 720;

    /// <summary>Where the tutorial is kept: in the projects folder, so it also shows in the Project Manager.</summary>
    public static string DefaultPath => Path.Combine(ProjectLibrary.Default.ProjectsFolder, FileName);

    /// <summary>Makes the tutorial file when it is not there yet (or <paramref name="overwrite"/> is true). Returns its path.</summary>
    public static string Create(bool overwrite = false)
    {
        string path = DefaultPath;
        if (File.Exists(path) && !overwrite) return path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        ProjectWriter.Save(BuildPages(), 0, path);
        return path;
    }

    // ------------------------------------------------------------------ colors

    private static ColorRgba C(int r, int g, int b, int a = 255) => new((byte)r, (byte)g, (byte)b, (byte)a);

    private static readonly ColorRgba Ink = C(30, 41, 59), Subtle = C(100, 116, 139), Paper = C(248, 250, 252), White = C(255, 255, 255),
        Blue = C(59, 130, 246), Indigo = C(79, 70, 229), Purple = C(124, 58, 237), Pink = C(236, 72, 153), Red = C(239, 68, 68), Orange = C(249, 115, 22),
        Amber = C(245, 158, 11), Green = C(34, 197, 94), Teal = C(20, 184, 166), Sky = C(14, 165, 233), Slate = C(51, 65, 85);

    // ------------------------------------------------------------------ one page

    private sealed class Page
    {
        private readonly List<Layer> _layers;
        public Document Doc { get; }
        public string Name { get; }

        public Page(string name, ColorRgba background)
        {
            Name = name;
            Doc = Document.CreateBlank(W, H, background);
            _layers = Doc.Layers.ToList();
        }

        public Layer Add(LayerObject obj, string name)
        {
            var layer = new Layer(name, W, H);
            layer.SetObject(obj);
            _layers.Add(layer);
            return layer;
        }

        public ProjectPage Finish()
        {
            Doc.SetLayers(_layers, _layers[^1]);
            return new ProjectPage(Name, Doc);
        }

        public Layer Shape(string name, ShapeKind kind, double x, double y, double w, double h, ColorRgba? fill, ColorRgba? stroke = null, double strokeWidth = 0, double radius = 0, double rotation = 0, Func<ShapeObject, ShapeObject>? more = null)
        {
            var shape = new ShapeObject(new ObjectBox(x, y, w, h, rotation), kind, fill, stroke, strokeWidth, CornerRadius: radius);
            return Add(more == null ? shape : more(shape), name);
        }

        /// <summary>Text with its top-left corner at (x, y), or centered on x when <paramref name="center"/> is true. Lines are separated by line breaks.</summary>
        public Layer Text(string text, double x, double y, double size, ColorRgba color, bool bold = false, bool center = false, double lineSpacing = 1.15, Func<TextObject, TextObject>? more = null, string family = "Segoe UI")
        {
            var t = new TextObject(new ObjectBox(0, 0, 1, 1), text, family, size, color, Bold: bold, Align: center ? TextAlign.Center : TextAlign.Left, LineSpacing: lineSpacing);
            if (more != null) t = more(t);
            var (w, h) = ObjectRendering.Renderer.MeasureText(t);
            t = t with { Box = new ObjectBox(center ? x - w / 2 : x, y, w, h) };
            return Add(t, text.Split('\n')[0].Trim());
        }

        /// <summary>The colored header of a page: the page number, the title and one line about what it teaches.</summary>
        public void Header(int number, int total, string title, string lesson, ColorRgba accent)
        {
            Shape("Header", ShapeKind.Rectangle, 0, 0, W, 112, accent);
            Shape("Header shade", ShapeKind.Rectangle, 0, 100, W, 12, White, more: s => s with { Fill = C(255, 255, 255, 38) });
            Text(title, 56, 18, 40, White, bold: true);
            Text(lesson, 58, 68, 19, C(255, 255, 255, 225));
            Shape("Page chip", ShapeKind.Rectangle, W - 150, 34, 104, 40, White, radius: 20, more: s => s with { Fill = C(255, 255, 255, 50) });
            Text($"{number} / {total}", W - 98, 41, 20, White, bold: true, center: true);
        }

        /// <summary>A list of numbered steps down the left side.</summary>
        public void Steps(string[] steps, ColorRgba accent, double top = 150, double lineGap = 82)
        {
            for (int i = 0; i < steps.Length; i++)
            {
                double y = top + i * lineGap;
                Shape($"Step {i + 1}", ShapeKind.Ellipse, 56, y, 34, 34, accent);
                Text((i + 1).ToString(), 73, y + 4, 19, White, bold: true, center: true);
                Text(steps[i], 106, y + 1, 19, Ink, lineSpacing: 1.2);
            }
        }

        /// <summary>A little yellow note with a hint.</summary>
        public void Note(string text, double x, double y, double w, double h)
        {
            Shape("Note", ShapeKind.Rectangle, x, y, w, h, C(254, 240, 138), radius: 8, more: s => s with { Shadow = new ShadowStyle(C(0, 0, 0, 55), 5, 60, 10) });
            Text(text, x + 16, y + 12, 17, C(113, 63, 18));
        }

        public void Caption(string text, double cx, double y, double size = 15) => Text(text, cx, y, size, Subtle, center: true);

        /// <summary>Puts a group made by <see cref="Badges"/>, <see cref="Graphics"/> or <see cref="SmartArt"/> (built centered on the canvas) at a new center.</summary>
        public Layer Place(GroupObject group, double cx, double cy, string name)
            => Add((GroupObject)ObjectTransforms.Offset(group, cx - W / 2.0, cy - H / 2.0), name);
    }

    // ------------------------------------------------------------------ pictures made from nothing (so the tutorial has photos to play with)

    /// <summary>A small landscape: a sky, a sun and two hills. The colors differ per <paramref name="variant"/>.</summary>
    private static PixelBuffer Landscape(int width, int height, int variant)
    {
        var skies = new[] { (C(251, 146, 60), C(251, 207, 232)), (C(14, 116, 144), C(186, 230, 253)), (C(30, 64, 175), C(196, 181, 253)) };
        var hills = new[] { (C(124, 45, 18), C(180, 83, 9)), (C(6, 78, 59), C(20, 184, 166)), (C(49, 46, 129), C(99, 102, 241)) };
        var (top, bottom) = skies[variant % skies.Length];
        var (far, near) = hills[variant % hills.Length];
        var buffer = new PixelBuffer(width, height);
        double sunX = width * (0.3 + 0.2 * variant), sunY = height * 0.38, sunR = height * 0.14;
        for (int y = 0; y < height; y++)
        {
            double t = y / (double)height;
            for (int x = 0; x < width; x++)
            {
                var c = new ColorRgba((byte)(top.R + (bottom.R - top.R) * t), (byte)(top.G + (bottom.G - top.G) * t), (byte)(top.B + (bottom.B - top.B) * t));
                double dx = x - sunX, dy = y - sunY;
                if (dx * dx + dy * dy < sunR * sunR) c = C(254, 249, 195);
                double farLine = height * (0.62 + 0.07 * Math.Sin(x / (double)width * 7 + variant));
                double nearLine = height * (0.78 + 0.06 * Math.Sin(x / (double)width * 5 + 2 + variant));
                if (y > farLine) c = far;
                if (y > nearLine) c = near;
                buffer.SetPixel(x, y, c);
            }
        }
        return buffer;
    }

    // ------------------------------------------------------------------ the pages

    private static IReadOnlyList<ProjectPage> BuildPages()
    {
        const int total = 13;
        var builders = new Func<ProjectPage>[]
        {
            Welcome, () => CutOut(total), () => PhotoLook(total), () => Selecting(total), () => TextPage(total), () => ShapesPage(total), () => GraphicsPage(total), () => BadgesPage(total), () => ChartsPage(total),
            () => SmartArtPage(total), () => PhotosPage(total), () => EffectsPage(total), () => CodesPage(total), () => WrapUp(total),
        };
        var pages = new List<ProjectPage>();
        foreach (var build in builders) pages.Add(build());
        return pages;
    }

    private static ProjectPage Welcome()
    {
        var p = new Page("Welcome", C(30, 41, 99));
        p.Add((GroupObject)ObjectTransforms.Offset(Graphics.Build("blob2", W, H, 520), 400, -120), "Blob");
        p.Add((GroupObject)ObjectTransforms.Offset(Graphics.Build("blob1", W, H, 380), -440, 270), "Blob");
        p.Add((GroupObject)ObjectTransforms.Offset(Graphics.Build("sparkles", W, H, 160), 420, -210), "Sparkles");
        p.Text("Welcome to Flash Editor", 80, 110, 66, White, bold: true);
        p.Text("This tutorial is a real project. Click, drag and change anything on these pages:\nnothing here can break, and you can always press Ctrl+Z.", 84, 205, 24, C(255, 255, 255, 225), lineSpacing: 1.3);
        string[] lessons = { "1  Cut out, enlarge and fix photos", "2  Adjust, filters and layers", "3  Select, move and resize", "4  Text and designs", "5  Shapes, lines and curves", "6  Graphics and icons", "7  Badges" };
        string[] lessons2 = { "8  Charts", "9  SmartArt diagrams", "10  Photos and frames", "11  Shadows, glow and 3D", "12  QR codes and barcodes", "13  Layers, pages and export" };
        for (int i = 0; i < lessons.Length; i++) p.Text(lessons[i], 90, 306 + i * 36, 22, White);
        for (int i = 0; i < lessons2.Length; i++) p.Text(lessons2[i], 500, 306 + i * 36, 22, White);
        p.Shape("Photo frame", ShapeKind.Ellipse, 880, 330, 300, 300, C(226, 232, 240), White, 8, more: sh => sh with { Picture = new PictureFill(Photo(), "Face", Zoom: 2.2, PanX: 0, PanY: 1), Shadow = ShadowStyle.Default });
        p.Place(Badges.Build("new", W, H, 130), 1060, 240, "NEW badge");
        p.Note("Move to the next page: click it in the page strip under the canvas (or press Ctrl+Tab).\nGrid view shows all pages at once.", 84, 575, 760, 70);
        p.Text("Tip: the search box at the top right finds any command by name.", 84, 660, 17, C(255, 255, 255, 200));
        return p.Finish();
    }


    // ------------------------------------------------------------------ the photo of the tutorial

    private static PixelBuffer? _photo;

    /// <summary>The photo that ships with the program (a man in a red T-shirt: a typical mockup). Falls back to a drawn landscape if it cannot be read.</summary>
    /// <summary>A picture that ships with the program (Assets folder), or null if it cannot be read.</summary>
    private static PixelBuffer? Asset(string file)
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/FlashEditor;component/Assets/" + file, UriKind.Absolute));
            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(info.Stream, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            return BitmapConverter.FromBitmapSource(decoder.Frames[0]);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static PixelBuffer Photo()
    {
        if (_photo != null) return _photo;
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/FlashEditor;component/Assets/TutorialPhoto.jpg", UriKind.Absolute));
            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(info.Stream, System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            _photo = BitmapConverter.FromBitmapSource(decoder.Frames[0]);
        }
        catch (Exception)
        {
            _photo = Landscape(800, 800, 0);
        }
        return _photo;
    }

    /// <summary>The photo made smaller (a square, from the middle), for thumbnails and quick effects.</summary>
    private static PixelBuffer PhotoSquare(int size)
    {
        var photo = Photo();
        int side = Math.Min(photo.Width, photo.Height);
        var square = photo.Crop(new PixelRect((photo.Width - side) / 2, (photo.Height - side) / 2, side, side));
        return Resampler.Resize(square, size, size, ResampleMode.Smooth);
    }

    /// <summary>Changes the color of everything that is strongly red (the T-shirt) by turning its hue; the rest of the picture stays.</summary>
    private static PixelBuffer RecolorRed(PixelBuffer source, double hueShift)
    {
        var result = source.Clone();
        for (int y = 0; y < result.Height; y++)
            for (int x = 0; x < result.Width; x++)
            {
                var c = result.GetPixel(x, y);
                double redness = c.R / (Math.Max(c.G, c.B) + 8.0);
                double weight = Math.Clamp((redness - 1.6) / 0.9, 0, 1);
                if (c.R < 60 || weight <= 0) continue;
                ColorMath.RgbToHsv(c.R / 255.0, c.G / 255.0, c.B / 255.0, out double h, out double sat, out double v);
                ColorMath.HsvToRgb(h + hueShift, sat, v, out double r2, out double g2, out double b2);
                result.SetPixel(x, y, new ColorRgba(
                    (byte)Math.Round(c.R + (r2 * 255 - c.R) * weight), (byte)Math.Round(c.G + (g2 * 255 - c.G) * weight), (byte)Math.Round(c.B + (b2 * 255 - c.B) * weight), c.A));
            }
        return result;
    }

    // ------------------------------------------------------------------ pictures for the photo pages

    /// <summary>Blends a color over a pixel (straight alpha), so shapes with soft edges can be drawn into a see-through picture.</summary>
    private static void Over(PixelBuffer b, int x, int y, ColorRgba src, double coverage)
    {
        if (x < 0 || y < 0 || x >= b.Width || y >= b.Height || coverage <= 0) return;
        var dst = b.GetPixel(x, y);
        double sa = src.A / 255.0 * Math.Min(1, coverage), da = dst.A / 255.0, a = sa + da * (1 - sa);
        if (a <= 0) return;
        byte Mix(byte s, byte d) => (byte)Math.Round((s * sa + d * da * (1 - sa)) / a);
        b.SetPixel(x, y, new ColorRgba(Mix(src.R, dst.R), Mix(src.G, dst.G), Mix(src.B, dst.B), (byte)Math.Round(a * 255)));
    }

    /// <summary>A filled ellipse with soft (smooth) edges.</summary>
    private static void Disc(PixelBuffer b, double cx, double cy, double rx, double ry, ColorRgba color)
    {
        for (int y = (int)(cy - ry - 2); y <= cy + ry + 2; y++)
            for (int x = (int)(cx - rx - 2); x <= cx + rx + 2; x++)
            {
                double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry, d = Math.Sqrt(dx * dx + dy * dy);
                Over(b, x, y, color, (1 - d) * Math.Min(rx, ry) + 0.5);
            }
    }

    /// <summary>A red apple with a leaf, on a see-through background (the thing to cut out).</summary>
    private static PixelBuffer Apple(int size)
    {
        var b = new PixelBuffer(size, size);
        double s = size / 200.0;
        Disc(b, 70 * s, 118 * s, 56 * s, 58 * s, C(220, 38, 38));
        Disc(b, 124 * s, 118 * s, 56 * s, 58 * s, C(220, 38, 38));
        Disc(b, 96 * s, 130 * s, 58 * s, 52 * s, C(220, 38, 38));
        Disc(b, 64 * s, 96 * s, 14 * s, 18 * s, C(252, 165, 165));
        Disc(b, 98 * s, 66 * s, 5 * s, 20 * s, C(120, 53, 15));
        Disc(b, 122 * s, 56 * s, 24 * s, 11 * s, C(34, 197, 94));
        return b;
    }

    private static PixelBuffer Checker(int width, int height)
    {
        var b = new PixelBuffer(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                b.SetPixel(x, y, ((x / 10 + y / 10) % 2 == 0) ? C(226, 232, 240) : C(255, 255, 255));
        return b;
    }

    /// <summary>The picture with another one laid over it (the see-through parts show what is below).</summary>
    private static PixelBuffer WithOver(PixelBuffer baseImage, PixelBuffer top, int x, int y)
    {
        var result = baseImage.Clone();
        for (int j = 0; j < top.Height; j++)
            for (int i = 0; i < top.Width; i++)
            {
                var c = top.GetPixel(i, j);
                if (c.A > 0) Over(result, x + i, y + j, c, 1);
            }
        return result;
    }

    private static PixelBuffer Fx(PixelBuffer source, Core.Effects.ImageEffect effect, params (string Key, double Value)[] values)
    {
        var v = effect.CreateDefaultValues();
        foreach (var (key, value) in values) v[key] = value;
        return effect.Apply(source, v);
    }

    /// <summary>A picture object with its top-left corner at (x, y), shown at the given size.</summary>
    private static ImageObject Pic(PixelBuffer image, double x, double y, double w, double h, string name) => new(new ObjectBox(x, y, w, h), image, name);

    private static void BeforeAfter(Page p, string label, string afterText, PixelBuffer before, PixelBuffer after, double y, ColorRgba accent, Action<Page, double>? extraAfter = null)
    {
        p.Add(Pic(before, 656, y, 214, 140, "Before: " + label), "Before: " + label);
        p.Shape("Arrow", ShapeKind.Arrow, 880, y + 54, 44, 32, accent);
        if (extraAfter != null) extraAfter(p, y); else p.Add(Pic(after, 934, y, 214, 140, "After: " + label), "After: " + label);
        p.Text("Before", 763, y + 144, 14, Subtle, center: true);
        p.Text(afterText, 1041, y + 144, 14, Subtle, center: true);
    }

    /// <summary>A before picture, an arrow and an after picture (square), with what was done written under them.</summary>
    private static void Pair(Page p, double x, double y, string label, PixelBuffer before, PixelBuffer after, ColorRgba accent, double tilt = 0, double inset = 0)
    {
        double size = 128, box = size - inset * 2;
        p.Add(new ImageObject(new ObjectBox(x + inset, y + inset, box, box, tilt), before, "Before: " + label), "Before: " + label);
        p.Shape("Arrow", ShapeKind.Arrow, x + size + 6, y + size / 2 - 11, 28, 22, accent);
        p.Add(new ImageObject(new ObjectBox(x + size + 40 + inset, y + inset, box, box), after, "After: " + label), "After: " + label);
        p.Text(label, x + size + 20, y + size + 8, 14, Subtle, center: true);
    }

    private static ProjectPage CutOut(int total)
    {
        var p = new Page("Cut out and fix photos", Paper);
        p.Header(1, total, "Photo editing: cut out, enlarge and fix", "The Image tab and the AI tools, on a real photo. Click any picture on this page and try the same.", Teal);
        p.Steps(new[]
        {
            "Remove background: select a picture, then Image >\nRemove BG (AI). The subject stays, the rest becomes\nsee-through. Remove by Color works for flat colors.",
            "Recolor (Ctrl+Shift+R) and Magic wand change one color\nonly, like the T-shirt here. Select Subject (AI) selects\nthe main subject.",
            "Upscale 2x makes a picture bigger and cleaner; AI\nUpscale also rebuilds fine detail. Magic Resize turns a\ndesign into another size (post to story).",
            "Straighten levels a tilted photo; Crop (C) and\nPerspective Crop cut and flatten.",
            "Fill (AI): select something, then Select > Fill (AI)\npaints in what was behind it. The AI models download\nonce, after you say yes.",
        }, Teal, top: 140, lineGap: 110);

        var small = PhotoSquare(300);
        var tee = Asset("TutorialTee.jpg") ?? small;
        var teeRecolor = Asset("TutorialTeeRecolor.jpg") ?? RecolorRed(small, 215);
        var teeCutout = Asset("TutorialTeeCutout.png") ?? small;
        var fillBefore = Asset("TutorialFillBefore.jpg") ?? Landscape(320, 214, 1);
        var fillAfter = Asset("TutorialFillAfter.jpg") ?? Landscape(320, 214, 1);

        // row 1: the same photo, recolored and cut out
        p.Add(Pic(tee, 648, 140, 170, 170, "Original photo"), "Original photo");
        p.Add(Pic(teeRecolor, 840, 140, 170, 170, "Recolored T-shirt"), "Recolored T-shirt");
        double cutW = 170.0 * teeCutout.Width / teeCutout.Height;
        p.Shape("Checker", ShapeKind.Rectangle, 1032, 140, 170, 170, C(226, 232, 240), radius: 4);
        p.Add(Pic(teeCutout, 1032 + (170 - cutW) / 2, 140, cutW, 170, "Background removed"), "Background removed");
        p.Text("Original", 733, 314, 14, Subtle, center: true);
        p.Text("Recolor (T-shirt only)", 925, 314, 14, Subtle, center: true);
        p.Text("Remove BG (AI)", 1117, 314, 14, Subtle, center: true);

        // row 2: remove a person with Fill (AI)
        p.Add(Pic(fillBefore, 648, 360, 180, 120, "Before: Fill (AI)"), "Before: Fill (AI)");
        p.Shape("Arrow", ShapeKind.Arrow, 836, 408, 30, 24, Teal);
        p.Add(Pic(fillAfter, 874, 360, 180, 120, "After: Fill (AI)"), "After: Fill (AI)");
        p.Text("Fill (AI): the person is removed, the sea and rocks are painted in", 851, 484, 14, Subtle, center: true);
        p.Shape("Hint", ShapeKind.Rectangle, 1076, 360, 184, 120, C(254, 240, 138), radius: 8, more: sh => sh with { Shadow = new ShadowStyle(C(0, 0, 0, 55), 5, 60, 10) });
        p.Text("Try it: pick a\npicture, then Image >\nRemove BG (AI), or\nSelect > Fill (AI).", 1090, 372, 14.5, C(113, 63, 18));

        // row 3: enlarge and straighten
        var tiny = Resampler.Resize(PhotoSquare(40), 300, 300, ResampleMode.NearestNeighbor);
        Pair(p, 648, 530, "Upscale: small to big", tiny, small, Teal);
        Pair(p, 960, 530, "Straighten", small, small, Teal, tilt: 9, inset: 10);
        return p.Finish();
    }

    private static ProjectPage PhotoLook(int total)
    {
        var p = new Page("Adjust and filters", Paper);
        p.Header(2, total, "Photo editing: adjust, filters and layers", "Photoshop-style tools on the same photo: tone and color, filters, retouching, masks and blend modes.", Orange);
        p.Steps(new[]
        {
            "Adjust tab: brightness, contrast, hue, saturation,\ntemperature, levels, curves. As an Adjustment layer it\nis non-destructive: change or delete it any time.",
            "Properties > Adjust (for the selected picture): quick\nbrightness, contrast, saturation, hue, temperature and\ntint sliders, and blur. No dialog needed.",
            "Filter tab: blur, motion blur, sharpen, noise, pixelate,\nvignette, emboss, find edges. Select an area first to\nlimit the effect to it.",
            "Retouch tools in the toolbox: Healing, Clone stamp,\nPatch, Dodge / Burn, Liquify. Selections, layer masks,\nblend modes and opacity are in the Layer tab.",
        }, Orange, top: 140, lineGap: 135);

        var photo = PhotoSquare(300);
        var items = new (string Label, PixelBuffer Image)[]
        {
            ("Original", photo),
            ("Grayscale", Fx(photo, new Core.Effects.Adjustments.GrayscaleEffect())),
            ("Sepia", Fx(photo, new Core.Effects.Adjustments.SepiaEffect())),
            ("Warm (temperature +60)", Fx(photo, new Core.Effects.Adjustments.TemperatureEffect(), ("temperature", 60.0))),
            ("Cool (temperature -60)", Fx(photo, new Core.Effects.Adjustments.TemperatureEffect(), ("temperature", -60.0))),
            ("More contrast", Fx(photo, new Core.Effects.Adjustments.BrightnessContrastEffect(), ("contrast", 40.0))),
            ("Hue shift", Fx(photo, new Core.Effects.Adjustments.HueSaturationEffect(), ("hue", 60.0))),
            ("Blur", Fx(photo, new Core.Effects.Filters.GaussianBlurEffect(), ("radius", 5.0))),
            ("Sharpen", Fx(photo, new Core.Effects.Filters.SharpenEffect())),
            ("Vignette", Fx(photo, new Core.Effects.Filters.VignetteEffect())),
            ("Pixelate", Fx(photo, new Core.Effects.Filters.PixelateEffect(), ("size", 10.0))),
            ("Emboss", Fx(photo, new Core.Effects.Filters.EmbossEffect())),
        };
        for (int i = 0; i < items.Length; i++)
        {
            double x = 648 + (i % 4) * 152, y = 140 + (i / 4) * 180;
            p.Add(new ImageObject(new ObjectBox(x, y, 140, 140), items[i].Image, items[i].Label), items[i].Label);
            p.Text(items[i].Label, x + 70, y + 144, 12.5, Subtle, center: true);
        }
        return p.Finish();
    }

    private static ProjectPage Selecting(int total)
    {
        var p = new Page("Select and move", Paper);
        p.Header(3, total, "Select, move and resize", "Everything you add is an object on its own layer. Click it and it stays fully editable.", Blue);
        p.Steps(new[]
        {
            "Click an object with the Select / Move tool (V) to select it.",
            "Drag it to move. Drag a corner to resize it: the link\nbutton in Properties keeps the proportions.",
            "Drag the round handle above it to turn it\n(hold Shift for 15° steps).",
            "Shift+click, or Ctrl+drag a box, to select several.\nCtrl+G groups them, Ctrl+Shift+G ungroups.",
            "Arrow keys nudge by 1 pixel, Shift+arrows by 10.",
        }, Blue, lineGap: 100);
        p.Shape("Try me", ShapeKind.Rectangle, 700, 170, 230, 150, Blue, radius: 22, more: s => s with { Shadow = ShadowStyle.Default });
        p.Shape("Turn me", ShapeKind.Ellipse, 990, 190, 190, 190, Pink, rotation: 0, more: s => s with { Shadow = ShadowStyle.Default });
        p.Shape("Star", ShapeKind.Star, 740, 400, 190, 190, Amber, rotation: 18, more: s => s with { Shadow = ShadowStyle.Default });
        var g1 = new ShapeObject(new ObjectBox(1010, 430, 90, 90), ShapeKind.Hexagon, Green, null, 0);
        var g2 = new ShapeObject(new ObjectBox(1070, 490, 90, 90), ShapeKind.Heart, Red, null, 0);
        p.Add(GroupObject.Create(new[] { new GroupMember(g1, "Hexagon", 1), new GroupMember(g2, "Heart", 1) }), "A group");
        p.Caption("A group: it moves as one.\nDouble-click it to edit a single part.", 1085, 585, 14);
        p.Note("Try it on these shapes.\nThe Layers panel on the right lists every object.", 700, 640, 480, 62);
        return p.Finish();
    }

    private static ProjectPage TextPage(int total)
    {
        var p = new Page("Text", Paper);
        p.Header(4, total, "Text and designs", "Add words, then change font, color, spacing, curve and ready-made designs in Properties.", Purple);
        p.Steps(new[]
        {
            "Insert > Heading, Subheading or Body Text.\nDouble-click any text to change its words.",
            "Properties: font, size, bold, color, alignment,\nspace between letters, words and lines.",
            "Curve slider bends the text around a circle.",
            "Text designs: one click for shadow, outline,\nneon, 3D and gradient looks.",
        }, Purple, lineGap: 110);
        var samples = TextPreset.All.Take(8).ToList();
        for (int i = 0; i < samples.Count; i++)
        {
            double x = 690 + (i % 4) * 142, y = 150 + (i / 4) * 130;
            var aa = new TextObject(new ObjectBox(0, 0, 1, 1), "Aa", "Segoe UI", 64, Ink, Bold: true, Align: TextAlign.Center);
            aa = samples[i].ApplyTo(aa);
            var (w, h) = ObjectRendering.Renderer.MeasureText(aa);
            p.Add(aa with { Box = new ObjectBox(x + 60 - w / 2, y, w, h) }, "Design " + samples[i].Name);
            p.Caption(samples[i].Name, x + 60, y + h + 2, 13);
        }
        p.Text("Curved text", 930, 440, 44, Purple, bold: true, center: true, more: t => t with { Arc = 70 });
        p.Text("Bold and italic", 930, 548, 34, Ink, bold: true, center: true, more: t => t with { Italic = true });
        p.Text("Outlined", 930, 606, 50, White, bold: true, center: true, more: t => t with { OutlineWidth = 4, OutlineColor = Purple });
        return p.Finish();
    }

    private static ProjectPage ShapesPage(int total)
    {
        var p = new Page("Shapes", Paper);
        p.Header(5, total, "Shapes, lines and curves", "Insert > Shapes has dozens of shapes. Lines can bend, and the Curve tool draws your own.", Green);
        p.Steps(new[]
        {
            "Click a shape in the Insert tab, or open Shapes\nfor all of them. Change color and border in Properties.",
            "A line has a Bend slider (arc or wave) and arrow heads.\n'Edit as curve' lets you drag its points.",
            "Curve tool (P): click to place points, drag for\nsmooth bends. Double-click a point to switch it.",
            "Reshape corners (Properties) drags the corners of any\nshape: a square becomes a trapezium.",
        }, Green, lineGap: 110);
        var kinds = new[] { ShapeKind.Rectangle, ShapeKind.Ellipse, ShapeKind.Triangle, ShapeKind.Diamond, ShapeKind.Pentagon, ShapeKind.Hexagon, ShapeKind.Star, ShapeKind.Heart, ShapeKind.Arrow, ShapeKind.SpeechBubble, ShapeKind.Cloud, ShapeKind.Shield };
        var palette = new[] { Blue, Pink, Amber, Green, Purple, Orange, Red, Teal, Sky, Indigo, C(148, 163, 184), Slate };
        for (int i = 0; i < kinds.Length; i++)
            p.Shape(kinds[i].ToString(), kinds[i], 690 + (i % 4) * 135, 150 + (i / 4) * 120, 100, 92, palette[i], radius: kinds[i] == ShapeKind.Rectangle ? 16 : 0);
        p.Add(new ShapeObject(new ObjectBox(690, 520, 260, 8), ShapeKind.Line, null, Blue, 6, ArrowEnd: true, Bend: 0.5), "Bent arrow line");
        p.Add(new ShapeObject(new ObjectBox(690, 616, 260, 8), ShapeKind.Line, null, Orange, 5, Dash: StrokeStyle.Dashed), "Dashed line");
        p.Place(Graphics.Build("wave", W, H, 260), 820, 665, "Wavy line");
        var curve = new ShapeObject(new ObjectBox(990, 500, 220, 120), ShapeKind.Path, null, Pink, 6,
            Nodes: new[] { new CurveNode(new PointD(0, 0.9), new PointD(0, 0.9), new PointD(0.4, 0.9)), new CurveNode(new PointD(0.55, 0.45), new PointD(0.35, 0.45), new PointD(0.75, 0.45)), new CurveNode(new PointD(1, 0), new PointD(0.85, 0), new PointD(1, 0)) },
            ArrowEnd: true);
        p.Add(curve, "Curve");
        p.Caption("Bent line with an arrow", 820, 590, 13);
        p.Caption("A drawn curve", 1100, 636, 13);
        return p.Finish();
    }

    private static ProjectPage GraphicsPage(int total)
    {
        var p = new Page("Graphics and icons", Paper);
        p.Header(6, total, "Graphics and icons", "Insert > Graphics has 51 ready-made pieces, Icons has about 150 symbols.", Orange);
        p.Steps(new[]
        {
            "Insert > Graphics: lines, arrows, blobs, decorations,\nframes and patterns, in categories with a search box.",
            "Every graphic is a group of shapes. Ungroup it\n(Ctrl+Shift+G) to recolor or change single parts.",
            "Insert > Icons: each icon is text, so color, size,\noutline and shadow all work.",
        }, Orange, lineGap: 120);
        p.Place(Graphics.Build("blob3", W, H, 250), 800, 270, "Blob");
        p.Place(Graphics.Build("flower", W, H, 170), 800, 270, "Flower");
        p.Place(Graphics.Build("sparkles", W, H, 150), 1080, 230, "Sparkles");
        p.Place(Graphics.Build("confetti", W, H, 300), 1050, 400, "Confetti");
        p.Place(Graphics.Build("rings", W, H, 140), 690, 520, "Rings");
        p.Place(Graphics.Build("crown", W, H, 160), 860, 520, "Crown");
        p.Place(Graphics.Build("moon", W, H, 120), 1010, 520, "Moon and stars");
        p.Place(Graphics.Build("starsrow", W, H, 150), 1150, 520, "Stars");
        var glyphs = new[] { ("", Amber), ("", Blue), ("", Pink), ("", Teal), ("", Orange), ("", Red), ("", Green), ("", Amber) };
        for (int i = 0; i < glyphs.Length; i++)
            p.Text(glyphs[i].Item1, 700 + i * 70, 610, 46, glyphs[i].Item2, family: IconCatalog.FontFamily);
        p.Caption("Icons are text: change the color like any text", 920, 672, 13);
        return p.Finish();
    }

    private static ProjectPage BadgesPage(int total)
    {
        var p = new Page("Badges", Paper);
        p.Header(7, total, "Badges", "52 ready-made labels for sales, awards, social posts and info.", Red);
        p.Steps(new[]
        {
            "Insert > Badges. Pick a category on the left, or\ntype in the search box.",
            "The badge lands in the middle of the picture.\nDrag it where you want it.",
            "Ungroup (Ctrl+Shift+G), then double-click the text\nto write your own words and change the colors.",
        }, Red, lineGap: 125);
        var ids = new[] { "sale", "off", "bestchoice", "toprated", "freeship", "subscribe", "tip", "step1", "premium", "bogo", "live", "done" };
        for (int i = 0; i < ids.Length; i++)
            p.Place(Badges.Build(ids[i], W, H, 130), 735 + (i % 4) * 160, 215 + (i / 4) * 175, ids[i]);
        return p.Finish();
    }

    private static ProjectPage ChartsPage(int total)
    {
        var p = new Page("Charts", Paper);
        p.Header(8, total, "Charts", "Charts stay editable: change the numbers, type and colors at any time.", Teal);
        p.Steps(new[]
        {
            "Insert > Chart and pick a type: columns, bars, line,\npie, donut, area, lollipop, half donut, radial, progress, funnel.",
            "Select the chart. In Properties > Chart type your own\nnumbers, one per line, like \"Jan, 12\".",
            "Change type, colors (ready-made palettes), what is\nshown and the size of the text.",
        }, Teal, lineGap: 120);
        p.Add(ChartObject.Sample(new ObjectBox(650, 140, 290, 230), ChartKind.Columns) with { Title = "Columns" }, "Columns chart");
        p.Add(ChartObject.Sample(new ObjectBox(950, 140, 290, 230), ChartKind.Donut) with { Title = "Donut" }, "Donut chart");
        p.Add(ChartObject.Sample(new ObjectBox(650, 390, 290, 290), ChartKind.Progress) with
        {
            Title = "Progress", ValueSuffix = "%", ShowGrid = false,
            Items = new[] { new ChartItem("Design", 85), new ChartItem("Code", 64), new ChartItem("Docs", 48), new ChartItem("Tests", 72) },
        }, "Progress chart");
        p.Add(ChartObject.Sample(new ObjectBox(950, 390, 290, 290), ChartKind.Area) with { Title = "Area" }, "Area chart");
        return p.Finish();
    }

    private static ProjectPage SmartArtPage(int total)
    {
        var p = new Page("SmartArt", Paper);
        p.Header(9, total, "SmartArt diagrams", "Type your words, get a diagram. Like the SmartArt of Office.", Indigo);
        p.Steps(new[]
        {
            "Insert > SmartArt. Pick a layout: processes, lists,\ncycles, hierarchies, Venn, funnel, matrix, pyramid.",
            "Type your words, one per line, and choose a color set.",
            "The diagram is a group: ungroup it to move, recolor\nor retype single parts.",
        }, Indigo, lineGap: 120);
        p.Place(SmartArt.Build("process", new[] { "Plan", "Design", "Build", "Launch" }, "Colorful", W, H, 290), 800, 215, "Process");
        p.Place(SmartArt.Build("cycle", new[] { "Plan", "Do", "Check", "Act" }, "Blue", W, H, 230), 1075, 245, "Cycle");
        p.Place(SmartArt.Build("org", new[] { "Team lead", "Design", "Sales", "Support" }, "Warm", W, H, 320), 810, 500, "Organization chart");
        p.Place(SmartArt.Build("pyramid", new[] { "Vision", "Goals", "Tasks" }, "Purple", W, H, 250), 1100, 500, "Pyramid");
        return p.Finish();
    }

    private static ProjectPage PhotosPage(int total)
    {
        var p = new Page("Photos and frames", Paper);
        p.Header(10, total, "Photos and frames", "Put a picture inside any shape. The picture is part of the frame.", Pink);
        p.Steps(new[]
        {
            "Insert > Frame adds an empty frame. Select it and\nchoose Insert > Fill Frames to pick your picture.",
            "Photo Grid makes several frames in a collage layout.",
            "Double-click a filled frame, then drag to move the picture\nand turn the mouse wheel to zoom it. Esc when done.",
            "Properties > Picture in frame has zoom and position.\nMoving the frame takes the picture along.",
        }, Pink, lineGap: 105);
        p.Shape("Circle frame", ShapeKind.Ellipse, 690, 150, 230, 230, C(226, 232, 240), more: s => s with { Picture = new PictureFill(Photo(), "Face", Zoom: 2.2, PanX: 0, PanY: 1), Shadow = ShadowStyle.Default });
        p.Shape("Rounded frame", ShapeKind.Rectangle, 950, 150, 270, 200, C(226, 232, 240), radius: 28, more: s => s with { Picture = new PictureFill(Photo(), "Whole photo"), Shadow = ShadowStyle.Default });
        p.Shape("Heart frame", ShapeKind.Heart, 690, 420, 230, 210, C(226, 232, 240), more: s => s with { Picture = new PictureFill(Photo(), "Plants", Zoom: 2.4, PanX: -1, PanY: 0.1) });
        p.Shape("Photo", ShapeKind.Rectangle, 950, 400, 270, 200, C(226, 232, 240), White, 10, 4, 4, more: s => s with { Picture = new PictureFill(Photo(), "T-shirt", Zoom: 2.0, PanX: 0, PanY: -0.1), Shadow = new ShadowStyle(C(0, 0, 0, 90), 10, 60, 16) });
        p.Caption("Frames can be any shape", 805, 645, 14);
        p.Caption("With a border and a shadow", 1085, 645, 14);
        return p.Finish();
    }

    private static ProjectPage EffectsPage(int total)
    {
        var p = new Page("Effects", Paper);
        p.Header(11, total, "Shadows, glow and 3D", "Every object can get effects. They are all in Properties and all editable.", Amber);
        p.Steps(new[]
        {
            "Select an object and open the effect sections in\nProperties: Shadow, Glow, Outline, Inner shadow, 3D.",
            "Gradient (shapes and text) fades through several colors.",
            "Adjust (blur, tone) changes how an object looks without\nchanging the picture inside it.",
            "Turn an effect off again at any time: nothing is baked in.",
        }, Amber, lineGap: 105);
        var tiles = new (string Label, Func<ShapeObject, ShapeObject> Effect)[]
        {
            ("Shadow", s => s with { Shadow = new ShadowStyle(C(0, 0, 0, 120), 14, 55, 18) }),
            ("Glow", s => s with { Glow = new GlowStyle(C(255, 214, 90, 235), 26) }),
            ("Outline", s => s with { Outline = new OutlineStyle(White, 8), Shadow = ShadowStyle.Default }),
            ("Inner shadow", s => s with { InnerShadow = new InnerShadowStyle(C(0, 0, 0, 170), 8, 45, 12) }),
            ("3D", s => s with { ThreeD = new ThreeDStyle(26, 45, C(30, 58, 138), 0.5) }),
            ("Gradient", s => s with { Gradient = GradientStyle.Two(C(244, 114, 182), C(79, 70, 229), 45) }),
        };
        for (int i = 0; i < tiles.Length; i++)
        {
            double x = 690 + (i % 3) * 190, y = 170 + (i / 3) * 250;
            p.Shape(tiles[i].Label, ShapeKind.Rectangle, x, y, 140, 140, Blue, radius: 30, more: tiles[i].Effect);
            p.Caption(tiles[i].Label, x + 70, y + 178, 16);
        }
        return p.Finish();
    }

    private static ProjectPage CodesPage(int total)
    {
        var p = new Page("QR codes and barcodes", Paper);
        p.Header(12, total, "QR codes and barcodes", "Real codes that stay editable. Scan them with your phone to test.", Slate);
        p.Steps(new[]
        {
            "Insert > QR Code: type a web address, phone number\nor any text. Change colors and dot style in Properties.",
            "Insert > Barcode: Code 128, EAN-13, UPC-A, EAN-8 or\nCode 39. Wrong digits show a clear message.",
            "Keep the colors high-contrast (dark on light) and\ntest with a scanner before printing.",
        }, Slate, lineGap: 125);
        p.Add(new QrCodeObject(new ObjectBox(700, 160, 230, 230), "https://example.com", Ink, White, QrLevel.Medium), "QR code");
        p.Add(new QrCodeObject(new ObjectBox(980, 160, 230, 230), "Hello from Flash Editor", Indigo, C(238, 242, 255), QrLevel.High, QrStyle.Dots), "Dotted QR code");
        p.Caption("A web address", 815, 396);
        p.Caption("Dots, with a color", 1095, 396);
        p.Add(new BarcodeObject(new ObjectBox(700, 470, 250, 110), "590123412345", BarcodeKind.Ean13, Ink, White), "EAN-13 barcode");
        p.Add(new BarcodeObject(new ObjectBox(980, 470, 230, 110), "FLASH-12345", BarcodeKind.Code128, Ink, White), "Code 128 barcode");
        p.Caption("EAN-13 (shop products)", 825, 590);
        p.Caption("Code 128 (any text)", 1095, 590);
        return p.Finish();
    }

    private static ProjectPage WrapUp(int total)
    {
        var p = new Page("Layers, pages and export", Paper);
        p.Header(13, total, "Layers, pages and export", "The last lesson: keep things tidy, work with many pages, and save your result.", Blue);
        p.Steps(new[]
        {
            "Layers panel: drag to reorder, eye hides, lock protects.\nLayer tab has masks, clip, adjustment layers and groups.",
            "Pages (under the canvas): + adds one, Ctrl+click picks\nseveral, Ctrl+C / V copies pages, even into another project.",
            "File > Export: picture (PNG, JPG, WEBP), ready-made\nsizes for social media, PDF, animated GIF, SVG, PSD.",
            "Ctrl+S saves the whole project; everything stays editable.\nF1 lists all keyboard shortcuts, and you can change them.",
        }, Blue, lineGap: 112);
        p.Shape("Back layer", ShapeKind.Rectangle, 700, 170, 300, 200, Sky, radius: 24);
        p.Shape("Middle layer", ShapeKind.Ellipse, 850, 240, 230, 230, Pink, more: s => s with { Shadow = ShadowStyle.Default });
        p.Shape("Front layer", ShapeKind.Star, 760, 330, 170, 170, Amber, rotation: 12, more: s => s with { Shadow = ShadowStyle.Default });
        p.Caption("Three layers: the last one added is on top.", 890, 520, 14);
        p.Place(SmartArt.Build("steps", new[] { "Make", "Check", "Export" }, "Colorful", W, H, 250), 1010, 622, "Steps");
        p.Place(Badges.Build("done", W, H, 120), 1150, 180, "Done badge");
        p.Text("You are ready. Have fun!", 56, 660, 30, Blue, bold: true);
        return p.Finish();
    }
}
