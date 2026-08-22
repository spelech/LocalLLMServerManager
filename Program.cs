using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia;
using LocalLLMServerManager;
using Microsoft.AspNetCore.StaticFiles;
using LocalLLMServerManager.Endpoints;
using LocalLLMServerManager.Services;

namespace LocalLLMServerManager;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        MainInternal(args, runWeb: true);
    }

    public static void MainInternal(string[] args, bool runWeb = false)
    {
        if (args.Contains("--server") || args.Contains("--headless"))
        {
            var app = CreateWebApplication(args, isServiceMode: false);
            if (runWeb) app.Run();
        }
        else if (args.Contains("--service"))
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

    public static string SettingsFilePath() => new SettingsService().SettingsFilePath();

    public static AppSettings LoadSettings() => new SettingsService().LoadSettings();

    public static void SaveSettings(AppSettings settings) => new SettingsService().SaveSettings(settings);

    public static string ResolvePath(string? rawPath, string fallbackRelativePath = "")
    {
        var target = string.IsNullOrWhiteSpace(rawPath) ? fallbackRelativePath : rawPath;
        if (string.IsNullOrWhiteSpace(target))
        {
            return string.Empty;
        }

        if (!OperatingSystem.IsWindows() && target.Contains("%APPDATA%"))
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var appDataFallback = Path.Combine(userHome, ".config");
            target = target.Replace("%APPDATA%", appDataFallback);
        }
        var expanded = Environment.ExpandEnvironmentVariables(target);
        return Path.GetFullPath(expanded);
    }

    public static bool IsSafePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (path.Contains("..") || path.Contains("./") || path.Contains(".\\")) return false;

        char[] dangerousChars = { ';', '&', '|', '`', '$', '>', '<', '*', '?' };
        if (path.Any(c => dangerousChars.Contains(c))) return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            if (OperatingSystem.IsWindows())
            {
                var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (!string.IsNullOrEmpty(winDir) && fullPath.StartsWith(winDir, StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.IsNullOrEmpty(systemDir) && fullPath.StartsWith(systemDir, StringComparison.OrdinalIgnoreCase)) return false;
            }
            else
            {
                string[] blockedPrefixes = { "/etc", "/var", "/bin", "/sbin", "/usr/bin", "/usr/sbin", "/proc", "/sys", "/dev" };
                foreach (var prefix in blockedPrefixes)
                {
                    if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
                }
            }
        }
        catch
        {
            return false;
        }

        return true;
    }

    public static WebApplication CreateWebApplication(string[] args, bool isServiceMode = false, string url = "http://0.0.0.0:5246")
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.WebHost.UseUrls(url);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<VramOrchestrator>();
        builder.Services.AddSingleton<IAiEngineManager, AiEngineManager>();
        builder.Services.AddSingleton<IGitUpdateService, GitUpdateService>();
        builder.Services.AddSingleton<IToolDiscoveryService, ToolDiscoveryService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();
        builder.Services.AddSingleton<IGpuTelemetryProvider, GpuTelemetryProvider>();

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

        var contentTypeProvider = new FileExtensionContentTypeProvider();
        contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
        contentTypeProvider.Mappings[".symbols"] = "application/octet-stream";
        contentTypeProvider.Mappings[".wasm"] = "application/wasm";
        contentTypeProvider.Mappings[".clr"] = "application/octet-stream";
        contentTypeProvider.Mappings[".pdb"] = "application/octet-stream";
        contentTypeProvider.Mappings[".boot.json"] = "application/json";

        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = contentTypeProvider,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream"
        });

        // Basic VRAM Orchestration Middleware
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isForgeRequest = path.StartsWith("/sdapi", StringComparison.OrdinalIgnoreCase) ||
                                 path.StartsWith("/v1/images", StringComparison.OrdinalIgnoreCase);
            var isComfyRequest = path.StartsWith("/comfyapi", StringComparison.OrdinalIgnoreCase) ||
                                 path.StartsWith("/api/comfy/prompt", StringComparison.OrdinalIgnoreCase);

            var orchestrator = context.RequestServices.GetRequiredService<VramOrchestrator>();
            var settingsService = context.RequestServices.GetRequiredService<ISettingsService>();

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

                var settings = settingsService.LoadSettings();
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

        app.MapHealthEndpoints();
        app.MapModelProxyEndpoints();
        app.MapMcpEndpoints();
        app.MapEngineEndpoints();
        app.MapWorkflowEndpoints();
        app.MapDiscoveryEndpoints();

        // Service Update Route
        app.MapPost("/api/service/update", async (HttpContext httpContext, IGitUpdateService gitService) =>
        {
            ServiceUpdateRequest? request = null;
            if (httpContext.Request.HasJsonContentType())
            {
                try
                {
                    request = await httpContext.Request.ReadFromJsonAsync<ServiceUpdateRequest>();
                }
                catch { }
            }

            var repoDir = Directory.GetCurrentDirectory();
            var gitMessage = "";

            if (request != null && !string.IsNullOrWhiteSpace(request.Branch))
            {
                if (!gitService.IsValidBranchName(request.Branch))
                {
                    return Results.BadRequest(new { message = "Invalid branch name format." });
                }

                var fetchResult = await gitService.RunCommandAsync("git", new[] { "fetch" }, repoDir);
                if (!fetchResult.Success)
                {
                    gitMessage += $" (Git fetch failed: {fetchResult.Error.Trim()})";
                }
                else
                {
                    var checkoutResult = await gitService.RunCommandAsync("git", new[] { "checkout", request.Branch }, repoDir);
                    if (!checkoutResult.Success)
                    {
                        gitMessage += $" (Git checkout failed: {checkoutResult.Error.Trim()})";
                    }
                }
            }

            var pullResult = await gitService.RunCommandAsync("git", new[] { "pull" }, repoDir);
            if (!pullResult.Success)
            {
                return Results.Problem($"Service code update failed: {pullResult.Error.Trim()}{gitMessage}");
            }

            return Results.Ok(new { message = $"Service update pulled successfully.{gitMessage}" });
        });

        // Reverse Proxy endpoint registration
        try
        {
            app.MapReverseProxy();
        }
        catch { }

        return app;
    }

    public static (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfo()
    {
        return new GpuTelemetryProvider().GetGpuInfo();
    }

    public static (string GpuName, long TotalVramBytes, long UsedVramBytes)? GetLinuxMemoryInfo()
    {
        return new GpuTelemetryProvider().GetLinuxMemoryInfo();
    }

    public static (string GpuName, long TotalVramBytes, long UsedVramBytes)? ParseNvidiaSmiOutput(string output)
    {
        return new GpuTelemetryProvider().ParseNvidiaSmiOutput(output);
    }

    public static (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfoFromRegistry()
    {
        return new GpuTelemetryProvider().GetGpuInfoFromRegistry();
    }

    public static async Task<(bool Success, string Output, string Error)> RunCommandAsync(string fileName, string[] arguments, string workingDir)
    {
        return await new GitUpdateService().RunCommandAsync(fileName, arguments, workingDir);
    }

    public static bool IsValidBranchName(string? branch)
    {
        return new GitUpdateService().IsValidBranchName(branch ?? "");
    }

    public static bool IsValidServiceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');
    }

    public static bool IsValidPublishPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (path.Contains("..") || path.Contains('\0') || path.Contains('&') || path.Contains(';') || path.Contains('|') || path.Contains('$') || path.Contains('`'))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return System.Text.RegularExpressions.Regex.IsMatch(path, @"^[a-zA-Z]:\\[a-zA-Z0-9_\-\\\s\.]+$");
        }
        else
        {
            return System.Text.RegularExpressions.Regex.IsMatch(path, @"^\/[a-zA-Z0-9_\-\/\s\.]+$");
        }
    }
}

public record ServiceUpdateRequest(string? Branch = null);
