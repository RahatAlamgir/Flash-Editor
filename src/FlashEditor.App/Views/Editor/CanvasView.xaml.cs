using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FlashEditor.App.Rendering;
using FlashEditor.App.Tools;
using FlashEditor.App.ViewModels;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Views.Editor;

/// <summary>
/// The editing area. Shows the layers with zoom and pan, draws overlays (selection, previews,
/// brush outline) and forwards mouse input to the active tool.
/// </summary>
public partial class CanvasView : UserControl, ICanvasView
{
    private const double MinZoom = 0.02;
    private const double MaxZoom = 64;
    private static readonly double[] ZoomSteps =
        { 0.02, 0.05, 0.1, 0.167, 0.25, 0.333, 0.5, 0.667, 1, 1.5, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64 };

    private readonly List<(Shape Shape, double Thickness)> _overlays = new();
    private readonly DispatcherTimer _antsTimer;
    private readonly DrawingBrush _checker = CheckerboardBrush.Create(1);

    private MainViewModel? _main;
    private DocumentViewModel? _document;
    private double _zoom = 1;
    private Vector _offset;
    private bool _fitPending = true;

    // Mouse state
    private bool _spaceHeld;
    private Point? _panLast;
    private bool _toolDragging;
    private bool _releasingCapture;
    private ToolMouseArgs? _lastArgs;

    public CanvasView()
    {
        InitializeComponent();
        PageBackground.Fill = _checker;
        SelectionLight.StrokeDashArray = new DoubleCollection { 3, 3 };
        PreviewLight.StrokeDashArray = new DoubleCollection { 3, 3 };

        RegisterOverlay(PageBorder, 1);
        RegisterOverlay(SelectionDark, 3);
        RegisterOverlay(SelectionLight, 1.6);
        RegisterOverlay(PreviewDark, 3);
        RegisterOverlay(PreviewLight, 1.6);
        RegisterOverlay(FrameOutline, 1.5);
        RegisterOverlay(FrameHandles, 1.2);
        RegisterOverlay(BrushCursorOuter, 3);
        RegisterOverlay(BrushCursorInner, 1);

        // "Marching ants" animation for the selection outline.
        _antsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _antsTimer.Tick += (_, _) =>
        {
            SelectionLight.StrokeDashOffset = (SelectionLight.StrokeDashOffset + 0.5) % 6;
            PreviewLight.StrokeDashOffset = SelectionLight.StrokeDashOffset;
        };
    }

    public double Zoom => _zoom;

    private ToolBase? ActiveTool => _main?.Tools.ActiveTool;

