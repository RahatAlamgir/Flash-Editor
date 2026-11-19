using System.IO;
using FlashEditor.App.Services;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.IO;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Barcodes;
using FlashEditor.Core.Qr;

namespace FlashEditor.App.ViewModels.Commands;

/// <summary>The Insert menu: things that are made for you from a little information (a QR code, a chart) and ready-made frames.</summary>
public sealed class InsertCommands
{
    private readonly MainViewModel _main;

    public InsertCommands(MainViewModel main)
    {
        _main = main;
        AddQrCommand = new RelayCommand(AddQr);
        AddBarcodeCommand = new RelayCommand(p => AddBarcode(Enum.Parse<BarcodeKind>((string)(p ?? "Code128"))));
        AddTextCommand = new RelayCommand(p => AddText((string)(p ?? "Heading")));
        AddIconCommand = new RelayCommand(p => AddIcon((string)(p ?? "Icons")));
        AddSmartArtCommand = new RelayCommand(AddSmartArt);
        AddChartCommand = new RelayCommand(p => AddChart(Enum.Parse<ChartKind>((string)(p ?? "Columns"))));
        AddShapeCommand = new RelayCommand(p => AddShape(Enum.Parse<ShapeKind>((string)p!)));
        AddFrameCommand = new RelayCommand(p => AddFrame((string)(p ?? "Rectangle")));
        PhotoGridCommand = new RelayCommand(PhotoGrid);
        FillFramesCommand = new RelayCommand(FillFrames, () => SelectedFrames().Count > 0);
    }

    public RelayCommand AddQrCommand { get; }

    public RelayCommand AddSmartArtCommand { get; }

    /// <summary>Parameter: Icons or Badges (which page of the picker opens first).</summary>
    public RelayCommand AddIconCommand { get; }

    /// <summary>Parameter: Heading, Subheading or Body.</summary>
    public RelayCommand AddTextCommand { get; }

    /// <summary>Parameter: Code128, Ean13, UpcA, Ean8 or Code39.</summary>
    public RelayCommand AddBarcodeCommand { get; }

    /// <summary>Parameter: Columns, Bars, Line, Pie or Donut.</summary>
    public RelayCommand AddChartCommand { get; }

    /// <summary>Parameter: the name of a <see cref="ShapeKind"/> (Rectangle, Ellipse, Star, Line...).</summary>
    public RelayCommand AddShapeCommand { get; }

    /// <summary>Parameter: Rectangle, Rounded, Circle, Heart or Star.</summary>
    public RelayCommand AddFrameCommand { get; }
    public RelayCommand PhotoGridCommand { get; }
    public RelayCommand FillFramesCommand { get; }

    /// <summary>Replaces the file dialog (used by tests).</summary>
    public Func<string, IReadOnlyList<string>>? PicturePicker { get; set; }

    private Core.Documents.Document Doc => _main.Document.Model;

    /// <summary>Asks for the text (a web address, a phone number...) and puts a QR code in the middle of the picture.</summary>
    private void AddQr()
    {
        var text = _main.Dialogs.ShowTextInput("Insert QR Code", "What should the QR code say? (a web address, a phone number, any text)", "https://");
        if (string.IsNullOrWhiteSpace(text)) return;
        if (System.Text.Encoding.UTF8.GetByteCount(text) > QrEncoder.MaxBytes(QrLevel.Medium))
        {
            _main.Dialogs.ShowInfo("Insert QR Code", "That text is too long for a QR code. Use a shorter text or a short web address.");
            return;
        }
        var qr = QrCodeObject.Centered(text.Trim(), Doc.Width, Doc.Height, Math.Max(40, Math.Min(Doc.Width, Doc.Height) * 0.35));
        _main.AddObjectLayer(qr, "Insert QR Code");
        _main.SetStatus("Added a QR code. Change its text, colors and style in Properties, and test it with your phone.");
    }

