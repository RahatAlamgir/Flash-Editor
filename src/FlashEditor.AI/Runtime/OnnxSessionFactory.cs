using Microsoft.ML.OnnxRuntime;

namespace FlashEditor.AI.Runtime;

/// <summary>
/// Opens AI models on the best device available: the graphics card through DirectML (works with
/// NVIDIA, AMD and Intel GPUs on Windows 10/11), or the processor when there is no usable GPU or the
/// GPU fails to load the model.
/// </summary>
public static class OnnxSessionFactory
{
    /// <summary>Set to false to always use the processor (e.g. for testing).</summary>
    public static bool PreferGpu { get; set; } = true;

    /// <summary>Opens the model. <paramref name="device"/> says what is used, e.g. "GPU (DirectML)" or "CPU".</summary>
    public static InferenceSession Create(string modelPath, out string device)
    {
        if (PreferGpu)
        {
            try
            {
                var gpu = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                    EnableMemoryPattern = false,                  // required by DirectML
                    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,  // required by DirectML
                };
                gpu.AppendExecutionProvider_DML(0);
                var session = new InferenceSession(modelPath, gpu);
                device = "GPU (DirectML)";
                return session;
            }
            catch (Exception)
            {
                // No DirectX 12 GPU, an old driver, or the model does not run on it: use the processor.
            }
        }
        var cpu = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        device = "CPU";
        return new InferenceSession(modelPath, cpu);
    }
}
