using System;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
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

    public LocalLlmMcpTools(
        IGpuTelemetryProvider telemetryProvider,
        IAiEngineManager engineManager,
        IOllamaModelService ollamaModelService,
        IToolDiscoveryService toolDiscoveryService,
        IHttpClientFactory httpClientFactory)
    {
        _telemetryProvider = telemetryProvider;
        _engineManager = engineManager;
        _ollamaModelService = ollamaModelService;
        _toolDiscoveryService = toolDiscoveryService;
        _httpClientFactory = httpClientFactory;
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
}
