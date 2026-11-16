using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Tools.Painting;

/// <summary>
/// Shared logic for Brush and Eraser: one <see cref="BrushStroke"/> per mouse drag, recorded as a
/// single undo step. Shift+click draws a straight line from the end of the previous stroke.
/// </summary>
public abstract class PaintToolBase : ToolBase
{
    private readonly BrushOptions _options = new();
    private BrushStroke? _stroke;
    private Layer? _layer;
    private PointD? _lastStrokeEnd;

    protected PaintToolBase(IToolHost host) : base(host)
    {
        _options.PropertyChanged += (_, _) => OnPropertyChanged(nameof(BrushPreviewSize));
    }

    protected abstract bool IsEraser { get; }

    public override object Options => _options;
    public override Cursor Cursor => Cursors.Cross; // plus a round outline showing the brush size
    public override double BrushPreviewSize => _options.Size;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left || !TryGetEditableLayer(out var layer)) return;

        _layer = layer;
        _stroke = new BrushStroke(layer.Pixels, _options.ToSettings(), Host.Colors.Foreground, IsEraser, Document.Selection);

        if (e.Shift && _lastStrokeEnd is PointD previous)
            layer.NotifyPixelsChanged(_stroke.AddPoint(previous));
        layer.NotifyPixelsChanged(_stroke.AddPoint(e.Position));
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_stroke == null || _layer == null) return;
        _layer.NotifyPixelsChanged(_stroke.AddPoint(e.Position));
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (_stroke == null) return;
        _lastStrokeEnd = e.Position;
        FinishStroke();
    }

    public override void Deactivate() => FinishStroke();

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.OemCloseBrackets: _options.Grow(); return true;
            case Key.OemOpenBrackets: _options.Shrink(); return true;
            default: return false;
        }
    }

    private void FinishStroke()
    {
        if (_stroke != null && _layer != null)
            Push(PixelChangeAction.Create(Name, _layer, _stroke.Original, _stroke.TotalDirty));
        _stroke = null;
        _layer = null;
    }
}
