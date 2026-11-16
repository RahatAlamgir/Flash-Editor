using FlashEditor.Core.Common;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Tools.Options;

/// <summary>Settings for the Brush and Eraser tools.</summary>
public sealed class BrushOptions : ObservableObject
{
    private double _size = 20;
    private double _hardness = 80;
    private double _opacity = 100;

    /// <summary>Diameter in pixels.</summary>
    public double Size { get => _size; set => SetField(ref _size, Math.Clamp(Math.Round(value), 1, 1000)); }

    /// <summary>0..100 %</summary>
    public double Hardness { get => _hardness; set => SetField(ref _hardness, Math.Clamp(Math.Round(value), 0, 100)); }

    /// <summary>1..100 %</summary>
    public double Opacity { get => _opacity; set => SetField(ref _opacity, Math.Clamp(Math.Round(value), 1, 100)); }

    public BrushSettings ToSettings() => new(Size, Hardness / 100.0, Opacity / 100.0);

    /// <summary>"[" and "]" keys: change size in sensible steps.</summary>
    public void Grow() => Size += Size < 10 ? 1 : Size < 50 ? 5 : Size < 200 ? 10 : 50;
    public void Shrink() => Size -= Size <= 10 ? 1 : Size <= 50 ? 5 : Size <= 200 ? 10 : 50;
}
