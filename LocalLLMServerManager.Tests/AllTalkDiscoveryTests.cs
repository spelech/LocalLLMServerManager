using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using LocalLLMServerManager.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AllTalkDiscoveryTests
{
    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "LocalLLMServerManager.sln")))
        {
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        return dir ?? AppContext.BaseDirectory;
    }

    [Fact]
    public void DetectAudioEngine_WhenAllTalkInstalled_DiscoversExecutableAndVoiceDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_alltalk_" + Path.GetRandomFileName());
        var alltalkDir = Path.Combine(tempDir, "audio", "engines", "alltalk_tts");
        var voicesDir = Path.Combine(alltalkDir, "voices");
        Directory.CreateDirectory(voicesDir);
        var runBat = Path.Combine(alltalkDir, "run.bat");
        File.WriteAllText(runBat, "@echo off\r\necho Starting AllTalk");
        File.WriteAllText(Path.Combine(alltalkDir, "main.py"), "# alltalk");

        try
        {
            var discovery = new ToolDiscoveryService(new[] { tempDir });
            var result = discovery.DetectAudioEngine();

            Assert.True(result.IsInstalled);
            Assert.Equal(runBat, result.ExecutablePath);
            Assert.Equal(voicesDir, result.ModelsDirectory);
            Assert.Equal(alltalkDir, result.RootDirectory);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void DetectAudioEngine_WhenAllTalkHasCustomVoicesDir_DiscoversCustomVoices()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_alltalk_custom_" + Path.GetRandomFileName());
        var audioDir = Path.Combine(tempDir, "audio");
        var alltalkDir = Path.Combine(audioDir, "engines", "alltalk_tts");
        var customVoicesDir = Path.Combine(audioDir, "custom_voices");
        Directory.CreateDirectory(alltalkDir);
        Directory.CreateDirectory(customVoicesDir);
        var runBat = Path.Combine(alltalkDir, "run.bat");
        File.WriteAllText(runBat, "@echo off\r\necho Starting AllTalk");
        File.WriteAllText(Path.Combine(alltalkDir, "main.py"), "# alltalk");

        try
        {
            var discovery = new ToolDiscoveryService(new[] { tempDir });
            var result = discovery.DetectAudioEngine();

            Assert.True(result.IsInstalled);
            Assert.Equal(runBat, result.ExecutablePath);
            Assert.Equal(customVoicesDir, result.ModelsDirectory);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void SetupAllTalkXttsScript_GeneratesExpectedFilesAndDirectories()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "setup_alltalk_xtts.ps1");
        Assert.True(File.Exists(scriptPath), $"Script not found at {scriptPath}");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var tempTargetDir = Path.Combine(Path.GetTempPath(), "alltalk_setup_target_" + Path.GetRandomFileName());
        var tempVoicesDir = Path.Combine(Path.GetTempPath(), "alltalk_setup_voices_" + Path.GetRandomFileName());

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -TargetDir \"{tempTargetDir}\" -VoicesDir \"{tempVoicesDir}\" -SkipModelDownload",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            Assert.NotNull(process);
            process.WaitForExit(30000);
            Assert.Equal(0, process.ExitCode);

            Assert.True(File.Exists(Path.Combine(tempTargetDir, "main.py")), "main.py was not created");
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "requirements.txt")), "requirements.txt was not created");
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "run.bat")), "run.bat was not created");
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "start.bat")), "start.bat was not created");
            Assert.True(Directory.Exists(Path.Combine(tempTargetDir, "models", "xtts")), "models/xtts directory was not created");
            Assert.True(Directory.Exists(Path.Combine(tempTargetDir, "voices")), "voices directory was not created");
            Assert.True(Directory.Exists(tempVoicesDir), "custom voices directory was not created");

            var readmePath = Path.Combine(tempVoicesDir, "readme.txt");
            Assert.True(File.Exists(readmePath), "readme.txt was not created in custom voices directory");
            var readmeText = File.ReadAllText(readmePath);
            Assert.Contains("WAV", readmeText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("6", readmeText);

            var mainPy = File.ReadAllText(Path.Combine(tempTargetDir, "main.py"));
            Assert.Contains("/v1/audio/speech", mainPy);
            Assert.Contains("/v1/audio/voices", mainPy);
            Assert.Contains("/health", mainPy);
            Assert.Contains("alltalk", mainPy, StringComparison.OrdinalIgnoreCase);

            var reqs = File.ReadAllText(Path.Combine(tempTargetDir, "requirements.txt"));
            Assert.Contains("torch", reqs, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("TTS", reqs);
            Assert.Contains("fastapi", reqs, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("uvicorn", reqs, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("soundfile", reqs, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(tempTargetDir))
            {
                try { Directory.Delete(tempTargetDir, true); } catch { }
            }
            if (Directory.Exists(tempVoicesDir))
            {
                try { Directory.Delete(tempVoicesDir, true); } catch { }
            }
        }
    }
}
