using System.ComponentModel;
using System.Windows.Media;
using FlashEditor.App.Rendering;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Objects;

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
        Track();

        PickFillCommand = new RelayCommand(() => PickColor("Fill color", Shape?.Fill ?? ColorRgba.Black,
            c => Change("Fill Color", o => ((ShapeObject)o) with { Fill = c })), () => IsShape);
        PickStrokeCommand = new RelayCommand(() => PickColor("Border color", Shape?.Stroke ?? ColorRgba.Black,
            c => Change("Border Color", o => ((ShapeObject)o) with { Stroke = c })), () => IsShape);
        PickTextColorCommand = new RelayCommand(() => PickColor("Text color", Text?.Color ?? ColorRgba.Black,
            c => ChangeText("Text Color", t => t with { Color = c })), () => IsText);
        FlipHorizontalCommand = new RelayCommand(() => Change("Flip Horizontal", o => o with { FlipX = !o.FlipX }), () => HasObject);
        FlipVerticalCommand = new RelayCommand(() => Change("Flip Vertical", o => o with { FlipY = !o.FlipY }), () => HasObject);
        ResetRotationCommand = new RelayCommand(() => Rotation = 0, () => HasObject);
        ConvertToPixelsCommand = new RelayCommand(() => { if (_layer != null) _main.ConvertToPixels(_layer, announce: false); }, () => HasObject);
        EditTextCommand = new RelayCommand(EditText, () => IsText);
        OriginalSizeCommand = new RelayCommand(OriginalSize, () => IsImage);
    }

    // ------------------------------------------------------------------ what is selected

    public LayerObject? Object => _layer?.Object;
    private ShapeObject? Shape => Object as ShapeObject;
    private TextObject? Text => Object as TextObject;

    public bool HasObject => Object != null;

    /// <summary>Locked objects show their settings but cannot be changed.</summary>
    public bool IsLocked => _layer?.IsLocked == true;
    public bool CanEdit => HasObject && !IsLocked;
    public bool IsShape => Shape != null;
    public bool IsText => Text != null;
    public bool IsImage => Object is ImageObject;
    public bool IsGroup => Object is GroupObject;
    public bool CanFill => Shape?.CanFill == true;
    public bool HasCorners => Shape?.HasCorners == true;
    public bool HasPoints => Shape?.HasPoints == true;
    public bool IsLine => Shape?.IsLineLike == true;

    public string Title => Object switch
    {
        ShapeObject s => $"{s.SuggestedLayerName} shape",
        TextObject => "Text",
        ImageObject => "Image",
        GroupObject g => $"Group of {g.Members.Count}",
        _ => "",
    };

    // ------------------------------------------------------------------ position and size

    public double X { get => Round(Object?.Box.X); set => ChangeBox("Move", b => b with { X = value }); }
    public double Y { get => Round(Object?.Box.Y); set => ChangeBox("Move", b => b with { Y = value }); }

    public double Width
    {
        get => Round(Object?.Box.Width);
        set => Resize(Math.Max(1, value), null);
    }

    public double Height
    {
        get => Round(Object?.Box.Height);
        set => Resize(null, Math.Max(1, value));
    }

    public double Rotation
    {
        get => Math.Round(Object?.Box.Rotation ?? 0);
        set => ChangeBox("Rotate", b => b with { Rotation = ObjectBox.NormalizeAngle(value) });
    }

    /// <summary>When on, changing width also changes height (and the other way round).</summary>
    public bool KeepRatio { get => _keepRatio; set => SetField(ref _keepRatio, value); }

    // ------------------------------------------------------------------ shape

    public IReadOnlyList<ShapeKind> ShapeKinds { get; } = Enum.GetValues<ShapeKind>();

    public ShapeKind ShapeKind
    {
        get => Shape?.Shape ?? ShapeKind.Rectangle;
        set => Change("Change Shape", o => ((ShapeObject)o) with { Shape = value });
    }

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

    // ------------------------------------------------------------------ commands

    public RelayCommand PickFillCommand { get; }
    public RelayCommand PickStrokeCommand { get; }
    public RelayCommand PickTextColorCommand { get; }
    public RelayCommand FlipHorizontalCommand { get; }
    public RelayCommand FlipVerticalCommand { get; }
    public RelayCommand ResetRotationCommand { get; }
    public RelayCommand ConvertToPixelsCommand { get; }
    public RelayCommand EditTextCommand { get; }
    public RelayCommand OriginalSizeCommand { get; }

    // ------------------------------------------------------------------ changes

    /// <summary>Applies a change to the selected object and records it (merging repeats of the same change).</summary>
    public void Change(string name, Func<LayerObject, LayerObject> change)
    {
        if (_layer?.Object is not { } current) return;
        if (_layer.IsLocked) { _main.IsLockedWithMessage(_layer); return; }
        var updated = change(current);
        _main.Push(ObjectChangeAction.Apply(name, _layer, updated, mergeKey: name));
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

    private void Resize(double? newWidth, double? newHeight)
    {
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
