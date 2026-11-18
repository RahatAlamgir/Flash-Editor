using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Effects;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Layout;
using FlashEditor.Core.Objects;
using FlashEditor.Core.IO;
using FlashEditor.Core.Painting;
using FlashEditor.Core.Transforms;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>Image menu: operations on the whole canvas (all layers).</summary>
public sealed class ImageCommands
{
    private readonly MainViewModel _main;

    public ImageCommands(MainViewModel main)
    {
        _main = main;
        ImageSizeCommand = new RelayCommand(ImageSize);
        CanvasSizeCommand = new RelayCommand(CanvasSize);
        SplitSheetCommand = new RelayCommand(SplitSheet);
        Upscale2xCommand = new RelayCommand(Upscale2x, () => Doc.Width * 2 <= ImageDecoder.MaxDimension && Doc.Height * 2 <= ImageDecoder.MaxDimension);
        RevealAllCommand = new RelayCommand(RevealAll);
        TrimCommand = new RelayCommand(Trim);
        CropToSelectionCommand = new RelayCommand(CropToSelection, () => Doc.Selection != null);
        Rotate90ClockwiseCommand = new RelayCommand(() => Transform("Rotate 90° CW", ImageTransforms.Rotate90Clockwise, RotateObjects(90)));
        Rotate90CounterClockwiseCommand = new RelayCommand(() => Transform("Rotate 90° CCW", ImageTransforms.Rotate90CounterClockwise, RotateObjects(-90)));
        Rotate180Command = new RelayCommand(() => Transform("Rotate 180°", ImageTransforms.Rotate180, RotateObjects(180)));
        RotateCustomCommand = new RelayCommand(RotateCustom);
        StraightenCommand = new RelayCommand(Straighten);
        MagicResizeCommand = new RelayCommand(MagicResizeDocument);
        PerspectiveCropCommand = new RelayCommand(() => _main.Tools.ActiveTool = _main.Tools.Get<Tools.Transform.PerspectiveCropTool>());
        FlipHorizontalCommand = new RelayCommand(() => Transform("Flip Horizontal", ImageTransforms.FlipHorizontal,
            (o, w, h) => ObjectTransforms.FlipHorizontal(o, w)));
        FlipVerticalCommand = new RelayCommand(() => Transform("Flip Vertical", ImageTransforms.FlipVertical,
            (o, w, h) => ObjectTransforms.FlipVertical(o, h)));
    }

    public RelayCommand ImageSizeCommand { get; }
    public RelayCommand CanvasSizeCommand { get; }
    public RelayCommand SplitSheetCommand { get; }
    public RelayCommand Upscale2xCommand { get; }
    public RelayCommand RevealAllCommand { get; }
    public RelayCommand TrimCommand { get; }
    public RelayCommand CropToSelectionCommand { get; }
    public RelayCommand Rotate90ClockwiseCommand { get; }
    public RelayCommand Rotate90CounterClockwiseCommand { get; }
    public RelayCommand Rotate180Command { get; }
    public RelayCommand RotateCustomCommand { get; }
    public RelayCommand StraightenCommand { get; }
    public RelayCommand MagicResizeCommand { get; }
    public RelayCommand PerspectiveCropCommand { get; }
    public RelayCommand FlipHorizontalCommand { get; }
    public RelayCommand FlipVerticalCommand { get; }

    private Document Doc => _main.Document.Model;

    private void ImageSize()
    {
        var size = _main.Dialogs.ShowImageSize(Doc.Width, Doc.Height);
        if (size == null || (size.Width == Doc.Width && size.Height == Doc.Height)) return;
        double sx = size.Width / (double)Doc.Width, sy = size.Height / (double)Doc.Height;
        Transform("Image Size", p => Resampler.Resize(p, size.Width, size.Height, size.Mode),
            (o, w, h) => ObjectTransforms.Scale(o, sx, sy));
    }

    /// <summary>
    /// Splits an asset / sprite sheet into its separate items: as layers (each a movable, resizable
    /// picture at its original place) and / or as PNG files.
    /// </summary>
    private void SplitSheet()
    {
        var sheet = Core.Painting.Compositor.Flatten(Doc);
        var name = System.IO.Path.GetFileNameWithoutExtension(_main.Document.DisplayName);
        var folder = _main.Document.FilePath is { } path ? System.IO.Path.GetDirectoryName(path) : null;
        var result = _main.Dialogs.ShowSplitSheet(sheet, name, folder, Doc.Selection?.Mask);
        if (result == null) return;

        _main.Tools.ResetActiveTool();
        int saved = 0;
        if (result.Folder != null)
        {
            _main.Run("Saving the pieces", () =>
            {
                System.IO.Directory.CreateDirectory(result.Folder);
                int digits = Math.Max(2, result.Pieces.Count.ToString().Length);
                for (int i = 0; i < result.Pieces.Count; i++)
                {
                    string file = UniquePath(result.Folder, $"{result.BaseName}_{(i + 1).ToString().PadLeft(digits, '0')}", ".png");
                    ImageEncoder.Save(result.Pieces[i].Pixels, file, ImageFileFormat.Png);
                    saved++;
                }
            });
        }

        if (result.AsLayers) AddPiecesAsLayers(result);
        Doc.Selection = null;
        _main.SetStatus(result.Folder != null
            ? $"Split into {result.Pieces.Count} pieces; saved {saved} PNG files to {result.Folder}."
            : $"Split into {result.Pieces.Count} pieces, each on its own layer.");
    }

