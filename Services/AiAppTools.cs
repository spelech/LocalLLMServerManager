using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Services;

public class AiAppTools : IAiAppTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IGpuTelemetryProvider _telemetryProvider;
    private readonly IAiEngineManager _engineManager;
    private readonly IOllamaModelService _ollamaModelService;
    private readonly ISettingsService _settingsService;
    private readonly ICanIRunItService _canIRunItService;
    private readonly IHttpClientFactory _httpClientFactory;

    public AiAppTools(
        IGpuTelemetryProvider telemetryProvider,
        IAiEngineManager engineManager,
        IOllamaModelService ollamaModelService,
        ISettingsService settingsService,
        ICanIRunItService canIRunItService,
        IHttpClientFactory httpClientFactory)
    {
        _telemetryProvider = telemetryProvider;
        _engineManager = engineManager;
        _ollamaModelService = ollamaModelService;
        _settingsService = settingsService;
        _canIRunItService = canIRunItService;
        _httpClientFactory = httpClientFactory;
    }

    [Description("Get real-time GPU VRAM allocation, total memory, used memory, and GPU hardware name.")]
    public async Task<string> GetGpuVramTelemetryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var telemetry = await _telemetryProvider.GetTelemetryAsync();
            return JsonSerializer.Serialize(telemetry, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [Description("Check real-time health and connectivity of Ollama, Stable Diffusion Forge, ComfyUI, and Kokoro TTS ports.")]
    public async Task<string> CheckServicesHealthAsync(CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(2);

        async Task<object> CheckPort(string url)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var resp = await client.GetAsync(url, cancellationToken);
                sw.Stop();
                return new { online = resp.IsSuccessStatusCode, statusCode = (int)resp.StatusCode, latencyMs = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                return new { online = false, error = ex.Message };
            }
        }

        var settings = _settingsService.LoadSettings();
        var results = new
        {
            ollama = await CheckPort("http://127.0.0.1:11434/"),
            sdForge = await CheckPort("http://127.0.0.1:7860/"),
            comfyUi = await CheckPort(string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188/system_stats" : $"{settings.ComfyUiUrl.TrimEnd('/')}/system_stats"),
            kokoroTts = await CheckPort(string.IsNullOrWhiteSpace(settings.AudioEngineUrl) ? "http://127.0.0.1:8880/" : settings.AudioEngineUrl)
        };

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }

    [Description("List all installed Ollama LLM models, quantization formats, and memory footprint.")]
    public async Task<string> ListInstalledModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await _ollamaModelService.GetInstalledModelsAsync();
            return JsonSerializer.Serialize(models, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [Description("Start an AI backend engine process ('forge', 'comfyui', or 'ollama').")]
    public async Task<string> StartAiEngineAsync(string engine, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _engineManager.StartEngineAsync(engine);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [Description("Gracefully terminate an AI backend engine process ('forge' or 'comfyui').")]
    public async Task<string> StopAiEngineAsync(string engine, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _engineManager.StopEngineAsync(engine);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [Description("Unload all LLM models currently residing in GPU VRAM to free memory for diffusion or heavy workflows.")]
    public async Task<string> UnloadVramAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var payload = new StringContent("{\"model\":\"\",\"keep_alive\":0}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://127.0.0.1:11434/api/generate", payload, cancellationToken);
            return JsonSerializer.Serialize(new { success = response.IsSuccessStatusCode, status = (int)response.StatusCode, message = "VRAM unload requested" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [Description("Get current application configuration settings.")]
    public Task<string> GetAppSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.LoadSettings();
        // Mask API Key in output for security
        var safe = settings with
        {
            AiAssistantApiKey = string.IsNullOrWhiteSpace(settings.AiAssistantApiKey) ? "" : "******"
        };
        return Task.FromResult(JsonSerializer.Serialize(safe, JsonOptions));
    }

    [Description("Update a specific application setting key and persist changes to disk.")]
    public Task<string> UpdateAppSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            var current = _settingsService.LoadSettings();
            AppSettings updated = key.ToLowerInvariant() switch
            {
                "preferredimageengine" or "imageengine" => current with { PreferredImageEngine = value },
                "comfyuiurl" or "comfyurl" => current with { ComfyUiUrl = value },
                "audioengineurl" or "audiourl" => current with { AudioEngineUrl = value },
                "preferredaudiovoice" or "voice" => current with { PreferredAudioVoice = value },
                "selectedthemestyle" or "theme" => current with { SelectedThemeStyle = value },
                "aiassistantenabled" => current with { AiAssistantEnabled = bool.TryParse(value, out var b) && b },
                "aiassistantendpoint" => current with { AiAssistantEndpoint = value },
                "aiassistantapikey" => current with { AiAssistantApiKey = value },
                "aiassistantmodel" => current with { AiAssistantModel = value },
                "aiassistantpromptsdirectory" => current with { AiAssistantPromptsDirectory = value },
                "forgemodelspath" => current with { ForgeModelsPath = value },
                "comfymodelspath" => current with { ComfyModelsPath = value },
                "videomodelspath" => current with { VideoModelsPath = value },
                "audiopath" => current with { AudioPath = value },
                _ => throw new ArgumentException($"Setting '{key}' is not recognized or cannot be dynamically updated.")
            };

            _settingsService.SaveSettings(updated);
            return Task.FromResult(JsonSerializer.Serialize(new { success = true, key, value, message = $"Setting '{key}' updated successfully." }, JsonOptions));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions));
        }
    }

    [Description("Calculate hardware compatibility and VRAM fit for an LLM, diffusion, or video model.")]
    public async Task<string> CalculateHardwareFitAsync(
        string modelName,
        double? parametersBillions = null,
        string? quantization = null,
        string? modality = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var telemetry = await _telemetryProvider.GetTelemetryAsync();
            long vramMb = telemetry.TotalVramMb > 0 ? telemetry.TotalVramMb : 16384;
            long ramMb = 32768; // Default estimate or 32GB

            var mod = (modality ?? "llm").ToLowerInvariant();
            if (mod.Contains("diffus") || mod.Contains("image"))
            {
                var diffResult = _canIRunItService.EvaluateDiffusionFit(new DiffusionFitRequest(
                    ModelName: modelName,
                    Quantization: quantization ?? "FP8",
                    AvailableVramMb: vramMb,
                    AvailableRamMb: ramMb
                ));
                return JsonSerializer.Serialize(diffResult, JsonOptions);
            }

            if (mod.Contains("video"))
            {
                var vidResult = _canIRunItService.EvaluateVideoFit(new VideoFitRequest(
                    ModelName: modelName,
                    Quantization: quantization ?? "FP8",
                    AvailableVramMb: vramMb,
                    AvailableRamMb: ramMb
                ));
                return JsonSerializer.Serialize(vidResult, JsonOptions);
            }

            // LLM evaluation
            double p = parametersBillions ?? ParseParamsFromName(modelName);
            var llmResult = _canIRunItService.EvaluateLlmFit(new LlmFitRequest(
                ParametersBillions: p,
                Quantization: quantization ?? "Q4_K_M",
                AvailableVramMb: vramMb,
                AvailableRamMb: ramMb
            ));
            return JsonSerializer.Serialize(llmResult, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
        }
    }

    [Description("Generate an image using Stable Diffusion Forge or ComfyUI.")]
    public async Task<string> GenerateImageAsync(
        string prompt,
        string? negativePrompt = null,
        string? engine = null,
        int width = 1024,
        int height = 1024,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return JsonSerializer.Serialize(new { success = false, error = "prompt is required" });

        var settings = _settingsService.LoadSettings();
        var targetEngine = !string.IsNullOrWhiteSpace(engine) ? engine.ToLowerInvariant() : settings.PreferredImageEngine.ToLowerInvariant();

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);

            if (targetEngine.Contains("forge") || targetEngine.Contains("sd"))
            {
                var forgePayload = new
                {
                    prompt,
                    negative_prompt = negativePrompt ?? "",
                    width,
                    height,
                    steps = 20
                };
                var content = new StringContent(JsonSerializer.Serialize(forgePayload), Encoding.UTF8, "application/json");
                var resp = await client.PostAsync("http://127.0.0.1:7860/sdapi/v1/txt2img", content, cancellationToken);
                return JsonSerializer.Serialize(new
                {
                    success = resp.IsSuccessStatusCode,
                    statusCode = (int)resp.StatusCode,
                    engine = "forge",
                    message = resp.IsSuccessStatusCode ? "Image generation triggered on Stable Diffusion Forge." : "Forge returned an error."
                });
            }
            else
            {
                var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl.TrimEnd('/');
                var comfyPayload = new { prompt = new { } };
                var content = new StringContent(JsonSerializer.Serialize(comfyPayload), Encoding.UTF8, "application/json");
                var resp = await client.PostAsync($"{comfyUrl}/prompt", content, cancellationToken);
                return JsonSerializer.Serialize(new
                {
                    success = resp.IsSuccessStatusCode,
                    statusCode = (int)resp.StatusCode,
                    engine = "comfyui",
                    message = resp.IsSuccessStatusCode ? "Workflow queued on ComfyUI." : "ComfyUI returned an error."
                });
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [Description("Synthesize spoken audio from text using local Kokoro TTS.")]
    public async Task<string> SynthesizeSpeechAsync(
        string text,
        string voice = "af_heart",
        string format = "mp3",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return JsonSerializer.Serialize(new { success = false, error = "text is required" });

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var settings = _settingsService.LoadSettings();
            var ttsUrl = string.IsNullOrWhiteSpace(settings.AudioEngineUrl) ? "http://127.0.0.1:8880" : settings.AudioEngineUrl.TrimEnd('/');

            var body = JsonSerializer.Serialize(new { text, voice, format });
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{ttsUrl}/api/tts", content, cancellationToken);

            var mediaId = Guid.NewGuid().ToString("N")[..8];
            return JsonSerializer.Serialize(new
            {
                success = resp.IsSuccessStatusCode,
                statusCode = (int)resp.StatusCode,
                voice,
                text,
                outputUrl = $"/output/speech_{mediaId}.{format}",
                message = resp.IsSuccessStatusCode ? "Speech synthesized successfully." : "TTS engine returned an error."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [Description("Search in-app technical documentation procedures, steps, prerequisites, and warnings.")]
    public Task<string> QueryAppDocumentationAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            var docVm = new DocumentationViewModel();
            var q = (query ?? "").Trim().ToLowerInvariant();

            var matches = docVm.Sections.Where(s =>
                s.Title.ToLowerInvariant().Contains(q) ||
                s.Summary.ToLowerInvariant().Contains(q) ||
                s.Steps.Any(st => st.Title.ToLowerInvariant().Contains(q) || st.Action.ToLowerInvariant().Contains(q))
            ).ToList();

            if (!matches.Any())
            {
                matches = docVm.Sections.Take(3).ToList();
            }

            var results = matches.Select(m => new
            {
                title = m.Title,
                icon = m.Icon,
                summary = m.Summary,
                prerequisite = m.Prerequisite,
                steps = m.Steps.Select(s => $"{s.StepNumber}. {s.Title}: {s.Action} -> {s.ExpectedResult}").ToList(),
                notes = m.Notes,
                warnings = m.Warnings
            });

            return Task.FromResult(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(new { success = false, error = ex.Message }));
        }
    }

    private static double ParseParamsFromName(string modelName)
    {
        var lower = (modelName ?? "").ToLowerInvariant();
        if (lower.Contains("70b")) return 70.0;
        if (lower.Contains("32b")) return 32.0;
        if (lower.Contains("14b")) return 14.0;
        if (lower.Contains("8b")) return 8.0;
        if (lower.Contains("7b")) return 7.0;
        if (lower.Contains("3b")) return 3.0;
        if (lower.Contains("1b") || lower.Contains("1.5b")) return 1.5;
        if (lower.Contains("671b")) return 671.0;
        return 8.0; // Default fallback 8B
    }
}
