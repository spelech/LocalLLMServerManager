using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Services;

[McpServerToolType]
public sealed class LocalLlmMcpTools
{
    private readonly IGpuTelemetryProvider _telemetryProvider;
    private readonly IAiEngineManager _engineManager;
    private readonly IOllamaModelService _ollamaModelService;
    private readonly IToolDiscoveryService _toolDiscoveryService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHuggingFaceSearchService _hfSearchService;
    private readonly ICivitaiSearchService _civitaiSearchService;
    private readonly ISettingsService _settingsService;

    public LocalLlmMcpTools(
        IGpuTelemetryProvider telemetryProvider,
        IAiEngineManager engineManager,
        IOllamaModelService ollamaModelService,
        IToolDiscoveryService toolDiscoveryService,
        IHttpClientFactory httpClientFactory,
        IHuggingFaceSearchService? hfSearchService = null,
        ICivitaiSearchService? civitaiSearchService = null,
        ISettingsService? settingsService = null)
    {
        _telemetryProvider = telemetryProvider;
        _engineManager = engineManager;
        _ollamaModelService = ollamaModelService;
        _toolDiscoveryService = toolDiscoveryService;
        _httpClientFactory = httpClientFactory;
        _hfSearchService = hfSearchService ?? new HuggingFaceSearchService();
        _civitaiSearchService = civitaiSearchService ?? new CivitaiSearchService();
        _settingsService = settingsService ?? new SettingsService();
    }

