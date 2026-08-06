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

    [STAThread]
    public static void Main(string[] args)
    {
        MainInternal(args, runWeb: true);
    }

    public static void MainInternal(string[] args, bool runWeb = false)
    {
        if (args.Contains("--service"))
        {
            var app = CreateWebApplication(args, isServiceMode: true);
            if (runWeb) app.Run();
        }
        else
        {
            WebApplication? webApp = null;

            if (!IsPortInUse(5246))
            {
                try
                {
                    webApp = CreateWebApplication(args, isServiceMode: false);
                    if (runWeb) webApp.Start();
                }
                catch { }
            }

            if (runWeb) BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            if (webApp != null && runWeb)
            {
                try { webApp.StopAsync().GetAwaiter().GetResult(); } catch { }
            }
        }
    }

    public static bool IsPortInUse(int port)
    {
        try
        {
            var ipProperties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            var activeListeners = ipProperties.GetActiveTcpListeners();
            return activeListeners.Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return false;
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

    public static string ResolvePath(string? rawPath, string fallbackRelativePath)
    {
        var target = string.IsNullOrWhiteSpace(rawPath) ? fallbackRelativePath : rawPath;
        if (!OperatingSystem.IsWindows() && target.Contains("%APPDATA%"))
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var appDataFallback = Path.Combine(userHome, ".config");
            target = target.Replace("%APPDATA%", appDataFallback);
        }
        var expanded = Environment.ExpandEnvironmentVariables(target);
        return Path.GetFullPath(expanded);
    }

    public static WebApplication CreateWebApplication(string[] args, bool isServiceMode = false, string url = "http://0.0.0.0:5246")
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.UseUrls(url);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<VramOrchestrator>();

        try
        {
            var proxySection = builder.Configuration.GetSection("ReverseProxy");
            if (proxySection.Exists())
            {
                builder.Services.AddReverseProxy().LoadFromConfig(proxySection);
            }
        }
        catch { }

        if (isServiceMode)
        {
            builder.Host.UseWindowsService(options =>
            {
                options.ServiceName = "LocalLLMServerManager";
            });
        }

        var app = builder.Build();

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
                Version = "3.0.0"
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
                    return Results.Ok(new[] { new { id = "meta-llama/Llama-3.3-8B-Instruct-GGUF", author = "meta-llama", likes = 100, downloads = 500 } });
                }
                var content = await response.Content.ReadAsStringAsync();
                return Results.Content(content, "application/json");
            }
            catch
            {
                return Results.Ok(new[] { new { id = "meta-llama/Llama-3.3-8B-Instruct-GGUF", author = "meta-llama", likes = 100, downloads = 500 } });
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
                    return Results.Ok(new { id = repoId, author = "meta-llama", siblings = new[] { new { rfilename = "model.gguf" } } });
                }
                var content = await response.Content.ReadAsStringAsync();
                return Results.Content(content, "application/json");
            }
            catch
            {
                return Results.Ok(new { id = repoId, author = "meta-llama", siblings = new[] { new { rfilename = "model.gguf" } } });
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
            catch
            {
                return Results.Ok(new { items = new[] { new { id = 1, name = "Test Model", type = "Checkpoint" } } });
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
            catch
            {
                return Results.Ok(new { id = id, name = "Test Model Detail" });
            }
        });

        // GPU details retrieval endpoint (NVIDIA CUDA priority + Registry scoring fallback)
        app.MapGet("/api/gpu/vram", () =>
        {
            var (gpuName, vramBytes, usedVramBytes) = GetGpuInfo();
            return Results.Ok(new
            {
                GpuName = gpuName,
                VramBytes = vramBytes,
                UsedVramBytes = usedVramBytes,
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

        // Model Context Protocol (MCP) Server Integration for AI Agents
        app.MapGet("/api/mcp/tools", () => Results.Ok(new
        {
            tools = new object[]
            {
                new
                {
                    name = "get_telemetry",
                    description = "Returns GPU VRAM usage, engine status (Ollama, SD Forge, ComfyUI), and system health.",
                    parameters = new { type = "object", properties = new { } }
                },
                new
                {
                    name = "list_models",
                    description = "Lists installed Ollama LLM models, size, and load status.",
                    parameters = new { type = "object", properties = new { } }
                },
                new
                {
                    name = "unload_vram",
                    description = "Unloads all models or a specified model from GPU VRAM.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            model = new { type = "string", description = "Optional model name to unload. If omitted, unloads all models." }
                        }
                    }
                },
                new
                {
                    name = "toggle_engine",
                    description = "Starts or stops AI engine (forge or comfy).",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            engine = new { type = "string", @enum = new[] { "forge", "comfy" }, description = "Engine to toggle" },
                            action = new { type = "string", @enum = new[] { "start", "stop" }, description = "Action to perform" }
                        },
                        required = new[] { "engine", "action" }
                    }
                }
            }
        }));

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
            var forgeModelsDir = ResolvePath(settings.ForgeModelsPath, @"%APPDATA%\AI\SD_Forge\models");
            var targetDir = modelType.ToLowerInvariant() switch
            {
                "lora" => Path.Combine(forgeModelsDir, "Lora"),
                "controlnet" => Path.Combine(forgeModelsDir, "ControlNet"),
                "vae" => Path.Combine(forgeModelsDir, "VAE"),
                "embedding" => Path.Combine(forgeModelsDir, "embeddings"),
                _ => Path.Combine(forgeModelsDir, "Stable-diffusion")
            };

            if (string.IsNullOrWhiteSpace(forgeModelsDir) || !Directory.Exists(forgeModelsDir))
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
        app.MapGet("/api/comfy/workflows", async () =>
        {
            var workflowsDir = Path.Combine(AppContext.BaseDirectory, "Workflows");

            if (!Directory.Exists(workflowsDir))
            {
                return Results.Ok(new List<object>());
            }

            var jsonFiles = Directory.GetFiles(workflowsDir, "*.json");
            using var semaphore = new SemaphoreSlim(16);

            var tasks = jsonFiles.Select(async file =>
            {
                await semaphore.WaitAsync();
                try
                {
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                    using var doc = await JsonDocument.ParseAsync(stream);
                    var root = doc.RootElement;

                    var name = root.TryGetProperty("name", out var n) ? n.GetString() : Path.GetFileNameWithoutExtension(file);
                    var type = root.TryGetProperty("type", out var t) ? t.GetString() : "general";
                    var description = root.TryGetProperty("description", out var d) ? d.GetString() : "";

                    return new
                    {
                        id = Path.GetFileNameWithoutExtension(file),
                        name,
                        type,
                        description,
                        filePath = file
                    };
                }
                catch
                {
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            var result = results.Where(r => r != null).ToList();

            return Results.Ok(result);
        });

        app.MapGet("/api/comfy/workflows/{id}", async (string id) =>
        {
            var workflowsDir = Path.Combine(AppContext.BaseDirectory, "Workflows");
            var filePath = Path.Combine(workflowsDir, $"{id}.json");

            if (!File.Exists(filePath))
            {
                return Results.NotFound($"Workflow preset '{id}' not found.");
            }

            var content = await File.ReadAllTextAsync(filePath);
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
            var outputDir = ResolvePath(settings.ThreeDModelsPath, @"%APPDATA%\AI\3d_outputs");

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

            var path = ResolvePath(settings.ComfyUiExecutablePath, @"%APPDATA%\AI\ComfyUI\run_nvidia_gpu.bat");
            if (!System.IO.File.Exists(path)) return Results.NotFound(new { message = $"ComfyUI executable not found at: {path}" });

            comfyProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                    Arguments = OperatingSystem.IsWindows() ? $"/c \"{path}\"" : $"\"{path}\"",
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
            var path = ResolvePath(settings.ForgeExecutablePath, @"%APPDATA%\AI\SD_Forge\webui-user.bat");
            if (!System.IO.File.Exists(path)) return Results.NotFound(new { message = $"SD Forge executable not found at: {path}" });

            forgeProcess = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                    Arguments = OperatingSystem.IsWindows() ? $"/c \"{path}\"" : $"\"{path}\"",
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

    public static (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfo()
    {
        // 1. Try nvidia-smi first for exact CUDA telemetry
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,memory.total,memory.used --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                var parsed = ParseNvidiaSmiOutput(output);
            if (parsed.HasValue) return parsed.Value;
            }
        }
        catch { }

        if (OperatingSystem.IsLinux())
        {
            var linuxMem = GetLinuxMemoryInfo();
            if (linuxMem.HasValue) return linuxMem.Value;
        }

        return GetGpuInfoFromRegistry();
    }

    public static (string GpuName, long TotalVramBytes, long UsedVramBytes)? GetLinuxMemoryInfo()
    {
        if (!OperatingSystem.IsLinux()) return null;
        try
        {
            if (File.Exists("/proc/meminfo"))
            {
                long totalKb = 0;
                long availableKb = 0;
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        var parts = line.Split(':', StringSplitOptions.TrimEntries);
                        if (parts.Length >= 2)
                        {
                            var val = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                            long.TryParse(val, out totalKb);
                        }
                    }
                    else if (line.StartsWith("MemAvailable:"))
                    {
                        var parts = line.Split(':', StringSplitOptions.TrimEntries);
                        if (parts.Length >= 2)
                        {
                            var val = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                            long.TryParse(val, out availableKb);
                        }
                    }
                }
                if (totalKb > 0)
                {
                    long totalBytes = totalKb * 1024;
                    long usedBytes = Math.Max(0, (totalKb - availableKb) * 1024);
                    return ("Linux System Memory", totalBytes, usedBytes);
                }
            }
        }
        catch { }
        return null;
    }

    public static (string GpuName, long TotalVramBytes, long UsedVramBytes)? ParseNvidiaSmiOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        var parts = output.Split(',');
        if (parts.Length >= 3)
        {
            string name = parts[0].Trim();
            if (long.TryParse(parts[1].Trim(), out long totalMb) &&
                long.TryParse(parts[2].Trim(), out long usedMb))
            {
                return (name, totalMb * 1024 * 1024, usedMb * 1024 * 1024);
            }
        }
        return null;
    }

    public static (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfoFromRegistry()
    {
        // 2. Registry Fallback: Find discrete NVIDIA/AMD GPU over Intel integrated
        string bestGpuName = "Generic GPU";
        long bestVramBytes = 8L * 1024 * 1024 * 1024;
        int bestScore = -1;

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

                                    int score = 1;
                                    if (driverDesc.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                                        provider.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                                    {
                                        score = 10;
                                    }
                                    else if (driverDesc.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                                             driverDesc.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                                    {
                                        score = 5;
                                    }
                                    else if (driverDesc.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                                    {
                                        score = 0; // Low score for integrated graphics
                                    }

                                    var qwMemSize = subKey.GetValue("HardwareInformation.qwMemorySize");
                                    long vram = 0;
                                    if (qwMemSize != null)
                                    {
                                        try { vram = Convert.ToInt64(qwMemSize); } catch { }
                                    }

                                    if (vram <= 0)
                                    {
                                        var dwMemSize = subKey.GetValue("HardwareInformation.MemorySize");
                                        if (dwMemSize != null)
                                        {
                                            try
                                            {
                                                byte[]? rawBytes = dwMemSize as byte[];
                                                if (rawBytes != null && rawBytes.Length >= 4)
                                                    vram = BitConverter.ToUInt32(rawBytes, 0);
                                                else
                                                    vram = Convert.ToInt64(dwMemSize);
                                            }
                                            catch { }
                                        }
                                    }

                                    if (score > bestScore || (score == bestScore && vram > bestVramBytes))
                                    {
                                        bestScore = score;
                                        bestGpuName = driverDesc;
                                        if (vram > 0) bestVramBytes = vram;
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

        return (bestGpuName, bestVramBytes, 0L);
    }
}
