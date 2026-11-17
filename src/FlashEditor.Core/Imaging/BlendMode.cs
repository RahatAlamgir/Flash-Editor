using System.Text.Json.Serialization;

namespace FlashEditor.Core.Imaging;

/// <summary>How a layer mixes with what is below it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BlendMode
{
    Normal,
    Multiply,
    Screen,
    Overlay,
    SoftLight,
    HardLight,
    Darken,
    Lighten,
    Difference,
}

public static class BlendModes
{
    public static IReadOnlyList<BlendMode> All { get; } = Enum.GetValues<BlendMode>();

    /// <summary>Name for menus ("Soft light").</summary>
    public static string NameOf(BlendMode mode) => mode switch
    {
        BlendMode.SoftLight => "Soft light",
        BlendMode.HardLight => "Hard light",
        _ => mode.ToString(),
    };

    public static IReadOnlyList<string> Names { get; } = All.Select(NameOf).ToArray();

    /// <summary>What the mode does, in plain words (tooltip).</summary>
    public static string Describe(BlendMode mode) => mode switch
    {
        BlendMode.Multiply => "Darkens: the layer's colors multiply with what is below (good for shadows)",
        BlendMode.Screen => "Lightens: like projecting light on top (good for glows)",
        BlendMode.Overlay => "More contrast: darks get darker and lights lighter, based on what is below",
        BlendMode.SoftLight => "A gentle version of Overlay",
        BlendMode.HardLight => "A strong version of Overlay, based on the layer itself",
        BlendMode.Darken => "Keeps the darker color of the two",
        BlendMode.Lighten => "Keeps the lighter color of the two",
        BlendMode.Difference => "Shows how different the two colors are (inverts where the layer is white)",
        _ => "The layer simply covers what is below",
    };
}
