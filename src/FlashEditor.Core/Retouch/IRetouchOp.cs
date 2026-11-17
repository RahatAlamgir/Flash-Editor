using FlashEditor.Core.Imaging;

namespace FlashEditor.Core.Retouch;

/// <summary>
/// What a retouching tool does to one pixel (copy from elsewhere, heal, lighten, blur...). The tool decides where and how
/// strongly (the brush coverage); the operation decides what the pixel becomes.
/// </summary>
public interface IRetouchOp
{
    /// <summary>
    /// Changes the pixel (x, y) in <paramref name="dst"/>, which holds the same pixel as <paramref name="original"/> when called.
    /// <paramref name="strength"/> is 0..1: how far the pixel goes towards the result (0 = unchanged).
    /// </summary>
    void Apply(PixelBuffer original, byte[] dst, int x, int y, double strength);

    /// <summary>
    /// Tells the operation which pixels are being painted over (the blemish), so it can look at what is around them instead.
    /// Called before the first <see cref="Apply"/>; most operations ignore it.
    /// </summary>
    void Attach(Func<int, int, bool> isPainted) { }
}
