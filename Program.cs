using System.Text.Json;
using System.Text;
using LocalLLMServerManager;

// Settings helper functions (must precede app.Run() in top-level programs)
static string SettingsFilePath()
{
    return Path.Combine(AppContext.BaseDirectory, "settings.json");
}

static AppSettings LoadSettings()
{
    try
    {
        var path = SettingsFilePath();
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
    }
    catch { }
    return new AppSettings();
}

static void SaveSettings(AppSettings settings)
{
    File.WriteAllText(
        SettingsFilePath(),
        JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
}

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

// CivitAI search proxy (avoids CORS)
app.MapGet("/api/civitai/search", async (HttpClient http, string? q, string? types, string? sort) =>
{
    try
    {
        var queryType = string.IsNullOrWhiteSpace(types) ? "Checkpoint" : types;
        var querySort = string.IsNullOrWhiteSpace(sort) ? "Most Downloaded" : sort;
        var url = $"https://civitai.com/api/v1/models?limit=20&nsfw=false&types={Uri.EscapeDataString(queryType)}&sort={Uri.EscapeDataString(querySort)}";
        if (!string.IsNullOrWhiteSpace(q))
        {
            url += $"&query={Uri.EscapeDataString(q)}";
        }
        var response = await http.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();
        return Results.Content(content, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// CivitAI single model detail proxy
app.MapGet("/api/civitai/model", async (HttpClient http, int id) =>
{
    try
    {
        var response = await http.GetAsync($"https://civitai.com/api/v1/models/{id}");
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

// ---------------------------------------------------------------------------
// Settings endpoints
// ---------------------------------------------------------------------------
app.MapGet("/api/settings", () =>
{
    var settings = LoadSettings();
    return Results.Ok(settings);
});

app.MapPost("/api/settings", async (HttpContext ctx) =>
{
    try
    {
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(ctx.Request.Body);
        if (settings == null) return Results.BadRequest("Invalid body");

        // Validate directory exists if provided
        if (!string.IsNullOrWhiteSpace(settings.ForgeModelsPath) && !Directory.Exists(settings.ForgeModelsPath))
        {
            return Results.BadRequest($"Directory does not exist: {settings.ForgeModelsPath}");
        }

        SaveSettings(settings);
        return Results.Ok(settings);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// ---------------------------------------------------------------------------
// CivitAI direct download — streams to the configured Forge models directory.
// Progress is reported as Server-Sent Events so the UI can show a live bar.
// ---------------------------------------------------------------------------
app.MapPost("/api/civitai/download", async (HttpContext ctx, HttpClient http) =>
{
    string? url = null;
    string? fileName = null;

    try
    {
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
        url = body.GetProperty("url").GetString();
        fileName = body.GetProperty("fileName").GetString();
    }
    catch
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("Invalid JSON body");
        return;
    }

    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(fileName))
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("url and fileName are required");
        return;
    }

    // Sanitise filename — strip any path traversal characters
    fileName = Path.GetFileName(fileName);

    var settings = LoadSettings();
    if (string.IsNullOrWhiteSpace(settings.ForgeModelsPath) || !Directory.Exists(settings.ForgeModelsPath))
    {
        ctx.Response.StatusCode = 400;
        await ctx.Response.WriteAsync("Forge models path is not configured or does not exist. Configure it in the Stable Diffusion tab.");
        return;
    }

    var destPath = Path.Combine(settings.ForgeModelsPath, fileName);

    // Use SSE to stream progress back
    ctx.Response.Headers["Content-Type"] = "text/event-stream";
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no";

    async Task SendEvent(string eventName, string data)
    {
        await ctx.Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n");
        await ctx.Response.Body.FlushAsync();
    }

    try
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            await SendEvent("error", $"Remote server returned {(int)response.StatusCode}");
            return;
        }

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await SendEvent("start", JsonSerializer.Serialize(new { fileName, totalBytes }));

        using var stream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long bytesRead = 0;
        int read;
        var lastReportAt = 0L;

        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read));
            bytesRead += read;

            // Send progress update every ~500 KB or on the last chunk
            if (bytesRead - lastReportAt > 512 * 1024 || (totalBytes > 0 && bytesRead >= totalBytes))
            {
                lastReportAt = bytesRead;
                var pct = totalBytes > 0 ? (int)(bytesRead * 100 / totalBytes) : -1;
                await SendEvent("progress", JsonSerializer.Serialize(new { bytesRead, totalBytes, pct }));
            }
        }

        await SendEvent("done", JsonSerializer.Serialize(new { fileName, destPath }));
    }
    catch (Exception ex)
    {
        await SendEvent("error", ex.Message);
        // Clean up partial file
        if (File.Exists(destPath)) File.Delete(destPath);
    }
});

app.Run();
