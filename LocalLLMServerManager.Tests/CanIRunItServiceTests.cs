using System;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class CanIRunItServiceTests
{
    private readonly ICanIRunItService _service = new CanIRunItService();

    [Theory]
    [InlineData("Q2_K", 2.65)]
    [InlineData("Q3_K_M", 3.50)]
    [InlineData("Q4_K_M", 4.50)]
    [InlineData("Q5_K_M", 5.50)]
    [InlineData("Q6_K", 6.60)]
    [InlineData("Q8_0", 8.50)]
    [InlineData("FP16", 16.00)]
    [InlineData("BF16", 16.00)]
    [InlineData("FP8", 8.00)]
    [InlineData("Q4_0", 4.50)]
    [InlineData("Q5_0", 5.50)]
    [InlineData("unknown_quant", 4.50)]
    [InlineData(null, 4.50)]
    [InlineData("", 4.50)]
    public void QuantizationBits_ReturnsCorrectBits(string? quant, double expectedBits)
    {
        var bits = CanIRunItService.GetBitsPerWeight(quant);
        Assert.Equal(expectedBits, bits, tolerance: 0.01);
    }

    [Theory]
    [InlineData("FP16", 16.0)]
    [InlineData("BF16", 16.0)]
    [InlineData("Q8_0", 8.0)]
    [InlineData("Q4_0", 4.0)]
    [InlineData(null, 16.0)]
    [InlineData("", 16.0)]
    public void KvBits_ReturnsCorrectBits(string? kvPrecision, double expectedBits)
    {
        var bits = CanIRunItService.GetKvBitsPerElement(kvPrecision);
        Assert.Equal(expectedBits, bits, tolerance: 0.01);
    }

    [Fact]
    public void Llama33_70B_On16GbVram_PartialOffload()
    {
        // 16 GB VRAM = 16384 MB, 64 GB RAM = 65536 MB
        var request = new LlmFitRequest(
            ParametersBillions: 70.0,
            Quantization: "Q4_K_M",
            ContextLength: 8192,
            KvPrecision: "FP16",
            AvailableVramMb: 16384,
            AvailableRamMb: 65536
        );

        var result = _service.EvaluateLlmFit(request);

        Assert.Equal(FitVerdict.PartialOffload, result.FitVerdict);
        Assert.Equal(80, result.TotalLayers);
        Assert.True(result.GpuLayers > 0, $"Expected GpuLayers > 0, got {result.GpuLayers}");
        Assert.True(result.GpuLayers < 80, $"Expected GpuLayers < 80, got {result.GpuLayers}");
        Assert.Equal(80, result.GpuLayers + result.CpuLayers);
        Assert.True(result.ModelWeightMb > 40000 && result.ModelWeightMb < 45000, $"Expected Weight ~42GB, got {result.ModelWeightMb}");
        Assert.True(result.KvCacheMb > 2000 && result.KvCacheMb < 3000, $"Expected KV ~2.5GB, got {result.KvCacheMb}");
        Assert.True(result.EstimatedTokPerSec > 0.0);
        Assert.Contains("Offloading", result.RecommendationMessage);
    }

    [Fact]
    public void Qwen25_32B_On24GbVram_FullVramFit()
    {
        // 24 GB VRAM = 24576 MB
        var request = new LlmFitRequest(
            ParametersBillions: 32.0,
            Quantization: "Q4_K_M",
            ContextLength: 4096,
            KvPrecision: "FP16",
            AvailableVramMb: 24576,
            AvailableRamMb: 32768
        );

        var result = _service.EvaluateLlmFit(request);

        Assert.Equal(FitVerdict.FullVram, result.FitVerdict);
        Assert.Equal(64, result.TotalLayers);
        Assert.Equal(64, result.GpuLayers);
        Assert.Equal(0, result.CpuLayers);
        Assert.Equal(0, result.TotalRamMb);
        Assert.True(result.TotalVramMb <= 24576);
        Assert.True(result.EstimatedTokPerSec >= 20.0, $"Expected Tok/sec >= 20, got {result.EstimatedTokPerSec}");
        Assert.Contains("100% in GPU VRAM", result.RecommendationMessage);
    }

    [Fact]
    public void LlmFit_ZeroVram_CpuOnly()
    {
        var request = new LlmFitRequest(
            ParametersBillions: 8.0,
            Quantization: "Q4_K_M",
            ContextLength: 4096,
            KvPrecision: "FP16",
            AvailableVramMb: 0,
            AvailableRamMb: 32768
        );

        var result = _service.EvaluateLlmFit(request);

        Assert.Equal(FitVerdict.CpuOnly, result.FitVerdict);
        Assert.Equal(0, result.GpuLayers);
        Assert.Equal(32, result.CpuLayers);
        Assert.True(result.TotalRamMb > 0);
        Assert.True(result.EstimatedTokPerSec > 0.0);
        Assert.Contains("CPU/System RAM", result.RecommendationMessage);
    }

    [Fact]
    public void DeepSeekR1_671B_On16GbVram_WithHighRam_CpuOnlyOrPartial()
    {
        // 16 GB VRAM = 16384 MB, 512 GB RAM = 524288 MB
        var request = new LlmFitRequest(
            ParametersBillions: 671.0,
            Quantization: "Q4_K_M",
            ContextLength: 4096,
            KvPrecision: "FP16",
            AvailableVramMb: 16384,
            AvailableRamMb: 524288
        );

        var result = _service.EvaluateLlmFit(request);

        Assert.True(result.FitVerdict == FitVerdict.PartialOffload || result.FitVerdict == FitVerdict.CpuOnly);
        Assert.True(result.TotalRamMb > 300000, $"Expected large RAM usage for 671B, got {result.TotalRamMb}");
        Assert.True(result.EstimatedTokPerSec > 0.0);
    }

    [Fact]
    public void DeepSeekR1_671B_On16GbVram_WithLowRam_OutOfMemory()
    {
        // 16 GB VRAM, 32 GB RAM (Total ~48GB, 671B Q4 requires ~400GB)
        var request = new LlmFitRequest(
            ParametersBillions: 671.0,
            Quantization: "Q4_K_M",
            ContextLength: 4096,
            KvPrecision: "FP16",
            AvailableVramMb: 16384,
            AvailableRamMb: 32768
        );

        var result = _service.EvaluateLlmFit(request);

        Assert.Equal(FitVerdict.OutOfMemory, result.FitVerdict);
        Assert.Equal(0.0, result.EstimatedTokPerSec);
        Assert.Contains("Insufficient", result.RecommendationMessage);
    }

    [Fact]
    public void LlmFit_CustomArchitectureOverrides_Respected()
    {
        var req = new LlmFitRequest(
            ParametersBillions: 70.0,
            Quantization: "Q8_0",
            ContextLength: 8192,
            KvPrecision: "FP16",
            AvailableVramMb: 49152,
            AvailableRamMb: 65536,
            TotalLayers: 90,
            KvHeads: 16,
            HeadDim: 128
        );

        var result = _service.EvaluateLlmFit(req);

        Assert.Equal(90, result.TotalLayers);
        Assert.Equal(FitVerdict.PartialOffload, result.FitVerdict);
    }

    [Fact]
    public void LlmFit_CustomContextAndKvPrecision_AdjustsKvCacheMb()
    {
        var reqFp16_4k = new LlmFitRequest(8.0, "Q4_K_M", ContextLength: 4096, KvPrecision: "FP16", AvailableVramMb: 16384, AvailableRamMb: 32768);
        var reqFp16_32k = new LlmFitRequest(8.0, "Q4_K_M", ContextLength: 32768, KvPrecision: "FP16", AvailableVramMb: 16384, AvailableRamMb: 32768);
        var reqQ4_32k = new LlmFitRequest(8.0, "Q4_K_M", ContextLength: 32768, KvPrecision: "Q4_0", AvailableVramMb: 16384, AvailableRamMb: 32768);

        var resFp16_4k = _service.EvaluateLlmFit(reqFp16_4k);
        var resFp16_32k = _service.EvaluateLlmFit(reqFp16_32k);
        var resQ4_32k = _service.EvaluateLlmFit(reqQ4_32k);

        Assert.True(resFp16_32k.KvCacheMb > resFp16_4k.KvCacheMb * 7);
        Assert.True(resFp16_32k.KvCacheMb > resQ4_32k.KvCacheMb * 3);
    }

    [Fact]
    public void Flux1Dev_On16GbVram_DiffusionFit()
    {
        var fp8Req = new DiffusionFitRequest("Flux.1 Dev", Quantization: "FP8", Resolution: 1024, AvailableVramMb: 16384, AvailableRamMb: 32768);
        var fp16Req = new DiffusionFitRequest("Flux.1 Dev", Quantization: "FP16", Resolution: 1024, AvailableVramMb: 16384, AvailableRamMb: 32768);

        var fp8Result = _service.EvaluateDiffusionFit(fp8Req);
        var fp16Result = _service.EvaluateDiffusionFit(fp16Req);

        Assert.True(fp8Result.TotalVramMb + fp8Result.TotalRamMb < fp16Result.TotalVramMb + fp16Result.TotalRamMb);
        Assert.True(fp16Result.FitVerdict == FitVerdict.PartialOffload || fp16Result.FitVerdict == FitVerdict.FullVram);
        Assert.True(fp8Result.EstimatedSecondsPerImage > 0.0);
    }

    [Fact]
    public void Diffusion_SdxlAndSd15_FitCalculations()
    {
        var sdxl = _service.EvaluateDiffusionFit(new DiffusionFitRequest("SDXL 1.0", Quantization: "FP16", Resolution: 1024, AvailableVramMb: 12288, AvailableRamMb: 32768));
        var sd15 = _service.EvaluateDiffusionFit(new DiffusionFitRequest("SD 1.5", Quantization: "FP16", Resolution: 512, AvailableVramMb: 8192, AvailableRamMb: 16384));

        Assert.Equal(FitVerdict.FullVram, sdxl.FitVerdict);
        Assert.Equal(FitVerdict.FullVram, sd15.FitVerdict);
        Assert.True(sd15.EstimatedSecondsPerImage < sdxl.EstimatedSecondsPerImage);
    }

    [Fact]
    public void Wan22_14B_On16GbVram_VideoFit()
    {
        var req = new VideoFitRequest("Wan 2.2 14B", Quantization: "FP8", FrameCount: 49, Resolution: 720, AvailableVramMb: 16384, AvailableRamMb: 32768);
        var result = _service.EvaluateVideoFit(req);

        Assert.Equal("Wan 2.2 14B", result.ModelName);
        Assert.True(result.DiTModelMb > 10000);
        Assert.True(result.FrameContextMb > 2000);
        Assert.True(result.VaeDecodeMb > 1000);
        Assert.True(result.FitVerdict == FitVerdict.PartialOffload || result.FitVerdict == FitVerdict.FullVram);
    }

    [Fact]
    public void Video_LtxAndHunyuan_FitCalculations()
    {
        var ltx = _service.EvaluateVideoFit(new VideoFitRequest("LTX-Video", "FP16", 49, 720, AvailableVramMb: 12288, AvailableRamMb: 32768));
        var hunyuan = _service.EvaluateVideoFit(new VideoFitRequest("HunyuanVideo", "FP8", 49, 720, AvailableVramMb: 16384, AvailableRamMb: 32768));

        Assert.Equal(FitVerdict.FullVram, ltx.FitVerdict);
        Assert.True(hunyuan.FitVerdict == FitVerdict.PartialOffload || hunyuan.FitVerdict == FitVerdict.FullVram);
    }

    [Fact]
    public void Wan22_13B_On8GbVram_FullVramFit()
    {
        var req = new VideoFitRequest("Wan 2.2 1.3B", Quantization: "FP16", FrameCount: 49, Resolution: 720, AvailableVramMb: 8192, AvailableRamMb: 16384);
        var result = _service.EvaluateVideoFit(req);

        Assert.Equal(FitVerdict.FullVram, result.FitVerdict);
        Assert.True(result.TotalVramMb <= 8192);
        Assert.Equal(0, result.TotalRamMb);
    }

    [Fact]
    public void AudioEngines_KokoroAndWhisper_MemoryFootprints()
    {
        var kokoro = _service.EvaluateAudioFit("Kokoro", vramMb: 8192, ramMb: 16384);
        var whisper = _service.EvaluateAudioFit("Faster-Whisper", vramMb: 8192, ramMb: 16384);
        var xtts = _service.EvaluateAudioFit("AllTalk XTTS-v2", vramMb: 8192, ramMb: 16384);
        var musicgen = _service.EvaluateAudioFit("MusicGen", vramMb: 8192, ramMb: 16384);

        Assert.Equal(FitVerdict.FullVram, kokoro.FitVerdict);
        Assert.True(kokoro.VramRequiredMb <= 600, $"Expected Kokoro <= 600MB, got {kokoro.VramRequiredMb}");
        Assert.True(kokoro.EstimatedRealtimeFactor >= 30.0);

        Assert.Equal(FitVerdict.FullVram, whisper.FitVerdict);
        Assert.True(whisper.VramRequiredMb >= 1500 && whisper.VramRequiredMb <= 2500);

        Assert.Equal(FitVerdict.FullVram, xtts.FitVerdict);
        Assert.True(xtts.VramRequiredMb >= 2000 && xtts.VramRequiredMb <= 3000);

        Assert.Equal(FitVerdict.FullVram, musicgen.FitVerdict);
        Assert.True(musicgen.VramRequiredMb >= 3000);
    }

    [Fact]
    public void ThreeDEngines_TrellisAndHunyuan3D_MemoryFootprints()
    {
        var trellis = _service.Evaluate3DFit("TRELLIS V2", vramMb: 16384, ramMb: 32768);
        var hunyuan3d = _service.Evaluate3DFit("Hunyuan3D-2", vramMb: 12288, ramMb: 32768);

        Assert.Equal(FitVerdict.FullVram, trellis.FitVerdict);
        Assert.True(trellis.VramRequiredMb >= 10000 && trellis.VramRequiredMb <= 14000);

        Assert.Equal(FitVerdict.PartialOffload, hunyuan3d.FitVerdict);
        Assert.True(hunyuan3d.VramRequiredMb >= 14000);
    }

    [Theory]
    [InlineData("meta-llama/Llama-3.3-70B-Instruct-GGUF", "text-generation", 16384, 65536, FitVerdict.PartialOffload)]
    [InlineData("Qwen/Qwen2.5-32B-Instruct-GGUF", "text-generation", 24576, 32768, FitVerdict.FullVram)]
    [InlineData("deepseek-ai/DeepSeek-R1-GGUF", "text-generation", 8192, 16384, FitVerdict.OutOfMemory)]
    [InlineData("hexgrad/Kokoro-82M", "text-to-speech", 8192, 16384, FitVerdict.FullVram)]
    [InlineData("black-forest-labs/FLUX.1-dev", "text-to-image", 16384, 32768, FitVerdict.FullVram)]
    public void QuickFitBadge_AcrossVariousModelsAndModalities(string modelName, string modality, long vramMb, long ramMb, FitVerdict expectedVerdict)
    {
        var badge = _service.EvaluateQuickFit(modelName, fileSizeBytes: null, modality: modality, vramMb: vramMb, ramMb: ramMb);

        Assert.Equal(expectedVerdict, badge.FitVerdict);
        Assert.NotEmpty(badge.BadgeText);
        Assert.NotEmpty(badge.BadgeColorHex);
        Assert.NotEmpty(badge.Tooltip);
    }

    [Fact]
    public void QuickFitBadge_WithFileSizeBytes_CalculatesCorrectly()
    {
        // 4.5 GB file size = 4831838208 bytes
        long fileSizeBytes = 4831838208L;
        var badge = _service.EvaluateQuickFit("custom-model.gguf", fileSizeBytes, "text-generation", vramMb: 8192, ramMb: 16384);

        Assert.Equal(FitVerdict.FullVram, badge.FitVerdict);
        Assert.Contains("Full VRAM", badge.BadgeText);
    }

    [Fact]
    public void EstimateStudioHardwareFit_CalculatesAccurately()
    {
        var service = new CanIRunItService();
        // 480p Video on 16GB GPU with 12GB Free -> Ready
        var fit = service.EstimateStudioHardwareFit(StudioModality.Video, 832, 480, 48, "wan2.2", 12000, 16000);
        Assert.Equal("Ready", fit.StatusText);
        Assert.False(fit.RequiresLlmUnload);

        // 720p Video with only 4GB Free on 12GB GPU -> Requires LLM unload
        var fitTight = service.EstimateStudioHardwareFit(StudioModality.Video, 1280, 720, 48, "wan2.2", 4000, 12000);
        Assert.True(fitTight.RequiresLlmUnload);
    }

    [Fact]
    public void EstimateStudioHardwareFit_VideoExceedsGpuLimit_Recommends480pPreview()
    {
        var service = new CanIRunItService();
        // 1080p Video on 8GB GPU (estimated ~18.5GB) -> Exceeds GPU Limit
        var fitOom = service.EstimateStudioHardwareFit(StudioModality.Video, 1920, 1080, 48, "wan2.2", 4000, 8000);
        Assert.Equal("Exceeds GPU Limit", fitOom.StatusText);
        Assert.Equal("Quick 480p Preview", fitOom.RecommendedPresetName);
        Assert.Equal(FitVerdict.OutOfMemory, fitOom.FitBadge.FitVerdict);
    }

    [Fact]
    public void EstimateStudioHardwareFit_ImageModalities_ScalesByPixelArea()
    {
        var service = new CanIRunItService();
        // 1024x1024 Image Baseline ~4000 MB
        var fit1024 = service.EstimateStudioHardwareFit(StudioModality.Image, 1024, 1024, 0, "flux", 8000, 12000);
        Assert.Equal(4000, Math.Round(fit1024.EstimatedVramMb));
        Assert.Equal("Ready", fit1024.StatusText);
        Assert.False(fit1024.RequiresLlmUnload);

        // 2048x2048 Image (4x pixels) -> ~16000 MB
        var fit2048 = service.EstimateStudioHardwareFit(StudioModality.Image, 2048, 2048, 0, "flux", 8000, 12000);
        Assert.Equal(16000, Math.Round(fit2048.EstimatedVramMb));
        Assert.Equal("Exceeds GPU Limit", fit2048.StatusText);
        Assert.Equal("Quick 480p Preview", fit2048.RecommendedPresetName);
    }

    [Fact]
    public void EstimateStudioHardwareFit_AudioModalities_CalculatesKokoroAndStableAudio()
    {
        var service = new CanIRunItService();
        // Kokoro TTS ~1500 MB
        var fitKokoro = service.EstimateStudioHardwareFit(StudioModality.Audio, 0, 0, 0, "kokoro", 2000, 8000);
        Assert.Equal(1500, Math.Round(fitKokoro.EstimatedVramMb));
        Assert.Equal("Ready", fitKokoro.StatusText);
        Assert.False(fitKokoro.RequiresLlmUnload);

        // Stable Audio / MusicGen ~2500 MB
        var fitStable = service.EstimateStudioHardwareFit(StudioModality.Audio, 0, 0, 0, "stable-audio", 1000, 8000);
        Assert.Equal(2500, Math.Round(fitStable.EstimatedVramMb));
        Assert.Equal("Tight Fit", fitStable.StatusText);
        Assert.True(fitStable.RequiresLlmUnload);
    }
}

