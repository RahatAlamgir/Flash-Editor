using FlashEditor.Core.Common;

namespace FlashEditor.App.Tools.Options;

/// <summary>Settings for the Paint Bucket tool.</summary>
public sealed class BucketOptions : ObservableObject
{
    private double _tolerance = 32;
    private bool _contiguous = true;

    /// <summary>0 = exact color only, 255 = everything.</summary>
    public double Tolerance { get => _tolerance; set => SetField(ref _tolerance, Math.Clamp(Math.Round(value), 0, 255)); }

    /// <summary>Only fill connected pixels (otherwise fill every similar pixel).</summary>
    public bool Contiguous { get => _contiguous; set => SetField(ref _contiguous, value); }
}
