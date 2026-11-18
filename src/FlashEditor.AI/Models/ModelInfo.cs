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

    /// <summary>
    /// LaMa "big-lama" (Apache 2.0 license), as converted to ONNX by Carve (huggingface.co/Carve/LaMa-ONNX). Fills a hidden area of a picture
    /// with what would plausibly be there (removes objects). Works on 512 x 512 pictures; the answer has values 0..255.
    /// </summary>
    public static readonly ModelInfo LamaInpainting = new(
        DisplayName: "LaMa object removal model",
        FileName: "lama_fp32.onnx",
        DownloadUrl: "https://huggingface.co/Carve/LaMa-ONNX/resolve/main/lama_fp32.onnx",
        SizeBytes: 208_044_816,
        Sha256: "1faef5301d78db7dda502fe59966957ec4b79dd64e16f03ed96913c7a4eb68d6",
        InputSize: 512,
        Mean: new[] { 0f, 0f, 0f },
        Std: new[] { 1f, 1f, 1f });

    /// <summary>
    /// MobileSAM (Apache 2.0 license) image encoder, as converted to ONNX by Acly (huggingface.co/Acly/MobileSAM). Studies a picture so that
    /// clicking on an object can select it. Works on pictures whose longest side is 1024; the model normalizes the colors itself.
    /// </summary>
    public static readonly ModelInfo MobileSamEncoder = new(
        DisplayName: "MobileSAM click-to-select model (picture part)",
        FileName: "mobile_sam_image_encoder.onnx",
        DownloadUrl: "https://huggingface.co/Acly/MobileSAM/resolve/main/mobile_sam_image_encoder.onnx",
        SizeBytes: 28_157_093,
        Sha256: "580f5fb648ea1062c0aabc26217aed56921985f03f0cbbd852bba81d760cc749",
        InputSize: 1024,
        Mean: new[] { 0f, 0f, 0f },
        Std: new[] { 1f, 1f, 1f });

    /// <summary>MobileSAM mask decoder (the small second part): turns a click or box into the outline of the object.</summary>
    public static readonly ModelInfo MobileSamDecoder = new(
        DisplayName: "MobileSAM click-to-select model (outline part)",
        FileName: "sam_mask_decoder_single.onnx",
        DownloadUrl: "https://huggingface.co/Acly/MobileSAM/resolve/main/sam_mask_decoder_single.onnx",
        SizeBytes: 16_501_323,
        Sha256: "93915fc7c993ab9d59ab8c9ccd3bce37f7509c81ab4150a74abd4d2abbd8570d",
        InputSize: 256,
        Mean: new[] { 0f, 0f, 0f },
        Std: new[] { 1f, 1f, 1f });

    public string SizeText => $"{SizeBytes / (1024.0 * 1024.0):0} MB";
}
