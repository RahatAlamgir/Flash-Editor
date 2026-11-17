using System.Windows;
using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.ViewModels;

/// <summary>A gradient preset with a small picture of it for the Properties panel.</summary>
public sealed class GradientPresetItem
{
    public GradientPresetItem(GradientPreset preset)
    {
        Preset = preset;
        var stops = ObjectRenderer.StopsOf(preset.CreateStyle());
        Brush brush;
        if (preset.Radial)
        {
            brush = new RadialGradientBrush(stops);
        }
        else
        {
            double a = preset.Angle * Math.PI / 180, dx = Math.Cos(a) / 2, dy = Math.Sin(a) / 2;
            brush = new LinearGradientBrush(stops, 0) { StartPoint = new Point(0.5 - dx, 0.5 - dy), EndPoint = new Point(0.5 + dx, 0.5 + dy) };
        }
        brush.Freeze();
        Preview = brush;
    }

    public GradientPreset Preset { get; }
    public string Name => Preset.Name;
    public Brush Preview { get; }
}
