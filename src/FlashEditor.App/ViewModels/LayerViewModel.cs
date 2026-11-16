using System.ComponentModel;
using System.Windows.Media.Imaging;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Common;
using FlashEditor.Core.Documents;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.ViewModels;

/// <summary>
/// Screen-side wrapper of a <see cref="Layer"/>: keeps a WPF bitmap in sync with the layer pixels and
/// records name / opacity / visibility changes made in the UI as undoable steps.
/// </summary>
public sealed class LayerViewModel : ObservableObject, IDisposable
{
    private readonly HistoryManager _history;
    private WriteableBitmap _bitmap;
    private bool _isEditingName;

    public LayerViewModel(Layer model, HistoryManager history)
    {
        Model = model;
        _history = history;
        _bitmap = BitmapConverter.CreateWriteable(model.Pixels);

        model.PropertyChanged += OnModelPropertyChanged;
        model.PixelsChanged += OnPixelsChanged;
        model.PixelsReplaced += OnPixelsReplaced;
    }

    public Layer Model { get; }

    /// <summary>Displayed on the canvas and as the thumbnail in the Layers panel.</summary>
    public WriteableBitmap Bitmap => _bitmap;

    public int Width => Model.Width;
    public int Height => Model.Height;

    public string Name
    {
        get => Model.Name;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? Model.Name : value.Trim();
            if (value == Model.Name) return;
            var old = Model.Name;
            Model.Name = value;
            _history.Push(new LayerPropertyAction(Model, LayerProperty.Name, old, value));
        }
    }

    /// <summary>Layer opacity as a percentage (0..100) for the slider.</summary>
    public double OpacityPercent
    {
        get => Math.Round(Model.Opacity * 100);
        set
        {
            double newValue = Math.Clamp(value, 0, 100) / 100.0;
            if (Math.Abs(newValue - Model.Opacity) < 0.0001) return;
            double old = Model.Opacity;
            Model.Opacity = newValue;
            _history.Push(new LayerPropertyAction(Model, LayerProperty.Opacity, old, newValue));
        }
    }

    /// <summary>Opacity 0..1 for the canvas Image element.</summary>
    public double Opacity => Model.Opacity;

    public bool IsVisible
    {
        get => Model.IsVisible;
        set
        {
            if (value == Model.IsVisible) return;
            Model.IsVisible = value;
            _history.Push(new LayerPropertyAction(Model, LayerProperty.Visibility, !value, value));
        }
    }

    /// <summary>Locked layers cannot be moved or edited (toggle with the lock icon).</summary>
    public bool IsLocked
    {
        get => Model.IsLocked;
        set
        {
            if (value == Model.IsLocked) return;
            Model.IsLocked = value;
            _history.Push(new LayerPropertyAction(Model, LayerProperty.Lock, !value, value));
        }
    }

    /// <summary>True while the name is being edited inline in the Layers panel.</summary>
    public bool IsEditingName
    {
        get => _isEditingName;
        set => SetField(ref _isEditingName, value);
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Layer.Name): OnPropertyChanged(nameof(Name)); break;
            case nameof(Layer.Opacity):
                OnPropertyChanged(nameof(Opacity));
                OnPropertyChanged(nameof(OpacityPercent));
                break;
            case nameof(Layer.IsVisible): OnPropertyChanged(nameof(IsVisible)); break;
            case nameof(Layer.IsLocked): OnPropertyChanged(nameof(IsLocked)); break;
        }
    }

    private void OnPixelsChanged(object? sender, PixelRect region)
        => BitmapConverter.Update(_bitmap, Model.Pixels, region);

    private void OnPixelsReplaced(object? sender, EventArgs e)
    {
        if (_bitmap.PixelWidth == Model.Width && _bitmap.PixelHeight == Model.Height)
        {
            BitmapConverter.Update(_bitmap, Model.Pixels, Model.Pixels.Bounds);
            return;
        }
        _bitmap = BitmapConverter.CreateWriteable(Model.Pixels);
        OnPropertyChanged(nameof(Bitmap));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
    }

    public void Dispose()
    {
        Model.PropertyChanged -= OnModelPropertyChanged;
        Model.PixelsChanged -= OnPixelsChanged;
        Model.PixelsReplaced -= OnPixelsReplaced;
    }
}
