using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using FlashEditor.App.ViewModels;
using FlashEditor.Core.Snapping;

namespace FlashEditor.App.Views.Editor;

/// <summary>Rulers, grid, guide lines and snap lines of the canvas (the buttons above the canvas switch them on and off).</summary>
public partial class CanvasView
{
    private const double GuideGrabDistance = 4;     // screen pixels

    private DocumentViewModel? _aidsDocument;
    private RulerControl? _rulerDrag;                // the ruler a new guide is being pulled from
    private (bool Vertical, int Index)? _guideDrag;  // an existing guide being moved
    private bool _guideCursor;

    /// <summary>Starts following the aid settings (called once when the canvas is connected to the main view model).</summary>
    private void AttachAids(MainViewModel main)
    {
        main.Aids.PropertyChanged += (_, _) => UpdateAids();
    }

    /// <summary>Follows the guides of the document that is shown.</summary>
    private void FollowGuidesOf(DocumentViewModel document)
    {
        if (_aidsDocument != null) _aidsDocument.GuidesChanged -= OnGuidesChanged;
        _aidsDocument = document;
        document.GuidesChanged += OnGuidesChanged;
        UpdateAids();
    }

    private void OnGuidesChanged(object? sender, EventArgs e) => UpdateAids();

    /// <summary>Brings the rulers, the grid and the guide lines up to date (after zoom, pan, resize or a setting change).</summary>
    private void UpdateAids()
    {
        if (_main == null || _document == null) return;
        var aids = _main.Aids;

        var rulers = aids.ShowRulers ? Visibility.Visible : Visibility.Collapsed;
        TopRuler.Visibility = LeftRuler.Visibility = RulerCorner.Visibility = rulers;
        double ox = Math.Round(_offset.X), oy = Math.Round(_offset.Y);
        TopRuler.SetView(_zoom, ox, _document.Width);
        LeftRuler.SetView(_zoom, oy, _document.Height);

        AidsLayer.Zoom = _zoom;
        AidsLayer.Visible = new Rect(-ox / _zoom, -oy / _zoom, Math.Max(1, Viewport.ActualWidth) / _zoom, Math.Max(1, Viewport.ActualHeight) / _zoom);
        AidsLayer.PageSize = new Size(_document.Width, _document.Height);
        AidsLayer.ShowGrid = aids.ShowGrid;
        AidsLayer.GridSize = aids.GridSize;
        AidsLayer.ShowGuides = aids.ShowGuides;
        AidsLayer.VerticalGuides = _document.VerticalGuides;
        AidsLayer.HorizontalGuides = _document.HorizontalGuides;
        AidsLayer.InvalidateVisual();
    }

    public void ShowSnapLines(IReadOnlyList<double> xs, IReadOnlyList<double> ys, IReadOnlyList<GapMark>? gaps = null)
    {
        gaps ??= Array.Empty<GapMark>();
        if (AidsLayer.SnapX.Count == 0 && AidsLayer.SnapY.Count == 0 && AidsLayer.Gaps.Count == 0 && xs.Count == 0 && ys.Count == 0 && gaps.Count == 0) return;
        AidsLayer.Gaps = gaps;
        AidsLayer.SnapX = xs;
        AidsLayer.SnapY = ys;
        AidsLayer.InvalidateVisual();
    }

    /// <summary>Shows where the mouse is on the two rulers.</summary>
    private void UpdateRulerMarkers(Point? viewportPosition)
    {
        TopRuler.SetMarker(viewportPosition?.X ?? double.NaN);
        LeftRuler.SetMarker(viewportPosition?.Y ?? double.NaN);
    }

    // ------------------------------------------------------------------ pulling a new guide out of a ruler

