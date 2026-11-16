using FlashEditor.Core.Common;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Tools.Options;

/// <summary>Settings for the Refine brush (fix the result of Remove Background).</summary>
public sealed class RefineOptions : ObservableObject
{
    private bool _restore = true;
    private double _size = 40;
    private double _hardness = 50;

    /// <summary>True = paint the original back; false = remove (make transparent).</summary>
    public bool Restore
    {
        get => _restore;
        set
        {
            if (!SetField(ref _restore, value)) return;
            OnPropertyChanged(nameof(Remove));
        }
    }

    public bool Remove { get => !_restore; set => Restore = !value; }

    public double Size { get => _size; set => SetField(ref _size, Math.Clamp(Math.Round(value), 1, 1000)); }

    /// <summary>0 = very soft edge, 100 = crisp edge.</summary>
    public double Hardness { get => _hardness; set => SetField(ref _hardness, Math.Clamp(Math.Round(value), 0, 100)); }

    public BrushSettings ToSettings() => new(Size, Hardness / 100.0, 1.0);

    public void Grow() => Size += Size < 10 ? 1 : Size < 50 ? 5 : Size < 200 ? 10 : 50;
    public void Shrink() => Size -= Size <= 10 ? 1 : Size <= 50 ? 5 : Size <= 200 ? 10 : 50;
}
