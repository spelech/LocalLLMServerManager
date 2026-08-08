using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (VramOrchestrator orchestrator) =>
        {
            var settings = Program.LoadSettings();
            var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl;

            var ollamaHealthy = await orchestrator.IsOllamaHealthyAsync();
            var forgeHealthy = await orchestrator.IsForgeHealthyAsync();
            var comfyHealthy = await orchestrator.IsComfyUiHealthyAsync(comfyUrl);

            return Results.Ok(new
            {
                Status = (ollamaHealthy || forgeHealthy || comfyHealthy) ? "Healthy" : "Degraded",
                Ollama = ollamaHealthy ? "Online" : "Offline",
                StableDiffusion = forgeHealthy ? "Online" : "Offline",
                ComfyUI = comfyHealthy ? "Online" : "Offline",
                PreferredImageEngine = settings.PreferredImageEngine,
                Version = "3.3.0"
            });
        });
    }
}
