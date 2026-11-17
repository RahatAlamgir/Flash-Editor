using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Common;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.ViewModels;

/// <summary>
/// One color of the selected object's gradient, as a row in the Properties panel: a color swatch, its place (0..100 %) and a remove button.
/// Rows are kept and updated in place (not recreated) while a slider is being dragged.
/// </summary>
public sealed class GradientStopItem : ObservableObject
{
    private Brush _brush = Brushes.Transparent;
    private double _percent;
    private bool _canRemove;

    public GradientStopItem(int index, Action<int> pick, Action<int, double> move, Action<int> remove, Action<int, double> fade)
    {
        Index = index;
        PickCommand = new RelayCommand(() => pick(Index));
        RemoveCommand = new RelayCommand(() => remove(Index), () => CanRemove);
        _move = move;
        _fade = fade;
    }

    private readonly Action<int, double> _move;
    private readonly Action<int, double> _fade;
    private double _opacity = 100;

    /// <summary>Position of the stop in the gradient's list.</summary>
    public int Index { get; }

    public RelayCommand PickCommand { get; }
    public RelayCommand RemoveCommand { get; }

    public Brush Brush { get => _brush; private set => SetField(ref _brush, value); }

    public bool CanRemove
    {
        get => _canRemove;
        private set => SetField(ref _canRemove, value);
    }

    /// <summary>Where the color sits along the gradient, 0..100 %.</summary>
    public double Percent
    {
        get => _percent;
        set
        {
            value = Math.Round(Math.Clamp(value, 0, 100));
            if (Math.Abs(value - _percent) < 1e-9) return;
            _percent = value;
            OnPropertyChanged();
            _move(Index, value / 100.0);
        }
    }

    /// <summary>How solid the color is, 0..100 % (0 = see-through).</summary>
    public double OpacityPercent
    {
        get => _opacity;
        set
        {
            value = Math.Round(Math.Clamp(value, 0, 100));
            if (Math.Abs(value - _opacity) < 1e-9) return;
            _opacity = value;
            OnPropertyChanged();
            _fade(Index, value / 100.0);
        }
    }

    /// <summary>Shows the current values of the stop (does not change the object).</summary>
    public void Show(ColorStop stop, bool canRemove)
    {
        var brush = new SolidColorBrush(BitmapConverter.ToWpf(stop.Color with { A = 255 }));
        brush.Freeze();
        Brush = brush;
        CanRemove = canRemove;
        double opacity = Math.Round(stop.Color.A / 255.0 * 100);
        if (Math.Abs(opacity - _opacity) > 1e-9) { _opacity = opacity; OnPropertyChanged(nameof(OpacityPercent)); }
        double percent = Math.Round(stop.Position * 100);
        if (Math.Abs(percent - _percent) > 1e-9) { _percent = percent; OnPropertyChanged(nameof(Percent)); }
    }
}
