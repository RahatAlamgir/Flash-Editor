using System.Windows;
using FlashEditor.App.Rendering;
using FlashEditor.Core.Imaging;

namespace FlashEditor.App.Services;

/// <summary>Content of the clipboard: pixels plus where they were copied from.</summary>
public sealed record ClipboardImage(PixelBuffer Pixels, int OriginX, int OriginY, bool HasOrigin);

/// <summary>
/// Copy/paste of images. Copies keep full transparency internally and also go to the Windows
/// clipboard so other apps can paste them. Images copied in other apps can be pasted here too.
/// </summary>
public sealed class ClipboardService
{
    private const string MarkerFormat = "FlashEditor.InternalCopy";
    private ClipboardImage? _internal;
    private string? _internalId;

    public void Copy(PixelBuffer pixels, int originX, int originY)
    {
        _internal = new ClipboardImage(pixels, originX, originY, true);
        _internalId = Guid.NewGuid().ToString();
        try
        {
            var data = new DataObject();
            data.SetImage(BitmapConverter.ToFrozenBitmap(pixels));
            data.SetData(MarkerFormat, _internalId);
            Clipboard.SetDataObject(data, copy: true);
        }
        catch (Exception)
        {
            // The Windows clipboard can be locked by another app; the internal copy still works.
        }
    }

    public ClipboardImage? Get()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            // Our own copy is still on the clipboard: use the internal version (keeps transparency).
            if (_internal != null && data?.GetData(MarkerFormat) as string == _internalId)
                return _internal;

            if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } image)
                return new ClipboardImage(BitmapConverter.FromBitmapSource(image), 0, 0, false);
        }
        catch (Exception)
        {
            // Clipboard locked or holding something unreadable.
        }
        return _internal;
    }

    public bool HasImage
    {
        get
        {
            if (_internal != null) return true;
            try { return Clipboard.ContainsImage(); }
            catch { return false; }
        }
    }
}
