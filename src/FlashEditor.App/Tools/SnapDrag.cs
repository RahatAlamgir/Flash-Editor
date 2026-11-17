using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Snapping;

namespace FlashEditor.App.Tools;

/// <summary>
/// Snapping for one drag of a tool: the layers are looked at the first time it is needed, then every mouse move asks "does this snap?".
/// Shows the pink guide lines while something is snapped; <see cref="End"/> hides them. Holding Alt turns snapping off for a moment.
/// </summary>
public sealed class SnapDrag
{
    private const double ThresholdPixels = 6;   // on screen

    private readonly IToolHost _host;
    private SnapSession? _session;
    private bool _started;

    public SnapDrag(IToolHost host) => _host = host;

    private SnapSession? Session(IEnumerable<Layer> exclude)
    {
        if (!_started)
        {
            _started = true;
            _session = _host.CreateSnapSession(exclude);
        }
        return _session;
    }

    /// <summary>The area a layer covers (its object's outline, or its painted pixels).</summary>
    public static PixelRect BoundsOf(Layer layer) => layer.Object != null ? layer.Object.VisualBounds() : layer.VisibleBounds();

    /// <summary>
    /// A move of <paramref name="start"/> by (dx, dy): returns the move after snapping its edges and center to the nearest line.
    /// </summary>
    public (double Dx, double Dy) Move(IEnumerable<Layer> dragged, PixelRect start, double dx, double dy, double zoom, bool snapAllowed)
    {
        var session = snapAllowed ? Session(dragged) : null;
        if (session == null || start.IsEmpty) { Show(SnapResult.None); return (dx, dy); }

        var result = session.SnapBox(start.X + dx, start.Y + dy, start.Right + dx, start.Bottom + dy, ThresholdPixels / zoom);
        Show(result);
        return (dx + result.Dx, dy + result.Dy);
    }

    /// <summary>A mouse position (drawing a shape, dragging a handle or a corner point) moved onto the nearest line.</summary>
    public PointD Point(IEnumerable<Layer> dragged, PointD position, double zoom, bool snapAllowed)
    {
        var session = snapAllowed ? Session(dragged) : null;
        if (session == null) { Show(SnapResult.None); return position; }

        var result = session.SnapPoint(position, ThresholdPixels / zoom);
        Show(result);
        return new PointD(position.X + result.Dx, position.Y + result.Dy);
    }

    /// <summary>A mouse position moved onto the nearest line (nothing is being dragged, so every layer counts as a target).</summary>
    public PointD Point(PointD position, double zoom, bool snapAllowed) => Point(Array.Empty<Layer>(), position, zoom, snapAllowed);

    /// <summary>Ends the drag: forgets the layers and hides the snap lines.</summary>
    public void End()
    {
        _session = null;
        _started = false;
        _host.Canvas?.ShowSnapLines(Array.Empty<double>(), Array.Empty<double>(), Array.Empty<GapMark>());
    }

    private void Show(SnapResult result) => _host.Canvas?.ShowSnapLines(result.LinesX, result.LinesY, result.Gaps);
}
