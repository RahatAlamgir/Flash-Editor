using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;
using Selection = FlashEditor.Core.Selections.Selection;

namespace FlashEditor.App.Tools.Selecting;

/// <summary>
/// Moves pixels: the selected pixels of a layer, or a whole pixel layer.
/// <para>
/// Nothing is cut off at the canvas edge: pixels moved outside are kept on the layer (hidden) and come
/// back when moved back. For selections, the whole floating piece is remembered between moves, so
/// dragging a piece half off the canvas and back again restores it completely.
/// </para>
/// While dragging only the canvas-sized pixels are updated (fast); the full layer is rebuilt at the end.
/// </summary>
internal sealed class PixelMover
{
    /// <summary>A selected piece being moved (kept between moves until the layer is edited by something else).</summary>
    private sealed class FloatingPiece
    {
        public required Layer Layer { get; init; }
        public required Selection Original { get; init; }     // selection where the piece started
        public required PixelBuffer Pixels { get; init; }     // the piece itself (size of Original.Bounds)
        public required PixelBuffer Cleared { get; init; }    // full layer without the piece
        public required int ClearedX { get; init; }
        public required int ClearedY { get; init; }
        public int Dx, Dy;                                    // total offset from Original
        public Selection? CurrentSelection;                   // the selection last set by a move
        public long LayerVersion;                             // layer version right after the last move
    }

    private readonly IToolHost _host;
    private Layer? _layer;
    private LayerState? _before;          // copy of the layer before this move (for undo)
    private PixelBuffer? _source;         // whole-layer mode: full content before moving
    private int _sourceX, _sourceY;
    private FloatingPiece? _piece;        // selection mode
    private bool _pieceActive;
    private PixelBuffer? _clearedCanvas;  // canvas part of _piece.Cleared (fast preview)
    private int _baseDx, _baseDy;         // piece offset when this move started
    private PixelRect _lastRect;
    private int _dx, _dy;

    public PixelMover(IToolHost host) => _host = host;

    private Document Document => _host.Document.Model;

    public bool IsMoving => _layer != null;

    /// <summary>Starts moving the selection's pixels (or the whole layer without a selection).</summary>
    public void Begin(Layer layer)
    {
        if (IsMoving) return;
        _layer = layer;
        _before = layer.SaveState().DeepCopy();
        _dx = _dy = 0;

        var selection = Document.Selection;
        _pieceActive = selection != null;
        if (selection == null)
        {
            var (content, x, y) = layer.GetFullContent();
            (_source, _sourceX, _sourceY) = (content.Clone(), x, y);
            return;
        }

        // Continue with the remembered piece if nothing else touched the layer or selection since.
        bool canContinue = _piece != null && _piece.Layer == layer && _piece.LayerVersion == layer.Version
                           && ReferenceEquals(_piece.CurrentSelection, selection);
        if (!canContinue)
        {
            var (content, x, y) = layer.GetFullContent();
            var cleared = content.Clone();
            var canvas = layer.Pixels.Clone();
            SelectionPixels.Clear(canvas, selection);
            cleared.CopyFrom(canvas, canvas.Bounds, -x, -y);
            _piece = new FloatingPiece
            {
                Layer = layer,
                Original = selection,
                Pixels = SelectionPixels.Extract(layer.Pixels, selection),
                Cleared = cleared,
                ClearedX = x,
                ClearedY = y,
            };
        }

        _baseDx = _piece!.Dx;
        _baseDy = _piece.Dy;
        _clearedCanvas = new PixelBuffer(Document.Width, Document.Height);
        _clearedCanvas.CopyFrom(_piece.Cleared, _piece.ClearedX, _piece.ClearedY);
        _lastRect = PieceRect(_baseDx, _baseDy);
    }

    private PixelRect PieceRect(int dx, int dy) => _piece!.Original.Bounds.Offset(dx, dy);

    /// <summary>Shows the pixels moved by (dx, dy) from where this move started.</summary>
    public void MoveTo(int dx, int dy)
    {
        if (_layer == null || (dx == _dx && dy == _dy)) return;
        _dx = dx;
        _dy = dy;
        var pixels = _layer.Pixels;

        if (_pieceActive && _piece != null && _clearedCanvas != null)
        {
            var newRect = PieceRect(_baseDx + dx, _baseDy + dy);
            var dirty = _lastRect.Union(newRect).Union(PieceRect(_baseDx, _baseDy)).Intersect(pixels.Bounds);
            pixels.CopyFrom(_clearedCanvas, dirty, dirty.X, dirty.Y);
            Compositor.DrawOver(pixels, _piece.Pixels, newRect.X, newRect.Y);
            _layer.NotifyPixelsChanged(dirty);
            _lastRect = newRect;
            _host.Canvas?.SetSelectionOffset(dx, dy);
        }
        else if (_source != null)
        {
            // Whole layer: hidden parts come back into view as they cross the canvas edge.
            pixels.Clear();
            pixels.CopyFrom(_source, _sourceX + dx, _sourceY + dy);
            _layer.NotifyPixelsChanged();
        }
        _host.SetStatus($"Move: {dx:+0;-0;0}, {dy:+0;-0;0} px");
    }

    /// <summary>Finishes the move and returns its undo step (null when nothing moved).</summary>
    public IHistoryAction? End()
    {
        IHistoryAction? action = null;
        if (_layer != null && _before != null)
        {
            if (_dx == 0 && _dy == 0)
            {
                _layer.RestoreState(_before); // nothing moved; undo any preview
                if (_pieceActive && _piece != null) _piece.LayerVersion = _layer.Version;
            }
            else
            {
                _layer.RestoreState(BuildResult());
                action = new LayerContentAction("Move", _layer, _before, _layer.SaveState());

                if (_pieceActive && _piece != null)
                {
                    _piece.Dx = _baseDx + _dx;
                    _piece.Dy = _baseDy + _dy;
                    _piece.CurrentSelection = _piece.Original.Offset(_piece.Dx, _piece.Dy);
                    Document.Selection = _piece.CurrentSelection;
                    _piece.LayerVersion = _layer.Version;
                }
                _host.SetStatus(_layer.HasHiddenPixels
                    ? "Moved. Parts outside the canvas are kept (Image > Reveal All shows them)."
                    : "Moved.");
            }
            _host.Canvas?.SetSelectionOffset(0, 0);
        }
        _layer = null;
        _before = null;
        _source = _clearedCanvas = null;
        return action;
    }

    /// <summary>The final layer content, including parts that went past the canvas edge.</summary>
    private LayerState BuildResult()
    {
        int w = Document.Width, h = Document.Height;
        if (!_pieceActive || _piece == null)
            return LayerState.Place(_source!, _sourceX + _dx, _sourceY + _dy, w, h);

        // Drop the piece onto the layer without it, growing the buffer if it went past the edge.
        var clearedRect = new PixelRect(_piece.ClearedX, _piece.ClearedY, _piece.Cleared.Width, _piece.Cleared.Height);
        var pieceRect = PieceRect(_baseDx + _dx, _baseDy + _dy);
        var all = clearedRect.Union(pieceRect);
        var combined = new PixelBuffer(all.Width, all.Height);
        combined.CopyFrom(_piece.Cleared, clearedRect.X - all.X, clearedRect.Y - all.Y);
        Compositor.DrawOver(combined, _piece.Pixels, pieceRect.X - all.X, pieceRect.Y - all.Y);
        return LayerState.Place(combined, all.X, all.Y, w, h);
    }
}
