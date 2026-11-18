using System.Windows.Input;
using FlashEditor.App.Tools;
using FlashEditor.App.Tools.Navigation;
using FlashEditor.App.Tools.Options;
using FlashEditor.App.Tools.Painting;
using FlashEditor.App.Tools.Retouch;
using FlashEditor.App.Tools.Selecting;
using FlashEditor.App.Tools.Shapes;
using FlashEditor.App.Tools.Text;
using FlashEditor.App.Tools.Transform;
using FlashEditor.Core.Common;

namespace FlashEditor.App.ViewModels;

/// <summary>The toolbox: list of tools and which one is active.</summary>
public sealed class ToolsViewModel : ObservableObject
{
    private ToolBase _activeTool;

    public ToolsViewModel(IToolHost host)
    {
        Tools = new ToolBase[]
        {
            new MoveTool(host),
            new RectSelectTool(host),
            new RectSelectTool(host, ellipse: true),
            new LassoTool(host),
            new PolygonLassoTool(host),
            new PenTool(host),
            new QuickSelectTool(host),
            new MagicWandTool(host),
            new CropTool(host),
            new BrushTool(host),
            new EraserTool(host),
            new RefineTool(host),
            new CloneStampTool(host),
            new SpotHealingTool(host),
            new HealingBrushTool(host),
            new PatchTool(host),
            new DodgeBurnTool(host),
            new BlurSharpenTool(host),
            new BucketTool(host),
            new EyedropperTool(host),
            new ShapeTool(host, new ShapeOptions(host.Dialogs)),
            new TextTool(host),
            new HandTool(host),
            new ZoomTool(host),
            new MaskBrushTool(host),
            new PerspectiveCropTool(host),
            new LiquifyTool(host),
            new RemoveBrushTool(host),
            new ClickSelectTool(host),
        };
        _activeTool = Get<MoveTool>(); // the program starts on Select / Move
        _activeTool.IsActive = true;

        // Similar tools share one button in the toolbox (indexes into the list above).
        int[][] layout =
        {
            new[] { 0 }, new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 }, new[] { 6, 7, 28 }, new[] { 8, 25 },
            new[] { 9, 10, 11, 24 }, new[] { 13, 14, 15, 27 }, new[] { 12 }, new[] { 16 }, new[] { 17, 26 },
            new[] { 18 }, new[] { 19 }, new[] { 20 }, new[] { 21 }, new[] { 22 }, new[] { 23 },
        };
        Groups = layout.Select(ix => new ToolGroup(ix.Select(i => Tools[i]).ToList(), t => ActiveTool = t)).ToList();
        foreach (var group in Groups) group.OnActiveToolChanged(_activeTool);
    }

    public IReadOnlyList<ToolBase> Tools { get; }

    /// <summary>The toolbox buttons: each holds one tool or a group of similar tools.</summary>
    public IReadOnlyList<ToolGroup> Groups { get; }

    public ToolBase ActiveTool
    {
        get => _activeTool;
        set
        {
            if (value == null || value == _activeTool) return;
            _activeTool.Deactivate();
            _activeTool.IsActive = false;
            _activeTool = value;
            _activeTool.IsActive = true;
            _activeTool.Activate();
            foreach (var group in Groups) group.OnActiveToolChanged(_activeTool);
            OnPropertyChanged();
        }
    }

    public T Get<T>() where T : ToolBase => Tools.OfType<T>().First();

    /// <summary>
    /// Selects the tool whose shortcut is <paramref name="key"/>. When several tools share a key
    /// (M = rectangle / ellipse select, L = lasso / polygon lasso), pressing it again switches between them.
    /// </summary>
    public bool TrySelectByShortcut(Key key)
    {
        var matches = Tools.Where(t => t.Shortcut == key).ToList();
        if (matches.Count == 0) return false;
        int current = matches.IndexOf(_activeTool);
        ActiveTool = matches[(current + 1) % matches.Count];
        return true;
    }

    /// <summary>Called when another document is opened, so tools drop any half-finished work.</summary>
    public void ResetActiveTool()
    {
        _activeTool.Deactivate();
        _activeTool.Activate();
    }
}
