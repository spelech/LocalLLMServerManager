using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using Moq;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AudioWorkflowTests
{
    private static string ResolveWorkflowPath(string relativePath)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, relativePath),
            Path.Combine(Directory.GetCurrentDirectory(), relativePath),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", relativePath)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativePath))
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    [Fact]
    public void StableAudioWorkflow_IsValidJson_AndContainsStableAudioModelLoader()
    {
        var path = ResolveWorkflowPath(Path.Combine("Workflows", "Audio", "stable_audio_open_sfx.json"));
        Assert.True(File.Exists(path), $"File not found at {path}");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("audio", root.GetProperty("type").GetString());
        Assert.True(root.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()));
        Assert.True(root.TryGetProperty("description", out var descProp) && !string.IsNullOrWhiteSpace(descProp.GetString()));

        var workflow = root.GetProperty("workflow");
        var hasClip = false;
        var hasLoader = false;
        var hasSampler = false;
        var hasSaveAudio = false;

        foreach (var node in workflow.EnumerateObject())
        {
            if (node.Value.TryGetProperty("class_type", out var classTypeProp))
            {
                var classType = classTypeProp.GetString();
                if (classType == "CLIPTextEncode") hasClip = true;
                if (classType == "StableAudioModelLoader" || classType == "AudioModelLoader") hasLoader = true;
                if (classType == "KSampler") hasSampler = true;
                if (classType == "SaveAudio") hasSaveAudio = true;
            }
        }

        Assert.True(hasClip, "Workflow should contain CLIPTextEncode node");
        Assert.True(hasLoader, "Workflow should contain StableAudioModelLoader node");
        Assert.True(hasSampler, "Workflow should contain KSampler node");
        Assert.True(hasSaveAudio, "Workflow should contain SaveAudio node");
    }

    [Fact]
    public void MusicGenWorkflow_IsValidJson_AndContainsAudioTypeAndRequiredNodes()
    {
        var path = ResolveWorkflowPath(Path.Combine("Workflows", "Audio", "musicgen_melody.json"));
        Assert.True(File.Exists(path), $"File not found at {path}");

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("audio", root.GetProperty("type").GetString());
        Assert.Equal("MusicGen Melody & Instrumental Studio", root.GetProperty("name").GetString());
        Assert.Contains("MusicGen", root.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);

        var workflow = root.GetProperty("workflow");
        var hasClip = false;
        var hasLoader = false;
        var hasSampler = false;
        var hasSaveAudio = false;

        foreach (var node in workflow.EnumerateObject())
        {
            if (node.Value.TryGetProperty("class_type", out var classTypeProp))
            {
                var classType = classTypeProp.GetString();
                if (classType == "CLIPTextEncode") hasClip = true;
                if (classType == "MusicGenModelLoader" || classType == "AudioModelLoader") hasLoader = true;
                if (classType == "MusicGenSampler" || classType == "KSampler") hasSampler = true;
                if (classType == "SaveAudio") hasSaveAudio = true;
            }
        }

        Assert.True(hasClip, "Workflow should contain CLIPTextEncode node");
        Assert.True(hasLoader, "Workflow should contain MusicGenModelLoader / AudioModelLoader node");
        Assert.True(hasSampler, "Workflow should contain MusicGenSampler / KSampler node");
        Assert.True(hasSaveAudio, "Workflow should contain SaveAudio node");
    }

    [Fact]
    public async Task GetComponentsAsync_IncludesAudioTtsAndMusicPacksWithCorrectMetadata()
    {
        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());
        var service = new ComponentManagerService(settingsMock.Object);

        var components = (await service.GetComponentsAsync()).ToList();

        var ttsPack = components.FirstOrDefault(c => c.Id == "audio-tts");
        Assert.NotNull(ttsPack);
        Assert.Equal("Kokoro TTS & Audio Engine", ttsPack.Name);
        Assert.Equal("350 MB", ttsPack.DiskSizeEstimate);

        var musicPack = components.FirstOrDefault(c => c.Id == "audio-music");
        Assert.NotNull(musicPack);
        Assert.Equal("MusicGen & Stable Audio Studio", musicPack.Name);
        Assert.Equal("6.8 GB", musicPack.DiskSizeEstimate);
        Assert.Equal("6 GB", musicPack.MinVramRequired);
        Assert.Contains("Stable Audio Open 1.0 SFX", musicPack.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MusicGen", musicPack.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsMusicPackInstalled_WhenComfyCheckpointExists_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_music_pack_" + Path.GetRandomFileName());
        var checkpointsDir = Path.Combine(tempDir, "models", "checkpoints");
        Directory.CreateDirectory(checkpointsDir);
        var checkpointPath = Path.Combine(checkpointsDir, "stable_audio_open_1_0.safetensors");
        File.WriteAllText(checkpointPath, "mock weights");
        File.WriteAllText(Path.Combine(tempDir, "main.py"), "# mock");

        try
        {
            var settingsMock = new Mock<ISettingsService>();
            settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(ComfyUiExecutablePath: Path.Combine(tempDir, "main.py")));
            var service = new ComponentManagerService(settingsMock.Object);

            Assert.True(service.IsMusicPackInstalled);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task InstallAndUninstall_AudioMusicPack_PerformsDirectoryOperationsAndProgress()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_music_install_" + Path.GetRandomFileName());

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(ComfyUiExecutablePath: Path.Combine(tempDir, "main.py")));
        var service = new ComponentManagerService(settingsMock.Object);

        double lastProgress = 0;
        var progressMock = new Mock<IProgress<double>>();
        progressMock.Setup(p => p.Report(It.IsAny<double>())).Callback<double>(val => lastProgress = val);

        var installResult = await service.InstallComponentAsync("audio-music", progressMock.Object);
        Assert.True(installResult);
        Assert.Equal(100.0, lastProgress);

        var uninstallResult = await service.UninstallComponentAsync("audio-music");
        Assert.True(uninstallResult);
    }
}
