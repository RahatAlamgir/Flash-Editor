using FlashEditor.App.Services;
using FlashEditor.App.ViewModels.Infrastructure;
using FlashEditor.Core.Common;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.ViewModels;

/// <summary>Foreground / background colors (the two overlapping swatches in the toolbox).</summary>
public sealed class ColorsViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private ColorRgba _foreground = ColorRgba.Black;
    private ColorRgba _background = ColorRgba.White;

    public ColorsViewModel(IDialogService dialogs)
    {
        _dialogs = dialogs;
        SwapCommand = new RelayCommand(Swap);
        ResetCommand = new RelayCommand(Reset);
        PickForegroundCommand = new RelayCommand(() =>
        {
            var c = _dialogs.PickColor(Foreground, "Foreground Color");
            if (c != null) Foreground = c.Value;
        });
        PickBackgroundCommand = new RelayCommand(() =>
        {
            var c = _dialogs.PickColor(Background, "Background Color");
            if (c != null) Background = c.Value;
        });
    }

    /// <summary>Used by brush, bucket, shapes and text.</summary>
    public ColorRgba Foreground
    {
        get => _foreground;
        set => SetField(ref _foreground, value);
    }

    /// <summary>Used for shape fill and "new image" backgrounds.</summary>
    public ColorRgba Background
    {
        get => _background;
        set => SetField(ref _background, value);
    }

    public RelayCommand SwapCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand PickForegroundCommand { get; }
    public RelayCommand PickBackgroundCommand { get; }

    public void Swap() => (Foreground, Background) = (Background, Foreground);

    public void Reset()
    {
        Foreground = ColorRgba.Black;
        Background = ColorRgba.White;
    }
}
