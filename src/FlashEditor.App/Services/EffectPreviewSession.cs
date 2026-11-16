using FlashEditor.Core.Documents;
using FlashEditor.Core.Effects;
using FlashEditor.Core.History;
using FlashEditor.Core.Imaging;
using FlashEditor.Core.Selections;

namespace FlashEditor.App.Services;

/// <summary>
/// Live preview of an effect on a layer while its dialog is open. Work runs on a background thread;
/// older, still-running previews are cancelled when the user moves a slider again.
/// </summary>
public sealed class EffectPreviewSession
{
    private readonly Layer _layer;
    private readonly ImageEffect _effect;
    private readonly Selection? _selection;
    private readonly PixelBuffer _original;
    private CancellationTokenSource? _pending;
    private int _version;

    public EffectPreviewSession(Layer layer, ImageEffect effect, Selection? selection)
    {
        _layer = layer;
        _effect = effect;
        _selection = selection;
        _original = layer.Pixels.Clone();
    }

    /// <summary>Recomputes the preview with new values (fire and forget).</summary>
    public async void Update(EffectValues values)
    {
        int version = ++_version;
        _pending?.Cancel();
        var cts = _pending = new CancellationTokenSource();
        var snapshot = values.Clone();
        try
        {
            var result = await Task.Run(() => EffectApplier.Apply(_original, _effect, snapshot, _selection, cts.Token), cts.Token);
            if (version != _version) return; // a newer preview superseded this one
            Show(result);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>Applies the final values and returns the history step (null if nothing changed).</summary>
    public async Task<IHistoryAction?> CommitAsync(EffectValues values)
    {
        _version++;
        _pending?.Cancel();
        var snapshot = values.Clone();
        var result = await Task.Run(() => EffectApplier.Apply(_original, _effect, snapshot, _selection));
        Show(result);
        var region = _selection?.Bounds ?? _original.Bounds;
        return PixelChangeAction.Create(_effect.Name, _layer, _original, region);
    }

    /// <summary>Puts the original pixels back.</summary>
    public void Cancel()
    {
        _version++;
        _pending?.Cancel();
        Show(_original);
    }

    private void Show(PixelBuffer pixels)
    {
        _layer.Pixels.CopyFrom(pixels, 0, 0);
        _layer.NotifyPixelsChanged();
    }
}