    private void OnRulerMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_document == null) return;
        _rulerDrag = (RulerControl)sender;
        _rulerDrag.CaptureMouse();
        ShowTempGuide(e);
        e.Handled = true;
    }

    private void OnRulerMouseMove(object sender, MouseEventArgs e)
    {
        if (_rulerDrag == null) return;
        ShowTempGuide(e);
    }

    private void OnRulerMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_rulerDrag == null || _document == null) return;
        var ruler = _rulerDrag;
        _rulerDrag = null;
        ruler.ReleaseMouseCapture();

        var inside = e.GetPosition(Viewport);
        bool onCanvas = inside.X >= 0 && inside.Y >= 0 && inside.X <= Viewport.ActualWidth && inside.Y <= Viewport.ActualHeight;
        var world = e.GetPosition(World);
        AidsLayer.TempVertical = AidsLayer.TempHorizontal = double.NaN;
        if (onCanvas)
        {
            if (ruler.IsVertical) _document.VerticalGuides.Add(Math.Round(world.X));
            else _document.HorizontalGuides.Add(Math.Round(world.Y));
            _main!.Aids.ShowGuides = true;         // a new guide is always shown
            _document.NotifyGuidesChanged();
        }
        UpdateAids();
    }

    private void ShowTempGuide(MouseEventArgs e)
    {
        var world = e.GetPosition(World);
        if (_rulerDrag!.IsVertical) AidsLayer.TempVertical = Math.Round(world.X);
        else AidsLayer.TempHorizontal = Math.Round(world.Y);
        AidsLayer.InvalidateVisual();
    }

    // ------------------------------------------------------------------ moving a guide that is already there

    /// <summary>The guide under the mouse, if the Select / Move tool is active and guides are shown.</summary>
    private (bool Vertical, int Index)? GuideAt(Point viewportPosition)
    {
        if (_main == null || _document == null || !_main.Aids.ShowGuides || ActiveTool?.ShowsTransformFrame != true) return null;
        double ox = Math.Round(_offset.X), oy = Math.Round(_offset.Y);
        for (int i = 0; i < _document.VerticalGuides.Count; i++)
            if (Math.Abs(_document.VerticalGuides[i] * _zoom + ox - viewportPosition.X) <= GuideGrabDistance) return (true, i);
        for (int i = 0; i < _document.HorizontalGuides.Count; i++)
            if (Math.Abs(_document.HorizontalGuides[i] * _zoom + oy - viewportPosition.Y) <= GuideGrabDistance) return (false, i);
        return null;
    }

    private bool TryBeginGuideDrag(MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || Keyboard.Modifiers != ModifierKeys.None) return false;
        if (GuideAt(e.GetPosition(Viewport)) is not { } guide) return false;
        _guideDrag = guide;
        Viewport.CaptureMouse();
        e.Handled = true;
        return true;
    }

    /// <summary>Moves the guide being dragged to the mouse. Returns true while a guide is being dragged.</summary>
    private bool DragGuide(MouseEventArgs e)
    {
        if (_guideDrag is not { } guide || _document == null) return false;
        var world = e.GetPosition(World);
        var list = guide.Vertical ? _document.VerticalGuides : _document.HorizontalGuides;
        if (guide.Index < list.Count) list[guide.Index] = Math.Round(guide.Vertical ? world.X : world.Y);
        UpdateAids();
        return true;
    }

    private bool EndGuideDrag(MouseEventArgs e)
    {
        if (_guideDrag is not { } guide || _document == null) return false;
        _guideDrag = null;
        ReleaseCapture();

        // Dropped on a ruler or outside the canvas: the guide is removed.
        var inside = e.GetPosition(Viewport);
        bool onCanvas = inside.X >= 0 && inside.Y >= 0 && inside.X <= Viewport.ActualWidth && inside.Y <= Viewport.ActualHeight;
        var list = guide.Vertical ? _document.VerticalGuides : _document.HorizontalGuides;
        if (!onCanvas && guide.Index < list.Count) list.RemoveAt(guide.Index);
        _document.NotifyGuidesChanged();
        return true;
    }

    /// <summary>A resize cursor while the mouse is over a guide (Select / Move tool).</summary>
    private void UpdateGuideCursor(Point viewportPosition)
    {
        if (GuideAt(viewportPosition) is { } guide)
        {
            Viewport.Cursor = guide.Vertical ? Cursors.SizeWE : Cursors.SizeNS;
            _guideCursor = true;
        }
        else if (_guideCursor)
        {
            _guideCursor = false;
            UpdateCursor();
        }
    }
}
