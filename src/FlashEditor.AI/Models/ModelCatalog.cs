namespace FlashEditor.AI.Models;

/// <summary>What a model is for, in plain words (shown in the AI Models window).</summary>
public sealed record ModelDescription(string Title, string UsedFor);

/// <summary>Every AI model Flash Editor can use, so the AI Models window can list them.</summary>
public static class ModelCatalog
{
    /// <summary>All models, in the order they are shown.</summary>
    public static IReadOnlyList<ModelInfo> All { get; } = new[]
    {
        ModelInfo.IsNetGeneralUse,
        ModelInfo.RealEsrganX4Plus,
        ModelInfo.LamaInpainting,
    };

    /// <summary>The name and the features that need the model.</summary>
    public static ModelDescription Describe(ModelInfo model)
    {
        if (model == ModelInfo.IsNetGeneralUse)
            return new ModelDescription("Background removal and subject selection", "Image > Remove Background (AI), Select > Select Subject (AI)");
        if (model == ModelInfo.RealEsrganX4Plus)
            return new ModelDescription("Upscaling and clean-up", "Image > AI Upscale / Clean Up");
        if (model == ModelInfo.LamaInpainting)
            return new ModelDescription("Object removal (fill)", "Image > Remove / Fill Selection (AI)");
        return new ModelDescription(model.DisplayName, "");
    }
}
