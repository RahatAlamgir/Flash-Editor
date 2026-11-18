using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// A ready-made text design: font, color or gradient and effects (shadow, glow, outline, 3D...) in one click. It never changes the words,
/// the place, the rotation or the alignment. Effects are made for text of about 64 px and are scaled to the size of the text they go on.
/// </summary>
public sealed record TextPreset(
    string Name,
    string Description,
    string Font,
    ColorRgba Color,
    bool Bold = false,
    bool Italic = false,
    GradientStyle? Gradient = null,
    ShadowStyle? Shadow = null,
    GlowStyle? Glow = null,
    InnerShadowStyle? InnerShadow = null,
    OutlineStyle? Outline = null,
    ThreeDStyle? ThreeD = null,
    double LetterOutline = 0,
    ColorRgba? LetterOutlineColor = null,
    ColorRgba? PreviewBackground = null)
{
    /// <summary>The size the effects were made for.</summary>
    public const double ReferenceSize = 64;

    /// <summary>The text with this design (words, box, alignment and flips stay as they were).</summary>
    public TextObject ApplyTo(TextObject text)
    {
        double k = Math.Max(0.1, text.FontSize / ReferenceSize);
        return text with
        {
            FontFamily = Font,
            Color = Color,
            Bold = Bold,
            Italic = Italic,
            Gradient = Gradient,
            Shadow = Shadow?.Scaled(k),
            Glow = Glow?.Scaled(k),
            InnerShadow = InnerShadow?.Scaled(k),
            Outline = Outline?.Scaled(k),
            ThreeD = ThreeD?.Scaled(k),
            OutlineWidth = LetterOutline * k,
            OutlineColor = LetterOutline > 0 ? LetterOutlineColor ?? ColorRgba.Black : null,
        };
    }

    private static ColorRgba C(int r, int g, int b, int a = 255) => new((byte)r, (byte)g, (byte)b, (byte)a);

    public static IReadOnlyList<TextPreset> All { get; } = new[]
    {
        new TextPreset("Clean", "Plain dark title, no effects", "Segoe UI", C(31, 41, 55), Bold: true),
        new TextPreset("Impact", "Big white letters with a black edge, like a meme", "Impact", C(255, 255, 255),
            LetterOutline: 4, LetterOutlineColor: C(0, 0, 0), PreviewBackground: C(110, 120, 140)),
        new TextPreset("Neon Pink", "Glowing pink neon sign", "Bahnschrift", C(255, 190, 230), Bold: true,
            Glow: new GlowStyle(C(255, 40, 170, 255), 18, 140), PreviewBackground: C(24, 18, 40)),
        new TextPreset("Neon Blue", "Glowing cyan neon sign", "Bahnschrift", C(200, 250, 255), Bold: true,
            Glow: new GlowStyle(C(0, 200, 255, 255), 18, 140), PreviewBackground: C(14, 20, 40)),
        new TextPreset("Retro 3D", "Chunky yellow letters with a red side", "Arial Black", C(255, 214, 0),
            ThreeD: new ThreeDStyle(9, 45, C(214, 62, 40), 0.3), Shadow: new ShadowStyle(C(0, 0, 0, 90), 14, 45, 6)),
        new TextPreset("Sticker", "White letters, thick dark edge and a hard shadow", "Arial Black", C(255, 255, 255),
            Outline: new OutlineStyle(C(30, 41, 90), 7), Shadow: new ShadowStyle(C(0, 0, 0, 255), 6, 45, 0), PreviewBackground: C(255, 200, 80)),
        new TextPreset("Sunset", "Pink to orange fade", "Segoe UI Black", C(255, 120, 100),
            Gradient: GradientStyle.Evenly(new[] { C(255, 70, 110), C(255, 140, 70), C(255, 205, 100) }, 0),
            Shadow: new ShadowStyle(C(120, 30, 40, 70), 5, 90, 10)),
        new TextPreset("Ocean", "Cool blue fade", "Segoe UI Black", C(0, 150, 255),
            Gradient: GradientStyle.Two(C(0, 220, 255), C(30, 80, 230), 90), Shadow: new ShadowStyle(C(10, 30, 90, 70), 5, 90, 10)),
        new TextPreset("Gold", "Shiny gold lettering", "Georgia", C(212, 160, 40), Bold: true,
            Gradient: GradientStyle.Evenly(new[] { C(255, 240, 170), C(214, 160, 40), C(255, 235, 150), C(176, 120, 20) }, 90),
            Shadow: new ShadowStyle(C(0, 0, 0, 110), 4, 90, 6), PreviewBackground: C(40, 34, 30)),
        new TextPreset("Fire", "Flames from yellow to red with an orange glow", "Impact", C(255, 120, 0),
            Gradient: GradientStyle.Evenly(new[] { C(255, 245, 120), C(255, 140, 0), C(200, 20, 0) }, 90),
            Glow: new GlowStyle(C(255, 90, 0, 200), 12, 110), PreviewBackground: C(30, 20, 20)),
        new TextPreset("Elegant", "Slim serif italics with a soft shadow", "Georgia", C(70, 45, 35), Italic: true,
            Shadow: new ShadowStyle(C(0, 0, 0, 55), 3, 90, 6)),
        new TextPreset("Script", "Flowing script handwriting", "Segoe Script", C(40, 40, 60), Bold: true),
        new TextPreset("Comic", "Yellow comic letters with a black edge", "Comic Sans MS", C(255, 232, 40), Bold: true,
            LetterOutline: 3.5, LetterOutlineColor: C(0, 0, 0), Shadow: new ShadowStyle(C(0, 0, 0, 255), 5, 45, 0), PreviewBackground: C(80, 110, 200)),
        new TextPreset("Long Shadow", "White letters with a long, flat shadow", "Segoe UI Black", C(255, 255, 255),
            Shadow: new ShadowStyle(C(20, 30, 60, 110), 38, 45, 0), PreviewBackground: C(90, 130, 230)),
        new TextPreset("Letterpress", "Pressed into the paper", "Segoe UI", C(150, 150, 156), Bold: true,
            InnerShadow: new InnerShadowStyle(C(0, 0, 0, 160), 3, 90, 3), Shadow: new ShadowStyle(C(255, 255, 255, 230), 2, 90, 0)),
        new TextPreset("Terminal", "Green computer-screen text", "Consolas", C(90, 255, 140), Bold: true,
            Glow: new GlowStyle(C(40, 255, 110, 190), 8, 100), PreviewBackground: C(10, 20, 14)),
    };
}