    /// <summary>Connects the canvas to the main view model (called once by the main window).</summary>
    public void Attach(MainViewModel main)
    {
        _main = main;
        main.Canvas = this;
        main.PropertyChanged += OnMainPropertyChanged;
        main.Tools.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ToolsViewModel.ActiveTool)) OnToolChanged();
        };
        main.SelectedLayersChanged += (_, _) => RefreshTransformFrame();
        main.ActivePartChanged += (_, _) => RefreshTransformFrame();
        AttachDocument(main.Document);
        OnToolChanged();
    }

    /// <summary>Space held = temporary hand tool.</summary>
    public void SetSpacePan(bool held)
    {
        if (_spaceHeld == held) return;
        _spaceHeld = held;
        UpdateCursor();
    }

    // ------------------------------------------------------------------ document

    private void OnMainPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsReshaping)) RefreshTransformFrame();
        if (e.PropertyName == nameof(MainViewModel.Document) && _main != null)
            AttachDocument(_main.Document);
    }

    private void AttachDocument(DocumentViewModel document)
    {
        if (_document != null)
        {
            _document.Model.PropertyChanged -= OnDocumentPropertyChanged;
            // Remember where this picture was zoomed / scrolled to, for when its tab is shown again.
            _document.ViewZoom = _zoom;
            _document.ViewOffset = _offset;
        }
        _document = document;
        DataContext = document;
        document.Model.PropertyChanged += OnDocumentPropertyChanged;
        UpdatePageSize();
        UpdateSelectionOutline();
        RefreshTransformFrame();
        if (document.ViewZoom is { } zoom)
        {
            _zoom = zoom;
            _offset = document.ViewOffset;
            _fitPending = false;
            ApplyTransform();
            return;
        }
        _fitPending = true;
        if (Viewport.ActualWidth > 0) FitToWindow();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Document.Selection): UpdateSelectionOutline(); break;
            case nameof(Document.ActiveLayer): RefreshTransformFrame(); break;
            case nameof(Document.Width):
            case nameof(Document.Height): UpdatePageSize(); RefreshTransformFrame(); break;
        }
    }

    private void UpdatePageSize()
    {
        if (_document == null) return;
        double w = _document.Width, h = _document.Height;
        PageBackground.Width = PageBorder.Width = w;
        PageBackground.Height = PageBorder.Height = h;
    }

    private void UpdateSelectionOutline()
    {
        var selection = _document?.Model.Selection;
        if (selection == null)
        {
            SelectionFill.Data = SelectionDark.Data = SelectionLight.Data = null;
            UpdateAntsTimer();
            return;
        }

        var geometry = new PathGeometry { FillRule = FillRule.EvenOdd };
        foreach (var polygon in selection.Outline)
        {
            if (polygon.Count < 2) continue;
            var figure = new PathFigure { StartPoint = new Point(polygon[0].X, polygon[0].Y), IsClosed = true, IsFilled = true };
            figure.Segments.Add(new PolyLineSegment(polygon.Skip(1).Select(p => new Point(p.X, p.Y)), true));
            geometry.Figures.Add(figure);
        }
        geometry.Freeze();
        SelectionFill.Data = SelectionDark.Data = SelectionLight.Data = geometry;
        UpdateAntsTimer();
    }

    private void UpdateAntsTimer()
    {
        bool any = SelectionLight.Data != null || PreviewLight.Data != null;
        if (any) _antsTimer.Start(); else _antsTimer.Stop();
    }

    // ------------------------------------------------------------------ ICanvasView

    public void AddOverlay(Shape shape, double screenStrokeThickness = 1)
    {
        // Insert below the brush cursor so the cursor stays on top.
        OverlayCanvas.Children.Insert(OverlayCanvas.Children.IndexOf(BrushCursorOuter), shape);
        RegisterOverlay(shape, screenStrokeThickness);
    }

    public void RemoveOverlay(Shape shape)
    {
        OverlayCanvas.Children.Remove(shape);
        _overlays.RemoveAll(o => o.Shape == shape);
    }

    public void ShowSelectionPreview(Geometry? outline)
    {
        // Fill only closed shapes (lasso is closed on release; while drawing we still tint it).
        PreviewFill.Data = PreviewDark.Data = PreviewLight.Data = outline;
        UpdateAntsTimer();
    }

    public void SetSelectionOffset(double dx, double dy)
        => SelectionLayer.RenderTransform = dx == 0 && dy == 0 ? Transform.Identity : new TranslateTransform(dx, dy);

    public void PanBy(Vector screenDelta)
    {
        _offset += screenDelta;
        ApplyTransform();
    }

    public void ZoomAt(Point viewportPoint, double newZoom)
    {
        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        // Keep the image point under the mouse in the same place.
        var imagePoint = ((Vector)viewportPoint - _offset) / _zoom;
        _zoom = newZoom;
        _offset = (Vector)viewportPoint - imagePoint * _zoom;
        ApplyTransform();
    }

    public void ZoomIn() => ZoomAt(ViewportCenter, ZoomSteps.FirstOrDefault(z => z > _zoom * 1.001, MaxZoom));

    public void ZoomOut() => ZoomAt(ViewportCenter, ZoomSteps.LastOrDefault(z => z < _zoom * 0.999, MinZoom));

    public void ActualSize() => ZoomAt(ViewportCenter, 1);

    public void FitToWindow()
    {
        if (_document == null) return;
        double vw = Viewport.ActualWidth, vh = Viewport.ActualHeight;
        if (vw <= 0 || vh <= 0)
        {
            _fitPending = true;
            return;
        }
        _fitPending = false;
        double margin = 40;
        double fit = Math.Min((vw - margin) / _document.Width, (vh - margin) / _document.Height);
        _zoom = Math.Clamp(Math.Min(fit, 8), MinZoom, MaxZoom);
        _offset = new Vector((vw - _document.Width * _zoom) / 2, (vh - _document.Height * _zoom) / 2);
        ApplyTransform();
    }

    private Point ViewportCenter => new(Viewport.ActualWidth / 2, Viewport.ActualHeight / 2);

    private void ApplyTransform()
    {
        WorldTransform.Matrix = new Matrix(_zoom, 0, 0, _zoom, Math.Round(_offset.X), Math.Round(_offset.Y));

        // Outlines keep a constant on-screen width.
        foreach (var (shape, thickness) in _overlays)
            if (thickness > 0) shape.StrokeThickness = thickness / _zoom;

        // Checkerboard squares stay 8 screen pixels.
        double cell = 8 / _zoom;
        _checker.Viewport = new Rect(0, 0, cell * 2, cell * 2);

        RenderOptions.SetBitmapScalingMode(LayersHost,
            _zoom >= 2 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.Linear);

        if (_main != null) _main.Zoom = _zoom;
        DrawTransformFrame(); // handles keep their screen size
    }

    private void RegisterOverlay(Shape shape, double thickness)
    {
        _overlays.Add((shape, thickness));
        if (thickness > 0) shape.StrokeThickness = thickness / _zoom;
    }

    // ------------------------------------------------------------------ tool / cursor

    private void OnToolChanged()
    {
        _cursorOverride = null;
        RefreshTransformFrame();
        UpdateCursor();
        UpdateBrushCursor(null);
    }

    private void UpdateCursor()
    {
        bool panning = _spaceHeld || _panLast != null;
        Viewport.Cursor = panning ? Cursors.Hand : _cursorOverride ?? ActiveTool?.Cursor ?? Cursors.Arrow;
    }

    private void UpdateBrushCursor(PointD? position)
    {
        double size = ActiveTool?.BrushPreviewSize ?? 0;
        bool show = position != null && size > 0 && !_spaceHeld && _panLast == null;
        var visibility = show ? Visibility.Visible : Visibility.Collapsed;
        BrushCursorOuter.Visibility = BrushCursorInner.Visibility = visibility;
        if (!show) return;

        foreach (var ellipse in new[] { BrushCursorOuter, BrushCursorInner })
        {
            ellipse.Width = ellipse.Height = size;
            System.Windows.Controls.Canvas.SetLeft(ellipse, position!.Value.X - size / 2);
            System.Windows.Controls.Canvas.SetTop(ellipse, position.Value.Y - size / 2);
        }
    }

    // ------------------------------------------------------------------ mouse

    private ToolMouseArgs CreateArgs(MouseEventArgs e, MouseButton? button)
    {
        var p = e.GetPosition(World);
        return new ToolMouseArgs(new PointD(p.X, p.Y), e.GetPosition(Viewport), button,
            e.LeftButton == MouseButtonState.Pressed, Keyboard.Modifiers,
            e is MouseButtonEventArgs b ? b.ClickCount : 0);
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        Viewport.Focus();
        if (_panLast != null || _toolDragging) return;

        if (e.ChangedButton == MouseButton.Middle || (_spaceHeld && e.ChangedButton == MouseButton.Left))
        {
            _panLast = e.GetPosition(Viewport);
            Viewport.CaptureMouse();
            UpdateCursor();
            UpdateBrushCursor(null);
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Right && !ToolUsesRightButton)
        {
            var p = e.GetPosition(World);
            ShowQuickMenu(new PointD(p.X, p.Y));
            e.Handled = true;
            return;
        }

        if (ActiveTool == null || e.ChangedButton is not (MouseButton.Left or MouseButton.Right)) return;
        _toolDragging = true;
        Viewport.CaptureMouse();
        _lastArgs = CreateArgs(e, e.ChangedButton);
        ActiveTool.MouseDown(_lastArgs);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var args = CreateArgs(e, null);
        UpdateStatus(args);

        if (_panLast is Point last)
        {
            var now = e.GetPosition(Viewport);
            PanBy(now - last);
            _panLast = now;
            return;
        }

        UpdateBrushCursor(args.Position);
        _lastArgs = args;
        ActiveTool?.MouseMove(args);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panLast != null)
        {
            if (e.ChangedButton is MouseButton.Middle or MouseButton.Left)
            {
                _panLast = null;
                ReleaseCapture();
                UpdateCursor();
            }
            return;
        }

        if (!_toolDragging) return;
        _toolDragging = false;
        ReleaseCapture(); // release before the tool runs: it may open a dialog
        ActiveTool?.MouseUp(CreateArgs(e, e.ChangedButton));
        e.Handled = true;
    }

    /// <summary>Capture lost unexpectedly (e.g. Alt+Tab): finish the tool action cleanly.</summary>
    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_releasingCapture) return;
        _panLast = null;
        if (_toolDragging && _lastArgs != null)
        {
            _toolDragging = false;
            ActiveTool?.MouseUp(_lastArgs);
        }
        UpdateCursor();
    }

    private void ReleaseCapture()
    {
        _releasingCapture = true;
        try { Viewport.ReleaseMouseCapture(); }
        finally { _releasingCapture = false; }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = Math.Pow(1.2, e.Delta / 120.0);
        ZoomAt(e.GetPosition(Viewport), _zoom * factor);
        e.Handled = true;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        UpdateBrushCursor(null);
        if (_main != null) _main.CursorText = "";
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitPending) FitToWindow();
    }

    private void UpdateStatus(ToolMouseArgs args)
    {
        if (_main == null || _document == null) return;
        bool inside = args.X >= 0 && args.Y >= 0 && args.X < _document.Width && args.Y < _document.Height;
        _main.CursorText = inside ? $"X {args.X}   Y {args.Y}" : "";
    }
}
