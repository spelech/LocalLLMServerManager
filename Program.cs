using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia;
using LocalLLMServerManager;

namespace LocalLLMServerManager;

public class Program
{
    private static System.Diagnostics.Process? comfyProcess = null;
    private static System.Diagnostics.Process? forgeProcess = null;
    private static JobObject aiEnginesJob = new JobObject();

    public static async Task Main(string[] args)
    {
        if (args.Contains("--service"))
        {
            // Windows Service Mode (Session 0)
            var app = CreateWebApplication(args, isServiceMode: true);
            await app.RunAsync();
        }
        else
        {
            // Session 1 Desktop & Tray Mode
            WebApplication? webApp = null;
            try
            {
                // Check if server is already running (e.g., via Windows Service)
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(2);
                var resp = await http.GetAsync("http://127.0.0.1:5246/health");
                if (!resp.IsSuccessStatusCode)
                {
                    webApp = CreateWebApplication(args, isServiceMode: false);
                    webApp.Urls.Add("http://0.0.0.0:5246");
                    await webApp.StartAsync();
                }
            }
            catch
            {
                try
                {
                    webApp = CreateWebApplication(args, isServiceMode: false);
                    webApp.Urls.Add("http://0.0.0.0:5246");
                    await webApp.StartAsync();
                }
                catch { }
            }

            // Start Avalonia UI Desktop Lifetime
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            if (webApp != null)
            {
                try { await webApp.StopAsync(); } catch { }
            }
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    public static string SettingsFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "settings.json");
    }

    public static AppSettings LoadSettings()
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

    public static void SaveSettings(AppSettings settings)
    {
        File.WriteAllText(
            SettingsFilePath(),
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static WebApplication CreateWebApplication(string[] args, bool isServiceMode)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<VramOrchestrator>();

        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        if (isServiceMode)
        {
            builder.Host.UseWindowsService(options =>
            {
                options.ServiceName = "LocalLLMServerManager";
            });
        }

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
            var isComfyRequest = path.StartsWith("/comfyapi", StringComparison.OrdinalIgnoreCase) ||
                                 path.StartsWith("/api/comfy/prompt", StringComparison.OrdinalIgnoreCase);

            var orchestrator = context.RequestServices.GetRequiredService<VramOrchestrator>();

            if (isForgeRequest)
            {
                await orchestrator.EnsureVramForImageGenerationAsync();

                if (!await orchestrator.IsForgeHealthyAsync())
                {
                    var http = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
                    await http.PostAsync("http://127.0.0.1:5246/api/forge/start", null);
                    for (int i = 0; i < 30; i++)
                    {
                        if (await orchestrator.IsForgeHealthyAsync()) break;
                        await Task.Delay(2000);
                    }
                }
            }
            else if (isComfyRequest)
            {
                await orchestrator.EnsureVramForComfyUiAsync();

                var settings = LoadSettings();
                var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl;
                if (!await orchestrator.IsComfyUiHealthyAsync(comfyUrl))
                {
                    var http = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient();
                    await http.PostAsync("http://127.0.0.1:5246/api/comfy/start", null);
                    for (int i = 0; i < 30; i++)
                    {
                        if (await orchestrator.IsComfyUiHealthyAsync(comfyUrl)) break;
                        await Task.Delay(2000);
                    }
                }
            }

            await next();
        });

