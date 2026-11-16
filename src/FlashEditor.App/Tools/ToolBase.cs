using System.Windows.Input;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;

namespace FlashEditor.App.Tools;

/// <summary>
/// Base class for every tool in the toolbox. The canvas forwards mouse and key events to the active
/// tool; the tool edits the document and pushes undo steps.
/// </summary>
public abstract class ToolBase : ObservableObject
{
    private bool _isActive;

    protected ToolBase(IToolHost host) => Host = host;

    protected IToolHost Host { get; }
    protected Document Document => Host.Document.Model;

    public abstract string Name { get; }

    /// <summary>Icon resource name (see Themes/Icons.xaml, without the "Icon." prefix).</summary>
    public abstract string IconKey { get; }

    /// <summary>Single-key shortcut that selects this tool.</summary>
    public abstract Key Shortcut { get; }

    public string ToolTip => $"{Name} ({Shortcut})";

    /// <summary>Settings shown in the options bar (a settings object, or a hint string).</summary>
    public virtual object? Options => null;

    public virtual Cursor Cursor => Cursors.Cross;

    /// <summary>True when the canvas should show the resize/rotate box around the selected layer.</summary>
    public virtual bool ShowsTransformFrame => false;

    /// <summary>Diameter (image pixels) of the round brush outline shown under the mouse; 0 = none.</summary>
    public virtual double BrushPreviewSize => 0;

    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    public virtual void Activate() { }
    public virtual void Deactivate() { }

    public virtual void MouseDown(ToolMouseArgs e) { }
    public virtual void MouseMove(ToolMouseArgs e) { }
    public virtual void MouseUp(ToolMouseArgs e) { }

    /// <summary>Return true if the tool handled the key.</summary>
    public virtual bool KeyDown(Key key, ModifierKeys modifiers) => false;

    /// <summary>
    /// Gets the active layer if it can be painted on; otherwise shows a hint and returns false.
    /// Editable objects (shapes, text, images) are converted to pixels first (undoable).
    /// </summary>
    protected bool TryGetEditableLayer(out Layer layer)
    {
        layer = Document.ActiveLayer!;
        if (layer == null)
        {
            Host.SetStatus("Select a layer first.");
            return false;
        }
        if (!layer.IsVisible)
        {
            Host.SetStatus("The active layer is hidden. Show it (eye icon) to edit it.");
            return false;
        }
        if (layer.IsLocked)
        {
            Host.SetStatus($"\"{layer.Name}\" is locked. Unlock it with the lock icon in the Layers panel, right-click > Unlock, or Ctrl+L.");
            return false;
        }
        Host.ConvertToPixels(layer);
        return true;
    }

    protected void Push(IHistoryAction? action)
    {
        if (action != null) Document.History.Push(action);
    }
}
