using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (VramOrchestrator orchestrator, HttpContext context) =>
        {
            var settings = Program.LoadSettings();
            var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl;

            var ollamaTask = orchestrator.IsOllamaHealthyAsync(context.RequestAborted);
            var forgeTask = orchestrator.IsForgeHealthyAsync(context.RequestAborted);
            var comfyTask = orchestrator.IsComfyUiHealthyAsync(comfyUrl, context.RequestAborted);

            await Task.WhenAll(ollamaTask, forgeTask, comfyTask);

            var ollamaHealthy = await ollamaTask;
            var forgeHealthy = await forgeTask;
            var comfyHealthy = await comfyTask;

            return Results.Ok(new
            {
                Status = (ollamaHealthy || forgeHealthy || comfyHealthy) ? "Healthy" : "Degraded",
                Ollama = ollamaHealthy ? "Online" : "Offline",
                StableDiffusion = forgeHealthy ? "Online" : "Offline",
                ComfyUI = comfyHealthy ? "Online" : "Offline",
                PreferredImageEngine = settings.PreferredImageEngine,
                Version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "3.10.0"
            });
        });
    }
}
