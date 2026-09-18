using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LocalLLMServerManager.Shared.Interfaces;

/// <summary>
/// Natural Language Integration Bridge: allows the AI Copilot to query telemetry,
/// manage engines, update settings, trigger workflows, and query documentation.
/// </summary>
public interface IAiAppTools
{
    /// <summary>
    /// Returns live GPU VRAM usage and hardware identity.
    /// </summary>
    Task<string> GetGpuVramTelemetryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks health and connectivity of Ollama, Forge, ComfyUI, and Kokoro TTS runtimes.
    /// </summary>
    Task<string> CheckServicesHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all installed Ollama LLM models and local model assets.
    /// </summary>
    Task<string> ListInstalledModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a backend engine ('forge', 'comfyui', or 'ollama').
    /// </summary>
    Task<string> StartAiEngineAsync(string engine, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops an active engine ('forge' or 'comfyui').
    /// </summary>
    Task<string> StopAiEngineAsync(string engine, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads all models from VRAM to free GPU memory.
    /// </summary>
    Task<string> UnloadVramAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves current application settings.
    /// </summary>
    Task<string> GetAppSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifies a specific application setting key and persists to disk.
    /// </summary>
    Task<string> UpdateAppSettingAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates whether a specified model fits in the user's GPU VRAM and RAM.
    /// </summary>
    Task<string> CalculateHardwareFitAsync(string modelName, double? parametersBillions = null, string? quantization = null, string? modality = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers an image generation workflow via Forge or ComfyUI.
    /// </summary>
    Task<string> GenerateImageAsync(string prompt, string? negativePrompt = null, string? engine = null, int width = 1024, int height = 1024, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synthesizes text to speech using local Kokoro TTS.
    /// </summary>
    Task<string> SynthesizeSpeechAsync(string text, string voice = "af_heart", string format = "mp3", CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches in-app technical documentation procedures and guides.
    /// </summary>
    Task<string> QueryAppDocumentationAsync(string query, CancellationToken cancellationToken = default);
}
