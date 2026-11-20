using FlashEditor.App.Tools;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Layout;
using FlashEditor.Core.Objects;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>Align the selected layers to each other (one layer: to the canvas) and spread three or more evenly.</summary>
public sealed class AlignCommands
{
    private readonly MainViewModel _main;

    public AlignCommands(MainViewModel main)
    {
        _main = main;
        // The command parameter is the name of an AlignKind / DistributeKind, e.g. "Left" or "CenterHorizontally".
        AlignCommand = new RelayCommand(p => Align(Enum.Parse<AlignKind>((string)p!)), p => Movable().Count >= 1);
        AlignBothCommand = new RelayCommand(p => AlignBoth((string)p!), p => Movable().Count >= 1);
        DistributeCommand = new RelayCommand(p => Distribute(Enum.Parse<DistributeKind>((string)p!)), p => Movable().Count >= 3);
    }

    /// <summary>Parameter: Left, CenterHorizontally, Right, Top, CenterVertically or Bottom.</summary>
    public RelayCommand AlignCommand { get; }

    /// <summary>Parameter: "Left,Top", "CenterHorizontally,CenterVertically"... (a horizontal and a vertical kind): both directions in one step.</summary>
    public RelayCommand AlignBothCommand { get; }

    /// <summary>Parameter: Horizontally or Vertically. Needs three or more layers.</summary>
    public RelayCommand DistributeCommand { get; }

    /// <summary>Selected layers that can be moved (visible and not locked).</summary>
    private List<Layer> Movable() => _main.SelectedLayers.Where(l => l.IsVisible && !l.IsLocked && !SnapDrag.BoundsOf(l).IsEmpty).ToList();

    private void Align(AlignKind kind)
    {
        var layers = Movable();
        if (layers.Count == 0) return;
        var doc = _main.Document.Model;
        var offsets = Alignment.Align(layers.Select(SnapDrag.BoundsOf).ToList(), doc.Bounds, kind);
        Apply(layers, offsets, "Align");
    }

    private void AlignBoth(string kinds)
    {
        var parts = kinds.Split(',');
        var layers = Movable();
        if (layers.Count == 0 || parts.Length != 2) return;
        var doc = _main.Document.Model;
        var offsets = Alignment.AlignBoth(layers.Select(SnapDrag.BoundsOf).ToList(), doc.Bounds, Enum.Parse<AlignKind>(parts[0]), Enum.Parse<AlignKind>(parts[1]));
        Apply(layers, offsets, "Align");
    }

    private void Distribute(DistributeKind kind)
    {
        var layers = Movable();
        if (layers.Count < 3) return;
        var offsets = Alignment.Distribute(layers.Select(SnapDrag.BoundsOf).ToList(), kind);
        Apply(layers, offsets, "Distribute");
    }

    /// <summary>Moves every layer by its own offset as one undo step.</summary>
    private void Apply(List<Layer> layers, (int Dx, int Dy)[] offsets, string name)
    {
        var doc = _main.Document.Model;
        var steps = new List<IHistoryAction?>();
        for (int i = 0; i < layers.Count; i++)
        {
            var (dx, dy) = offsets[i];
            if (dx == 0 && dy == 0) continue;
            var layer = layers[i];
            if (layer.Object is { } obj)
            {
                steps.Add(ObjectChangeAction.Apply(name, layer, ObjectTransforms.Offset(obj, dx, dy)));
            }
            else
            {
                var before = layer.SaveState().DeepCopy();
                var (content, x, y) = layer.GetFullContent();
                layer.RestoreState(LayerState.Place(content.Clone(), x + dx, y + dy, doc.Width, doc.Height));
                steps.Add(new LayerContentAction(name, layer, before, layer.SaveState()));
            }
        }
        steps.RemoveAll(s => s == null);
        if (steps.Count == 0)
        {
            _main.SetStatus("Already lined up.");
            return;
        }
        _main.Push(new CompositeAction(name, steps));
        _main.SetStatus($"{name}: {steps.Count} layer{(steps.Count == 1 ? "" : "s")} moved.");
    }
}
