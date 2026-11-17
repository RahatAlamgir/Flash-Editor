using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>A ready-made look for a drop shadow (one click in the Properties panel).</summary>
/// <param name="Opacity">How dark the shadow is, 0..255.</param>
/// <param name="Color">
/// A color the preset always uses (a glow is blue, a neon shadow pink). Null = the preset keeps the color the object's
/// shadow already has (black when it has none yet).
/// </param>
public sealed record ShadowPreset(string Name, string Description, double Distance, double Angle, double Blur, byte Opacity, ColorRgba? Color = null)
{
    /// <summary>Soft, wide and light: the usual gentle shadow under a card or a button.</summary>
    public static ShadowPreset Soft { get; } = new("Soft", "Gentle, wide shadow under the object", Distance: 8, Angle: 90, Blur: 24, Opacity: 90);

    /// <summary>Sharp-edged and offset: a graphic, "sticker" look.</summary>
    public static ShadowPreset Hard { get; } = new("Hard", "Crisp shadow with no blur, offset to the bottom right", Distance: 10, Angle: 45, Blur: 0, Opacity: 190);

    /// <summary>Far away and very blurry: the object seems to float above the page.</summary>
    public static ShadowPreset Lifted { get; } = new("Lifted", "Big, faint shadow far below: the object floats", Distance: 30, Angle: 90, Blur: 44, Opacity: 80);

    /// <summary>Barely there: just enough to separate the object from the page.</summary>
    public static ShadowPreset Subtle { get; } = new("Subtle", "Very light, small shadow", Distance: 3, Angle: 90, Blur: 8, Opacity: 60);

    /// <summary>Tight and dark right under the object, as if it lay on the surface.</summary>
    public static ShadowPreset Contact { get; } = new("Contact", "Small, tight shadow: the object lies on the page", Distance: 2, Angle: 90, Blur: 4, Opacity: 130);

    /// <summary>Solid black and offset: a bold comic / sticker look.</summary>
    public static ShadowPreset Sticker { get; } = new("Sticker", "Solid black offset shadow, comic style", Distance: 7, Angle: 45, Blur: 0, Opacity: 255);

    /// <summary>A long, straight shadow stretching away at an angle.</summary>
    public static ShadowPreset Long { get; } = new("Long", "Long, hard shadow stretching away", Distance: 44, Angle: 45, Blur: 0, Opacity: 70);

    /// <summary>A soft colored halo around the object (no offset).</summary>
    public static ShadowPreset Glow { get; } = new("Glow", "Soft blue halo around the object", Distance: 0, Angle: 0, Blur: 34, Opacity: 210, Color: new ColorRgba(70, 150, 255));

    /// <summary>A tight, bright halo like a neon sign.</summary>
    public static ShadowPreset Neon { get; } = new("Neon", "Bright pink neon glow", Distance: 0, Angle: 0, Blur: 16, Opacity: 255, Color: new ColorRgba(255, 60, 170));

    /// <summary>Falls to the lower left instead of the right.</summary>
    public static ShadowPreset Left { get; } = new("Left", "Shadow falling to the bottom left", Distance: 14, Angle: 135, Blur: 16, Opacity: 110);

    public static IReadOnlyList<ShadowPreset> All { get; } = new[]
    {
        Soft, Hard, Lifted, Subtle, Contact, Sticker, Long, Glow, Neon, Left,
    };

    /// <summary>
    /// The shadow this preset gives. A preset without its own color keeps the color of an existing shadow (only the
    /// strength comes from the preset), so a colored shadow stays colored.
    /// </summary>
    public ShadowStyle CreateStyle(ShadowStyle? current = null)
    {
        var color = (Color ?? current?.Color ?? ColorRgba.Black) with { A = Opacity };
        return new ShadowStyle(color, Distance, Angle, Blur);
    }
}
