using FlashEditor.Core.Common;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Retouch;

namespace FlashEditor.App.Tools.Options;

/// <summary>What the options bar shows for a retouching tool.</summary>
[Flags]
public enum RetouchParts
{
    None = 0,
    Strength = 1,      // how strong a stroke is
    Aligned = 2,       // keep the copy distance between strokes (Clone Stamp, Healing Brush)
    DodgeBurn = 4,     // Dodge / Burn and the brightness range
    BlurSharpen = 8,   // Blur / Sharpen
    Hardness = 16,
}

/// <summary>Settings of the retouching tools (clone, heal, dodge/burn, blur/sharpen). Each tool shows only the parts it uses.</summary>
public sealed class RetouchOptions : ObservableObject
{
    private double _size = 30;
    private double _hardness = 60;
    private double _strength;
    private bool _aligned = true;
    private bool _burn;
    private bool _sharpen;
    private ToneRange _range = ToneRange.Midtones;

    public RetouchOptions(RetouchParts parts, double strength = 100)
    {
        Parts = parts;
        _strength = strength;
    }

    public RetouchParts Parts { get; }
    public bool HasStrength => Parts.HasFlag(RetouchParts.Strength);
    public bool HasAligned => Parts.HasFlag(RetouchParts.Aligned);
    public bool HasDodgeBurn => Parts.HasFlag(RetouchParts.DodgeBurn);
    public bool HasBlurSharpen => Parts.HasFlag(RetouchParts.BlurSharpen);
    public bool HasHardness => Parts.HasFlag(RetouchParts.Hardness);

    /// <summary>Spot Healing has no settings besides the brush.</summary>
    public bool IsSpot => Parts == RetouchParts.Hardness;

    /// <summary>Text for the strength slider ("Opacity", "Exposure", "Strength").</summary>
    public string StrengthLabel => HasDodgeBurn ? "Exposure" : HasBlurSharpen ? "Strength" : "Opacity";

    public double Size { get => _size; set => SetField(ref _size, Math.Clamp(Math.Round(value), 1, 1000)); }
    public double Hardness { get => _hardness; set => SetField(ref _hardness, Math.Clamp(Math.Round(value), 0, 100)); }
    public double Strength { get => _strength; set => SetField(ref _strength, Math.Clamp(Math.Round(value), 1, 100)); }

    /// <summary>On: strokes keep the same distance to the copied place; off: every stroke starts at the copied place again.</summary>
    public bool Aligned { get => _aligned; set => SetField(ref _aligned, value); }

    public bool Burn
    {
        get => _burn;
        set { if (SetField(ref _burn, value)) OnPropertyChanged(nameof(Dodge)); }
    }
    public bool Dodge { get => !_burn; set => Burn = !value; }

    public bool Sharpen
    {
        get => _sharpen;
        set { if (SetField(ref _sharpen, value)) OnPropertyChanged(nameof(Blur)); }
    }
    public bool Blur { get => !_sharpen; set => Sharpen = !value; }

    public ToneRange Range { get => _range; set => SetField(ref _range, value); }
    public IReadOnlyList<ToneRange> Ranges { get; } = Enum.GetValues<ToneRange>();

    /// <summary>Brush size and edge. The strength of a stroke is chosen by the tool (see <paramref name="opacity"/>).</summary>
    public BrushSettings ToSettings(double opacity) => new(Size, Hardness / 100.0, Math.Clamp(opacity, 0.01, 1.0));

    public void Grow() => Size += Size < 10 ? 1 : Size < 50 ? 5 : Size < 200 ? 10 : 50;
    public void Shrink() => Size -= Size <= 10 ? 1 : Size <= 50 ? 5 : Size <= 200 ? 10 : 50;
}
