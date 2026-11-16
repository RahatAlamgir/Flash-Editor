using FlashEditor.Core.Common;

namespace FlashEditor.App.Tools.Options;

/// <summary>Settings for the Magic Wand.</summary>
public sealed class MagicWandOptions : ObservableObject
{
    private double _tolerance = 32;
    private bool _contiguous = true;
    private bool _sampleAllLayers = true;

    /// <summary>How different a color may be and still be selected (0..255).</summary>
    public double Tolerance { get => _tolerance; set => SetField(ref _tolerance, Math.Clamp(Math.Round(value), 0, 255)); }

    /// <summary>Only connected pixels (off = every similar pixel in the picture).</summary>
    public bool Contiguous { get => _contiguous; set => SetField(ref _contiguous, value); }

    /// <summary>Look at the whole visible picture instead of only the selected layer.</summary>
    public bool SampleAllLayers { get => _sampleAllLayers; set => SetField(ref _sampleAllLayers, value); }
}
