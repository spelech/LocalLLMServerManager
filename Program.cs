using System.Text.Json;
using System.Text;
using LocalLLMServerManager;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddSingleton<VramOrchestrator>();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Configure optional Windows Service hosting
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "LocalLLMServerManager";
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

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

// Hugging Face search proxy endpoint
app.MapGet("/api/hf/search", async (string q, HttpClient httpClient) =>
{
    try
    {
        var requestUrl = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(q)}&filter=gguf&sort=downloads&direction=-1&limit=20";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("User-Agent", "LocalLLMServerManager");

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return Results.StatusCode((int)response.StatusCode);
        }
        var content = await response.Content.ReadAsStringAsync();
        return Results.Content(content, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// Hugging Face repository details proxy endpoint
app.MapGet("/api/hf/model", async (string repoId, HttpClient httpClient) =>
{
    try
    {
        var requestUrl = $"https://huggingface.co/api/models/{Uri.EscapeDataString(repoId)}";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("User-Agent", "LocalLLMServerManager");

        var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return Results.StatusCode((int)response.StatusCode);
        }
        var content = await response.Content.ReadAsStringAsync();
        return Results.Content(content, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// GPU VRAM retrieval endpoint (Native WMI)
app.MapGet("/api/gpu/vram", () =>
{
    long vramBytes = 8L * 1024 * 1024 * 1024; // Default to 8GB
    try
    {
        if (OperatingSystem.IsWindows())
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController");
            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                var ramStr = obj["AdapterRAM"]?.ToString();
                if (long.TryParse(ramStr, out long ramBytes) && ramBytes > 0)
                {
                    vramBytes = Math.Max(vramBytes, ramBytes);
                }
            }
        }
    }
    catch
    {
        // Ignore and fallback
    }
    return Results.Ok(new { totalVramBytes = vramBytes });
});

app.MapReverseProxy();

app.Run();
