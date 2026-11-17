using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.Services;

/// <summary>
/// Ready-made starting points for a new picture: a background and a few editable shapes and texts, laid out for any canvas size.
/// Everything stays editable (change the words, colors, gradient...).
/// </summary>
public static class DocumentTemplates
{
    public const string Blank = "Blank";

    public static IReadOnlyList<string> Names { get; } = new[] { Blank, "Title slide", "Quote card", "Sale poster", "Photo with caption", "Announcement" };

    /// <summary>Adds the template's layers to a new document (a blank one with the background already in place). Unknown names do nothing.</summary>
    public static void Apply(Document document, string? template)
    {
        if (string.IsNullOrEmpty(template) || template == Blank) return;
        var layers = new List<Layer>(document.Layers);
        double w = document.Width, h = document.Height, s = Math.Min(w, h);

        void Add(LayerObject obj, string name)
        {
            var layer = new Layer(name, document.Width, document.Height);
            layer.SetObject(obj);
            layers.Add(layer);
        }

        // A text centered at (cx, cy); its box is fitted to the text.
        TextObject Text(string text, double cx, double cy, double size, ColorRgba color, bool bold = false, TextAlign align = TextAlign.Center)
        {
            var t = new TextObject(new ObjectBox(0, 0, 10, 10), text, "Segoe UI", size, color, Bold: bold, Align: align);
            var (tw, th) = ObjectRendering.Renderer.MeasureText(t);
            return t with { Box = ObjectBox.FromCenter(new PointD(cx, cy), tw, th) };
        }
        ShapeObject Rect(double x, double y, double rw, double rh, ColorRgba fill, double radius = 0)
            => new(new ObjectBox(x, y, rw, rh), ShapeKind.Rectangle, fill, null, 0, CornerRadius: radius);
        ShapeObject Background(GradientPreset look)
            => Rect(0, 0, w, h, look.Colors[0]) with { Gradient = look.CreateStyle() };

        var white = new ColorRgba(255, 255, 255);
        var dark = new ColorRgba(30, 41, 59);
        var shadow = new ShadowStyle(new ColorRgba(0, 0, 0, 90), s * 0.01, 90, s * 0.02);

        switch (template)
        {
            case "Title slide":
                Add(Background(GradientPreset.Ocean), "Background");
                Add(Text("Your Title Here", w / 2, h * 0.44, s * 0.11, white, bold: true) with { Shadow = shadow }, "Title");
                Add(Rect(w / 2 - w * 0.08, h * 0.54, w * 0.16, Math.Max(3, s * 0.008), white), "Line");
                Add(Text("A short line that explains it", w / 2, h * 0.62, s * 0.045, new ColorRgba(235, 245, 255)), "Subtitle");
                break;

            case "Quote card":
                Add(Background(GradientPreset.Night), "Background");
                Add(Text("“", w / 2, h * 0.25, s * 0.28, new ColorRgba(255, 255, 255, 120), bold: true), "Quote mark");
                Add(Text("Write a short quote here,\nin your own words.", w / 2, h * 0.5, s * 0.07, white, bold: true), "Quote");
                Add(Text("— Name", w / 2, h * 0.76, s * 0.04, new ColorRgba(200, 205, 255)), "Author");
                break;

            case "Sale poster":
                Add(Background(GradientPreset.Fire), "Background");
                Add((Text("SALE", w / 2, h * 0.38, s * 0.26, white, bold: true) with
                {
                    Shadow = shadow,
                    ThreeD = new ThreeDStyle(s * 0.025, 90, new ColorRgba(120, 20, 20), 0.5),
                }), "Sale");
                Add(Text("UP TO 50% OFF", w / 2, h * 0.62, s * 0.07, white, bold: true), "Offer");
                Add(Rect(w * 0.25, h * 0.74, w * 0.5, s * 0.1, white, s * 0.05), "Button");
                Add(Text("Shop now", w / 2, h * 0.74 + s * 0.05, s * 0.045, new ColorRgba(200, 30, 30), bold: true), "Button text");
                break;

            case "Photo with caption":
            {
                Add(Rect(0, 0, w, h, white), "Background");
                double pw = w * 0.8, ph = h * 0.62, px = (w - pw) / 2, py = h * 0.08;
                Add(Rect(px, py, pw, ph, new ColorRgba(226, 232, 240), s * 0.02) with { Shadow = shadow }, "Photo (place your picture here)");
                Add(Text("Caption goes here", w / 2, py + ph + h * 0.1, s * 0.055, dark, bold: true), "Caption");
                Add(Text("A few words about the picture", w / 2, py + ph + h * 0.19, s * 0.035, new ColorRgba(100, 116, 139)), "Details");
                break;
            }

            case "Announcement":
            {
                Add(Background(GradientPreset.Candy), "Background");
                Add(Rect(w * 0.1, h * 0.14, w * 0.8, h * 0.72, new ColorRgba(255, 255, 255, 235), s * 0.04) with { Shadow = shadow }, "Card");
                Add(Text("BIG NEWS", w / 2, h * 0.34, s * 0.1, new ColorRgba(147, 51, 234), bold: true), "Headline");
                Add(Text("Tell people what is happening,\nwhen and where.", w / 2, h * 0.54, s * 0.045, dark), "Text");
                Add(Text("Date  •  Place", w / 2, h * 0.7, s * 0.04, new ColorRgba(100, 116, 139), bold: true), "Details");
                break;
            }

            default:
                return;
        }

        document.SetLayers(layers, layers[^1]);
    }
}
