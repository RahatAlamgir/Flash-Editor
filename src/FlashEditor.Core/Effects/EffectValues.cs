namespace FlashEditor.Core.Effects;

/// <summary>The chosen value for each parameter of an effect, by key.</summary>
public sealed class EffectValues
{
    private readonly Dictionary<string, double> _values = new();

    public EffectValues() { }

    public EffectValues(IEnumerable<EffectParameter> parameters)
    {
        foreach (var p in parameters) _values[p.Key] = p.Default;
    }

    public double this[string key]
    {
        get => _values.TryGetValue(key, out var v) ? v : 0;
        set => _values[key] = value;
    }

    public double Get(string key, double fallback) => _values.TryGetValue(key, out var v) ? v : fallback;

    public bool GetBool(string key) => this[key] >= 0.5;

    public EffectValues Clone()
    {
        var copy = new EffectValues();
        foreach (var (k, v) in _values) copy._values[k] = v;
        return copy;
    }
}
