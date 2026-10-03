using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class LocalModelScannerServiceTests
{
    private readonly LocalModelScannerService _scanner = new();

    [Theory]
    [InlineData("flux1-dev-fp8.safetensors", LocalModelCategory.ImageCheckpoint, "Flux.1")]
    [InlineData("juggernautXL_v9.safetensors", LocalModelCategory.ImageCheckpoint, "SDXL")]
    [InlineData("ponyDiffusionV6XL_v6.safetensors", LocalModelCategory.ImageCheckpoint, "Pony XL")]
    [InlineData("illustriousXL_v01.safetensors", LocalModelCategory.ImageCheckpoint, "Illustrious XL")]
    [InlineData("realisticVisionV60B1_v60B1VAE.safetensors", LocalModelCategory.ImageCheckpoint, "SD 1.5")]
    [InlineData("v1-5-pruned-emaonly.safetensors", LocalModelCategory.ImageCheckpoint, "SD 1.5")]
    [InlineData("wan2.2_t2v.json", LocalModelCategory.Video, "Wan 2.2")]
    [InlineData("ltx2.5_t2v.json", LocalModelCategory.Video, "LTX-Video 2.5")]
    [InlineData("hunyuanvideo1.5_t2v.json", LocalModelCategory.Video, "HunyuanVideo")]
    [InlineData("animatediff_sdxl_api.json", LocalModelCategory.Video, "AnimateDiff")]
    [InlineData("kokoro_v1.safetensors", LocalModelCategory.Audio, "Kokoro TTS")]
    [InlineData("trellis_v2_api.json", LocalModelCategory.ThreeD, "TRELLIS V2")]
    public void DetectArchitecture_IdentifiesCorrectFamily(string fileName, LocalModelCategory category, string expectedFamily)
    {
        var result = LocalModelScannerService.DetectArchitecture(fileName, category);
        Assert.Equal(expectedFamily, result);
    }

    [Theory]
    [InlineData(500, "500 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1024 * 1024 * 144, "144.0 MB")]
    [InlineData(1024L * 1024L * 1024L * 6L, "6.00 GB")]
    public void FormatBytes_FormatsCorrectly(long bytes, string expected)
    {
        var result = LocalModelScannerService.FormatBytes(bytes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CleanDisplayName_CleansSuffixesAndUnderscores()
    {
        var result = LocalModelScannerService.CleanDisplayName("animatediff_sdxl_api");
        Assert.Equal("animatediff sdxl", result);
    }

    [Fact]
    public async Task ScanAllModelsAsync_DiscoversFilesAcrossDirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LLMServerManager_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var checkpointsDir = Path.Combine(tempDir, "models", "checkpoints");
            var loraDir = Path.Combine(tempDir, "models", "Lora");
            var videoDir = Path.Combine(tempDir, "Workflows", "Video");
            var audioDir = Path.Combine(tempDir, "models", "tts");

            Directory.CreateDirectory(checkpointsDir);
            Directory.CreateDirectory(loraDir);
            Directory.CreateDirectory(videoDir);
            Directory.CreateDirectory(audioDir);

            File.WriteAllText(Path.Combine(checkpointsDir, "juggernaut_sdxl.safetensors"), "fake-safetensors-content");
            File.WriteAllText(Path.Combine(loraDir, "detail_tweaker.safetensors"), "fake-lora-content");
            File.WriteAllText(Path.Combine(videoDir, "wan2.2_t2v.json"), "{}");
            File.WriteAllText(Path.Combine(audioDir, "kokoro_voice.safetensors"), "voice-weights");

            var models = await _scanner.ScanAllModelsAsync(baseDirectory: tempDir);

            Assert.NotNull(models);
            Assert.True(models.Count >= 4);

            var checkpoint = models.FirstOrDefault(m => m.FileName == "juggernaut_sdxl.safetensors");
            Assert.NotNull(checkpoint);
            Assert.Equal(LocalModelCategory.ImageCheckpoint, checkpoint.Category);
            Assert.Equal("SDXL", checkpoint.Architecture);

            var lora = models.FirstOrDefault(m => m.FileName == "detail_tweaker.safetensors");
            Assert.NotNull(lora);
            Assert.Equal(LocalModelCategory.ImageLora, lora.Category);

            var video = models.FirstOrDefault(m => m.FileName == "wan2.2_t2v.json");
            Assert.NotNull(video);
            Assert.Equal(LocalModelCategory.Video, video.Category);
            Assert.Equal("Wan 2.2", video.Architecture);

            var audio = models.FirstOrDefault(m => m.FileName == "kokoro_voice.safetensors");
            Assert.NotNull(audio);
            Assert.Equal(LocalModelCategory.Audio, audio.Category);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
