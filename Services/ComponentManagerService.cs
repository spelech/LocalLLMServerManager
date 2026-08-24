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

    public bool IsVideoPackInstalled
    {
        get
        {
            var settings = _settingsService.LoadSettings();
            var comfyPath = Program.ResolvePath(settings.ComfyUiExecutablePath);
            var comfyDir = !string.IsNullOrEmpty(comfyPath) ? Path.GetDirectoryName(comfyPath) : null;

            // Check for Workflows/Video or ComfyUI video diffusion models directory
            var videoWorkflowDir = Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Video");
            var diffusionModelsDir = comfyDir != null ? Path.Combine(comfyDir, "models", "diffusion_models") : null;

            return Directory.Exists(videoWorkflowDir) || (diffusionModelsDir != null && Directory.Exists(diffusionModelsDir));
        }
    }

    public bool IsAudioPackInstalled
    {
        get
        {
            // Check for kokoro-fastapi directory / audio server binaries / models
            var kokoroDir = Path.Combine(Directory.GetCurrentDirectory(), "kokoro-fastapi");
            var audioModelsDir = Path.Combine(Directory.GetCurrentDirectory(), "models", "audio");

            return Directory.Exists(kokoroDir) || Directory.Exists(audioModelsDir);
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
            var videoWorkflowDir = Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Video");
            Directory.CreateDirectory(videoWorkflowDir);

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
            var kokoroDir = Path.Combine(Directory.GetCurrentDirectory(), "kokoro-fastapi");
            var audioModelsDir = Path.Combine(Directory.GetCurrentDirectory(), "models", "audio");

            Directory.CreateDirectory(kokoroDir);
            Directory.CreateDirectory(audioModelsDir);

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
            var videoWorkflowDir = Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Video");
            if (Directory.Exists(videoWorkflowDir))
            {
                Directory.Delete(videoWorkflowDir, true);
            }
            return Task.FromResult(true);
        }

        if (string.Equals(componentId, "audio-tts", StringComparison.OrdinalIgnoreCase))
        {
            var kokoroDir = Path.Combine(Directory.GetCurrentDirectory(), "kokoro-fastapi");
            if (Directory.Exists(kokoroDir))
            {
                Directory.Delete(kokoroDir, true);
            }
            var audioModelsDir = Path.Combine(Directory.GetCurrentDirectory(), "models", "audio");
            if (Directory.Exists(audioModelsDir))
            {
                Directory.Delete(audioModelsDir, true);
            }
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }
}
