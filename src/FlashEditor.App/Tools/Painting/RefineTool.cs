using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Tools.Painting;

/// <summary>
/// Refine brush, for fixing the result of Remove Background:
/// <list type="bullet">
/// <item><b>Restore</b> paints back what the AI removed by mistake (from the picture as it was before).</item>
/// <item><b>Remove</b> paints away background the AI missed.</item>
/// </list>
/// Hold Alt to use the other mode while painting. [ and ] change the size. Each stroke is one undo step.
/// </summary>
public sealed class RefineTool : ToolBase
{
    private readonly RefineOptions _options = new();
    private BrushStroke? _stroke;
    private Layer? _layer;

    public RefineTool(IToolHost host) : base(host)
    {
        _options.PropertyChanged += (_, _) => OnPropertyChanged(nameof(BrushPreviewSize));
    }

    public override string Name => "Refine (restore / remove)";
    public override string IconKey => "Refine";
    public override Key Shortcut => Key.R;
    public override object Options => _options;
    public override Cursor Cursor => Cursors.Cross;
    public override double BrushPreviewSize => _options.Size;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left || !TryGetEditableLayer(out var layer)) return;
        bool restore = _options.Restore != e.Alt; // Alt = the other mode, just for this stroke

        if (restore)
        {
            var source = layer.RestoreSource;
            if (source == null || source.Width != layer.Width || source.Height != layer.Height)
            {
                Host.SetStatus(source == null
                    ? "Nothing to restore on this layer: Restore brings back what Remove Background took away. Use Remove (or the Eraser) here."
                    : "Can't restore: the picture size changed after the background was removed.");
                return;
            }
            _stroke = new BrushStroke(layer.Pixels, _options.ToSettings(), source, Document.Selection);
        }
        else
        {
            _stroke = new BrushStroke(layer.Pixels, _options.ToSettings(), Core.Imaging.ColorRgba.Transparent, erase: true, Document.Selection);
        }
        _layer = layer;
        layer.NotifyPixelsChanged(_stroke.AddPoint(e.Position));
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_stroke == null || _layer == null) return;
        _layer.NotifyPixelsChanged(_stroke.AddPoint(e.Position));
    }

    public override void MouseUp(ToolMouseArgs e) => Finish();

    public override void Deactivate() => Finish();

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.OemCloseBrackets: _options.Grow(); return true;
            case Key.OemOpenBrackets: _options.Shrink(); return true;
            case Key.X: _options.Restore = !_options.Restore; return true; // quick switch
            default: return false;
        }
    }

    private void Finish()
    {
        if (_stroke != null && _layer != null)
            Push(PixelChangeAction.Create(_options.Restore ? "Refine: Restore" : "Refine: Remove", _layer, _stroke.Original, _stroke.TotalDirty));
        _stroke = null;
        _layer = null;
    }
}
