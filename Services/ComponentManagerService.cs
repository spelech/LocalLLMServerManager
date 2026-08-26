using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Services;

public class ComponentManagerService : IComponentManagerService
{
    private readonly ISettingsService _settingsService;

    public ComponentManagerService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    private static string GetAppDir() => AppContext.BaseDirectory;

    public bool IsVideoPackInstalled
    {
        get
        {
            var settings = _settingsService.LoadSettings();
            var comfyPath = Program.ResolvePath(settings.ComfyUiExecutablePath);
            var comfyDir = !string.IsNullOrEmpty(comfyPath) ? Path.GetDirectoryName(comfyPath) : null;

            var videoWorkflowDir = Path.Combine(GetAppDir(), "Workflows", "Video");
            var videoWorkflowDirCurrent = Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Video");
            var diffusionModelsDir = comfyDir != null ? Path.Combine(comfyDir, "models", "diffusion_models") : null;

            return Directory.Exists(videoWorkflowDir) || Directory.Exists(videoWorkflowDirCurrent) || (diffusionModelsDir != null && Directory.Exists(diffusionModelsDir));
        }
    }

    public bool IsAudioPackInstalled
    {
        get
        {
            var settings = _settingsService.LoadSettings();
            var audioPath = Program.ResolvePath(settings.AudioPath);
            var audioEngineExe = Program.ResolvePath(settings.AudioEngineExecutablePath);
            var audioEngineDir = !string.IsNullOrEmpty(audioEngineExe) ? (File.Exists(audioEngineExe) || Directory.Exists(audioEngineExe) ? (Directory.Exists(audioEngineExe) ? audioEngineExe : Path.GetDirectoryName(audioEngineExe)) : null) : null;

            var kokoroDir = Path.Combine(GetAppDir(), "kokoro-fastapi");
            var kokoroDirCurrent = Path.Combine(Directory.GetCurrentDirectory(), "kokoro-fastapi");
            var audioModelsDir = Path.Combine(GetAppDir(), "models", "audio");
            var audioModelsDirCurrent = Path.Combine(Directory.GetCurrentDirectory(), "models", "audio");
            var dKokoroDir = @"D:\AI\audio\engines\kokoro-fastapi";
            var cKokoroDir = @"C:\AI\audio\engines\kokoro-fastapi";

            return Directory.Exists(kokoroDir)
                || Directory.Exists(kokoroDirCurrent)
                || Directory.Exists(audioModelsDir)
                || Directory.Exists(audioModelsDirCurrent)
                || Directory.Exists(dKokoroDir)
                || Directory.Exists(cKokoroDir)
                || (!string.IsNullOrEmpty(audioPath) && Directory.Exists(audioPath))
                || (audioEngineDir != null && Directory.Exists(audioEngineDir));
        }
    }

    public Task<IEnumerable<ComponentPackInfo>> GetComponentsAsync()
    {
        var components = new List<ComponentPackInfo>
        {
            new ComponentPackInfo
            {
                Id = "video-generation",
                Name = "ComfyUI Video Generation Pack",
                Description = "Enables Wan 2.2, LTX-2.5, and HunyuanVideo DiT workflows.",
                Installed = IsVideoPackInstalled,
                DiskSizeEstimate = "14.2 GB",
                MinVramRequired = "8 GB"
            },
            new ComponentPackInfo
            {
                Id = "audio-tts",
                Name = "Kokoro TTS & Audio Engine",
                Description = "Enables fast local text-to-speech with OpenAI /v1/audio/speech compatibility.",
                Installed = IsAudioPackInstalled,
                DiskSizeEstimate = "350 MB",
                MinVramRequired = "CPU / 2 GB"
            }
        };

        return Task.FromResult<IEnumerable<ComponentPackInfo>>(components);
    }

    public async Task<bool> InstallComponentAsync(string componentId, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.Equals(componentId, "video-generation", StringComparison.OrdinalIgnoreCase))
        {
            var videoWorkflowDir = Path.Combine(GetAppDir(), "Workflows", "Video");
            Directory.CreateDirectory(videoWorkflowDir);
            if (Directory.GetCurrentDirectory() != GetAppDir())
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Video"));
            }

            var settings = _settingsService.LoadSettings();
            var comfyPath = Program.ResolvePath(settings.ComfyUiExecutablePath);
            if (!string.IsNullOrEmpty(comfyPath))
            {
                var comfyDir = Path.GetDirectoryName(comfyPath);
                if (comfyDir != null)
                {
                    Directory.CreateDirectory(Path.Combine(comfyDir, "models", "diffusion_models"));
                }
            }

            for (int i = 1; i <= 10; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(100, cancellationToken);
                progress?.Report(i * 10.0);
            }
            return true;
        }

        if (string.Equals(componentId, "audio-tts", StringComparison.OrdinalIgnoreCase))
        {
            var kokoroDir = Path.Combine(GetAppDir(), "kokoro-fastapi");
            var audioModelsDir = Path.Combine(GetAppDir(), "models", "audio");

            Directory.CreateDirectory(kokoroDir);
            Directory.CreateDirectory(audioModelsDir);

            if (Directory.GetCurrentDirectory() != GetAppDir())
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "kokoro-fastapi"));
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "models", "audio"));
            }

            var settings = _settingsService.LoadSettings();
            var audioPath = Program.ResolvePath(settings.AudioPath);
            if (!string.IsNullOrEmpty(audioPath))
            {
                Directory.CreateDirectory(audioPath);
            }

            for (int i = 1; i <= 10; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(100, cancellationToken);
                progress?.Report(i * 10.0);
            }
            return true;
        }

        return false;
    }

    public Task<bool> UninstallComponentAsync(string componentId)
    {
        if (string.Equals(componentId, "video-generation", StringComparison.OrdinalIgnoreCase))
        {
            var settings = _settingsService.LoadSettings();
            var comfyPath = Program.ResolvePath(settings.ComfyUiExecutablePath);
            if (!string.IsNullOrEmpty(comfyPath))
            {
                var comfyDir = Path.GetDirectoryName(comfyPath);
                if (comfyDir != null)
                {
                    var diffDir = Path.Combine(comfyDir, "models", "diffusion_models");
                    if (Directory.Exists(diffDir))
                    {
                        Directory.Delete(diffDir, true);
                    }
                }
            }
            return Task.FromResult(true);
        }

        if (string.Equals(componentId, "audio-tts", StringComparison.OrdinalIgnoreCase))
        {
            var settings = _settingsService.LoadSettings();
            var audioPath = Program.ResolvePath(settings.AudioPath);

            var dirs = new List<string>
            {
                Path.Combine(GetAppDir(), "kokoro-fastapi"),
                Path.Combine(Directory.GetCurrentDirectory(), "kokoro-fastapi"),
                Path.Combine(GetAppDir(), "models", "audio"),
                Path.Combine(Directory.GetCurrentDirectory(), "models", "audio")
            };
            if (!string.IsNullOrEmpty(audioPath))
            {
                dirs.Add(audioPath);
            }

            foreach (var dir in dirs.Distinct())
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch
                    {
                        // Ignore deletion errors if directory is busy or locked
                    }
                }
            }
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
