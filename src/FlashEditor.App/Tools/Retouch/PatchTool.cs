using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Retouch;
using FlashEditor.Core.Selections;

namespace FlashEditor.App.Tools.Retouch;

/// <summary>
/// Patch: draw around a blemish, then drag the outlined area onto a clean part of the picture. The blemish is replaced by that
/// clean part, blended to the light and color around it. Esc cancels.
/// </summary>
public sealed class PatchTool : ToolBase
{
    private readonly List<PointD> _points = new();
    private bool _drawing;

    private Selection? _region;          // the outlined area (the blemish)
    private PointD[]? _outline;

    private bool _dragging;
    private PointD _dragStart;
    private Layer? _layer;
    private PixelBuffer? _original;
    private (int Dx, int Dy) _offset;

    public PatchTool(IToolHost host) : base(host) { }

    public override string Name => "Patch";
    public override string IconKey => "Patch";
    public override Key Shortcut => Key.J;
    public override object Options => "Draw around the blemish. Then drag the outlined area onto a clean part of the picture. Esc = cancel.";

    public override void MouseDown(ToolMouseArgs e)
    {
        if (e.Button != MouseButton.Left) return;

        // Inside the outlined area: start dragging it onto a clean place.
        if (_region != null && _region.IsSelected(e.X, e.Y))
        {
            if (!TryGetEditableLayer(out var layer)) return;
            _layer = layer;
            _original = layer.Pixels.Clone();
            _dragStart = e.Position;
            _offset = (0, 0);
            _dragging = true;
            return;
        }

        // Elsewhere: draw a new outline.
        ClearRegion();
        _points.Clear();
        _drawing = true;
        AddPoint(e.Position);
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_drawing)
        {
            double minDistance = 1.0 / (Host.Canvas?.Zoom ?? 1.0);
            if (_points.Count == 0 || _points[^1].DistanceTo(e.Position) >= minDistance) AddPoint(e.Position);
        }
        else if (_dragging && _region != null && _layer != null && _original != null)
        {
            _offset = ((int)Math.Round(e.Position.X - _dragStart.X), (int)Math.Round(e.Position.Y - _dragStart.Y));
            ApplyPatch();
            ShowOutline(_outline!, _offset.Dx, _offset.Dy);    // the outline follows the mouse: it shows where the clean part comes from
        }
    }

    public override void MouseUp(ToolMouseArgs e)
    {
        if (_drawing)
        {
            _drawing = false;
            _region = _points.Count >= 3 ? Selection.FromPolygon(_points.ToArray(), Document.Width, Document.Height) : null;
            _outline = _region == null ? null : _points.ToArray();
            _points.Clear();
            if (_region == null) Host.Canvas?.ShowSelectionPreview(null);
            else
            {
                ShowOutline(_outline!, 0, 0);
                Host.SetStatus("Now drag the outlined area onto a clean part of the picture.");
            }
            return;
        }
        if (_dragging) FinishDrag();
    }

    public override bool KeyDown(Key key, ModifierKeys modifiers)
    {
        if (key != Key.Escape || (_region == null && !_drawing)) return false;
        CancelDrag();
        _drawing = false;
        _points.Clear();
        ClearRegion();
        return true;
    }

    public override void Deactivate()
    {
        CancelDrag();
        _drawing = false;
        _points.Clear();
        ClearRegion();
    }

    // ------------------------------------------------------------------ patching

    private void ApplyPatch()
    {
        var region = _region!;
        IRetouchOp op = new CloneOp(0, 0);
        if (_offset != (0, 0))
        {
            var heal = new HealOp(_offset.Dx, _offset.Dy, Math.Clamp(Math.Max(region.Bounds.Width, region.Bounds.Height) / 2 + 6, 6, 120));
            heal.Attach(region.IsSelected);        // the tone comes from around the outlined area, not from the blemish in it
            op = heal;
        }
        var rect = RetouchRegion.Apply(_layer!.Pixels, _original!, op, region, Document.Selection, region.Bounds);
        _layer.NotifyPixelsChanged(rect);
    }

    private void FinishDrag()
    {
        _dragging = false;
        if (_layer != null && _original != null && _region != null && _offset != (0, 0))
            Push(PixelChangeAction.Create(Name, _layer, _original, _region.Bounds));
        else CancelDrag();
        _layer = null;
        _original = null;
        ClearRegion();
    }

    /// <summary>Puts the picture back as it was before the patch was dragged.</summary>
    private void CancelDrag()
    {
        if (!_dragging || _layer == null || _original == null || _region == null) { _dragging = false; return; }
        _dragging = false;
        _offset = (0, 0);
        ApplyPatch();
    }

    private void ClearRegion()
    {
        _region = null;
        _outline = null;
        Host.Canvas?.ShowSelectionPreview(null);
    }

    // ------------------------------------------------------------------ outline

    private void AddPoint(PointD p)
    {
        _points.Add(p);
        if (_points.Count >= 2) ShowOutline(_points.ToArray(), 0, 0);
    }

    private void ShowOutline(IReadOnlyList<PointD> points, double dx, double dy)
    {
        var figure = new PathFigure { StartPoint = new Point(points[0].X + dx, points[0].Y + dy), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new PolyLineSegment(points.Skip(1).Select(q => new Point(q.X + dx, q.Y + dy)), true));
        var geometry = new PathGeometry(new[] { figure }) { FillRule = FillRule.EvenOdd };
        geometry.Freeze();
        Host.Canvas?.ShowSelectionPreview(geometry);
    }
}
