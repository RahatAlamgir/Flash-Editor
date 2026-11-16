using FlashEditor.Core.Common;

namespace FlashEditor.App.Tools.Options;

/// <summary>Settings for the Quick Select brush.</summary>
public sealed class QuickSelectOptions : ObservableObject
{
    private double _size = 30;
    private double _tolerance = 32;
    private bool _sampleAllLayers = true;

    /// <summary>Brush diameter in image pixels.</summary>
    public double Size { get => _size; set => SetField(ref _size, Math.Clamp(Math.Round(value), 2, 500)); }

    /// <summary>How different a color may be and still be selected (0..255).</summary>
    public double Tolerance { get => _tolerance; set => SetField(ref _tolerance, Math.Clamp(Math.Round(value), 1, 255)); }

    /// <summary>Look at the whole visible picture instead of only the selected layer.</summary>
    public bool SampleAllLayers { get => _sampleAllLayers; set => SetField(ref _sampleAllLayers, value); }
}
