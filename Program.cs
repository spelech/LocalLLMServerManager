using System.Text.Json;
using System.Text;
using LocalLLMServerManager;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<VramOrchestrator>();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseHttpsRedirection();

// Basic VRAM Orchestration Middleware
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var isForgeRequest = path.StartsWith("/sdapi", StringComparison.OrdinalIgnoreCase) || 
                         path.StartsWith("/v1/images", StringComparison.OrdinalIgnoreCase);

    if (isForgeRequest)
    {
        var orchestrator = context.RequestServices.GetRequiredService<VramOrchestrator>();
        await orchestrator.EnsureVramForImageGenerationAsync();
    }

    await next();
});

// Health check endpoint
app.MapGet("/health", async (VramOrchestrator orchestrator) =>
{
    var ollamaHealthy = await orchestrator.IsOllamaHealthyAsync();
    var forgeHealthy = await orchestrator.IsForgeHealthyAsync();

    return Results.Ok(new
    {
        Status = ollamaHealthy && forgeHealthy ? "Healthy" : "Degraded",
        Ollama = ollamaHealthy ? "Online" : "Offline",
        StableDiffusion = forgeHealthy ? "Online" : "Offline"
    });
});

app.MapReverseProxy();

app.Run();
