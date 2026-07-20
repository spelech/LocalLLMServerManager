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
                        try
                        {
                            using var subKey = baseKey.OpenSubKey(subKeyName);
                            if (subKey != null)
                            {
                                var provider = subKey.GetValue("ProviderName")?.ToString() ?? "";
                                var driverDesc = subKey.GetValue("DriverDesc")?.ToString() ?? "";
                                
                                // Skip basic render driver or remote/virtual displays
                                if (driverDesc.Contains("Basic Render") || 
                                    (provider.Contains("Microsoft") && driverDesc.Contains("Indirect")) ||
                                    driverDesc.Contains("Virtual Desktop"))
                                {
                                    continue;
                                }

                                // 1. Attempt to read 64-bit QWORD memory size
                                var qwMemSize = subKey.GetValue("HardwareInformation.qwMemorySize");
                                if (qwMemSize != null)
                                {
                                    try
                                    {
                                        long size = Convert.ToInt64(qwMemSize);
                                        if (size > 0)
                                        {
                                            vramBytes = size;
                                            gpuName = driverDesc;
                                            // Break early if we found a physical dedicated GPU (NVIDIA/Radeon)
                                            if (gpuName.Contains("NVIDIA") || gpuName.Contains("Radeon") || gpuName.Contains("GeForce") || gpuName.Contains("AMD"))
                                            {
                                                break;
                                            }
                                        }
                                    }
                                    catch {}
                                }
                                
                                // 2. Fallback to DWORD memory size (ensure it is not a byte array)
                                var memorySize = subKey.GetValue("HardwareInformation.MemorySize");
                                if (memorySize != null && memorySize is not byte[])
                                {
                                    try
                                    {
                                        long size = Convert.ToInt64(memorySize);
                                        if (size > 0)
                                        {
                                            vramBytes = size;
                                            gpuName = driverDesc;
                                            if (gpuName.Contains("NVIDIA") || gpuName.Contains("Radeon") || gpuName.Contains("GeForce") || gpuName.Contains("AMD"))
                                            {
                                                break;
                                            }
                                        }
                                    }
                                    catch {}
                                }
                            }
                        }
                        catch
                        {
                            // Ignore single key error and continue to scan other keys
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
