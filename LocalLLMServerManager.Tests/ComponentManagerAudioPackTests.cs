using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using Moq;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ComponentManagerAudioPackTests
{
    [Fact]
    public async Task GetComponentsAsync_IncludesAudioTtsPackWithCorrectMetadata()
    {
        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());
        var service = new ComponentManagerService(settingsMock.Object);

        var components = (await service.GetComponentsAsync()).ToList();
        var audioPack = components.FirstOrDefault(c => c.Id == "audio-tts");

        Assert.NotNull(audioPack);
        Assert.Equal("Kokoro TTS & Audio Engine", audioPack.Name);
        Assert.Equal("350 MB", audioPack.DiskSizeEstimate);
        Assert.Equal("CPU / 2 GB", audioPack.MinVramRequired);
        Assert.Contains("text-to-speech", audioPack.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/v1/audio/speech", audioPack.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IsAudioPackInstalled_WhenAudioPathInSettingsExists_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_audio_pack_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);

        try
        {
            var settingsMock = new Mock<ISettingsService>();
            settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(AudioPath: tempDir));
            var service = new ComponentManagerService(settingsMock.Object);

            Assert.True(service.IsAudioPackInstalled);
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
    public void IsAudioPackInstalled_WhenAudioEngineExecutableExists_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_audio_engine_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var exePath = Path.Combine(tempDir, "main.py");
        File.WriteAllText(exePath, "# mock runner");

        try
        {
            var settingsMock = new Mock<ISettingsService>();
            settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(AudioEngineExecutablePath: exePath));
            var service = new ComponentManagerService(settingsMock.Object);

            Assert.True(service.IsAudioPackInstalled);
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
    public async Task InstallAndUninstall_AudioPack_PerformsDirectoryOperationsAndProgress()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_audio_install_" + Path.GetRandomFileName());

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(AudioPath: tempDir));
        var service = new ComponentManagerService(settingsMock.Object);

        double lastProgress = 0;
        var progressMock = new Mock<IProgress<double>>();
        progressMock.Setup(p => p.Report(It.IsAny<double>())).Callback<double>(val => lastProgress = val);

        var installResult = await service.InstallComponentAsync("audio-tts", progressMock.Object);
        Assert.True(installResult);
        Assert.Equal(100.0, lastProgress);
        Assert.True(Directory.Exists(tempDir));

        var uninstallResult = await service.UninstallComponentAsync("audio-tts");
        Assert.True(uninstallResult);
        Assert.False(Directory.Exists(tempDir));
    }

    [Fact]
    public void SetupKokoroTtsScript_FileExistsAndContainsRequiredEndpointsAndPackages()
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "setup_kokoro_tts.ps1");
        if (!File.Exists(scriptPath))
        {
            scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "setup_kokoro_tts.ps1");
        }
        if (!File.Exists(scriptPath))
        {
            // Traverse up to find repo root
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "scripts", "setup_kokoro_tts.ps1")))
            {
                dir = Directory.GetParent(dir)?.FullName;
            }
            if (dir != null)
            {
                scriptPath = Path.Combine(dir, "scripts", "setup_kokoro_tts.ps1");
            }
        }

        Assert.True(File.Exists(scriptPath), $"Script not found at {scriptPath}");
        var scriptContent = File.ReadAllText(scriptPath);

        Assert.Contains("/v1/audio/speech", scriptContent);
        Assert.Contains("/health", scriptContent);
        Assert.Contains("/v1/audio/voices", scriptContent);
        Assert.Contains("kokoro-onnx", scriptContent);
        Assert.Contains("soundfile", scriptContent);
        Assert.Contains("run.bat", scriptContent);
        Assert.Contains("start.bat", scriptContent);
    }

    [Fact]
    public void SetupKokoroTtsScript_Execution_GeneratesExpectedDirectoryAndFiles()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", "setup_kokoro_tts.ps1");
        if (!File.Exists(scriptPath))
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "scripts", "setup_kokoro_tts.ps1")))
            {
                dir = Directory.GetParent(dir)?.FullName;
            }
            if (dir != null)
            {
                scriptPath = Path.Combine(dir, "scripts", "setup_kokoro_tts.ps1");
            }
        }

        Assert.True(File.Exists(scriptPath), $"Script not found at {scriptPath}");

        var tempTargetDir = Path.Combine(Path.GetTempPath(), "kokoro_setup_test_" + Path.GetRandomFileName());
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -TargetDir \"{tempTargetDir}\" -SkipModelDownload",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            Assert.NotNull(process);
            process.WaitForExit(30000);
            Assert.Equal(0, process.ExitCode);

            Assert.True(File.Exists(Path.Combine(tempTargetDir, "main.py")));
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "requirements.txt")));
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "run.bat")));
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "start.bat")));
            Assert.True(Directory.Exists(Path.Combine(tempTargetDir, "models")));
            Assert.True(Directory.Exists(Path.Combine(tempTargetDir, "voices")));

            var mainPyText = File.ReadAllText(Path.Combine(tempTargetDir, "main.py"));
            Assert.Contains("/v1/audio/speech", mainPyText);
            Assert.Contains("/health", mainPyText);
            Assert.Contains("/v1/audio/voices", mainPyText);
            Assert.Contains("kokoro", mainPyText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempTargetDir))
            {
                try { Directory.Delete(tempTargetDir, true); } catch { }
            }
        }
    }
}
