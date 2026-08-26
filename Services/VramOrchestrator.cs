using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;

namespace LocalLLMServerManager;

public class VramOrchestrator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VramOrchestrator> _logger;

    public VramOrchestrator(HttpClient httpClient, ILogger<VramOrchestrator> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> IsOllamaHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(1.5));
            var response = await _httpClient.GetAsync("http://127.0.0.1:11434/", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsForgeHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(1.5));
            var response = await _httpClient.GetAsync("http://127.0.0.1:7860/sdapi/v1/progress", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsComfyUiHealthyAsync(string comfyUrl = "http://127.0.0.1:8188", CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(1.5));
            var baseUrl = comfyUrl.TrimEnd('/');
            var response = await _httpClient.GetAsync($"{baseUrl}/system_stats", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task EnsureVramForImageGenerationAsync()
    {
        await UnloadOllamaModelsAsync("Image Generation");
    }

    public async Task EnsureVramForComfyUiAsync()
    {
        await UnloadOllamaModelsAsync("ComfyUI Workflows (Video / 3D / Image)");
    }

    public async Task FreeComfyUiVramAsync(string comfyUrl = "http://127.0.0.1:8188")
    {
        try
        {
            var baseUrl = comfyUrl.TrimEnd('/');
            var payload = new { free_memory = true, unload_models = true };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"{baseUrl}/free", content);
            _logger.LogInformation("Triggered ComfyUI VRAM unload / free memory.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call ComfyUI free memory API.");
        }
    }

    private async Task UnloadOllamaModelsAsync(string reason)
    {
        try
        {
            var psResponse = await _httpClient.GetAsync("http://127.0.0.1:11434/api/ps");
            if (!psResponse.IsSuccessStatusCode) return;

            var psContent = await psResponse.Content.ReadAsStringAsync();
            var json = JsonNode.Parse(psContent);
            var models = json?["models"]?.AsArray();

            if (models != null && models.Count > 0)
            {
                _logger.LogInformation($"Active LLM found in VRAM. Issuing unload command to free up VRAM for {reason}.");

                foreach (var model in models)
                {
                    var modelName = model?["name"]?.ToString();
                    if (!string.IsNullOrEmpty(modelName))
                    {
                        var payload = new { model = modelName, keep_alive = 0 };
                        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                        await _httpClient.PostAsync("http://127.0.0.1:11434/api/generate", content);
                        _logger.LogInformation($"Unloaded model: {modelName}");
                    }
                }

                await Task.Delay(1500); // Give the system time to clear VRAM
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to orchestrate VRAM for {reason}.");
        }
    }
}