        // Health check endpoint
        app.MapGet("/health", async (VramOrchestrator orchestrator) =>
        {
            var settings = LoadSettings();
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
                Version = "2.0.0"
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

                                        if (driverDesc.Contains("Basic Render") ||
                                            (provider.Contains("Microsoft") && driverDesc.Contains("Indirect")) ||
                                            driverDesc.Contains("Virtual Desktop"))
                                        {
                                            continue;
                                        }

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
                                                    break;
                                                }
                                            }
                                            catch { }
                                        }

                                        var dwMemSize = subKey.GetValue("HardwareInformation.MemorySize");
                                        if (dwMemSize != null)
                                        {
                                            try
                                            {
                                                byte[]? rawBytes = dwMemSize as byte[];
                                                if (rawBytes != null && rawBytes.Length >= 4)
                                                {
                                                    uint size32 = BitConverter.ToUInt32(rawBytes, 0);
                                                    if (size32 > 0)
                                                    {
                                                        vramBytes = (long)size32;
                                                        gpuName = driverDesc;
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    long size = Convert.ToInt64(dwMemSize);
                                                    if (size > 0)
                                                    {
                                                        vramBytes = size;
                                                        gpuName = driverDesc;
                                                        break;
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }

            return Results.Ok(new
            {
                GpuName = gpuName,
                VramBytes = vramBytes,
                VramGB = Math.Round((double)vramBytes / (1024 * 1024 * 1024), 2)
            });
        });

        // Settings API
        app.MapGet("/api/settings", () => Results.Ok(LoadSettings()));
        app.MapPost("/api/settings", (AppSettings newSettings) =>
        {
            SaveSettings(newSettings);
            return Results.Ok(newSettings);
        });

        // CivitAI direct-to-disk SSE download streaming endpoint
        app.MapGet("/api/civitai/download", async (HttpContext ctx, HttpClient http, string fileUrl, string modelType, string fileName) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("Connection", "keep-alive");

            async Task SendEvent(string eventName, string data)
            {
                await ctx.Response.WriteAsync($"event: {eventName}\ndata: {data}\n\n");
                await ctx.Response.Body.FlushAsync();
            }

            var settings = LoadSettings();
            var targetDir = modelType.ToLowerInvariant() switch
            {
                "lora" => Path.Combine(settings.ForgeModelsPath, "Lora"),
                "controlnet" => Path.Combine(settings.ForgeModelsPath, "ControlNet"),
                "vae" => Path.Combine(settings.ForgeModelsPath, "VAE"),
                "embedding" => Path.Combine(settings.ForgeModelsPath, "embeddings"),
                _ => Path.Combine(settings.ForgeModelsPath, "Stable-diffusion")
            };

            if (string.IsNullOrWhiteSpace(settings.ForgeModelsPath) || !Directory.Exists(settings.ForgeModelsPath))
            {
                await SendEvent("error", "Forge Models Directory is not configured or does not exist. Please set it in Settings.");
                return;
            }

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var destPath = Path.Combine(targetDir, fileName);

            try
            {
                using var response = await http.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    await SendEvent("error", $"Download failed with HTTP {response.StatusCode}");
                    return;
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
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
                if (File.Exists(destPath)) File.Delete(destPath);
            }
        });

        // ComfyUI workflow presets
        app.MapGet("/api/comfy/workflows", () =>
        {
            var workflowsDir = Path.Combine(AppContext.BaseDirectory, "Workflows");
            var result = new List<object>();

            if (Directory.Exists(workflowsDir))
            {
                var jsonFiles = Directory.GetFiles(workflowsDir, "*.json");
                foreach (var file in jsonFiles)
                {
                    try
                    {
                        var content = File.ReadAllText(file);
                        using var doc = JsonDocument.Parse(content);
                        var root = doc.RootElement;

                        var name = root.TryGetProperty("name", out var n) ? n.GetString() : Path.GetFileNameWithoutExtension(file);
                        var type = root.TryGetProperty("type", out var t) ? t.GetString() : "general";
                        var description = root.TryGetProperty("description", out var d) ? d.GetString() : "";

                        result.Add(new
                        {
                            id = Path.GetFileNameWithoutExtension(file),
                            name,
                            type,
                            description,
                            filePath = file
                        });
                    }
                    catch { }
                }
            }

            return Results.Ok(result);
        });

        app.MapGet("/api/comfy/workflows/{id}", (string id) =>
        {
            var workflowsDir = Path.Combine(AppContext.BaseDirectory, "Workflows");
            var filePath = Path.Combine(workflowsDir, $"{id}.json");

            if (!File.Exists(filePath))
            {
                return Results.NotFound($"Workflow preset '{id}' not found.");
            }

            var content = File.ReadAllText(filePath);
            return Results.Content(content, "application/json");
        });

        app.MapPost("/api/comfy/prompt", async (HttpContext ctx, HttpClient http, VramOrchestrator orchestrator) =>
        {
            try
            {
                var settings = LoadSettings();
                var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl.TrimEnd('/');

                await orchestrator.EnsureVramForComfyUiAsync();

                using var reader = new StreamReader(ctx.Request.Body);
                var bodyText = await reader.ReadToEndAsync();

                var content = new StringContent(bodyText, Encoding.UTF8, System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json"));
                var response = await http.PostAsync($"{comfyUrl}/prompt", content);

                var responseContent = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return Results.StatusCode((int)response.StatusCode);
                }

                return Results.Content(responseContent, "application/json");
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapPost("/api/comfy/free", async (VramOrchestrator orchestrator) =>
        {
            var settings = LoadSettings();
            var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl;
            await orchestrator.FreeComfyUiVramAsync(comfyUrl);
            return Results.Ok(new { message = "ComfyUI VRAM free request sent." });
        });

        app.MapGet("/api/3d/files", () =>
        {
            var settings = LoadSettings();
            var outputDir = !string.IsNullOrWhiteSpace(settings.ThreeDModelsPath) && Directory.Exists(settings.ThreeDModelsPath)
                ? settings.ThreeDModelsPath
                : Path.Combine(AppContext.BaseDirectory, "wwwroot", "3d_outputs");

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var extensions = new[] { "*.glb", "*.gltf", "*.obj", "*.stl" };
            var files = extensions.SelectMany(ext => Directory.GetFiles(outputDir, ext))
                                  .Select(f => new FileInfo(f))
                                  .OrderByDescending(f => f.LastWriteTime)
                                  .Select(f => new
                                  {
                                      name = f.Name,
                                      sizeBytes = f.Length,
                                      created = f.LastWriteTime,
                                      relativePath = $"/3d_outputs/{f.Name}"
                                  });

            return Results.Ok(files);
        });

        app.MapPost("/api/comfy/start", async (VramOrchestrator orchestrator) =>
        {
            var settings = LoadSettings();
            var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl;

            if (comfyProcess != null)
            {
                if (!comfyProcess.HasExited && await orchestrator.IsComfyUiHealthyAsync(comfyUrl))
                {
                    return Results.Ok(new { message = "ComfyUI is already running" });
                }
                try { comfyProcess.Kill(true); } catch { }
                comfyProcess = null;
            }

            var path = string.IsNullOrWhiteSpace(settings.ComfyUiExecutablePath) ? @"D:\AI\ComfyUI\run_nvidia_gpu.bat" : settings.ComfyUiExecutablePath;
            if (!System.IO.File.Exists(path)) return Results.NotFound(new { message = $"ComfyUI executable not found at: {path}" });

            comfyProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{path}\"",
                    WorkingDirectory = System.IO.Path.GetDirectoryName(path) ?? "",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                }
            };
            comfyProcess.Start();
            aiEnginesJob.AddProcess(comfyProcess);
            return Results.Ok(new { message = "ComfyUI Started" });
        });

        app.MapPost("/api/comfy/stop", () =>
        {
            if (comfyProcess != null && !comfyProcess.HasExited)
            {
                comfyProcess.Kill(true);
                comfyProcess = null;
                return Results.Ok(new { message = "ComfyUI Stopped" });
            }
            return Results.Ok(new { message = "ComfyUI is not running" });
        });

        app.MapPost("/api/forge/start", () =>
        {
            if (forgeProcess != null && !forgeProcess.HasExited) return Results.Ok(new { message = "SD Forge is already running" });

            var settings = LoadSettings();
            var path = string.IsNullOrWhiteSpace(settings.ForgeExecutablePath) ? @"D:\AI\SD_Forge\webui-user.bat" : settings.ForgeExecutablePath;
            if (!System.IO.File.Exists(path)) return Results.NotFound(new { message = $"SD Forge executable not found at: {path}" });

            forgeProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{path}\"",
                    WorkingDirectory = System.IO.Path.GetDirectoryName(path) ?? "",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                }
            };
            forgeProcess.Start();
            aiEnginesJob.AddProcess(forgeProcess);
            return Results.Ok(new { message = "SD Forge Started" });
        });

        app.MapPost("/api/forge/stop", () =>
        {
            if (forgeProcess != null && !forgeProcess.HasExited)
            {
                forgeProcess.Kill(true);
                forgeProcess = null;
                return Results.Ok(new { message = "SD Forge Stopped" });
            }
            return Results.Ok(new { message = "SD Forge is not running" });
        });

        app.MapPost("/api/service/update", () =>
        {
            var repoDir = Directory.GetCurrentDirectory();
            var script = "$ServiceName = 'LocalLLMServerManager'; Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1; dotnet publish -c Release -o C:\\LocalLLMServerManager --nologo; Start-Service -Name $ServiceName -ErrorAction SilentlyContinue;";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                WorkingDirectory = repoDir,
                UseShellExecute = true,
                CreateNoWindow = true
            };

            Process.Start(psi);
            return Results.Ok(new { message = "Service update spawned in detached process. Rebuilding and restarting service..." });
        });

        return app;
    }
}
