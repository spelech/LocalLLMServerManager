using System;
using System.IO;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ToolDiscoveryServiceAudioTests
{
    [Fact]
    public void DetectAudioEngine_WhenLocatedInAudioEnginesSubdir_ReturnsIsInstalledTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_ai_audio_" + Path.GetRandomFileName());
        var kokoroDir = Path.Combine(tempDir, "audio", "engines", "kokoro-fastapi");
        Directory.CreateDirectory(kokoroDir);
        var mainPy = Path.Combine(kokoroDir, "main.py");
        File.WriteAllText(mainPy, "# test");

        try
        {
            var discovery = new ToolDiscoveryService(new[] { tempDir });
            var result = discovery.DetectAudioEngine();

            Assert.True(result.IsInstalled);
            Assert.Equal(mainPy, result.ExecutablePath);
            Assert.Equal(kokoroDir, result.RootDirectory);
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
    public void DetectAudioEngine_WhenLocatedInAudioSubdir_ReturnsIsInstalledTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_ai_audio_" + Path.GetRandomFileName());
        var kokoroDir = Path.Combine(tempDir, "audio", "kokoro");
        Directory.CreateDirectory(kokoroDir);
        var mainPy = Path.Combine(kokoroDir, "main.py");
        File.WriteAllText(mainPy, "# test");

        try
        {
            var discovery = new ToolDiscoveryService(new[] { tempDir });
            var result = discovery.DetectAudioEngine();

            Assert.True(result.IsInstalled);
            Assert.Equal(mainPy, result.ExecutablePath);
            Assert.Equal(kokoroDir, result.RootDirectory);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Theory]
    [InlineData("automatic-speech-recognition", "whisper.bin", "audio/stt")]
    [InlineData("speech-to-text", "model.bin", "audio/stt")]
    [InlineData("stt", "model.bin", "audio/stt")]
    [InlineData(null, "whisper-large-v3-turbo.bin", "audio/stt")]
    [InlineData("audio-engine", "alltalk.zip", "audio/engines")]
    [InlineData(null, "kokoro-fastapi.zip", "audio/engines")]
    [InlineData("text-to-speech", "kokoro.onnx", "models/tts")]
    public void DownloadManager_ResolveTargetDirectory_AudioRouting(string? tagOrType, string? fileName, string expectedSubdir)
    {
        var root = "/test/root";
        var resolved = DownloadManager.ResolveTargetDirectory(tagOrType, fileName, root);
        var normalizedExpected = Path.Combine(root, expectedSubdir.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal(normalizedExpected, resolved);
    }
}