    private void AddPiecesAsLayers(Services.SplitSheetResult result)
    {
        var originals = Doc.Layers.Where(l => l.IsVisible).ToList();
        var layers = result.Pieces.Select((piece, i) =>
        {
            var layer = new Layer($"{result.BaseName} {i + 1}", Doc.Width, Doc.Height);
            var b = piece.Bounds;
            layer.SetObject(new ImageObject(new ObjectBox(b.X, b.Y, b.Width, b.Height), piece.Pixels, layer.Name));
            return layer;
        }).ToList();

        var steps = new List<IHistoryAction?>
        {
            LayerStructureAction.Record("Split Sheet", Doc, () =>
            {
                var all = Doc.Layers.ToList();
                all.AddRange(layers);
                Doc.SetLayers(all, layers[^1]);
            }),
        };
        if (result.HideOriginal)
        {
            foreach (var layer in originals)
            {
                layer.IsVisible = false;
                steps.Add(new LayerPropertyAction(layer, LayerProperty.Visibility, true, false));
            }
        }
        _main.Push(new CompositeAction("Split Asset Sheet", steps));
        _main.SetSelection(layers, layers[^1]);
    }

    private static string UniquePath(string folder, string name, string extension)
    {
        string path = System.IO.Path.Combine(folder, name + extension);
        for (int n = 2; System.IO.File.Exists(path); n++)
            path = System.IO.Path.Combine(folder, $"{name} ({n}){extension}");
        return path;
    }

    /// <summary>
    /// Doubles the resolution with the sharpest resize method, then cleans it up with Enhance
    /// (for small or blurry pictures). Shapes and text stay editable.
    /// </summary>
    private void Upscale2x()
    {
        var enhance = new Core.Effects.Filters.EnhanceEffect();
        var values = enhance.CreateDefaultValues();
        values["sharpen"] = 90;
        values["radius"] = 1.2;
        values["clarity"] = 20;
        values["denoise"] = 20;
        int w = Doc.Width * 2, h = Doc.Height * 2;
        Transform("Upscale 2x", p => enhance.Apply(Resampler.Resize(p, w, h, ResampleMode.Lanczos), values),
            (o, _, _) => ObjectTransforms.Scale(o, 2, 2));
    }

    /// <summary>Adds or removes space around the picture (no scaling).</summary>
    private void CanvasSize()
    {
        var settings = _main.Dialogs.ShowCanvasSize(Doc.Width, Doc.Height);
        if (settings == null || (settings.Width == Doc.Width && settings.Height == Doc.Height)) return;
        int shiftX = (int)Math.Round((settings.Width - Doc.Width) * settings.AnchorX);
        int shiftY = (int)Math.Round((settings.Height - Doc.Height) * settings.AnchorY);
        Reframe("Canvas Size", settings.Width, settings.Height, shiftX, shiftY);
    }

    /// <summary>Grows the canvas so pixels that were moved past its edge become visible again.</summary>
    private void RevealAll()
    {
        var all = Doc.Bounds;
        foreach (var layer in Doc.Layers)
        {
            var visible = layer.VisibleBounds();
            if (!visible.IsEmpty) all = all.Union(visible);
        }
        if (all == Doc.Bounds)
        {
            _main.SetStatus("Reveal All: nothing is outside the canvas.");
            return;
        }
        if (all.Width > ImageDecoder.MaxDimension || all.Height > ImageDecoder.MaxDimension)
        {
            _main.Dialogs.ShowError($"The result would be larger than {ImageDecoder.MaxDimension} px.");
            return;
        }
        Reframe("Reveal All", all.Width, all.Height, -all.X, -all.Y);
    }

    /// <summary>Crops away fully transparent borders around the visible picture.</summary>
    private void Trim()
    {
        var bounds = Compositor.Flatten(Doc).VisibleBounds();
        if (bounds.IsEmpty || bounds == Doc.Bounds)
        {
            _main.SetStatus(bounds.IsEmpty ? "Trim: the image is completely transparent." : "Trim: no transparent edges to remove.");
            return;
        }
        Reframe("Trim", bounds.Width, bounds.Height, -bounds.X, -bounds.Y);
    }

