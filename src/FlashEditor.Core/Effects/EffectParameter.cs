namespace FlashEditor.Core.Effects;

public enum ParameterKind
{
    /// <summary>Number chosen with a slider.</summary>
    Slider,
    /// <summary>On/off checkbox; value is 0 or 1.</summary>
    Toggle,
}

/// <summary>
/// Describes one setting of an effect. The UI builds its dialog (sliders/checkboxes) from these,
/// so adding a new effect never needs new dialog code.
/// </summary>
public sealed record EffectParameter(
    string Key,
    string Label,
    double Min,
    double Max,
    double Default,
    double Step = 1,
    string Unit = "",
    ParameterKind Kind = ParameterKind.Slider)
{
    public static EffectParameter Toggle(string key, string label, bool defaultOn)
        => new(key, label, 0, 1, defaultOn ? 1 : 0, 1, "", ParameterKind.Toggle);
}
