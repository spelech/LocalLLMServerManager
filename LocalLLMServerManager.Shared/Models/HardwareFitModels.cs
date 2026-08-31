namespace LocalLLMServerManager.Shared.Models;

/// <summary>
/// Verdict representing how a given AI model will run on the current hardware configuration.
/// </summary>
public enum FitVerdict
{
    /// <summary>
    /// Model fits 100% in GPU VRAM with full hardware acceleration.
    /// </summary>
    FullVram,

    /// <summary>
    /// Model partially fits in GPU VRAM with remaining layers offloaded to System RAM / CPU.
    /// </summary>
    PartialOffload,

    /// <summary>
    /// Model cannot offload significant layers to GPU and runs purely on CPU / System RAM.
    /// </summary>
    CpuOnly,

    /// <summary>
    /// Model exceeds combined VRAM and System RAM, resulting in Out Of Memory error.
    /// </summary>
    OutOfMemory
}

/// <summary>
/// Request parameters for evaluating an LLM text-generation model.
/// </summary>
public record LlmFitRequest(
    double ParametersBillions,
    string Quantization = "Q4_K_M",
    int ContextLength = 4096,
    string KvPrecision = "FP16",
    long AvailableVramMb = 16384,
    long AvailableRamMb = 32768,
    int? TotalLayers = null,
    int? KvHeads = null,
    int? HeadDim = null
);

/// <summary>
/// Evaluation result for an LLM text-generation workload.
/// </summary>
public record LlmFitResult(
    long ModelWeightMb,
    long KvCacheMb,
    long OverheadMb,
    long TotalVramMb,
    long TotalRamMb,
    int GpuLayers,
    int CpuLayers,
    int TotalLayers,
    FitVerdict FitVerdict,
    double EstimatedTokPerSec,
    string RecommendationMessage
);

/// <summary>
/// Request parameters for evaluating a diffusion/image generation model.
/// </summary>
public record DiffusionFitRequest(
    string ModelName = "Flux.1 Dev",
    string Quantization = "FP8",
    int Resolution = 1024,
    long AvailableVramMb = 16384,
    long AvailableRamMb = 32768
);

/// <summary>
/// Evaluation result for a diffusion/image generation workload.
/// </summary>
public record DiffusionFitResult(
    string ModelName,
    long BaseModelMb,
    long EncodersMb,
    long VaeMb,
    long LatentBufferMb,
    long TotalVramMb,
    long TotalRamMb,
    FitVerdict FitVerdict,
    double EstimatedSecondsPerImage,
    string RecommendationMessage
);

/// <summary>
/// Request parameters for evaluating a video generation model.
/// </summary>
public record VideoFitRequest(
    string ModelName = "Wan 2.2 14B",
    string Quantization = "FP8",
    int FrameCount = 49,
    int Resolution = 720,
    long AvailableVramMb = 16384,
    long AvailableRamMb = 32768
);

/// <summary>
/// Evaluation result for a video generation workload.
/// </summary>
public record VideoFitResult(
    string ModelName,
    long DiTModelMb,
    long FrameContextMb,
    long VaeDecodeMb,
    long TotalVramMb,
    long TotalRamMb,
    FitVerdict FitVerdict,
    double EstimatedSecondsPerFrame,
    string RecommendationMessage
);

/// <summary>
/// Evaluation result for an audio/speech engine workload.
/// </summary>
public record AudioFitResult(
    string EngineName,
    long VramRequiredMb,
    long RamRequiredMb,
    FitVerdict FitVerdict,
    double EstimatedRealtimeFactor,
    string RecommendationMessage
);

/// <summary>
/// Evaluation result for a 3D mesh generation model.
/// </summary>
public record ThreeDFitResult(
    string ModelName,
    long VramRequiredMb,
    long RamRequiredMb,
    FitVerdict FitVerdict,
    double EstimatedSecondsPerMesh,
    string RecommendationMessage
);

/// <summary>
/// Ambient visual badge representing quick hardware compatibility.
/// </summary>
public record QuickFitBadge(
    string BadgeText,
    string BadgeColorHex,
    string Tooltip,
    FitVerdict FitVerdict
);

/// <summary>
/// Hardware telemetry snapshot containing GPU VRAM and System RAM metrics.
/// </summary>
public record TelemetryInfo(
    string GpuName,
    double TotalVramMb,
    double FreeVramMb,
    double UsedVramMb,
    double TotalRamMb,
    double AvailableRamMb
);

