using FlashEditor.Core.Imaging;
using FlashEditor.Core.Qr;

namespace FlashEditor.Core.Objects;

/// <summary>How the dark squares of a QR code are drawn.</summary>
public enum QrStyle { Square, Rounded, Dots }

/// <summary>
/// A QR code that stays editable: the text, colors and the amount of error correction can be changed at any time and the code stays sharp at
/// any size. (Dots and rounded squares look nice but are a little harder to scan: test your code.)
/// </summary>
/// <param name="Content">What the code says: a web address, a phone number, plain text...</param>
/// <param name="Quiet">The empty border around the code, in modules. Scanners need at least 2 to 4.</param>
public sealed record QrCodeObject(
    ObjectBox Box,
    string Content,
    ColorRgba Foreground,
    ColorRgba Background,
    QrLevel Level = QrLevel.Medium,
    QrStyle Style = QrStyle.Square,
    int Quiet = 2,
    bool TransparentBackground = false,
    bool FlipX = false,
    bool FlipY = false) : LayerObject(Box, FlipX, FlipY)
{
    public override string KindName => "QR code";
    public override string SuggestedLayerName => "QR code";

    /// <summary>A square QR code of the given size, centered on the canvas.</summary>
    public static QrCodeObject Centered(string content, int canvasWidth, int canvasHeight, double size)
        => new(new ObjectBox((canvasWidth - size) / 2, (canvasHeight - size) / 2, size, size), content, ColorRgba.Black, ColorRgba.White);
}
