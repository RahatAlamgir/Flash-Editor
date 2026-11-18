using System.Windows.Input;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Tools.Retouch;

/// <summary>Settings of the Liquify tool.</summary>
public sealed class LiquifyOptions : ObservableObject
{
    private LiquifyMode _mode = LiquifyMode.Push;
    private double _size = 120, _strength = 70;

    public static IReadOnlyList<LiquifyMode> Modes { get; } = new[] { LiquifyMode.Push, LiquifyMode.Bloat, LiquifyMode.Pucker };

    public LiquifyMode Mode { get => _mode; set => SetField(ref _mode, value); }

    /// <summary>Brush size in pixels.</summary>
    public double Size { get => _size; set => SetField(ref _size, Math.Clamp(Math.Round(value), 5, 600)); }

    /// <summary>1..100 %.</summary>
    public double Strength { get => _strength; set => SetField(ref _strength, Math.Clamp(Math.Round(value), 1, 100)); }
}

/// <summary>
/// Liquify: drag to push the pixels along (like smearing wet paint), or click and hold to swell (Bloat) or pinch (Pucker) what is under the brush.
/// Good for reshaping a smile, a curve or a small part of a picture. Works on the pixels of the active layer.
/// </summary>
public sealed class LiquifyTool : ToolBase
{
    private readonly LiquifyOptions _options = new();
    private Layer? _layer;
    private PixelBuffer? _original;
    private PointD _last;
    private PixelRect _dirty = PixelRect.Empty;

    public LiquifyTool(IToolHost host) : base(host)
    {
        _options.PropertyChanged += (_, _) => OnPropertyChanged(nameof(BrushPreviewSize));
    }

    public override string Name => "Liquify";
    public override string IconKey => "Liquify";
    public override Key Shortcut => Key.Q;
    public override object Options => _options;
    public override double BrushPreviewSize => _options.Size;

    public override void Activate()
        => Host.SetStatus("Liquify: drag to push the pixels along. Choose Bloat or Pucker in the options bar to swell or pinch what is under the brush.");

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left || !TryGetEditableLayer(out var layer)) return;
        _layer = layer;
        _original = layer.Pixels.Clone();
        _last = e.Position;
        _dirty = PixelRect.Empty;
        if (_options.Mode != LiquifyMode.Push) Step(e.Position, 0, 0);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_layer == null) return;
        double dx = e.Position.X - _last.X, dy = e.Position.Y - _last.Y;
        if (_options.Mode == LiquifyMode.Push)
        {
            if (Math.Abs(dx) + Math.Abs(dy) < 0.5) return;
            Step(e.Position, dx, dy);
            _last = e.Position;
        }
        else if (Math.Abs(dx) + Math.Abs(dy) >= 1) { Step(e.Position, 0, 0); _last = e.Position; }
    }

    public override void MouseUp(ToolMouseArgs e) => Finish();

    public override void Deactivate() => Finish();

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        switch (key)
        {
            case Key.OemCloseBrackets: _options.Size += 10; return true;
            case Key.OemOpenBrackets: _options.Size -= 10; return true;
            default: return false;
        }
    }

    private void Step(PointD at, double dx, double dy)
    {
        if (_layer == null) return;
        // Bloat and Pucker act a little at every step; a slower, softer effect than one big jump.
        double strength = _options.Mode == LiquifyMode.Push ? _options.Strength / 100.0 : _options.Strength / 100.0 * 0.35;
        var area = Liquify.Apply(_layer.Pixels, at.X, at.Y, _options.Size / 2, strength, _options.Mode, dx, dy);
        if (area.IsEmpty) return;
        _dirty = _dirty.IsEmpty ? area : _dirty.Union(area);
        _layer.NotifyPixelsChanged(area);
    }

    private void Finish()
    {
        if (_layer != null && _original != null && !_dirty.IsEmpty)
            Push(PixelChangeAction.Create(Name, _layer, _original, _dirty));
        _layer = null;
        _original = null;
        _dirty = PixelRect.Empty;
    }
}