    /// <summary>Asks for the text and puts a barcode of the chosen kind in the middle of the picture.</summary>
    private void AddBarcode(BarcodeKind kind)
    {
        string sample = kind switch
        {
            BarcodeKind.Ean13 => "590123412345",
            BarcodeKind.UpcA => "03600029145",
            BarcodeKind.Ean8 => "9638507",
            BarcodeKind.Code39 => "FLASH-123",
            _ => "FLASH-12345",
        };
        string title = kind switch
        {
            BarcodeKind.Ean13 => "EAN-13", BarcodeKind.UpcA => "UPC-A", BarcodeKind.Ean8 => "EAN-8", BarcodeKind.Code39 => "Code 39", _ => "Code 128",
        };
        var text = _main.Dialogs.ShowTextInput($"Insert {title} Barcode", BarcodeEncoder.Describe(kind), sample);
        if (string.IsNullOrWhiteSpace(text)) return;
        if (BarcodeEncoder.Validate(text.Trim(), kind) is { } problem)
        {
            _main.Dialogs.ShowInfo($"Insert {title} Barcode", problem);
            return;
        }
        double w = Math.Max(120, Math.Min(Doc.Width * 0.5, Doc.Height * 1.2)), h = w * 0.42;
        var code = new BarcodeObject(new ObjectBox((Doc.Width - w) / 2, (Doc.Height - h) / 2, w, h), text.Trim(), kind, ColorRgba.Black, ColorRgba.White);
        _main.AddObjectLayer(code, "Insert Barcode");
        _main.SetStatus("Added a barcode. Change its text, type and colors in Properties. Test it with a scanner or your phone before printing.");
    }

    /// <summary>Asks for the words and puts a heading, subheading or body text in the middle of the picture.</summary>
    private void AddText(string kind)
    {
        var (title, sample, scale, bold) = kind switch
        {
            "Subheading" => ("Add a Subheading", "A subheading", 0.06, true),
            "Body" => ("Add Body Text", "A little bit of body text", 0.04, false),
            _ => ("Add a Heading", "Your heading", 0.11, true),
        };
        var text = _main.Dialogs.ShowTextInput(title, "Type your text (Enter = new line):", sample);
        if (string.IsNullOrWhiteSpace(text)) return;

        double size = Math.Max(14, Math.Round(Math.Min(Doc.Width, Doc.Height) * scale));
        var obj = new TextObject(new ObjectBox(0, 0, 1, 1), text.Replace("\r\n", "\n"), "Segoe UI", size, _main.Colors.Foreground, bold);
        var (w, h) = ObjectRendering.Renderer.MeasureText(obj);
        obj = obj with { Box = new ObjectBox(Math.Round((Doc.Width - w) / 2), Math.Round((Doc.Height - h) / 2), w, h) };
        _main.AddObjectLayer(obj, "Add Text");
        _main.SetStatus("Text added. Double-click it to change the words; font, color and designs are in Properties.");
    }

    /// <summary>Opens the icon / badge picker and puts the choice in the middle of the picture.</summary>
    private void AddIcon(string start)
    {
        var choice = _main.Dialogs.ShowIconPicker(start);
        if (choice == null) return;
        double side = Math.Min(Doc.Width, Doc.Height);
        if (choice.IsGraphic)
        {
            var graphic = Rendering.Graphics.Build(choice.Id, Doc.Width, Doc.Height, Math.Max(120, Math.Min(Doc.Width * 0.6, side * 0.8)));
            var graphicLayer = _main.AddObjectLayer(graphic, "Insert Graphic");
            graphicLayer.Name = choice.Name;
            _main.SetStatus($"Added \"{choice.Name}\". It is a group of shapes: ungroup it (Ctrl+Shift+G) to recolor or change its parts.");
            return;
        }
        if (choice.IsBadge)
        {
            var badge = Rendering.Badges.Build(choice.Id, Doc.Width, Doc.Height, Math.Max(80, side * 0.3));
            var layer = _main.AddObjectLayer(badge, "Insert Badge");
            layer.Name = choice.Name;
            _main.SetStatus($"Added the \"{choice.Name}\" badge. Ungroup it (Ctrl+Shift+G) to change its colors and words.");
            return;
        }
        var icon = new TextObject(new ObjectBox(0, 0, 1, 1), choice.Glyph, Services.IconCatalog.FontFamily, Math.Max(24, Math.Round(side * 0.2)), _main.Colors.Foreground);
        var (w, h) = ObjectRendering.Renderer.MeasureText(icon);
        icon = icon with { Box = new ObjectBox(Math.Round((Doc.Width - w) / 2), Math.Round((Doc.Height - h) / 2), w, h) };
        var iconLayer = _main.AddObjectLayer(icon, "Insert Icon");
        iconLayer.Name = choice.Name + " icon";
        _main.SetStatus($"Added the {choice.Name.ToLowerInvariant()} icon. It is text: change its color, size, outline and shadow in Properties.");
    }

