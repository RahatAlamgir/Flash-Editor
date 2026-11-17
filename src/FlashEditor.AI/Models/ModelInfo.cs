namespace FlashEditor.AI.Models;

/// <summary>
/// Describes an AI model: where to download it, how to check it, and how the model wants its input.
/// <para>
/// Most models are a single .onnx file. Some come as a zip holding the .onnx file plus a separate
/// weights file (<see cref="ArchiveFiles"/>); then <see cref="SizeBytes"/> / <see cref="Sha256"/>
/// describe the zip, and the listed files are unpacked into a folder named <see cref="Folder"/>.
/// </para>
/// </summary>
public sealed record ModelInfo(
    string DisplayName,
    string FileName,
    string DownloadUrl,
    long SizeBytes,
    string Sha256,
    int InputSize,
    float[] Mean,
    float[] Std,
    IReadOnlyDictionary<string, long>? ArchiveFiles = null,
    string? Folder = null)
{
    /// <summary>
    /// IS-Net "general use" (DIS, Apache 2.0 license), as published by the rembg project.
    /// Finds the main subject of a photo; 1024 x 1024 input.
    /// </summary>
    public static readonly ModelInfo IsNetGeneralUse = new(
        DisplayName: "IS-Net background removal model",
        FileName: "isnet-general-use.onnx",
        DownloadUrl: "https://github.com/danielgatis/rembg/releases/download/v0.0.0/isnet-general-use.onnx",
        SizeBytes: 178_648_008,
        Sha256: "60920e99c45464f2ba57bee2ad08c919a52bbf852739e96947fbb4358c0d964a",
        InputSize: 1024,
        Mean: new[] { 0.5f, 0.5f, 0.5f },
        Std: new[] { 1.0f, 1.0f, 1.0f });

    /// <summary>
    /// Real-ESRGAN x4plus (BSD-3 license), as converted to ONNX and published by Qualcomm AI Hub
    /// (huggingface.co/qualcomm/Real-ESRGAN-x4plus, release v0.66.0). Makes pictures 4x larger and
    /// sharper; works on 128 x 128 tiles (output 512 x 512), RGB values 0..1.
    /// </summary>
    public static readonly ModelInfo RealEsrganX4Plus = new(
        DisplayName: "Real-ESRGAN x4plus upscaling model",
        FileName: "real_esrgan_x4plus.onnx",
        DownloadUrl: "https://qaihub-public-assets.s3.us-west-2.amazonaws.com/qai-hub-models/models/real_esrgan_x4plus/releases/v0.66.0/real_esrgan_x4plus-onnx-float.zip",
        SizeBytes: 62_174_027,
        Sha256: "05aa31323c6a7e831493171cac970e398697709cae7e7ae8e0760e6da4d64ca4",
        InputSize: 128,
        Mean: new[] { 0f, 0f, 0f },
        Std: new[] { 1f, 1f, 1f },
        ArchiveFiles: new Dictionary<string, long>
        {
            ["real_esrgan_x4plus.onnx"] = 3_158_313,
            ["real_esrgan_x4plus.data"] = 66_737_664,
        },
        Folder: "real_esrgan_x4plus");

    public string SizeText => $"{SizeBytes / (1024.0 * 1024.0):0} MB";
}
