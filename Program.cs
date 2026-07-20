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

// GPU details retrieval endpoint (Native Registry)
app.MapGet("/api/gpu/vram", () =>
{
    string gpuName = "Generic GPU";
    long vramBytes = 8L * 1024 * 1024 * 1024; // Default to 8GB
    
    try
    {
        if (OperatingSystem.IsWindows())
        {
            const string regPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            using var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath);
            if (baseKey != null)
            {
                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    if (subKeyName.Length == 4 && int.TryParse(subKeyName, out _))
                    {
                        using var subKey = baseKey.OpenSubKey(subKeyName);
                        if (subKey != null)
                        {
                            var provider = subKey.GetValue("ProviderName")?.ToString() ?? "";
                            var driverDesc = subKey.GetValue("DriverDesc")?.ToString() ?? "";
                            
                            // Skip basic render driver or virtual devices
                            if (driverDesc.Contains("Basic Render") || provider.Contains("Microsoft") && driverDesc.Contains("Indirect Display"))
                            {
                                continue;
                            }

                            var qwMemSize = subKey.GetValue("HardwareInformation.qwMemorySize");
                            if (qwMemSize != null)
                            {
                                long size = Convert.ToInt64(qwMemSize);
                                if (size > 0)
                                {
                                    vramBytes = size;
                                    gpuName = driverDesc;
                                    break;
                                }
                            }
                            
                            var memorySize = subKey.GetValue("HardwareInformation.MemorySize");
                            if (memorySize != null)
                            {
                                long size = Convert.ToInt64(memorySize);
                                if (size > 0)
                                {
                                    vramBytes = size;
                                    gpuName = driverDesc;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    catch
    {
        // Ignore and fallback
    }
    return Results.Ok(new { totalVramBytes = vramBytes, gpuName });
});

app.MapReverseProxy();

app.Run();
