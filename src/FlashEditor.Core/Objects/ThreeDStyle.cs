using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// Makes an object look three-dimensional: it gets a solid side (an extrusion, like a block or a 3D letter) that runs away from it
/// in one direction, and optionally a bevel (a lit top-left edge and a shaded bottom-right edge, as if the face were raised).
/// Like a shadow it follows the real outline, and its direction is on the canvas: turning the object does not turn the side.
/// </summary>
/// <param name="Depth">How deep the side is, in pixels (0 = no side, only the bevel).</param>
/// <param name="Angle">Direction the side runs: 0 = right, 90 = down, 180 = left, -90 = up.</param>
/// <param name="Color">Color of the side, or null to take the object's own color, darker.</param>
/// <param name="Bevel">0..1, how strong the lit and shaded edges are (0 = none).</param>
public sealed record ThreeDStyle(double Depth = 20, double Angle = 45, ColorRgba? Color = null, double Bevel = 0.5)
{
    /// <summary>What "3D" starts with: a medium side going to the bottom right and a gentle bevel.</summary>
    public static ThreeDStyle Default { get; } = new();

    public double OffsetX => Depth * Math.Cos(Angle * Math.PI / 180);
    public double OffsetY => Depth * Math.Sin(Angle * Math.PI / 180);

    /// <summary>How far the effect can reach beyond the object's outline, in pixels.</summary>
    public double Extent => Math.Max(0, Depth) + 2;

    /// <summary>The same effect for an object that was scaled by <paramref name="factor"/>.</summary>
    public ThreeDStyle Scaled(double factor) => this with { Depth = Depth * factor };

    /// <summary>The same effect after the whole picture was turned by <paramref name="degrees"/> (clockwise).</summary>
    public ThreeDStyle Rotated(double degrees) => this with { Angle = ObjectBox.NormalizeAngle(Angle + degrees) };
}

/// <summary>A ready-made 3D look (one click in the Properties panel).</summary>
public sealed record ThreeDPreset(string Name, string Description, double Depth, double Angle, double Bevel)
{
    public static ThreeDPreset Block { get; } = new("Block", "A solid block going to the bottom right", 24, 45, 0.35);
    public static ThreeDPreset Long { get; } = new("Long", "A deep side stretching far away", 70, 45, 0.2);
    public static ThreeDPreset Pop { get; } = new("Pop", "Raised face with shiny edges, very little depth", 6, 90, 0.9);
    public static ThreeDPreset Floor { get; } = new("Floor", "A thick side going straight down, like a button", 18, 90, 0.5);
    public static ThreeDPreset Left { get; } = new("Left", "A block going to the bottom left", 24, 135, 0.35);

    public static IReadOnlyList<ThreeDPreset> All { get; } = new[] { Block, Long, Pop, Floor, Left };

    /// <summary>The effect this preset gives; the color the object's 3D effect already has is kept.</summary>
    public ThreeDStyle CreateStyle(ThreeDStyle? current = null) => new(Depth, Angle, current?.Color, Bevel);
}