    /// <summary>Opens the SmartArt picker and puts the diagram in the middle of the picture.</summary>
    private void AddSmartArt()
    {
        var choice = _main.Dialogs.ShowSmartArt();
        if (choice == null) return;
        double side = Math.Min(Doc.Width, Doc.Height);
        var diagram = Rendering.SmartArt.Build(choice.LayoutId, choice.Items, choice.Scheme, Doc.Width, Doc.Height, Math.Max(160, Math.Min(Doc.Width * 0.8, side * 1.2)));
        var layer = _main.AddObjectLayer(diagram, "Insert SmartArt");
        layer.Name = choice.Name;
        _main.SetStatus($"Added the \"{choice.Name}\" diagram. Ungroup it (Ctrl+Shift+G) to move, recolor or retype single parts.");
    }

    /// <summary>A chart with sample numbers in the middle of the picture; change the numbers in Properties.</summary>
    private void AddChart(ChartKind kind)
    {
        double w = Doc.Width * 0.72, h = Doc.Height * 0.62;
        var box = new ObjectBox((Doc.Width - w) / 2, (Doc.Height - h) / 2, w, h);
        var brand = AppSettings.Current.BrandColors.Select(hex => ParseHex(hex)).ToList();
        var chart = ChartObject.Sample(box, kind) with { Colors = brand.Count >= 2 ? brand : ChartPalettes.Ocean };
        // Some kinds look best with their own kind of numbers.
        chart = kind switch
        {
            ChartKind.Progress => chart with
            {
                Title = "Skills", ValueSuffix = "%", ShowGrid = false,
                Items = new[] { new ChartItem("Design", 85), new ChartItem("Development", 64), new ChartItem("Marketing", 48), new ChartItem("Support", 72) },
            },
            ChartKind.Funnel => chart with
            {
                Title = "Sales funnel",
                Items = new[] { new ChartItem("Visitors", 1200), new ChartItem("Leads", 640), new ChartItem("Trials", 320), new ChartItem("Customers", 140) },
            },
            ChartKind.HalfDonut or ChartKind.RadialBars => chart with { Title = "Share", Items = new[] { new ChartItem("Mobile", 46), new ChartItem("Desktop", 34), new ChartItem("Tablet", 20) } },
            _ => chart,
        };
        _main.AddObjectLayer(chart, "Insert Chart");
        _main.SetStatus("Added a chart with sample numbers. Type your own numbers (one per line, like \"Jan, 12\") in Properties.");
    }

    // ------------------------------------------------------------------ shapes

    /// <summary>A shape in the middle of the picture (blue; a line is black). Resize, turn and color it with the handles and Properties.</summary>
    private void AddShape(ShapeKind kind)
    {
        double size = Math.Max(40, Math.Min(Doc.Width, Doc.Height) * 0.35);
        bool line = kind == ShapeKind.Line;
        double w = line ? size * 1.4 : kind is ShapeKind.Rectangle or ShapeKind.Parallelogram or ShapeKind.Trapezoid or ShapeKind.Arrow or ShapeKind.DoubleArrow
            or ShapeKind.SpeechBubble or ShapeKind.OvalBubble or ShapeKind.Cloud or ShapeKind.Tag or ShapeKind.Home ? size * 1.3 : size;
        double h = line ? 6 : size;
        var box = new ObjectBox((Doc.Width - w) / 2, (Doc.Height - h) / 2, w, h);
        var blue = new ColorRgba(59, 130, 246);
        var shape = new ShapeObject(box, kind,
            Fill: line ? null : blue,
            Stroke: line ? ColorRgba.Black : null,
            StrokeWidth: 4,
            Points: 5,
            Inner: kind == ShapeKind.Pie ? 0 : 0.5,
            Sweep: 270);
        _main.AddObjectLayer(shape, $"Add {shape.SuggestedLayerName}");
        _main.SetStatus($"Added a {shape.SuggestedLayerName.ToLowerInvariant()}. Drag the handles to resize or turn it; change colors and more in Properties.");
    }

    // ------------------------------------------------------------------ frames and photo grids

    private static readonly ColorRgba FrameColor = new(226, 232, 240);

    /// <summary>An empty frame: a shape to drop a picture into (Insert > Fill Frames with Pictures).</summary>
    private void AddFrame(string kind)
    {
        double size = Math.Max(60, Math.Min(Doc.Width, Doc.Height) * 0.42);
        double w = kind is "Rectangle" or "Rounded" ? size * 1.3 : size;
        var box = new ObjectBox((Doc.Width - w) / 2, (Doc.Height - size) / 2, w, size);
        ShapeObject shape = kind switch
        {
            "Rounded" => new ShapeObject(box, ShapeKind.Rectangle, FrameColor, null, 0, CornerRadius: size * 0.12),
            "Circle" => new ShapeObject(box, ShapeKind.Ellipse, FrameColor, null, 0),
            "Heart" => new ShapeObject(box, ShapeKind.Heart, FrameColor, null, 0),
            "Star" => new ShapeObject(box, ShapeKind.Star, FrameColor, null, 0),
            _ => new ShapeObject(box, ShapeKind.Rectangle, FrameColor, null, 0),
        };
        var layer = _main.AddObjectLayer(shape, "Add Frame");
        layer.Name = "Frame";
        _main.SetStatus("Added an empty frame. Choose Insert > Fill Frames with Pictures to put a photo in it.");
    }

