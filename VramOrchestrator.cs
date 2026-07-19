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

    public async Task<bool> IsOllamaHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("http://127.0.0.1:11434/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsForgeHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("http://127.0.0.1:7860/sdapi/v1/progress");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task EnsureVramForImageGenerationAsync()
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
                _logger.LogInformation("Active LLM found in VRAM. Issuing unload command to free up VRAM for Image Generation.");
                
                foreach (var model in models)
                {
                    var modelName = model["name"]?.ToString();
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
            _logger.LogError(ex, "Failed to orchestrate VRAM for image generation.");
        }
    }
}