    private void Reframe(string name, int width, int height, int shiftX, int shiftY)
    {
        _main.Tools.ResetActiveTool();
        _main.Run(name, () =>
        {
            _main.Push(CanvasChangeAction.Reframe(name, Doc, width, height, shiftX, shiftY));
            _main.Canvas?.FitToWindow();
            _main.SetStatus($"{name}: {Doc.Width} x {Doc.Height}");
        });
    }

    private void CropToSelection()
    {
        if (Doc.Selection is not { } selection) return;
        var rect = selection.Bounds;
        Reframe("Crop", rect.Width, rect.Height, -rect.X, -rect.Y);
    }

    private void RotateCustom()
    {
        var parameter = new EffectParameter("angle", "Angle (clockwise)", -180, 180, 15, 0.5, "°");
        var values = _main.Dialogs.ShowParameters("Rotate Image", new[] { parameter },
            new EffectValues(new[] { parameter }), null, "The canvas grows so no corner is cut off. New areas are transparent.");
        if (values == null || values["angle"] == 0) return;
        double angle = values["angle"];
        Transform($"Rotate {angle:0.#}°", p => ImageTransforms.Rotate(p, angle), RotateObjects(angle));
    }

    /// <summary>
    /// Magic Resize: the design becomes another size and keeps its layout. Objects stay editable (they keep their proportions and relative
    /// place; text scales), layers of plain pixels are resampled.
    /// </summary>
    private void MagicResizeDocument()
    {
        var pages = _main.Document.Project.Pages;
        var settings = _main.Dialogs.ShowMagicResize(Doc.Width, Doc.Height, pages.Count);
        if (settings == null) return;

        _main.Tools.ResetActiveTool();
        var targets = settings.AllPages ? pages.ToList() : new List<DocumentViewModel> { _main.Document };
        int changed = 0;
        _main.Run("Magic Resize", () =>
        {
            foreach (var page in targets)
            {
                var model = page.Model;
                if (model.Width == settings.Width && model.Height == settings.Height) continue;
                int oldW = model.Width, oldH = model.Height, newW = settings.Width, newH = settings.Height;
                var action = CanvasChangeAction.Apply("Magic Resize", model,
                    p => MagicResize.MapPixels(p, newW, newH),
                    (o, _, _) => MagicResize.MapObject(o, oldW, oldH, newW, newH));
                model.History.Push(action);
                changed++;
            }
            _main.Canvas?.FitToWindow();
            _main.SetStatus(changed == 0
                ? "The picture already has that size."
                : $"Magic Resize: {changed} page{(changed == 1 ? "" : "s")} now {settings.Width} x {settings.Height} px. Ctrl+Z undoes it.");
        });
    }

    /// <summary>Straighten: turn the picture a little (a tilted horizon) and, if wanted, cut off the empty corners that the turn leaves.</summary>
    private void Straighten()
    {
        var angle = new EffectParameter("angle", "Angle (clockwise)", -45, 45, 0, 0.1, "°");
        var crop = EffectParameter.Toggle("crop", "Cut off the empty corners", true);
        var values = _main.Dialogs.ShowParameters("Straighten", new[] { angle, crop },
            new EffectValues(new[] { angle, crop }), null,
            "For a tilted horizon or a crooked photo: turn the picture until it is level. Positive turns clockwise.");
        if (values == null || values["angle"] == 0) return;
        double degrees = values["angle"];
        bool cut = values["crop"] >= 0.5;
        Transform($"Straighten {degrees:0.#}°", p => PerspectiveTools.Straighten(p, degrees, cut), RotateObjects(degrees));
    }

    /// <summary>Objects turn with the image around its center (the canvas may grow).</summary>
    private Func<LayerObject, int, int, LayerObject> RotateObjects(double degrees)
    {
        int oldW = Doc.Width, oldH = Doc.Height;
        return (o, w, h) => ObjectTransforms.Rotate(o, degrees, oldW, oldH, w, h);
    }

    private void Transform(string name, Func<PixelBuffer, PixelBuffer> transform,
        Func<LayerObject, int, int, LayerObject>? objectTransform = null)
    {
        _main.Tools.ResetActiveTool();
        _main.Run(name, () =>
        {
            _main.Push(CanvasChangeAction.Apply(name, Doc, transform, objectTransform));
            _main.Canvas?.FitToWindow();
            _main.SetStatus($"{name}: {Doc.Width} x {Doc.Height}");
        });
    }
}
