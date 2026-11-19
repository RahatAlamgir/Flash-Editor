using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.App.Services;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;
using FlashEditor.Core.Barcodes;
using FlashEditor.Core.Qr;

namespace FlashEditor.App.ViewModels;

/// <summary>
/// The Properties panel: settings of the selected shape, text or image (Canva-style).
/// Every change is applied at once and recorded as an undo step (slider drags merge into one step).
/// </summary>
public sealed class ObjectPropertiesViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private Layer? _layer;
    private bool _keepRatio = true;

    public ObjectPropertiesViewModel(MainViewModel main)
    {
        _main = main;
        main.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.Document)) Track(); };
        main.ActivePartChanged += (_, _) => OnPropertyChanged(string.Empty); // another part of a group was selected
        main.SelectedLayersChanged += (_, _) => OnPropertyChanged(string.Empty); // the number or kinds of selected layers changed
        main.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainViewModel.IsReshaping)) OnPropertyChanged(nameof(IsReshaping)); };
        Track();
        SavedStyles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSavedStyles));

        PickFillCommand = new RelayCommand(() => PickColor("Fill color", Shape?.Fill ?? ColorRgba.Black,
            c => Change("Fill Color", o => ((ShapeObject)o) with { Fill = c })), () => IsShape);
        PickStrokeCommand = new RelayCommand(() => PickColor("Border color", Shape?.Stroke ?? ColorRgba.Black,
            c => Change("Border Color", o => ((ShapeObject)o) with { Stroke = c })), () => IsShape);
        PickTextColorCommand = new RelayCommand(() => PickColor("Text color", Text?.Color ?? ColorRgba.Black,
            c => ChangeText("Text Color", t => t with { Color = c })), () => IsText);
        FlipHorizontalCommand = new RelayCommand(() => Change("Flip Horizontal", o => o with { FlipX = !o.FlipX }), () => CanInspect);
        FlipVerticalCommand = new RelayCommand(() => Change("Flip Vertical", o => o with { FlipY = !o.FlipY }), () => CanInspect);
        ResetRotationCommand = new RelayCommand(() => Rotation = 0, () => CanInspect);
        ConvertToPixelsCommand = new RelayCommand(() => { if (_layer != null) _main.ConvertToPixels(_layer, announce: false); }, () => HasObject && !IsPart);
        EditTextCommand = new RelayCommand(EditText, () => IsText);
        OriginalSizeCommand = new RelayCommand(OriginalSize, () => IsImage);
        PickShadowColorCommand = new RelayCommand(() => PickColor("Shadow color", (Object?.Shadow?.Color ?? ColorRgba.Black) with { A = 255 },
            c => ChangeShadow("Shadow Color", s => s with { Color = c with { A = s.Color.A } })), () => HasShadow);
        PickGlowColorCommand = new RelayCommand(() => PickColor("Glow color", (Object?.Glow?.Color ?? GlowStyle.Default.Color) with { A = 255 },
            c => ChangeGlow("Glow Color", g => g with { Color = c with { A = g.Color.A } })), () => HasGlow);
        PickInnerShadowColorCommand = new RelayCommand(() => PickColor("Inner shadow color", (Object?.InnerShadow?.Color ?? ColorRgba.Black) with { A = 255 },
            c => ChangeInnerShadow("Inner Shadow Color", s => s with { Color = c with { A = s.Color.A } })), () => HasInnerShadow);
        PickObjectOutlineColorCommand = new RelayCommand(() => PickColor("Outline color", Object?.Outline?.Color ?? OutlineStyle.Default.Color,
            c => ChangeObjectOutline("Outline Color", o => o with { Color = c })), () => HasObjectOutline);
        ApplyTextPresetCommand = new RelayCommand(p =>
        {
            if (p is TextPreset preset) Change($"Text design: {preset.Name}", o => o is TextObject t ? preset.ApplyTo(t) : o);
        }, _ => IsText);
        ApplyShadowPresetCommand = new RelayCommand(p =>
        {
            if (p is ShadowPreset preset) Change($"Shadow: {preset.Name}", o => o with { Shadow = preset.CreateStyle(o.Shadow) });
        }, _ => CanInspect);
        PickThreeDColorCommand = new RelayCommand(() => PickColor("3D color", Object?.ThreeD?.Color ?? new ColorRgba(90, 90, 90),
            c => ChangeThreeD("3D Color", t => t with { Color = c with { A = 255 } })), () => HasThreeD);
        ApplyThreeDPresetCommand = new RelayCommand(p =>
        {
            if (p is ThreeDPreset preset) Change($"3D: {preset.Name}", o => o with { ThreeD = preset.CreateStyle(o.ThreeD) });
        }, _ => CanInspect);
        AddGradientStopCommand = new RelayCommand(() => ChangeGradient("Add Gradient Color", g => g.WithStopAdded()),
            () => HasGradient && Object!.Gradient!.Stops.Count < GradientStyle.MaxStops);
        SwapGradientCommand = new RelayCommand(() => ChangeGradient("Reverse Gradient", g => g.Reversed()), () => HasGradient);
        ApplyGradientPresetCommand = new RelayCommand(p =>
        {
            if (p is GradientPresetItem item) Change($"Gradient: {item.Name}", o => WithGradient(o, item.Preset.CreateStyle()));
        }, _ => CanGradient);
        PickOutlineColorCommand = new RelayCommand(() => PickColor("Outline color", Text?.OutlineColor ?? ColorRgba.Black,
            c => ChangeText("Outline Color", t => t with { OutlineColor = c, OutlineWidth = t.OutlineWidth > 0 ? t.OutlineWidth : Math.Max(2, Math.Round(t.FontSize / 14)) })), () => IsText);
        SaveStyleCommand = new RelayCommand(SaveStyle, () => HasObject);
        ApplyStyleCommand = new RelayCommand(p =>
        {
            if (p is SavedStyle saved) Change($"Style: {saved.Name}", o => saved.Style.ApplyTo(o));
        }, _ => HasObject && CanEdit);
        DeleteStyleCommand = new RelayCommand(p => { if (p is SavedStyle saved) StyleLibrary.Default.Remove(saved); });
        MakeEditableCommand = new RelayCommand(MakeEditable, () => IsPixelLayer);
        ToggleReshapeCommand = new RelayCommand(() => _main.ToggleReshape(), () => CanReshape && CanEdit);
        ResetShapeCommand = new RelayCommand(() => Change("Reset Shape", o => o.WithoutReshape()), () => HasCustomOutline && CanEdit);
        UnlockCommand = new RelayCommand(() => _main.Layers.UnlockSelectedCommand.Execute(null), () => IsLocked);
        DeletePartCommand = new RelayCommand(() => _main.DeleteActivePart(), () => IsPart);
        LeavePartCommand = new RelayCommand(() => { if (_layer != null) _main.SelectPart(_layer, null); }, () => IsPart);
    }

    // ------------------------------------------------------------------ what is selected

    /// <summary>What is being edited: the selected part of a group, or else the active layer's object.</summary>
    private EditTarget Target => new(_layer, _main.ActivePartIndex);

    /// <summary>The edited object, as it looks on the canvas.</summary>
    public LayerObject? Object => Target.Object;
    private ShapeObject? Shape => Object as ShapeObject;
    private TextObject? Text => Object as TextObject;

    public bool HasObject => Object != null;

    /// <summary>The settings are shown for an object and also for a layer of plain pixels (it becomes a picture on the first change).</summary>
    public bool CanInspect => HasObject || IsPixelLayer;

    /// <summary>The box of the object, or for a layer of plain pixels the box around what is painted on it.</summary>
    private ObjectBox? Frame => Object?.Box ?? (_layer is { Object: null, IsAdjustment: false } pixels ? FlashEditor.App.Tools.Selecting.TransformHandles.FrameOf(pixels) : null);

    /// <summary>A layer of plain pixels (painted, or turned into pixels): it has no object settings such as a shadow.</summary>
    public bool IsPixelLayer => _layer is { Object: null, IsAdjustment: false };

    /// <summary>An adjustment layer (brightness / contrast, Levels, Curves): its settings are shown here.</summary>
    public bool IsAdjustmentLayer => _layer is { IsAdjustment: true };

    /// <summary>The "click something to edit it" hint: when no object is selected and the layer is not a pixel layer either.</summary>
    public bool ShowHint => !HasObject && !IsPixelLayer && !IsAdjustmentLayer;

    /// <summary>True when a part inside a group is selected (Canva style) rather than the group itself.</summary>
    public bool IsPart => Target.IsPart;

    /// <summary>Locked objects show their settings but cannot be changed.</summary>
    public bool IsLocked => _layer?.IsLocked == true;
    public bool CanEdit => CanInspect && !IsLocked;
    // ---- several layers selected: a change goes to all of them; settings that only fit one kind of object need a selection of that kind only

    /// <summary>The layers a change is applied to: all selected layers when several are selected, otherwise the active one.</summary>
    private IReadOnlyList<Layer> Targets
    {
        get
        {
            if (_main.ActivePartIndex == null && _main.SelectedLayers.Count > 1)
            {
                var many = _main.SelectedLayers.Where(l => !l.IsAdjustment).ToList();
                if (many.Count > 1) return many;
            }
            return _layer is { IsAdjustment: false } one ? new[] { one } : Array.Empty<Layer>();
        }
    }

    private static string KindOfLayer(Layer layer) => layer.Object switch
    {
        ShapeObject => "shape",
        TextObject => "text",
        GroupObject => "group",
        _ => "image",                    // a layer of plain pixels becomes a picture on the first change
    };

    /// <summary>True when more than one layer is selected.</summary>
    public bool IsMulti => Targets.Count > 1;

    /// <summary>True when the selected layers are of different kinds (a text and a shape, for example).</summary>
    public bool IsMixed => Targets.Count > 1 && Targets.Select(KindOfLayer).Distinct().Count() > 1;

    /// <summary>Saving a look, reshaping and typing new words only make sense for one object.</summary>
    public bool IsSingle => !IsMulti;
    public bool CanSaveLook => HasObject && !IsMulti;

    public bool IsShape => Shape != null && !IsMixed;
    public bool IsText => Text != null && !IsMixed;
    public bool IsImage => Object is ImageObject && !IsMixed;
    public bool IsGroup => !IsPart && Object is GroupObject && !IsMulti;
    public bool CanFill => Shape?.CanFill == true;
    public bool HasCorners => Shape?.HasCorners == true;
    public bool HasPoints => Shape?.HasPoints == true;
    public bool IsLine => Shape?.IsLineLike == true;

    /// <summary>A straight line or an open curve: arrow heads make sense.</summary>
    public bool HasArrows => Shape is { IsLineLike: true } or { Shape: ShapeKind.Path, Closed: false };

    public string Title => IsMulti ? $"{Targets.Count} layers selected" + (IsMixed ? " (different kinds)" : "") : (IsPart ? "Part: " : "") + Object switch
    {
        ShapeObject s => $"{s.SuggestedLayerName} shape",
        TextObject => "Text",
        ImageObject => "Image",
        GroupObject g => $"Group of {g.Members.Count}",
        _ => "",
    };

    /// <summary>Shown above the settings while a part of a group is selected.</summary>
    public string PartInfo => Target is { Part: { } index, Layer.Object: GroupObject group } && index < group.Members.Count
        ? $"Editing \"{group.Members[index].Name}\" inside the group"
        : "";

    // ------------------------------------------------------------------ position and size

    public double X { get => Round(Frame?.X); set => ChangeBox("Move", b => b with { X = value }); }
    public double Y { get => Round(Frame?.Y); set => ChangeBox("Move", b => b with { Y = value }); }

    public double Width
    {
        get => Round(Frame?.Width);
        set => Resize(Math.Max(1, value), null);
    }

    public double Height
    {
        get => Round(Frame?.Height);
        set => Resize(null, Math.Max(1, value));
    }

    public double Rotation
    {
        get => Math.Round(Object?.Box.Rotation ?? 0);
        set => ChangeBox("Rotate", b => b with { Rotation = ObjectBox.NormalizeAngle(value) });
    }

    /// <summary>When on, changing width also changes height (and the other way round).</summary>
    public bool KeepRatio { get => _keepRatio; set { if (SetField(ref _keepRatio, value)) _main.Canvas?.RefreshTransformFrame(); } }

    // ------------------------------------------------------------------ shape

    /// <summary>The kind of the selected shape (shown only: the kind is chosen when the shape is drawn).</summary>
    public ShapeKind ShapeKind => Shape?.Shape ?? ShapeKind.Rectangle;

    /// <summary>True for shapes with straight sides: their corner points can be dragged.</summary>
    public bool CanReshape => !IsPart && !IsMulti && Object?.CanReshape == true;

    /// <summary>True once the corners were dragged (a custom outline).</summary>
    public bool HasCustomOutline => !IsPart && Object?.HasCustomOutline == true;

    /// <summary>True while the corner points of the shape are shown for dragging.</summary>
    public bool IsReshaping => _main.IsReshaping;

    public bool FillEnabled
    {
        get => Shape?.Fill != null;
        set => Change("Fill", o => ((ShapeObject)o) with { Fill = value ? (Shape?.Stroke ?? new ColorRgba(59, 130, 246)) : null });
    }

    public Brush FillBrush => ToBrush(Shape?.Fill);

    public bool StrokeEnabled
    {
        get => Shape?.Stroke != null;
        set => Change("Border", o =>
        {
            var s = (ShapeObject)o;
            return s with { Stroke = value ? ColorRgba.Black : null, StrokeWidth = value && s.StrokeWidth < 1 ? 4 : s.StrokeWidth };
        });
    }

    public Brush StrokeBrush => ToBrush(Shape?.Stroke);

    // ------------------------------------------------------------------ QR code

    private QrCodeObject? Qr => Object as QrCodeObject;
    public bool IsQr => Qr != null && !IsMixed;

    private void ChangeQr(string name, Func<QrCodeObject, QrCodeObject> change)
        => Change(name, o => o is QrCodeObject q ? change(q) : o);

    public string QrContent { get => Qr?.Content ?? ""; set => ChangeQr("QR Text", q => q with { Content = value ?? "" }); }
    public int QrLevelIndex { get => (int)(Qr?.Level ?? QrLevel.Medium); set => ChangeQr("QR Error Correction", q => q with { Level = (QrLevel)Math.Clamp(value, 0, 3) }); }
    public int QrStyleIndex { get => (int)(Qr?.Style ?? QrStyle.Square); set => ChangeQr("QR Style", q => q with { Style = (QrStyle)Math.Clamp(value, 0, 2) }); }
    public double QrQuiet { get => Qr?.Quiet ?? 2; set => ChangeQr("QR Border", q => q with { Quiet = (int)Math.Clamp(Math.Round(value), 0, 12) }); }
    public bool QrTransparent { get => Qr?.TransparentBackground == true; set => ChangeQr("QR Background", q => q with { TransparentBackground = value }); }
    public Brush QrForeBrush => ToBrush(Qr?.Foreground);
    public Brush QrBackBrush => ToBrush(Qr?.Background);
    public static IReadOnlyList<string> QrLevelNames { get; } = new[] { "Low (7 % repair)", "Medium (15 %)", "Quartile (25 %)", "High (30 %)" };
    public static IReadOnlyList<string> QrStyleNames { get; } = new[] { "Squares", "Rounded", "Dots (harder to scan)" };

    /// <summary>True when the text is too long for the chosen level (nothing can be drawn then).</summary>
    public bool QrTooLong => Qr is { } q && System.Text.Encoding.UTF8.GetByteCount(q.Content) > QrEncoder.MaxBytes(q.Level);

    public RelayCommand PickQrForeCommand => _pickQrFore ??= new RelayCommand(() => PickColor("QR code color", Qr?.Foreground ?? ColorRgba.Black,
        c => ChangeQr("QR Color", q => q with { Foreground = c with { A = 255 } })), () => IsQr);
    private RelayCommand? _pickQrFore;
    public RelayCommand PickQrBackCommand => _pickQrBack ??= new RelayCommand(() => PickColor("QR code background", Qr?.Background ?? ColorRgba.White,
        c => ChangeQr("QR Background Color", q => q with { Background = c with { A = 255 } })), () => IsQr);
    private RelayCommand? _pickQrBack;

    // ------------------------------------------------------------------ picture in a frame

    private PictureFill? Pic => (Object as ShapeObject)?.Picture;
    public bool HasPicture => Pic != null && !IsMixed;

    private void ChangePic(string name, Func<PictureFill, PictureFill> change)
        => Change(name, o => o is ShapeObject { Picture: { } p } s ? s with { Picture = change(p) } : o);

    /// <summary>How far the picture is zoomed in inside the frame (100 % = it just covers the frame).</summary>
    public double PictureZoomPercent
    {
        get => Math.Round((Pic?.Zoom ?? 1) * 100);
        set => ChangePic("Picture Zoom", p => p with { Zoom = Math.Clamp(value, 25, 500) / 100.0 });
    }

    /// <summary>Which part of the picture shows: -100 = its left edge, 0 = the middle, 100 = its right edge.</summary>
    public double PicturePanXPercent
    {
        get => Math.Round((Pic?.PanX ?? 0) * 100);
        set => ChangePic("Picture Position", p => p with { PanX = Math.Clamp(value, -100, 100) / 100.0 });
    }

    public double PicturePanYPercent
    {
        get => Math.Round((Pic?.PanY ?? 0) * 100);
        set => ChangePic("Picture Position", p => p with { PanY = Math.Clamp(value, -100, 100) / 100.0 });
    }

    public RelayCommand RemovePictureCommand => _removePicture ??= new RelayCommand(
        () => Change("Remove Picture from Frame", o => o is ShapeObject s ? s with { Picture = null } : o), () => HasPicture && CanEdit);
    private RelayCommand? _removePicture;

    public RelayCommand ResetPictureCommand => _resetPicture ??= new RelayCommand(
        () => ChangePic("Reset Picture", p => p with { Zoom = 1, PanX = 0, PanY = 0 }), () => HasPicture && CanEdit);
    private RelayCommand? _resetPicture;

    // ------------------------------------------------------------------ barcode

    private BarcodeObject? Bar => Object as BarcodeObject;
    public bool IsBarcode => Bar != null && !IsMixed;

    private void ChangeBar(string name, Func<BarcodeObject, BarcodeObject> change)
        => Change(name, o => o is BarcodeObject b ? change(b) : o);

    public string BarcodeContent { get => Bar?.Content ?? ""; set => ChangeBar("Barcode Text", b => b with { Content = value ?? "" }); }
    public int BarcodeKindIndex { get => (int)(Bar?.Kind ?? BarcodeKind.Code128); set => ChangeBar("Barcode Kind", b => b with { Kind = (BarcodeKind)Math.Clamp(value, 0, 4) }); }
    public static IReadOnlyList<string> BarcodeKindNames { get; } = new[]
    {
        "Code 128 (any text)", "EAN-13 (shop products)", "UPC-A (US shop products)", "EAN-8 (small products)", "Code 39 (capitals, digits)",
    };
    public bool BarcodeShowText { get => Bar?.ShowText == true; set => ChangeBar("Barcode Numbers", b => b with { ShowText = value }); }
    public bool BarcodeTransparent { get => Bar?.TransparentBackground == true; set => ChangeBar("Barcode Background", b => b with { TransparentBackground = value }); }
    public Brush BarcodeForeBrush => ToBrush(Bar?.Foreground);
    public Brush BarcodeBackBrush => ToBrush(Bar?.Background);

    /// <summary>What this kind of barcode accepts.</summary>
    public string BarcodeHint => Bar == null ? "" : BarcodeEncoder.Describe(Bar.Kind);

    /// <summary>What is wrong with the text for this kind of barcode (empty when it is fine).</summary>
    public string BarcodeError => Bar == null ? "" : BarcodeEncoder.Validate(Bar.Content, Bar.Kind) ?? "";
    public bool HasBarcodeError => BarcodeError.Length > 0;

    public RelayCommand PickBarcodeForeCommand => _pickBarFore ??= new RelayCommand(() => PickColor("Barcode color", Bar?.Foreground ?? ColorRgba.Black,
        c => ChangeBar("Barcode Color", b => b with { Foreground = c with { A = 255 } })), () => IsBarcode);
    private RelayCommand? _pickBarFore;
    public RelayCommand PickBarcodeBackCommand => _pickBarBack ??= new RelayCommand(() => PickColor("Barcode background", Bar?.Background ?? ColorRgba.White,
        c => ChangeBar("Barcode Background Color", b => b with { Background = c with { A = 255 } })), () => IsBarcode);
    private RelayCommand? _pickBarBack;

    // ------------------------------------------------------------------ chart

    private ChartObject? Chart => Object as ChartObject;
    public bool IsChart => Chart != null && !IsMixed;

    private void ChangeChart(string name, Func<ChartObject, ChartObject> change)
        => Change(name, o => o is ChartObject c ? change(c) : o);

    public int ChartKindIndex { get => (int)(Chart?.Kind ?? ChartKind.Columns); set => ChangeChart("Chart Kind", c => c with { Kind = (ChartKind)Math.Clamp(value, 0, 4) }); }
    public static IReadOnlyList<string> ChartKindNames { get; } = new[] { "Columns", "Bars", "Line", "Pie", "Donut" };
    public string ChartTitle { get => Chart?.Title ?? ""; set => ChangeChart("Chart Title", c => c with { Title = value ?? "" }); }

    /// <summary>The chart types as buttons (the one in use is marked).</summary>
    public IReadOnlyList<ChartKindItem> ChartKinds => new[]
    {
        new ChartKindItem(ChartKind.Columns, "Columns", "ChartColumns", Chart?.Kind == ChartKind.Columns),
        new ChartKindItem(ChartKind.Bars, "Bars", "ChartBars", Chart?.Kind == ChartKind.Bars),
        new ChartKindItem(ChartKind.Line, "Line", "ChartLine", Chart?.Kind == ChartKind.Line),
        new ChartKindItem(ChartKind.Pie, "Pie", "ChartPie", Chart?.Kind == ChartKind.Pie),
        new ChartKindItem(ChartKind.Donut, "Donut", "ChartDonut", Chart?.Kind == ChartKind.Donut),
    };

    public RelayCommand SetChartKindCommand => _setChartKind ??= new RelayCommand(p =>
    {
        if (p is ChartKind kind) ChangeChart("Chart Kind", c => c with { Kind = kind });
    }, _ => IsChart);
    private RelayCommand? _setChartKind;

    /// <summary>Bars take room for the bar width; lines and pies do not have bars.</summary>
    public bool ChartHasBars => Chart?.Kind is ChartKind.Columns or ChartKind.Bars;

    // ---- the number table

    private readonly ObservableCollection<ChartRowViewModel> _chartRows = new();

    /// <summary>One row per number. The same list is kept while typing (so the cursor stays put) and rebuilt when the numbers changed some other way (undo, pasting).</summary>
    public ObservableCollection<ChartRowViewModel> ChartRows
    {
        get
        {
            var items = Chart?.Items;
            bool same = items != null && items.Count == _chartRows.Count
                && Enumerable.Range(0, items.Count).All(i => _chartRows[i].Label == items[i].Label && _chartRows[i].Value == items[i].Value);
            if (items == null) { if (_chartRows.Count > 0) _chartRows.Clear(); }
            else if (!same)
            {
                _chartRows.Clear();
                for (int i = 0; i < items.Count; i++) _chartRows.Add(new ChartRowViewModel(this, i, items[i], Chart!.ColorOf(i)));
            }
            else
                for (int i = 0; i < items!.Count; i++) _chartRows[i].SetColor(Chart!.ColorOf(i));
            return _chartRows;
        }
    }

    /// <summary>Called by a row when its name or number was edited.</summary>
    public void SetChartRow(int index, string? label, double? value)
        => ChangeChart("Chart Numbers", c =>
        {
            if (index < 0 || index >= c.Items.Count) return c;
            var items = c.Items.ToList();
            items[index] = new ChartItem(label ?? items[index].Label, value ?? items[index].Value);
            return c with { Items = items };
        });

    public void RemoveChartRow(int index)
        => ChangeChart("Remove Chart Row", c => index < 0 || index >= c.Items.Count ? c : c with { Items = c.Items.Where((_, i) => i != index).ToList() });

    public RelayCommand AddChartRowCommand => _addChartRow ??= new RelayCommand(() => ChangeChart("Add Chart Row", c =>
    {
        double next = c.Items.Count > 0 ? c.Items[^1].Value : 10;
        return c with { Items = c.Items.Append(new ChartItem($"Item {c.Items.Count + 1}", next)).ToList() };
    }), () => IsChart && (Chart?.Items.Count ?? 0) < 60);
    private RelayCommand? _addChartRow;

    /// <summary>Opens a box to paste a whole list of numbers at once (one per line: name, number).</summary>
    public RelayCommand PasteChartNumbersCommand => _pasteChart ??= new RelayCommand(() =>
    {
        var text = _main.Dialogs.ShowTextInput("Chart numbers", "One per line: a name and a number, like \"Jan, 12\". You can paste from a spreadsheet.",
            Chart == null ? "" : ChartData.Format(Chart.Items));
        if (text != null) ChangeChart("Chart Numbers", c => c with { Items = ChartData.Parse(text) });
    }, () => IsChart && IsSingle);
    private RelayCommand? _pasteChart;

    /// <summary>The color of one bar or slice (the chart gets its own list of colors if it only used a ready-made set before).</summary>
    public void PickChartRowColor(int index)
    {
        if (Chart == null || index < 0 || index >= Chart.Items.Count) return;
        PickColor("Color of " + Chart.Items[index].Label, Chart.ColorOf(index), picked => ChangeChart("Chart Color", c =>
        {
            var colors = Enumerable.Range(0, Math.Max(c.Items.Count, c.Colors.Count)).Select(i => c.ColorOf(i)).ToList();
            if (index < colors.Count) colors[index] = picked with { A = 255 };
            return c with { Colors = colors };
        }));
    }

    // ---- colors

    /// <summary>The ready-made color sets, and the Brand Kit colors when there are at least two.</summary>
    public IReadOnlyList<ChartPaletteItem> ChartPalettesList
    {
        get
        {
            var list = ChartPalettes.All.Select(p => new ChartPaletteItem(p.Name, p.Colors)).ToList();
            var brand = AppSettings.Current.BrandColors.Select(hex =>
            {
                var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                return new ColorRgba(c.R, c.G, c.B);
            }).ToList();
            if (brand.Count >= 2) list.Insert(0, new ChartPaletteItem("My brand colors", brand));
            return list;
        }
    }

    public RelayCommand ApplyChartPaletteCommand => _applyChartPalette ??= new RelayCommand(p =>
    {
        if (p is ChartPaletteItem item) ChangeChart("Chart Colors", c => c with { Colors = item.Colors });
    }, _ => IsChart);
    private RelayCommand? _applyChartPalette;

    // ---- what is shown

    public bool ChartShowValues { get => Chart?.ShowValues == true; set => ChangeChart("Chart Values", c => c with { ShowValues = value }); }
    public bool ChartShowLabels { get => Chart?.ShowLabels == true; set => ChangeChart("Chart Names", c => c with { ShowLabels = value }); }
    public bool ChartShowGrid { get => Chart?.ShowGrid == true; set => ChangeChart("Chart Grid", c => c with { ShowGrid = value }); }
    public double ChartBarWidth { get => Math.Round((Chart?.BarWidth ?? 0.62) * 100); set => ChangeChart("Bar Width", c => c with { BarWidth = Math.Clamp(value, 15, 100) / 100.0 }); }
    public double ChartTextScale { get => Math.Round((Chart?.TextScale ?? 1) * 100); set => ChangeChart("Chart Text Size", c => c with { TextScale = Math.Clamp(value, 50, 250) / 100.0 }); }
    public string ChartPrefix { get => Chart?.ValuePrefix ?? ""; set => ChangeChart("Chart Unit", c => c with { ValuePrefix = value ?? "" }); }
    public string ChartSuffix { get => Chart?.ValueSuffix ?? ""; set => ChangeChart("Chart Unit", c => c with { ValueSuffix = value ?? "" }); }

    public bool ChartHasBackground
    {
        get => Chart?.Background != null;
        set => ChangeChart("Chart Background", c => c with { Background = value ? new ColorRgba(255, 255, 255) : null });
    }
    public Brush ChartBackBrush => ToBrush(Chart?.Background);
    public Brush ChartTextBrush => ToBrush(Chart?.Ink);
    public RelayCommand PickChartBackCommand => _pickChartBack ??= new RelayCommand(() => PickColor("Chart background", Chart?.Background ?? ColorRgba.White,
        c => ChangeChart("Chart Background Color", ch => ch with { Background = c with { A = 255 } })), () => IsChart && Chart?.Background != null);
    private RelayCommand? _pickChartBack;
    public RelayCommand PickChartTextCommand => _pickChartText ??= new RelayCommand(() => PickColor("Chart text color", Chart?.Ink ?? ColorRgba.Black,
        c => ChangeChart("Chart Text Color", ch => ch with { TextColor = c with { A = 255 } })), () => IsChart);
    private RelayCommand? _pickChartText;

    // ------------------------------------------------------------------ adjust (brightness, contrast, saturation, hue, blur)

    public bool HasTone => Object?.Tone is { IsNeutral: false } || Object?.BlurAmount > 0;

    public double ToneBrightness { get => Math.Round(Object?.Tone?.Brightness ?? 0); set => ChangeTone(t => t with { Brightness = Math.Clamp(value, -100, 100) }); }
    public double ToneContrast { get => Math.Round(Object?.Tone?.Contrast ?? 0); set => ChangeTone(t => t with { Contrast = Math.Clamp(value, -100, 100) }); }
    public double ToneSaturation { get => Math.Round(Object?.Tone?.Saturation ?? 0); set => ChangeTone(t => t with { Saturation = Math.Clamp(value, -100, 100) }); }
    public double ToneHue { get => Math.Round(Object?.Tone?.Hue ?? 0); set => ChangeTone(t => t with { Hue = Math.Clamp(value, -180, 180) }); }

    public double BlurAmount
    {
        get => Math.Round(Object?.BlurAmount ?? 0, 1);
        set => Change("Blur", o => o with { BlurAmount = Math.Clamp(value, 0, 100) });
    }

    public RelayCommand ResetToneCommand => _resetTone ??= new RelayCommand(
        () => Change("Reset Adjustments", o => o with { Tone = null, BlurAmount = 0 }), () => HasTone);
    private RelayCommand? _resetTone;

    private void ChangeTone(Func<AdjustmentStyle, AdjustmentStyle> change)
        => Change("Adjust", o =>
        {
            var tone = change(o.Tone ?? AdjustmentStyle.Neutral);
            return o with { Tone = tone.IsNeutral ? null : tone };
        });

    /// <summary>0 = solid, 1 = dashed, 2 = dotted.</summary>
    public int StrokeStyleIndex
    {
        get => (int)(Shape?.Dash ?? StrokeStyle.Solid);
        set => Change("Border Style", o => ((ShapeObject)o) with { Dash = (StrokeStyle)Math.Clamp(value, 0, 2) });
    }

    public bool HasDash => Shape is { Dash: not StrokeStyle.Solid };
    public bool IsDashed => Shape?.Dash == StrokeStyle.Dashed;
    public bool IsDotted => Shape?.Dash == StrokeStyle.Dotted;

    /// <summary>Length of one dash in pixels (shown as the automatic length when nothing was chosen).</summary>
    public double DashLengthPx
    {
        get => Math.Round(Shape is { DashLength: > 0 } s ? s.DashLength : (Shape?.StrokeWidth ?? 0) * 3);
        set => Change("Dash Length", o => ((ShapeObject)o) with { DashLength = Math.Clamp(value, 1, 400) });
    }

    /// <summary>Space between the dashes (or the dots) in pixels.</summary>
    public double DashGapPx
    {
        get => Math.Round(Shape is { DashGap: > 0 } s ? s.DashGap : (Shape?.StrokeWidth ?? 0) * (Shape?.Dash == StrokeStyle.Dotted ? 1.2 : 2));
        set => Change("Dash Gap", o => ((ShapeObject)o) with { DashGap = Math.Clamp(value, 1, 400) });
    }

    public static IReadOnlyList<string> StrokeStyleNames { get; } = new[] { "Solid", "Dashed", "Dotted" };

    /// <summary>How much a line bows, -100 .. 100 % of its length (0 = straight).</summary>
    public double LineBendPercent
    {
        get => Math.Round((Shape?.Bend ?? 0) * 100);
        set => Change("Bend Line", o => o is ShapeObject s ? s with { Bend = Math.Clamp(value, -100, 100) / 100.0 } : o);
    }

    /// <summary>0 = arc (one bow), 1 = wave (up and down).</summary>
    public int LineBendStyleIndex
    {
        get => (int)(Shape?.BendStyle ?? LineBendStyle.Arc);
        set => Change("Bend Style", o => o is ShapeObject s ? s with { BendStyle = (LineBendStyle)Math.Clamp(value, 0, 1) } : o);
    }

    public static IReadOnlyList<string> LineBendStyleNames { get; } = new[] { "Arc (one bow)", "Wave (S-shape)" };

    /// <summary>Turns a (bent) line into an editable curve with points, so it can be bent in many places with the Curve tool.</summary>
    public RelayCommand LineToCurveCommand => _lineToCurve ??= new RelayCommand(() =>
    {
        if (Shape is not { IsLineLike: true } line || _layer == null) return;
        var (a, c1, c2, b) = LineCurves.Controls(line.Box.Width, line.Box.Height, line.Bend, line.BendStyle);
        var matrix = ObjectRenderer.LocalToCanvas(line);
        PointD Map(PointD p) { var q = matrix.Transform(new System.Windows.Point(p.X, p.Y)); return new PointD(q.X, q.Y); }
        var canvasNodes = new[] { new CurveNode(Map(a), Map(a), Map(c1)), new CurveNode(Map(b), Map(c2), Map(b)) };
        var (box, unit) = CurvePath.Fit(canvasNodes, false);
        Change("Line to Curve", o => ((ShapeObject)o) with
        {
            Shape = ShapeKind.Path, Box = box, Nodes = unit, Closed = false, Bend = 0, FlipX = false, FlipY = false, Warp = null,
            Stroke = line.Stroke ?? line.Fill, Fill = null,
        });
        _main.SelectTool<FlashEditor.App.Tools.Shapes.CurveTool>();
        _main.SetStatus("The line is now a curve. Drag its points, double-click the line to add points, double-click a point to make it smooth or sharp.");
    }, () => IsLine && CanEdit);
    private RelayCommand? _lineToCurve;

    public bool ArrowStart
    {
        get => Shape?.ArrowStart == true;
        set => Change("Arrow Head", o => ((ShapeObject)o) with { ArrowStart = value });
    }

    public bool ArrowEnd
    {
        get => Shape?.ArrowEnd == true;
        set => Change("Arrow Head", o => ((ShapeObject)o) with { ArrowEnd = value });
    }

    public double StrokeWidth
    {
        get => Math.Round(Shape?.StrokeWidth ?? 0, 1);
        set => Change("Border Width", o =>
        {
            var s = (ShapeObject)o;
            double width = Math.Clamp(value, 0, 200);
            // A line's box height follows its thickness.
            return s.IsLineLike ? s with { StrokeWidth = width, Box = ObjectBox.FromCenter(s.Box.Center, s.Box.Width, Math.Max(1, width), s.Box.Rotation) }
                                : s with { StrokeWidth = width };
        });
    }

    public double CornerRadius
    {
        get => Math.Round(Shape?.CornerRadius ?? 0);
        set => Change("Corner Radius", o => ((ShapeObject)o) with { CornerRadius = Math.Max(0, value) });
    }

    public double Points
    {
        get => Shape?.Points ?? 5;
        set => Change("Points", o => ((ShapeObject)o) with { Points = (int)Math.Clamp(Math.Round(value), 3, 24) });
    }

    public bool HasInner => Shape?.HasInner == true;
    public bool HasSweep => Shape?.HasSweep == true;
    public string InnerLabel => Shape == null ? "Hole" : ShapeNames.InnerLabel(Shape.Shape);

    /// <summary>Hole size (ring, half ring, pie, frame), arm thickness (cross) or bite (crescent), 0..95 %.</summary>
    public double InnerPercent
    {
        get => Math.Round((Shape?.Inner ?? 0.5) * 100);
        set => Change("Hole Size", o => ((ShapeObject)o) with { Inner = Math.Clamp(value, 0, 95) / 100 });
    }

    /// <summary>How many degrees of the circle a pie covers.</summary>
    public double Sweep
    {
        get => Math.Round(Shape?.Sweep ?? 270);
        set => Change("Pie Angle", o => ((ShapeObject)o) with { Sweep = Math.Clamp(Math.Round(value), 10, 360) });
    }

    // ------------------------------------------------------------------ text

    public string TextContent
    {
        get => Text?.Text ?? "";
        set
        {
            if (string.IsNullOrEmpty(value)) return; // empty text would leave an invisible object
            ChangeText("Edit Text", t => t with { Text = value.Replace("\r\n", "\n") });
        }
    }

    public FontFamily FontFamily
    {
        get => new(Text?.FontFamily ?? "Segoe UI");
        set { if (value != null) ChangeText("Font", t => t with { FontFamily = value.Source }); }
    }

    public double FontSize
    {
        get => Math.Round(Text?.FontSize ?? 48);
        set => ChangeText("Font Size", t => t with { FontSize = Math.Clamp(value, 4, 2000) });
    }

    public bool Bold { get => Text?.Bold == true; set => ChangeText("Bold", t => t with { Bold = value }); }
    public bool Italic { get => Text?.Italic == true; set => ChangeText("Italic", t => t with { Italic = value }); }

    public IReadOnlyList<TextAlign> Alignments { get; } = Enum.GetValues<TextAlign>();
    public TextAlign Align { get => Text?.Align ?? TextAlign.Left; set => ChangeText("Align Text", t => t with { Align = value }); }

    public Brush TextBrush => ToBrush(Text?.Color);

    /// <summary>A line around the letters.</summary>
    public bool OutlineEnabled
    {
        get => Text is { OutlineWidth: > 0, OutlineColor: not null };
        set => ChangeText("Text Outline", t => value
            ? t with { OutlineWidth = t.OutlineWidth > 0 ? t.OutlineWidth : Math.Max(2, Math.Round(t.FontSize / 14)), OutlineColor = t.OutlineColor ?? new ColorRgba(0, 0, 0) }
            : t with { OutlineWidth = 0 });
    }

    public double OutlineWidth
    {
        get => Math.Round(Text?.OutlineWidth ?? 0, 1);
        set => ChangeText("Outline Width", t => t with { OutlineWidth = Math.Clamp(value, 0, 200), OutlineColor = t.OutlineColor ?? new ColorRgba(0, 0, 0) });
    }

    public Brush OutlineBrush => ToBrush(Text?.OutlineColor ?? new ColorRgba(0, 0, 0));

    /// <summary>Space between the lines, 50..300 % (100 = normal).</summary>
    /// <summary>Extra space between letters, in percent of the font size (negative = tighter).</summary>
    public double LetterSpacingPercent
    {
        get => Math.Round((Text?.LetterSpacing ?? 0) * 100);
        set => ChangeText("Letter Spacing", t => t with { LetterSpacing = Math.Clamp(value, -30, 200) / 100.0 });
    }

    /// <summary>Extra space added at every space between words, in percent of the font size.</summary>
    public double WordSpacingPercent
    {
        get => Math.Round((Text?.WordSpacing ?? 0) * 100);
        set => ChangeText("Word Spacing", t => t with { WordSpacing = Math.Clamp(value, -50, 400) / 100.0 });
    }

    public double LineSpacingPercent
    {
        get => Math.Round((Text?.LineSpacing ?? 1) * 100);
        set => ChangeText("Line Spacing", t => t with { LineSpacing = Math.Clamp(value, 50, 300) / 100.0 });
    }

    /// <summary>Text on a curve: 0 = straight, positive bends it like a rainbow, negative like a smile (degrees of the circle the text follows).</summary>
    public double TextArc
    {
        get => Math.Round(Text?.Arc ?? 0);
        set => ChangeText("Curve", t => t with { Arc = Math.Clamp(value, -360, 360) });
    }

    public RelayCommand PickOutlineColorCommand { get; }

    // ------------------------------------------------------------------ glow, inner shadow and outline (every kind of object)

    public bool HasGlow => Object?.Glow != null;

    public bool GlowEnabled
    {
        get => HasGlow;
        set => Change("Glow", o => o with { Glow = value ? o.Glow ?? GlowStyle.Default : null });
    }

    public Brush GlowBrush => ToBrush(Object?.Glow is { } g ? g.Color with { A = 255 } : null);

    /// <summary>How strong the glow's color is, 0..100 %.</summary>
    public double GlowOpacity
    {
        get => Math.Round((Object?.Glow?.Color.A ?? 0) / 255.0 * 100);
        set => ChangeGlow("Glow Opacity", g => g with { Color = g.Color with { A = (byte)Math.Round(Math.Clamp(value, 0, 100) * 2.55) } });
    }

    public double GlowSize
    {
        get => Math.Round(Object?.Glow?.Size ?? 0);
        set => ChangeGlow("Glow Size", g => g with { Size = Math.Clamp(value, 1, 200) });
    }

    public double GlowIntensity
    {
        get => Math.Round(Object?.Glow?.Intensity ?? 0);
        set => ChangeGlow("Glow Intensity", g => g with { Intensity = Math.Clamp(value, 10, 400) });
    }

    private void ChangeGlow(string name, Func<GlowStyle, GlowStyle> change)
        => Change(name, o => o.Glow is { } glow ? o with { Glow = change(glow) } : o);

    public RelayCommand PickGlowColorCommand { get; }

    public bool HasInnerShadow => Object?.InnerShadow != null;

    public bool InnerShadowEnabled
    {
        get => HasInnerShadow;
        set => Change("Inner Shadow", o => o with { InnerShadow = value ? o.InnerShadow ?? InnerShadowStyle.Default : null });
    }

    public Brush InnerShadowBrush => ToBrush(Object?.InnerShadow is { } s ? s.Color with { A = 255 } : null);

    public double InnerShadowOpacity
    {
        get => Math.Round((Object?.InnerShadow?.Color.A ?? 0) / 255.0 * 100);
        set => ChangeInnerShadow("Inner Shadow Opacity", s => s with { Color = s.Color with { A = (byte)Math.Round(Math.Clamp(value, 0, 100) * 2.55) } });
    }

    public double InnerShadowDistance
    {
        get => Math.Round(Object?.InnerShadow?.Distance ?? 0);
        set => ChangeInnerShadow("Inner Shadow Distance", s => s with { Distance = Math.Clamp(value, 0, 200) });
    }

    public double InnerShadowAngle
    {
        get => Math.Round(Object?.InnerShadow?.Angle ?? 0);
        set => ChangeInnerShadow("Inner Shadow Angle", s => s with { Angle = ObjectBox.NormalizeAngle(value) });
    }

    public double InnerShadowBlur
    {
        get => Math.Round(Object?.InnerShadow?.Blur ?? 0);
        set => ChangeInnerShadow("Inner Shadow Blur", s => s with { Blur = Math.Clamp(value, 0, 200) });
    }

    private void ChangeInnerShadow(string name, Func<InnerShadowStyle, InnerShadowStyle> change)
        => Change(name, o => o.InnerShadow is { } shadow ? o with { InnerShadow = change(shadow) } : o);

    public RelayCommand PickInnerShadowColorCommand { get; }

    public bool HasObjectOutline => Object?.Outline != null;

    /// <summary>A line around the outside of the whole object (a shape's own border and a text's own outline are separate settings).</summary>
    public bool ObjectOutlineEnabled
    {
        get => HasObjectOutline;
        set => Change("Outline", o => o with { Outline = value ? o.Outline ?? OutlineStyle.Default : null });
    }

    public Brush ObjectOutlineBrush => ToBrush(Object?.Outline?.Color);

    public double ObjectOutlineWidth
    {
        get => Math.Round(Object?.Outline?.Width ?? 0);
        set => ChangeObjectOutline("Outline Width", o => o with { Width = Math.Clamp(value, 1, 100) });
    }

    private void ChangeObjectOutline(string name, Func<OutlineStyle, OutlineStyle> change)
        => Change(name, o => o.Outline is { } line ? o with { Outline = change(line) } : o);

    public RelayCommand PickObjectOutlineColorCommand { get; }

    // ------------------------------------------------------------------ shadow (every kind of object)

    public bool HasShadow => Object?.Shadow != null;

    public bool ShadowEnabled
    {
        get => HasShadow;
        set => Change("Shadow", o => o with { Shadow = value ? o.Shadow ?? ShadowStyle.Default : null });
    }

    /// <summary>The shadow color as a solid swatch (its strength is the separate Opacity setting).</summary>
    public Brush ShadowBrush => ToBrush(Object?.Shadow is { } s ? s.Color with { A = 255 } : null);

    /// <summary>How dark the shadow is, 0..100 %.</summary>
    public double ShadowOpacity
    {
        get => Math.Round((Object?.Shadow?.Color.A ?? 0) / 255.0 * 100);
        set => ChangeShadow("Shadow Opacity", s => s with { Color = s.Color with { A = (byte)Math.Round(Math.Clamp(value, 0, 100) * 2.55) } });
    }

    /// <summary>How far the shadow is shifted away from the object, in pixels.</summary>
    public double ShadowDistance
    {
        get => Math.Round(Object?.Shadow?.Distance ?? 0);
        set => ChangeShadow("Shadow Distance", s => s with { Distance = Math.Clamp(value, 0, 500) });
    }

    /// <summary>Direction of the shadow in degrees (0 = right, 90 = down, 180 = left, -90 = up).</summary>
    public double ShadowAngle
    {
        get => Math.Round(Object?.Shadow?.Angle ?? 0);
        set => ChangeShadow("Shadow Angle", s => s with { Angle = ObjectBox.NormalizeAngle(value) });
    }

    /// <summary>How soft the shadow's edge is, in pixels.</summary>
    public double ShadowBlur
    {
        get => Math.Round(Object?.Shadow?.Blur ?? 0);
        set => ChangeShadow("Shadow Blur", s => s with { Blur = Math.Clamp(value, 0, 300) });
    }

    private void ChangeShadow(string name, Func<ShadowStyle, ShadowStyle> change)
        => Change(name, o => o.Shadow is { } shadow ? o with { Shadow = change(shadow) } : o);

    /// <summary>How big the shadow is compared to the object, in % (100 = as big; less = small, more = big).</summary>
    public double ShadowSize
    {
        get => Math.Round((Object?.Shadow?.Size ?? 1.0) * 100);
        set => ChangeShadow("Shadow Size", s => s with { Size = Math.Clamp(value, 10, 400) / 100.0 });
    }

    // ------------------------------------------------------------------ 3D (every kind of object)

    public bool HasThreeD => Object?.ThreeD != null;

    public bool ThreeDEnabled
    {
        get => HasThreeD;
        set => Change("3D", o => o with { ThreeD = value ? o.ThreeD ?? ThreeDStyle.Default : null });
    }

    /// <summary>How deep the solid side of the object is, in pixels.</summary>
    public double ThreeDDepth
    {
        get => Math.Round(Object?.ThreeD?.Depth ?? 0);
        set => ChangeThreeD("3D Depth", t => t with { Depth = Math.Clamp(value, 0, 300) });
    }

    /// <summary>Direction the side runs (0 = right, 90 = down, 180 = left, -90 = up).</summary>
    public double ThreeDAngle
    {
        get => Math.Round(Object?.ThreeD?.Angle ?? 0);
        set => ChangeThreeD("3D Angle", t => t with { Angle = ObjectBox.NormalizeAngle(value) });
    }

    /// <summary>How strong the lit / shaded edges on the face are, 0..100 %.</summary>
    public double ThreeDBevel
    {
        get => Math.Round((Object?.ThreeD?.Bevel ?? 0) * 100);
        set => ChangeThreeD("3D Bevel", t => t with { Bevel = Math.Clamp(value, 0, 100) / 100.0 });
    }

    /// <summary>True when the side takes the object's own color (darker); false when a color was chosen.</summary>
    public bool ThreeDAutoColor
    {
        get => Object?.ThreeD?.Color == null;
        set => ChangeThreeD("3D Color", t => t with { Color = value ? null : t.Color ?? new ColorRgba(90, 90, 90) });
    }

    public Brush ThreeDBrush => ToBrush(Object?.ThreeD?.Color ?? new ColorRgba(90, 90, 90));

    private void ChangeThreeD(string name, Func<ThreeDStyle, ThreeDStyle> change)
        => Change(name, o => o.ThreeD is { } style ? o with { ThreeD = change(style) } : o);

    public IReadOnlyList<ThreeDPreset> ThreeDPresets => ThreeDPreset.All;

    /// <summary>Gives the object the 3D look of the preset given as the command parameter.</summary>
    public RelayCommand ApplyThreeDPresetCommand { get; }
    public RelayCommand PickThreeDColorCommand { get; }

    // ------------------------------------------------------------------ saved styles

    /// <summary>The looks the user saved (shadow, 3D, gradient and colors).</summary>
    public ObservableCollection<SavedStyle> SavedStyles => StyleLibrary.Default.Items;
    public bool HasSavedStyles => SavedStyles.Count > 0;

    public RelayCommand SaveStyleCommand { get; }
    public RelayCommand ApplyStyleCommand { get; }
    public RelayCommand DeleteStyleCommand { get; }

    private void SaveStyle()
    {
        if (Object is not { } obj) return;
        var name = _main.Dialogs.ShowTextInput("Save Style", "Name for this look:", Object.KindName + " style " + (SavedStyles.Count + 1));
        if (string.IsNullOrWhiteSpace(name)) return;
        StyleLibrary.Default.Add(name, ObjectStyle.From(obj));
        _main.SetStatus($"Saved the look \"{name.Trim()}\". Click it in Properties > Styles to use it on another object.");
    }

    // ------------------------------------------------------------------ gradient (shapes with a fill, and text)

    /// <summary>Shapes that can be filled and text can have a fading color instead of a flat one.</summary>
    public bool CanGradient => !IsMixed && (Shape?.CanFill == true || IsText || IsImage);

    public bool HasGradient => CanGradient && Object?.Gradient != null;

    /// <summary>For a picture the gradient is a see-through overlay (set each color's opacity).</summary>
    public string GradientTitle => IsImage ? "Gradient overlay" : "Gradient";

    /// <summary>A shape with a border can show the gradient on its border too.</summary>
    public bool CanGradientBorder => HasGradient && Shape is { Stroke: not null };

    public bool GradientBorder
    {
        get => Shape?.GradientBorder == true;
        set => Change("Gradient Border", o => ((ShapeObject)o) with { GradientBorder = value });
    }

    public bool GradientEnabled
    {
        get => HasGradient;
        set => Change("Gradient", o => value
            ? WithGradient(o, o.Gradient ?? (o is ImageObject ? GradientPreset.DarkBottom.CreateStyle() : GradientStyle.Starting(o is TextObject t ? t.Color : o is ShapeObject { Fill: { } f } ? f : new ColorRgba(59, 130, 246))))
            : o with { Gradient = null });
    }

    /// <summary>One row for each color of the gradient (kept and updated in place, so dragging a slider is not interrupted).</summary>
    public IReadOnlyList<GradientStopItem> GradientStops
    {
        get
        {
            var stops = Object?.Gradient?.Stops ?? Array.Empty<ColorStop>();
            if (_stopItems.Count != stops.Count)
            {
                _stopItems.Clear();
                for (int i = 0; i < stops.Count; i++)
                    _stopItems.Add(new GradientStopItem(i, PickGradientStop, MoveGradientStop, RemoveGradientStop, FadeGradientStop));
            }
            for (int i = 0; i < stops.Count; i++) _stopItems[i].Show(stops[i], stops.Count > GradientStyle.MinStops);
            return _stopItems;
        }
    }

    private readonly ObservableCollection<GradientStopItem> _stopItems = new();

    private void PickGradientStop(int index)
    {
        var current = Object?.Gradient?.Stops;
        if (current == null || index >= current.Count) return;
        PickColor("Gradient color", current[index].Color, c => ChangeGradient("Gradient Color", g => g.WithStop(index, st => st with { Color = c with { A = st.Color.A } })));
    }

    private void MoveGradientStop(int index, double position)
        => ChangeGradient("Gradient Position", g => g.WithStop(index, st => st with { Position = position }));

    private void FadeGradientStop(int index, double opacity)
        => ChangeGradient("Gradient Opacity", g => g.WithStop(index, st => st with { Color = st.Color with { A = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255) } }));

    private void RemoveGradientStop(int index) => ChangeGradient("Remove Gradient Color", g => g.WithStopRemoved(index));

    /// <summary>Direction of a straight gradient: 0 = left to right, 90 = top to bottom.</summary>
    public double GradientAngle
    {
        get => Math.Round(Object?.Gradient?.Angle ?? 0);
        set => ChangeGradient("Gradient Angle", g => g with { Angle = ObjectBox.NormalizeAngle(value) });
    }

    /// <summary>True: from the middle outward instead of along a line.</summary>
    public bool GradientRadial
    {
        get => Object?.Gradient?.Radial == true;
        set => ChangeGradient("Gradient Type", g => g with { Radial = value });
    }

    public bool GradientIsLinear => !GradientRadial;

    private static readonly IReadOnlyList<GradientPresetItem> ColorPresetItems = GradientPreset.All.Select(p => new GradientPresetItem(p)).ToList();
    private static readonly IReadOnlyList<GradientPresetItem> OverlayPresetItems = GradientPreset.Overlays.Select(p => new GradientPresetItem(p)).ToList();

    /// <summary>The ready-made looks: colors for shapes and text, see-through overlays for pictures.</summary>
    public IReadOnlyList<GradientPresetItem> GradientPresets => IsImage ? OverlayPresetItems : ColorPresetItems;

    public RelayCommand AddGradientStopCommand { get; }
    public RelayCommand SwapGradientCommand { get; }
    public RelayCommand ApplyGradientPresetCommand { get; }

    /// <summary>The object with the gradient; a shape also gets a fill (what the gradient fills), so it stays filled if the gradient is turned off.</summary>
    private static LayerObject WithGradient(LayerObject o, GradientStyle gradient) => o switch
    {
        ShapeObject s => s with { Gradient = gradient, Fill = s.Fill ?? gradient.From },
        _ => o with { Gradient = gradient },
    };

    private void ChangeGradient(string name, Func<GradientStyle, GradientStyle> change)
        => Change(name, o => o.Gradient is { } gradient ? o with { Gradient = change(gradient) } : o);

    // ------------------------------------------------------------------ commands

    private IReadOnlyList<ShadowPresetItem>? _presetItems;

    /// <summary>The shadow presets with their pictures (made the first time they are needed).</summary>
    /// <summary>The ready-made text designs, each with a small picture.</summary>
    public IReadOnlyList<TextPresetItem> TextPresetItems => _textPresetItems ??= TextPresetPreviews.Create();
    private IReadOnlyList<TextPresetItem>? _textPresetItems;
    public RelayCommand ApplyTextPresetCommand { get; }

    /// <summary>The fonts saved in the Brand Kit (shown as quick buttons).</summary>
    public IReadOnlyList<string> BrandFonts => AppSettings.Current.BrandFonts;
    public bool HasBrandFonts => AppSettings.Current.BrandFonts.Count > 0;
    public RelayCommand ApplyFontCommand => _applyFont ??= new RelayCommand(p =>
    {
        if (p is string name) ChangeText("Font", t => t with { FontFamily = name });
    }, _ => IsText);
    private RelayCommand? _applyFont;

    public IReadOnlyList<ShadowPresetItem> ShadowPresetItems => _presetItems ??= ShadowPresetPreviews.Create();

    /// <summary>The ready-made shadow looks (Soft, Hard, Lifted) shown as quick buttons.</summary>
    public IReadOnlyList<ShadowPreset> ShadowPresets => ShadowPreset.All;

    /// <summary>Gives the object the shadow of the preset given as the command parameter (adds a shadow when there is none).</summary>
    public RelayCommand ApplyShadowPresetCommand { get; }
    public RelayCommand PickShadowColorCommand { get; }

    /// <summary>Turns a pixel layer into a picture object, which can be resized, rotated and given a shadow.</summary>
    public RelayCommand MakeEditableCommand { get; }

    /// <summary>Shows / hides the corner points of the selected shape for dragging.</summary>
    public RelayCommand ToggleReshapeCommand { get; }

    /// <summary>Removes the custom outline: the shape goes back to the normal form of its kind.</summary>
    public RelayCommand ResetShapeCommand { get; }

    /// <summary>Unlocks the layer (shown next to the "locked" note, which would otherwise leave the panel greyed out).</summary>
    public RelayCommand UnlockCommand { get; }
    public RelayCommand DeletePartCommand { get; }
    public RelayCommand LeavePartCommand { get; }
    public RelayCommand PickFillCommand { get; }
    public RelayCommand PickStrokeCommand { get; }
    public RelayCommand PickTextColorCommand { get; }
    public RelayCommand FlipHorizontalCommand { get; }
    public RelayCommand FlipVerticalCommand { get; }
    public RelayCommand ResetRotationCommand { get; }

    /// <summary>Align to the canvas (parameter Left, CenterHorizontally, Right, Top, CenterVertically, Bottom).</summary>
    public RelayCommand AlignCommand => _main.Align.AlignCommand;
    public RelayCommand AlignBothCommand => _main.Align.AlignBothCommand;
    public RelayCommand RotateRightCommand => _main.Layers.RotateRightCommand;
    public RelayCommand RotateLeftCommand => _main.Layers.RotateLeftCommand;
    public RelayCommand ConvertToPixelsCommand { get; }
    public RelayCommand EditTextCommand { get; }
    public RelayCommand OriginalSizeCommand { get; }

    // ------------------------------------------------------------------ changes

    /// <summary>Applies a change to the selected object and records it (merging repeats of the same change).</summary>
    public void Change(string name, Func<LayerObject, LayerObject> change)
    {
        var targets = Targets;
        if (targets.Count > 1) { ChangeMany(name, targets, change); return; }
        if (!EnsureObject()) return;
        var target = Target;
        if (_layer == null || target.Object is not { } current) return;
        if (_layer.IsLocked) { _main.IsLockedWithMessage(_layer); return; }
        var updated = change(current);
        if (Equals(updated, current)) return;

        // Inside a group the change goes to one part; the group then fits itself around its parts again.
        var layerObject = target.WithObject(updated);
        if (layerObject == null) return;
        _main.Push(ObjectChangeAction.Apply(name, _layer, layerObject, mergeKey: target.IsPart ? $"{name} (part {target.Part})" : name));
    }

    /// <summary>The same change on every selected layer, as one undo step (locked layers are left alone).</summary>
    private void ChangeMany(string name, IReadOnlyList<Layer> targets, Func<LayerObject, LayerObject> change)
    {
        var steps = new List<IHistoryAction?>();
        int locked = 0;
        foreach (var layer in targets)
        {
            if (layer.IsLocked) { locked++; continue; }
            if (layer.Object == null && !_main.MakePictureObject(layer, "Make Editable")) continue;
            if (layer.Object is not { } current) continue;
            LayerObject updated;
            try { updated = change(current); }
            catch (InvalidCastException) { continue; }          // a setting that only fits another kind of object
            steps.Add(ObjectChangeAction.Apply(name, layer, updated, mergeKey: name));
        }
        steps.RemoveAll(s => s == null);
        if (steps.Count > 0) _main.Push(new CompositeAction(name, steps));
        if (locked > 0) _main.SetStatus($"{locked} locked layer{(locked == 1 ? " was" : "s were")} left unchanged.");
    }

    /// <summary>Text changes keep the box fitted to the text (top-left stays put).</summary>
    private void ChangeText(string name, Func<TextObject, TextObject> change) => Change(name, o =>
    {
        var updated = change((TextObject)o);
        var (w, h) = ObjectRendering.Renderer.MeasureText(updated);
        return updated with { Box = updated.Box with { Width = w, Height = h } };
    });

    private void ChangeBox(string name, Func<ObjectBox, ObjectBox> change)
        => Change(name, o => o with { Box = change(o.Box) });

    /// <summary>
    /// A layer of plain pixels becomes an editable picture the first time something about it is changed here (so position, size, rotation,
    /// shadow and the other settings work on every layer). True when there is an object to change.
    /// </summary>
    private bool EnsureObject()
    {
        if (_layer is not { Object: null, IsAdjustment: false } pixels) return Object != null;
        if (pixels.IsLocked) { _main.IsLockedWithMessage(pixels); return false; }
        return _main.MakePictureObject(pixels, "Make Editable");
    }

    private void Resize(double? newWidth, double? newHeight)
    {
        if (!EnsureObject()) return;
        if (Object is not { } obj) return;
        var b = obj.Box;
        bool keep = KeepRatio || obj is TextObject;
        double w = newWidth ?? (keep ? b.Width * newHeight!.Value / b.Height : b.Width);
        double h = newHeight ?? (keep ? b.Height * newWidth!.Value / b.Width : b.Height);
        if (obj is ShapeObject { IsLineLike: true }) h = b.Height; // lines only get longer or shorter

        if (obj is TextObject)
        {
            double scale = h / b.Height;
            ChangeText("Resize", t => t with { FontSize = Math.Clamp(t.FontSize * scale, 1, 2000) });
            return;
        }
        ChangeBox("Resize", box => box with { Width = w, Height = h });
    }

    private void MakeEditable()
    {
        if (_layer is not { Object: null } layer) return;
        if (layer.IsLocked) { _main.IsLockedWithMessage(layer); return; }
        if (_main.MakePictureObject(layer))
            _main.SetStatus($"\"{layer.Name}\" is now an editable picture: resize and rotate it, give it a shadow, change its look in the Properties panel.");
        else
            _main.SetStatus("Nothing to make editable: the layer is empty or hidden.");
    }

    private void PickColor(string title, ColorRgba initial, Action<ColorRgba> apply)
    {
        var color = _main.Dialogs.PickColor(initial, title);
        if (color is { } c) apply(c);
    }

    private void EditText()
    {
        if (Text is not { } text) return;
        var value = _main.Dialogs.ShowTextInput("Edit Text", "Text (Enter = new line):", text.Text);
        if (!string.IsNullOrWhiteSpace(value)) TextContent = value;
    }

    private void OriginalSize()
    {
        if (Object is not ImageObject image) return;
        ChangeBox("Original Size", b => ObjectBox.FromCenter(b.Center, image.Source.Width, image.Source.Height, b.Rotation));
    }

    // ------------------------------------------------------------------ tracking the active layer

    private Document? _trackedDocument;

    private void Track()
    {
        if (_trackedDocument != null) _trackedDocument.PropertyChanged -= OnDocumentChanged;
        _trackedDocument = _main.Document.Model;
        _trackedDocument.PropertyChanged += OnDocumentChanged;
        SetLayer(_trackedDocument.ActiveLayer);
    }

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Document.ActiveLayer)) SetLayer(_trackedDocument?.ActiveLayer);
    }

    private void SetLayer(Layer? layer)
    {
        if (_layer != null) _layer.PropertyChanged -= OnLayerChanged;
        _layer = layer;
        if (_layer != null) _layer.PropertyChanged += OnLayerChanged;
        // A picture or text starts with "keep proportions" on, a shape with it off (you can switch it for the layer you are working on).
        _keepRatio = true;
        OnPropertyChanged(string.Empty); // refresh everything
    }

    private void OnLayerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Layer.Object) or nameof(Layer.IsLocked)) OnPropertyChanged(string.Empty);
    }

    private static double Round(double? value) => Math.Round(value ?? 0);

    private static Brush ToBrush(ColorRgba? color)
    {
        var brush = new SolidColorBrush(color is { } c ? BitmapConverter.ToWpf(c) : Colors.Transparent);
        brush.Freeze();
        return brush;
    }
}
