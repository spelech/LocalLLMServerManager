using LocalLLMServerManager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LocalLLMServerManager.Endpoints;

public static class EngineEndpoints
{
    public static void MapEngineEndpoints(this WebApplication app)
    {
        app.MapGet("/api/gpu/vram", (IGpuTelemetryProvider telemetryProvider) =>
        {
            var (gpuName, vramBytes, usedVramBytes) = telemetryProvider.GetGpuInfo();
            return Results.Ok(new
            {
                GpuName = gpuName,
                VramBytes = vramBytes,
                UsedVramBytes = usedVramBytes,
                VramGB = Math.Round((double)vramBytes / (1024 * 1024 * 1024), 2)
            });
        });

        app.MapGet("/api/settings", (ISettingsService settingsService) =>
        {
            return Results.Ok(settingsService.LoadSettings());
        });

        app.MapPost("/api/settings", (AppSettings newSettings, ISettingsService settingsService) =>
        {
            settingsService.SaveSettings(newSettings);
            return Results.Ok(newSettings);
        });

        app.MapPost("/api/comfy/start", async (VramOrchestrator orchestrator, IAiEngineManager engineManager, ISettingsService settingsService, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EngineEndpoints");
            var settings = settingsService.LoadSettings();
            var execPath = string.IsNullOrWhiteSpace(settings.ComfyUiExecutablePath) ? @"C:\AI\ComfyUI\run_nvidia_gpu.bat" : settings.ComfyUiExecutablePath;

            if (!Program.IsSafePath(execPath) || !System.IO.File.Exists(Program.ResolvePath(execPath, @"C:\AI\ComfyUI\run_nvidia_gpu.bat")))
            {
                return Results.BadRequest(new { message = $"Invalid or unsafe executable path: {execPath}" });
            }

            await orchestrator.EnsureVramForComfyUiAsync();
            var success = await engineManager.StartComfyUiAsync(execPath, logger);

            if (success)
            {
                return Results.Ok(new { message = "ComfyUI Started", pid = engineManager.ComfyProcess?.Id });
            }
            return Results.Problem("Failed to start ComfyUI process");
        });

        app.MapPost("/api/comfy/stop", async (IAiEngineManager engineManager, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EngineEndpoints");
            await engineManager.StopComfyUiAsync(logger);
            return Results.Ok(new { message = "ComfyUI Stopped" });
        });

        app.MapPost("/api/forge/start", async (IAiEngineManager engineManager, ISettingsService settingsService, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EngineEndpoints");
            var settings = settingsService.LoadSettings();
            var execPath = string.IsNullOrWhiteSpace(settings.ForgeExecutablePath) ? @"C:\AI\webui\webui-user.bat" : settings.ForgeExecutablePath;

            if (!Program.IsSafePath(execPath) || !System.IO.File.Exists(Program.ResolvePath(execPath, @"C:\AI\webui\webui-user.bat")))
            {
                return Results.BadRequest(new { message = $"Invalid or unsafe executable path: {execPath}" });
            }

            var success = await engineManager.StartForgeAsync(execPath, logger);
            if (success)
            {
                return Results.Ok(new { message = "SD Forge Started", pid = engineManager.ForgeProcess?.Id });
            }
            return Results.Problem("Failed to start SD Forge process");
        });

        app.MapPost("/api/forge/stop", async (IAiEngineManager engineManager, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EngineEndpoints");
            await engineManager.StopForgeAsync(logger);
            return Results.Ok(new { message = "SD Forge Stopped" });
        });

        app.MapPost("/api/comfy/free", async (VramOrchestrator orchestrator) =>
        {
            await orchestrator.FreeComfyUiVramAsync();
            return Results.Ok(new { message = "ComfyUI VRAM freed" });
        });

        app.MapPost("/api/audio/start", async (IAiEngineManager engineManager, ISettingsService settingsService, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EngineEndpoints");
            var settings = settingsService.LoadSettings();
            var execPath = string.IsNullOrWhiteSpace(settings.AudioEngineExecutablePath) ? @"C:\AI\Kokoro-FastAPI\main.py" : settings.AudioEngineExecutablePath;

            if (!execPath.TrimStart().StartsWith("docker", StringComparison.OrdinalIgnoreCase))
            {
                if (!Program.IsSafePath(execPath) || !System.IO.File.Exists(Program.ResolvePath(execPath, @"C:\AI\Kokoro-FastAPI\main.py")))
                {
                    return Results.BadRequest(new { message = $"Invalid or unsafe executable path: {execPath}" });
                }
            }

            var success = await engineManager.StartAudioEngineAsync(execPath, logger);
            if (success)
            {
                return Results.Ok(new { message = "Audio Engine Started", pid = engineManager.AudioProcess?.Id });
            }
            return Results.Problem("Failed to start Audio Engine process");
        });

        app.MapPost("/api/audio/stop", async (IAiEngineManager engineManager, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("EngineEndpoints");
            await engineManager.StopAudioEngineAsync(logger);
            return Results.Ok(new { message = "Audio Engine Stopped" });
        });

        app.MapGet("/api/audio/voices", async (ISettingsService settingsService, System.Net.Http.IHttpClientFactory clientFactory) =>
        {
            var settings = settingsService.LoadSettings();
            var baseUrl = (string.IsNullOrWhiteSpace(settings.AudioEngineUrl) ? "http://127.0.0.1:8880" : settings.AudioEngineUrl).TrimEnd('/');

            try
            {
                var client = clientFactory.CreateClient();
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));

                var response = await client.GetAsync($"{baseUrl}/v1/audio/voices", cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    response = await client.GetAsync($"{baseUrl}/voices", cts.Token);
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(json, "application/json");
                }
            }
            catch { }

            var defaultVoices = new[]
            {
                "af_heart", "af_bella", "af_nicole", "af_sarah", "af_sky",
                "am_adam", "am_michael", "bf_emma", "bf_isabella", "bm_george", "bm_fable"
            };
            return Results.Ok(new { voices = defaultVoices, preferred = settings.PreferredAudioVoice });
        });
    }
}