    /// <summary>Adds the frames of a collage layout to the picture (one undo step).</summary>
    private void PhotoGrid()
    {
        var settings = _main.Dialogs.ShowPhotoGrid(Doc.Width, Doc.Height);
        if (settings == null) return;

        var frames = new List<Layer>();
        int number = 1;
        foreach (var (x, y, w, h) in settings.Layout.Place(Doc.Width, Doc.Height, settings.Padding, settings.Gap))
        {
            var shape = new ShapeObject(new ObjectBox(x, y, w, h), ShapeKind.Rectangle, FrameColor, null, 0, CornerRadius: Math.Min(settings.CornerRadius, Math.Min(w, h) / 2));
            var layer = new Layer("Frame " + number++, Doc.Width, Doc.Height);
            layer.SetObject(shape);
            frames.Add(layer);
        }
        _main.Push(LayerStructureAction.Record("Add Photo Grid", Doc, () =>
        {
            var list = Doc.Layers.ToList();
            list.AddRange(frames);
            Doc.SetLayers(list, frames[^1]);
        }));
        _main.SetSelection(frames, frames[^1]);
        _main.SetStatus($"Added {frames.Count} frames. They are selected: choose Insert > Fill Frames with Pictures to drop your photos in.");
    }

    /// <summary>The selected shapes that can be filled with a picture, in reading order (top to bottom, then left to right).</summary>
    private List<Layer> SelectedFrames()
        => _main.SelectedLayers.Where(l => l is { IsVisible: true, IsLocked: false, Object: ShapeObject { Shape: not ShapeKind.Line } })
            .OrderBy(l => Math.Round(l.Object!.Box.Center.Y / 20)).ThenBy(l => l.Object!.Box.Center.X).ToList();

    /// <summary>
    /// Puts the chosen pictures into the selected frames: each picture covers its frame and is clipped to the frame's shape. The picture is a
    /// normal layer above the frame, so it can be moved and resized to choose which part shows.
    /// </summary>
    private void FillFrames()
    {
        var frames = SelectedFrames();
        if (frames.Count == 0) { _main.SetStatus("Select one or more frames (shapes) first."); return; }
        string title = frames.Count == 1 ? "Choose a picture for the frame" : $"Choose up to {frames.Count} pictures for the frames";
        var files = PicturePicker?.Invoke(title) ?? _main.Dialogs.ShowOpenPictures(title);
        if (files.Count == 0) return;
        var chosen = files.Take(frames.Count).ToList();
        var doc = Doc;

        _main.RunAsync("Filling the frames", async () =>
        {
            var pictures = await Task.Run(() => chosen.Select(f =>
            {
                try { return (Name: Path.GetFileNameWithoutExtension(f), Image: ImageDecoder.Load(f)); }
                catch (Exception) { return (Name: Path.GetFileNameWithoutExtension(f), Image: (PixelBuffer?)null); }
            }).ToList());
            if (!ReferenceEquals(doc, _main.Document.Model)) return;

            // The picture becomes part of the frame (one object), so moving, resizing or turning the frame takes the picture along.
            var steps = new List<IHistoryAction?>();
            var filled = new List<Layer>();
            for (int i = 0; i < pictures.Count; i++)
            {
                if (pictures[i].Image is not { } image) continue;
                var frame = frames[i];
                if (frame.Object is not ShapeObject shape) continue;
                steps.Add(ObjectChangeAction.Apply("Fill Frame", frame, shape with { Picture = new PictureFill(image, pictures[i].Name) }));
                filled.Add(frame);
            }
            if (filled.Count == 0) { _main.SetStatus("None of those pictures could be opened."); return; }

            _main.Push(new CompositeAction("Fill Frames", steps));
            _main.SetSelection(filled, filled[^1]);
            _main.SetStatus($"Filled {filled.Count} frame{(filled.Count == 1 ? "" : "s")}. The picture is part of the frame: move or resize the frame and the picture goes along. Zoom and position of the picture are in Properties > Picture in frame.");
        });
    }

    private static ColorRgba ParseHex(string hex)
    {
        var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        return new ColorRgba(c.R, c.G, c.B);
    }
}
