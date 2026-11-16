using System.Windows.Input;
using FlashEditor.App.Tools;
using FlashEditor.App.Tools.Navigation;
using FlashEditor.App.Tools.Options;
using FlashEditor.App.Tools.Painting;
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
            new BucketTool(host),
            new EyedropperTool(host),
            new ShapeTool(host, new ShapeOptions(host.Dialogs)),
            new TextTool(host),
            new HandTool(host),
            new ZoomTool(host),
        };
        _activeTool = Get<BrushTool>();
        _activeTool.IsActive = true;
    }

    public IReadOnlyList<ToolBase> Tools { get; }

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
