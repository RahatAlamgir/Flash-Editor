using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using FlashEditor.Core.Snapping;

namespace FlashEditor.App.Tools;

/// <summary>What tools and view commands may ask of the canvas: overlays (previews) and zoom/pan.</summary>
public interface ICanvasView
{
    /// <summary>Current zoom factor (1 = 100%).</summary>
    double Zoom { get; }

    /// <summary>
    /// Adds a preview shape drawn in image coordinates above the layers. When
    /// <paramref name="screenStrokeThickness"/> is above 0 the stroke keeps that width on screen
    /// at every zoom level (good for outlines); 0 leaves the stroke in image pixels.
    /// </summary>
    void AddOverlay(Shape shape, double screenStrokeThickness = 1);

    void RemoveOverlay(Shape shape);

    /// <summary>
    /// Shows the outline of a selection that is still being drawn (rectangle or lasso), in the same
    /// high-contrast style as the real selection. Pass null to hide it.
    /// </summary>
    void ShowSelectionPreview(Geometry? outline);

    /// <summary>Redraws the box with handles around the selected layer.</summary>
    void RefreshTransformFrame();

    /// <summary>Shows the lines something just snapped to (vertical lines at x, horizontal lines at y); empty lists hide them.</summary>
    void ShowSnapLines(IReadOnlyList<double> xs, IReadOnlyList<double> ys, IReadOnlyList<GapMark>? gaps = null);

    /// <summary>Cursor to show instead of the tool's own (null = the tool's cursor).</summary>
    void SetCursorOverride(System.Windows.Input.Cursor? cursor);

    /// <summary>Temporarily shifts the selection outline while the Move tool drags it.</summary>
    void SetSelectionOffset(double dx, double dy);

    void PanBy(Vector screenDelta);
    void ZoomAt(Point viewportPoint, double newZoom);
    void ZoomIn();
    void ZoomOut();
    void FitToWindow();
    void ActualSize();
}
