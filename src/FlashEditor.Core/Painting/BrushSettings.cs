namespace FlashEditor.Core.Painting;

/// <summary>Brush shape and strength.</summary>
/// <param name="Size">Diameter in pixels (1..1000).</param>
/// <param name="Hardness">0 = very soft edge, 1 = crisp edge.</param>
/// <param name="Opacity">0..1, maximum strength of one stroke.</param>
public readonly record struct BrushSettings(double Size, double Hardness, double Opacity)
{
    public double Radius => Math.Max(0.5, Size / 2.0);
}
