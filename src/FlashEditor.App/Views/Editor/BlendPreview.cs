using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FlashEditor.App.Rendering;
using FlashEditor.App.ViewModels;
using FlashEditor.Core.Documents;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Painting;

namespace FlashEditor.App.Views.Editor;

/// <summary>
/// Shows the picture with its blend modes. The canvas normally lets WPF stack one image per layer, which can only do "normal".
/// As soon as a visible layer uses another blend mode, this image takes over: it keeps the whole picture mixed in one bitmap and
/// redraws just the part that changed. When no layer needs it, it hides and the fast per-layer view is used again.
/// </summary>
public sealed class BlendPreview : Image
{
    private DocumentViewModel? _document;
    private PixelBuffer? _flat;
    private WriteableBitmap? _bitmap;
    private PixelRect _pending = PixelRect.Empty;
    private bool _scheduled;
    private readonly List<Layer> _watched = new();
    private Action<bool>? _activeChanged;

    public BlendPreview()
    {
        Stretch = System.Windows.Media.Stretch.Fill;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
    }

    /// <summary>True while this image is the one showing the picture.</summary>
    public bool IsActive => Visibility == Visibility.Visible;

    /// <summary>Starts following a picture. <paramref name="activeChanged"/> is told when this image takes over (true) or hands back (false).</summary>
    public void Attach(DocumentViewModel? document, Action<bool> activeChanged)
    {
        Detach();
        _activeChanged = activeChanged;
        _document = document;
        if (document == null) return;
        document.Model.Layers.CollectionChanged += OnLayersChanged;
        document.Model.CanvasSizeChanged += OnCanvasChanged;
        Watch();
        Refresh(full: true);
    }

    private void Detach()
    {
        if (_document != null)
        {
            _document.Model.Layers.CollectionChanged -= OnLayersChanged;
            _document.Model.CanvasSizeChanged -= OnCanvasChanged;
        }
        Unwatch();
        _document = null;
        SetActive(false);
        _flat = null;
        _bitmap = null;
        Source = null;
    }

    private void Watch()
    {
        Unwatch();
        if (_document == null) return;
        foreach (var layer in _document.Model.Layers)
        {
            layer.PropertyChanged += OnLayerPropertyChanged;
            layer.PixelsChanged += OnPixelsChanged;
            layer.PixelsReplaced += OnPixelsReplaced;
            layer.MaskChanged += OnPixelsChanged;
            layer.MaskReplaced += OnPixelsReplaced;
            _watched.Add(layer);
        }
    }

    private void Unwatch()
    {
        foreach (var layer in _watched)
        {
            layer.PropertyChanged -= OnLayerPropertyChanged;
            layer.PixelsChanged -= OnPixelsChanged;
            layer.PixelsReplaced -= OnPixelsReplaced;
            layer.MaskChanged -= OnPixelsChanged;
            layer.MaskReplaced -= OnPixelsReplaced;
        }
        _watched.Clear();
    }

    private void OnLayersChanged(object? sender, NotifyCollectionChangedEventArgs e) { Watch(); Refresh(full: true); }
    private void OnCanvasChanged(object? sender, EventArgs e) { _flat = null; Refresh(full: true); }
    private void OnPixelsReplaced(object? sender, EventArgs e) => Refresh(full: true);

    private void OnLayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Layer.Opacity) or nameof(Layer.IsVisible) or nameof(Layer.BlendMode) or nameof(Layer.Adjustment)) Refresh(full: true);
    }

    private void OnPixelsChanged(object? sender, PixelRect region)
    {
        if (sender is Layer { IsVisible: true } && (_document?.Model.Layers.Any(NeedsBlend) ?? false)) Refresh(full: false, region);
    }

    private static bool NeedsBlend(Layer layer) => layer.IsVisible && layer.Opacity > 0 && (layer.BlendMode != BlendMode.Normal || layer.Adjustment != null);

    private void SetActive(bool active)
    {
        if (IsActive == active) return;
        Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        _activeChanged?.Invoke(active);
    }

    private void Refresh(bool full, PixelRect region = default)
    {
        if (_document == null) return;
        bool needed = _document.Model.Layers.Any(NeedsBlend);
        if (!needed) { SetActive(false); _pending = PixelRect.Empty; return; }

        var doc = _document.Model;
        if (_flat == null || _flat.Width != doc.Width || _flat.Height != doc.Height)
        {
            _flat = new PixelBuffer(doc.Width, doc.Height);
            _bitmap = BitmapConverter.CreateWriteable(_flat);
            Source = _bitmap;
            Width = doc.Width;
            Height = doc.Height;
            full = true;
        }
        _pending = full ? _flat.Bounds : _pending.IsEmpty ? region : _pending.Union(region);
        SetActive(true);
        if (_scheduled) return;
        _scheduled = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(Flush));
    }

    /// <summary>Mixes the changed part (several changes in a row are done together) and shows it.</summary>
    private void Flush()
    {
        _scheduled = false;
        if (_document == null || _flat == null || _bitmap == null || _pending.IsEmpty) return;
        var area = _pending;
        _pending = PixelRect.Empty;
        Compositor.FlattenRegion(_document.Model, area, _flat);
        BitmapConverter.Update(_bitmap, _flat, area);
    }
}
