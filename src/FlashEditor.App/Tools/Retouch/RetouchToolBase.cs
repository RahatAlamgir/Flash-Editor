using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Retouch;

namespace FlashEditor.App.Tools.Retouch;

/// <summary>
/// Shared logic of the retouching brushes: one <see cref="BrushStroke"/> per mouse drag, with an operation (clone, heal, dodge...)
/// deciding what happens to the pixels under the brush. The whole drag is one undo step.
/// </summary>
public abstract class RetouchToolBase : ToolBase
{
    private BrushStroke? _stroke;
    private Layer? _layer;

    protected RetouchToolBase(IToolHost host, RetouchOptions options) : base(host)
    {
        Settings = options;
        options.PropertyChanged += (_, _) => OnPropertyChanged(nameof(BrushPreviewSize));
    }

    protected RetouchOptions Settings { get; }

    public override object Options => Settings;
    public override Cursor Cursor => Cursors.Cross;
    public override double BrushPreviewSize => Settings.Size;

    /// <summary>The operation for a stroke that starts at <paramref name="e"/>, or null when it cannot start (a hint was shown).</summary>
    protected abstract IRetouchOp? BeginOperation(ToolMouseArgs e, Layer layer);

    /// <summary>How strong the stroke is, 0..1 (the brush opacity).</summary>
    protected virtual double StrokeOpacity => Settings.Strength / 100.0;

    /// <summary>Called after every mouse move of a stroke (for example to move the marker of the copied place).</summary>
    protected virtual void StrokeMoved(ToolMouseArgs e) { }

    /// <summary>
    /// Called when the mouse is released, before the stroke is stored. May redo the stroke with another operation
    /// (<see cref="BrushStroke.Reapply"/>); returns false to throw the stroke away (the pixels are put back by the caller).
    /// </summary>
    protected virtual bool EndStroke(BrushStroke stroke, Layer layer) => true;

    protected virtual void StrokeEnded() { }

    /// <summary>Handles a click before a stroke starts (Alt+click chooses the copied place). True = handled.</summary>
    protected virtual bool PreviewClick(ToolMouseArgs e) => false;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        if (PreviewClick(e)) return;
        if (!TryGetEditableLayer(out var layer)) return;

        var op = BeginOperation(e, layer);
        if (op == null) return;

        _layer = layer;
        _stroke = new BrushStroke(layer.Pixels, Settings.ToSettings(StrokeOpacity), op, Document.Selection);
        layer.NotifyPixelsChanged(_stroke.AddPoint(e.Position));
        StrokeMoved(e);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_stroke == null || _layer == null) return;
        _layer.NotifyPixelsChanged(_stroke.AddPoint(e.Position));
        StrokeMoved(e);
    }

    public override void MouseUp(ToolMouseArgs e) => Finish();

    public override void Deactivate()
    {
        Finish();
        StrokeEnded();
    }

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.OemCloseBrackets: Settings.Grow(); return true;
            case Key.OemOpenBrackets: Settings.Shrink(); return true;
            default: return false;
        }
    }

    private void Finish()
    {
        var stroke = _stroke;
        var layer = _layer;
        _stroke = null;
        _layer = null;
        if (stroke == null || layer == null) return;

        if (EndStroke(stroke, layer))
        {
            Push(PixelChangeAction.Create(Name, layer, stroke.Original, stroke.TotalDirty));
        }
        else
        {
            layer.Pixels.CopyFrom(stroke.Original.Crop(stroke.TotalDirty), stroke.TotalDirty.X, stroke.TotalDirty.Y); // nothing happened: put the pixels back
            layer.NotifyPixelsChanged(stroke.TotalDirty);
        }
        StrokeEnded();
    }
}
