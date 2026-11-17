using System.Diagnostics;
using System.Windows.Input;
using FlashEditor.App.Rendering;
using FlashEditor.App.Tools.Options;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Tools.Painting;

/// <summary>
/// Refine brush, for fixing the result of Remove Background:
/// <list type="bullet">
/// <item><b>Restore</b> paints back what was removed by mistake (from the picture as it was before).</item>
/// <item><b>Remove</b> paints away background that was missed.</item>
/// </list>
/// Hold Alt to use the other mode while painting. [ and ] change the size. Each stroke is one undo step.
/// <para>
/// A cut-out is a picture object that remembers the original picture. The brush paints on the picture's own pixels, so it keeps
/// working after the picture was moved, resized, rotated or mirrored, and the shadow follows the edited edge.
/// </para>
/// </summary>
public sealed class RefineTool : ToolBase
{
    private const int RedrawEveryMilliseconds = 35; // large pictures take a moment to redraw: not on every mouse move

    private readonly RefineOptions _options = new();

    // Painting on a pixel layer (older way)
    private BrushStroke? _stroke;
    private Layer? _layer;

    // Painting on a picture object
    private BrushStroke? _pictureStroke;
    private Layer? _pictureLayer;
    private ImageObject? _pictureBefore;
    private PixelBuffer? _pictureWork;      // the picture's pixels being edited (a copy: the undo step keeps the old ones)
    private PixelBuffer? _pictureOriginal;
    private int _redraws;
    private bool _pictureDirty;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastRedraw;
    private bool _pictureRestoring;

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
        if (e.Button != MouseButton.Left) return;

        // A picture object (the cut-out left by Remove Background, or a placed picture): paint on the picture itself.
        if (Document.ActiveLayer is { Object: ImageObject picture } pictureLayer)
        {
            BeginOnPicture(pictureLayer, picture, e);
            return;
        }

        if (!TryGetEditableLayer(out var layer)) return;
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
            _stroke = new BrushStroke(layer.Pixels, _options.ToSettings(), ColorRgba.Transparent, erase: true, Document.Selection);
        }
        _layer = layer;
        layer.NotifyPixelsChanged(_stroke.AddPoint(e.Position));
    }

    public override void MouseMove(ToolMouseArgs e)
    {
        if (_pictureStroke != null)
        {
            PaintOnPicture(e.Position);
            return;
        }
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

    // ------------------------------------------------------------------ painting on a picture object

    private void BeginOnPicture(Layer layer, ImageObject picture, ToolMouseArgs e)
    {
        if (!layer.IsVisible)
        {
            Host.SetStatus("The active layer is hidden. Show it (eye icon) to edit it.");
            return;
        }
        if (layer.IsLocked)
        {
            Host.SetStatus($"\"{layer.Name}\" is locked. Unlock it with the lock icon in the Layers panel, or Ctrl+L.");
            return;
        }

        // What Restore paints back: the picture before its background was removed. A picture that never had its background
        // removed uses its current pixels, so what Remove takes away can still be restored.
        var original = picture.Original ?? picture.Source;
        if (original.Width != picture.Source.Width || original.Height != picture.Source.Height)
        {
            Host.SetStatus("Can't restore: the saved original does not fit this picture any more.");
            return;
        }

        _pictureRestoring = _options.Restore != e.Alt; // Alt = the other mode, just for this stroke
        _pictureLayer = layer;
        _pictureBefore = picture;
        _pictureOriginal = original;
        _pictureWork = picture.Source.Clone();
        _redraws = 0;
        _pictureDirty = false;

        // The brush size is in canvas pixels; on the picture's own pixels it covers more or fewer, depending on how it is shown.
        var settings = _options.ToSettings() with { Size = Math.Max(1, _options.Size * picture.SourcePixelsPerCanvasPixel) };
        _pictureStroke = _pictureRestoring
            ? new BrushStroke(_pictureWork, settings, original, selection: null)
            : new BrushStroke(_pictureWork, settings, ColorRgba.Transparent, erase: true, selection: null);

        PaintOnPicture(e.Position);
    }

    private void PaintOnPicture(PointD canvasPoint)
    {
        if (_pictureStroke == null || _pictureBefore == null) return;
        var dirty = _pictureStroke.AddPoint(_pictureBefore.ToSource(canvasPoint));
        if (!dirty.IsEmpty) _pictureDirty = true;
        ShowPictureEdit(force: false);
    }

    /// <summary>Shows the edited picture on the layer (at most every few milliseconds, or right away when <paramref name="force"/>).</summary>
    private void ShowPictureEdit(bool force)
    {
        if (!_pictureDirty || _pictureLayer == null || _pictureBefore == null || _pictureWork == null || _pictureOriginal == null) return;
        if (!force && _clock.ElapsedMilliseconds - _lastRedraw < RedrawEveryMilliseconds) return;

        // Each redraw is a new revision, so the layer sees a change even though the pixel buffer is the same one.
        ObjectRenderer.InvalidateBitmap(_pictureWork);
        _pictureLayer.SetObject(_pictureBefore with
        {
            Source = _pictureWork,
            Original = _pictureOriginal,
            Revision = _pictureBefore.Revision + ++_redraws,
        });
        _pictureDirty = false;
        _lastRedraw = _clock.ElapsedMilliseconds;
    }

    private void FinishPicture()
    {
        ShowPictureEdit(force: true);
        if (_pictureLayer is { Object: ImageObject after } && _pictureBefore != null && !ReferenceEquals(after.Source, _pictureBefore.Source))
            Push(new ObjectChangeAction(_pictureRestoring ? "Refine: Restore" : "Refine: Remove", _pictureLayer, _pictureBefore, after));

        _pictureStroke = null;
        _pictureLayer = null;
        _pictureBefore = null;
        _pictureWork = null;
        _pictureOriginal = null;
    }

    private void Finish()
    {
        if (_pictureStroke != null) FinishPicture();

        if (_stroke != null && _layer != null)
            Push(PixelChangeAction.Create(_options.Restore ? "Refine: Restore" : "Refine: Remove", _layer, _stroke.Original, _stroke.TotalDirty));
        _stroke = null;
        _layer = null;
    }
}