    [McpServerTool, Description("Get real-time GPU VRAM allocation, total memory, used memory, and GPU hardware name via NVML CUDA.")]
    public async Task<string> GetGpuVramAsync()
    {
        var telemetry = await _telemetryProvider.GetTelemetryAsync();
        return JsonSerializer.Serialize(telemetry, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Check real-time health and connectivity of Ollama, Stable Diffusion Forge, and ComfyUI backend ports.")]
    public async Task<string> CheckHealthAsync()
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(2);

        async Task<object> CheckPort(string url)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var resp = await client.GetAsync(url);
                sw.Stop();
                return new { online = resp.IsSuccessStatusCode, status = (int)resp.StatusCode, latencyMs = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                return new { online = false, error = ex.Message };
            }
        }

        var results = new
        {
            ollama = await CheckPort("http://127.0.0.1:11434/"),
            sdForge = await CheckPort("http://127.0.0.1:7860/"),
            comfyUi = await CheckPort("http://127.0.0.1:8188/system_stats")
        };

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("List all installed Ollama LLM models, quantization formats, and memory/disk footprint.")]
    public async Task<string> ListModelsAsync()
    {
        var models = await _ollamaModelService.GetInstalledModelsAsync();
        return JsonSerializer.Serialize(models, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Trigger a model pull from the Ollama library or Hugging Face repository.")]
    public async Task<string> PullModelAsync([Description("Model identifier, e.g. 'llama3.2:latest' or 'qwen2.5-coder:7b'")] string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return JsonSerializer.Serialize(new { success = false, error = "modelName is required" });

        var started = await _ollamaModelService.PullModelAsync(modelName);
        return JsonSerializer.Serialize(new { success = started, modelName, message = started ? "Model pull initiated" : "Failed to initiate pull" });
    }

    [McpServerTool, Description("Unload all LLM models currently residing in GPU VRAM to free memory for diffusion or 3D workflows.")]
    public async Task<string> UnloadVramAsync()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var payload = new StringContent("{\"model\":\"\",\"keep_alive\":0}", System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://127.0.0.1:11434/api/generate", payload);
            return JsonSerializer.Serialize(new { success = response.IsSuccessStatusCode, status = (int)response.StatusCode, message = "VRAM unload requested" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Start an AI backend engine process ('forge', 'comfyui', or 'ollama').")]
    public async Task<string> StartEngineAsync([Description("Target engine: 'forge', 'comfyui', or 'ollama'")] string engine)
    {
        var result = await _engineManager.StartEngineAsync(engine);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Gracefully terminate an AI backend engine process ('forge' or 'comfyui').")]
    public async Task<string> StopEngineAsync([Description("Target engine: 'forge' or 'comfyui'")] string engine)
    {
        var result = await _engineManager.StopEngineAsync(engine);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Scan system drives and PATH for installed Ollama, ComfyUI, and SD Forge directories.")]
    public async Task<string> DetectToolsAsync()
    {
        var discovered = await _toolDiscoveryService.DetectAllToolsAsync();
        return JsonSerializer.Serialize(discovered, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Generate video from text prompt or image using ComfyUI DiT pipelines (Wan 2.2, LTX-2.5).")]
    public async Task<string> GenerateVideoAsync(
        [Description("Text prompt describing video content or animation.")] string prompt,
        [Description("ComfyUI video workflow pipeline (e.g. 'wan2.2_t2v', 'ltx2.5_t2v', 'hunyuanvideo1.5_t2v').")] string workflow = "wan2.2_t2v",
        [Description("Video frame width in pixels.")] int width = 832,
        [Description("Video frame height in pixels.")] int height = 480,
        [Description("Total number of video frames to render.")] int frames = 49)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return JsonSerializer.Serialize(new { success = false, error = "prompt is required" });

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var body = JsonSerializer.Serialize(new { prompt, workflow, width, height, frames });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://127.0.0.1:8188/prompt", content);

            var mediaId = Guid.NewGuid().ToString("N")[..8];
            var result = new
            {
                success = response.IsSuccessStatusCode,
                prompt,
                workflow,
                width,
                height,
                frames,
                mediaUrl = $"/output/video_{mediaId}.mp4",
                status = response.IsSuccessStatusCode ? "queued" : "error",
                statusCode = (int)response.StatusCode
            };
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Synthesize speech audio from text using local Kokoro / AllTalk TTS engine.")]
    public async Task<string> SynthesizeSpeechAsync(
        [Description("Text script to synthesize into speech audio.")] string text,
        [Description("Voice speaker profile (e.g. 'af_heart').")] string voice = "af_heart",
        [Description("Output audio file format, e.g. 'mp3' or 'wav'.")] string format = "mp3")
    {
        if (string.IsNullOrWhiteSpace(text))
            return JsonSerializer.Serialize(new { success = false, error = "text is required" });

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var body = JsonSerializer.Serialize(new { text, voice, format });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://127.0.0.1:7851/api/tts", content);

            var mediaId = Guid.NewGuid().ToString("N")[..8];
            var ext = string.IsNullOrWhiteSpace(format) ? "mp3" : format.TrimStart('.');
            var result = new
            {
                success = response.IsSuccessStatusCode,
                text,
                voice,
                format,
                mediaUrl = $"/output/speech_{mediaId}.{ext}",
                status = response.IsSuccessStatusCode ? "completed" : "error",
                statusCode = (int)response.StatusCode
            };
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Generate sound effects or ambient musical loops from a prompt.")]
    public async Task<string> GenerateAudioAsync(
        [Description("Text prompt describing sound effect, ambient loop, or music.")] string prompt,
        [Description("Target audio duration in seconds.")] int durationSeconds = 15)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return JsonSerializer.Serialize(new { success = false, error = "prompt is required" });

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var body = JsonSerializer.Serialize(new { prompt, durationSeconds });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://127.0.0.1:7860/api/audio/generate", content);

            var mediaId = Guid.NewGuid().ToString("N")[..8];
            var result = new
            {
                success = response.IsSuccessStatusCode,
                prompt,
                durationSeconds,
                mediaUrl = $"/output/audio_{mediaId}.wav",
                status = response.IsSuccessStatusCode ? "queued" : "error",
                statusCode = (int)response.StatusCode
            };
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Search Hugging Face Hub repositories for models and download quantizations (GGUF, Safetensors).")]
    public async Task<string> SearchHuggingFaceAsync(
        [Description("Search query or model name, e.g. 'qwen2.5-coder', 'deepseek', or 'flux'")] string query,
        [Description("Optional pipeline filter, e.g. 'text-generation', 'text-to-image', 'text-to-audio', 'text-to-video', or 'image-to-3d'")] string? pipelineTag = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return JsonSerializer.Serialize(new { success = false, error = "query is required" });

        try
        {
            var results = await _hfSearchService.SearchModelsAsync(query, pipelineTag);
            return JsonSerializer.Serialize(new { success = true, count = results.Count, models = results }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Search Civitai Hub for Stable Diffusion checkpoints, FLUX models, and LoRAs with direct download links.")]
    public async Task<string> SearchCivitaiAsync(
        [Description("Search query, e.g. 'juggernaut', 'realistic', 'flux', or 'anime'")] string query,
        [Description("Model type filter: 'Checkpoint', 'LORA', or 'All'")] string type = "Checkpoint",
        [Description("Sort order: 'Highest Rated', 'Most Downloaded', 'Newest'")] string sort = "Highest Rated")
    {
        if (string.IsNullOrWhiteSpace(query))
            return JsonSerializer.Serialize(new { success = false, error = "query is required" });

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var apiBase = "http://127.0.0.1:5246";
            var typeParam = string.IsNullOrWhiteSpace(type) || type.Equals("All", StringComparison.OrdinalIgnoreCase) ? "" : type;
            var results = await _civitaiSearchService.SearchModelsAsync(apiBase, query, typeParam, sort, client);
            return JsonSerializer.Serialize(new { success = true, count = results.Count, models = results }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Execute an automated studio generation workflow across modalities (text, image, audio, video, 3d).")]
    public async Task<string> RunStudioWorkflowAsync(
        [Description("Target workflow modality or preset ID ('text', 'image', 'audio', 'video', '3d', or specific workflow ID like 'wan2.2_t2v')")] string workflow,
        [Description("Text prompt describing content to generate")] string prompt,
        [Description("Optional negative prompt")] string? negativePrompt = null,
        [Description("Inference steps (default: 25)")] int steps = 25,
        [Description("Guidance / CFG scale (default: 7.0)")] double cfg = 7.0,
        [Description("Denoising strength for img2img / refiners (default: 0.7)")] double denoise = 0.7,
        [Description("Random seed (-1 for random)")] long seed = -1,
        [Description("Output width in pixels")] int width = 1024,
        [Description("Output height in pixels")] int height = 1024)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return JsonSerializer.Serialize(new { success = false, error = "prompt is required" });

        var w = (workflow ?? "image").Trim().ToLowerInvariant();
        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(180);

            if (w.Contains("text") || w.Contains("llm") || w.Contains("ollama"))
            {
                var payload = new { prompt, model = "llama3.2:latest", stream = false };
                var resp = await client.PostAsJsonAsync("http://127.0.0.1:11434/api/generate", payload);
                if (resp.IsSuccessStatusCode)
                {
                    var textResp = await resp.Content.ReadAsStringAsync();
                    return JsonSerializer.Serialize(new { success = true, modality = "text", response = textResp });
                }
                return JsonSerializer.Serialize(new { success = false, error = $"Ollama returned HTTP {(int)resp.StatusCode}" });
            }
            else if (w.Contains("video") || w.Contains("wan") || w.Contains("ltx"))
            {
                var payload = new
                {
                    WorkflowId = w.Contains("wan") || w == "video" ? "wan2.2_t2v" : w,
                    Prompt = prompt,
                    NegativePrompt = negativePrompt ?? "",
                    Width = width,
                    Height = height,
                    Frames = 49,
                    Seed = seed
                };
                var resp = await client.PostAsJsonAsync("http://127.0.0.1:5246/api/video/generate", payload);
                var content = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Serialize(new { success = resp.IsSuccessStatusCode, modality = "video", data = JsonNode.Parse(content) });
            }
            else if (w.Contains("audio") || w.Contains("sound") || w.Contains("music"))
            {
                var payload = new
                {
                    WorkflowId = "stable_audio_open_sfx",
                    Prompt = prompt,
                    NegativePrompt = negativePrompt,
                    DurationSeconds = Math.Max(5, (int)(steps * 0.6)),
                    Seed = seed
                };
                var resp = await client.PostAsJsonAsync("http://127.0.0.1:5246/api/audio/generate", payload);
                var content = await resp.Content.ReadAsStringAsync();
                return JsonSerializer.Serialize(new { success = resp.IsSuccessStatusCode, modality = "audio", data = JsonNode.Parse(content) });
            }
            else
            {
                // Default to Image via SD Forge / Comfy
                var payload = new
                {
                    prompt,
                    negative_prompt = negativePrompt ?? "",
                    steps = steps > 0 ? steps : 25,
                    cfg_scale = cfg > 0 ? cfg : 7.0,
                    seed = seed,
                    width,
                    height
                };
                var resp = await client.PostAsJsonAsync("http://127.0.0.1:7860/sdapi/v1/txt2img", payload);
                if (resp.IsSuccessStatusCode)
                {
                    var respStr = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(respStr);
                    var hasImages = doc.RootElement.TryGetProperty("images", out var imgElem) && imgElem.GetArrayLength() > 0;
                    return JsonSerializer.Serialize(new { success = true, modality = "image", imagesCount = hasImages ? imgElem.GetArrayLength() : 0 });
                }
                return JsonSerializer.Serialize(new { success = false, error = $"SD Forge returned HTTP {(int)resp.StatusCode}" });
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
