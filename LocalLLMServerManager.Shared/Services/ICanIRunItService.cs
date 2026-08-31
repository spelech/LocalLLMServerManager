using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Services;

/// <summary>
/// Service interface for computing hardware compatibility, memory footprints,
/// layer offloading distributions, and speed estimations across AI workloads.
/// </summary>
public interface ICanIRunItService
{
    /// <summary>
    /// Evaluates hardware sizing and layer distribution for LLM text generation workloads.
    /// </summary>
    LlmFitResult EvaluateLlmFit(LlmFitRequest request);

    /// <summary>
    /// Evaluates hardware sizing for diffusion/image generation workloads.
    /// </summary>
    DiffusionFitResult EvaluateDiffusionFit(DiffusionFitRequest request);

    /// <summary>
    /// Evaluates hardware sizing for video generation workloads.
    /// </summary>
    VideoFitResult EvaluateVideoFit(VideoFitRequest request);

    /// <summary>
    /// Evaluates hardware sizing for audio and speech processing engines.
    /// </summary>
    AudioFitResult EvaluateAudioFit(string engineName, long vramMb, long ramMb);

    /// <summary>
    /// Evaluates hardware sizing for 3D mesh generation models.
    /// </summary>
    ThreeDFitResult Evaluate3DFit(string modelName, long vramMb, long ramMb);

    /// <summary>
    /// Generates a lightweight compatibility badge suitable for search/library cards.
    /// </summary>
    QuickFitBadge EvaluateQuickFit(string modelName, long? fileSizeBytes, string modality, long vramMb, long ramMb);
}
