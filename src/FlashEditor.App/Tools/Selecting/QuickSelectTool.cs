using System.Diagnostics;
using System.Windows.Input;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Selections;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>
/// Quick Select brush: drag over an object and the selection spreads into similar colors around the
/// brush, following the object's edges. Each stroke adds to the selection; hold Alt to remove instead.
/// [ and ] change the brush size.
/// </summary>
public sealed class QuickSelectTool : ToolBase
{
    private readonly QuickSelectOptions _options = new();
    private readonly Stopwatch _sinceUpdate = new();
    private PixelBuffer? _source;
    private byte[]? _mask;
    private Selection? _startSelection;
    private bool _subtract;
    private PointD _last;

    public QuickSelectTool(IToolHost host) : base(host) { }

    public override string Name => "Quick Select (brush)";
    public override string IconKey => "QuickSelect";
    public override Key Shortcut => Key.W;
    public override object Options => _options;
    public override double BrushPreviewSize => _options.Size;

    private bool IsPainting => _mask != null;

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;
        _source = _options.SampleAllLayers || Document.ActiveLayer == null
            ? Compositor.Flatten(Document)
            : Document.ActiveLayer.Pixels;
        _mask = new byte[Document.Width * Document.Height];
        _startSelection = Document.Selection;
        _subtract = e.Alt;
        _last = e.Position;
        Dab(e.Position);
        Show(force: true);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (!IsPainting) return;
        // Dabs every quarter brush along the drag so fast strokes leave no gaps.
        double step = Math.Max(1, _options.Size / 4);
        double distance = _last.DistanceTo(e.Position);
        for (double t = step; t <= distance; t += step)
            Dab(new PointD(_last.X + (e.Position.X - _last.X) * t / distance, _last.Y + (e.Position.Y - _last.Y) * t / distance));
        if (distance >= step) _last = e.Position;
        Show(force: false);
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (!IsPainting) return;
        Show(force: true);
        _mask = null;
        _source = null;
        Host.SetStatus(Document.Selection == null
            ? "Nothing selected."
            : $"Selected {Document.Selection.Bounds.Width} x {Document.Selection.Bounds.Height} px. Keep dragging to add; Alt+drag removes. [ ] = brush size.");
    }

    public override void Deactivate()
    {
        _mask = null;
        _source = null;
    }

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.OemOpenBrackets) { _options.Size = Math.Max(2, _options.Size * 0.8); RaiseBrush(); return true; }
        if (key == Key.OemCloseBrackets) { _options.Size = _options.Size * 1.25; RaiseBrush(); return true; }
        return false;
    }

    private void RaiseBrush() => OnPropertyChanged(nameof(BrushPreviewSize));

    private void Dab(PointD at)
    {
        if (_source == null || _mask == null) return;
        QuickSelect.AddDab(_source, _mask, at.X, at.Y, _options.Size / 2, (int)_options.Tolerance);
    }

    /// <summary>Shows the selection while dragging (a few times per second; tracing the outline takes time).</summary>
    private void Show(bool force)
    {
        if (_mask == null) return;
        if (!force && _sinceUpdate.IsRunning && _sinceUpdate.ElapsedMilliseconds < 70) return;
        _sinceUpdate.Restart();
        var stroke = Selection.FromMask((byte[])_mask.Clone(), Document.Width, Document.Height);
        Document.Selection = Selection.Combine(_startSelection, stroke,
            _subtract ? Selection.CombineMode.Subtract : Selection.CombineMode.Add);
    }
}
