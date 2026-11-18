using FlashEditor.Core.Barcodes;
using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Objects;

/// <summary>
/// A barcode that stays editable: the text, the kind of barcode and the colors can be changed at any time, and the bars stay sharp at any size.
/// </summary>
/// <param name="Content">What the barcode says (digits for EAN / UPC; the check digit is added when missing).</param>
/// <param name="ShowText">Print the numbers or letters under the bars.</param>
public sealed record BarcodeObject(
    ObjectBox Box,
    string Content,
    BarcodeKind Kind,
    ColorRgba Foreground,
    ColorRgba Background,
    bool ShowText = true,
    bool TransparentBackground = false,
    bool FlipX = false,
    bool FlipY = false) : LayerObject(Box, FlipX, FlipY)
{
    public override string KindName => "Barcode";
    public override string SuggestedLayerName => "Barcode";
    public override bool KeepsAspectByDefault => false;
}
