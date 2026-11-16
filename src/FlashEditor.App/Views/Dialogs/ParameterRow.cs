using System.Globalization;
using FlashEditor.Core.Common;
using FlashEditor.Core.Effects;

namespace FlashEditor.App.Views.Dialogs;

/// <summary>One slider or checkbox row in <see cref="ParametersDialog"/>.</summary>
public sealed class ParameterRow : ObservableObject
{
    private double _value;

    public ParameterRow(EffectParameter parameter, double value)
    {
        Parameter = parameter;
        _value = Normalize(value);
    }

    public event EventHandler? Changed;

    public EffectParameter Parameter { get; }
    public string Label => Parameter.Label;
    public double Min => Parameter.Min;
    public double Max => Parameter.Max;
    public double Step => Parameter.Step;
    public bool IsToggle => Parameter.Kind == ParameterKind.Toggle;
    public bool IsSlider => !IsToggle;

    public double Value
    {
        get => _value;
        set
        {
            if (!SetField(ref _value, Normalize(value))) return;
            OnPropertyChanged(nameof(ValueText));
            OnPropertyChanged(nameof(BoolValue));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Editable number next to the slider.</summary>
    public string ValueText
    {
        get => Value.ToString("0.##", CultureInfo.CurrentCulture);
        set
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var v)) Value = v;
            else OnPropertyChanged(); // restore the text
        }
    }

    public string Unit => Parameter.Unit;

    public bool BoolValue
    {
        get => Value >= 0.5;
        set => Value = value ? 1 : 0;
    }

    private double Normalize(double v)
    {
        v = Math.Clamp(v, Parameter.Min, Parameter.Max);
        if (Parameter.Step > 0) v = Math.Round(v / Parameter.Step) * Parameter.Step;
        return Math.Round(v, 4);
    }
}
