using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.History;

public enum LayerProperty { Name, Opacity, Visibility, Lock, BlendMode, Adjustment }

/// <summary>Undo step for renaming a layer, changing its opacity or toggling visibility.</summary>
public sealed class LayerPropertyAction : IHistoryAction
{
    private readonly Layer _layer;
    private readonly LayerProperty _property;
    private readonly object _oldValue;
    private object _newValue;

    public LayerPropertyAction(Layer layer, LayerProperty property, object oldValue, object newValue)
    {
        _layer = layer;
        _property = property;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public string Name => _property switch
    {
        LayerProperty.Name => "Rename Layer",
        LayerProperty.Opacity => "Layer Opacity",
        LayerProperty.BlendMode => "Blend Mode",
        LayerProperty.Adjustment => "Adjustment",
        LayerProperty.Lock => (bool)_newValue ? "Lock Layer" : "Unlock Layer",
        _ => (bool)_newValue ? "Show Layer" : "Hide Layer",
    };

    public void Undo() => Apply(_oldValue);
    public void Redo() => Apply(_newValue);

    /// <summary>Consecutive opacity changes on the same layer become one step.</summary>
    public bool TryMerge(IHistoryAction next)
    {
        if (next is LayerPropertyAction other && other._layer == _layer
            && other._property == _property && _property is LayerProperty.Opacity or LayerProperty.Adjustment)
        {
            _newValue = other._newValue;
            return true;
        }
        return false;
    }

    private void Apply(object value)
    {
        switch (_property)
        {
            case LayerProperty.Name: _layer.Name = (string)value; break;
            case LayerProperty.Opacity: _layer.Opacity = (double)value; break;
            case LayerProperty.BlendMode: _layer.BlendMode = (BlendMode)value; break;
            case LayerProperty.Adjustment: _layer.Adjustment = (AdjustmentStyle)value; break;
            case LayerProperty.Visibility: _layer.IsVisible = (bool)value; break;
            case LayerProperty.Lock: _layer.IsLocked = (bool)value; break;
        }
    }
}
