using System;
using System.Linq;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class CanIRunItViewModelTests
{
    [Fact]
    public void InitialState_DefaultValues_CalculatesLlmFitCorrectly()
    {
        var vm = new CanIRunItViewModel();

        Assert.Equal("NVIDIA GeForce RTX 4070 Ti SUPER", vm.GpuName);
        Assert.Equal(16384.0, vm.TotalVramMb);
        Assert.Equal(16384.0, vm.FreeVramMb);
        Assert.Equal(32768.0, vm.TotalRamMb);
        Assert.Equal(32768.0, vm.AvailableRamMb);
        Assert.Equal("16.0 GB VRAM", vm.VramText);
        Assert.Equal("32.0 GB RAM", vm.RamText);

        Assert.Equal("LLM", vm.SelectedModality);
        Assert.Equal("Llama 3.1 8B", vm.SelectedPreset);
        Assert.Equal(8.0, vm.ParametersBillions);
        Assert.Equal("Q4_K_M", vm.SelectedQuantization);
        Assert.Equal(4096, vm.ContextLength);
        Assert.Equal("FP16", vm.KvCachePrecision);

        Assert.NotNull(vm.LlmResult);
        Assert.Equal(FitVerdict.FullVram, vm.LlmResult.FitVerdict);
        Assert.NotNull(vm.FitVerdictBadge);
        Assert.Equal(FitVerdict.FullVram, vm.FitVerdictBadge.FitVerdict);

        Assert.True(vm.ModelWeightsVramPercent > 0.0);
        Assert.True(vm.KvCacheVramPercent > 0.0);
        Assert.True(vm.OverheadVramPercent > 0.0);
        Assert.True(vm.FreeVramPercent > 0.0);
        double totalBar = vm.ModelWeightsVramPercent + vm.KvCacheVramPercent + vm.OverheadVramPercent + vm.FreeVramPercent;
        Assert.Equal(100.0, totalBar, tolerance: 0.5);

        Assert.Contains("Fits 100% in VRAM", vm.VerdictSummaryText);
        Assert.Contains("32 / 32 Layers in VRAM", vm.OffloadSummaryText);
        Assert.Contains("100% in GPU VRAM", vm.RecommendationText);
        Assert.NotEmpty(vm.SpeedEstimationText);
    }

    [Fact]
    public void PresetSwitching_Llama33_70B_UpdatesParametersAndRecalculates()
    {
        var vm = new CanIRunItViewModel();

        vm.SelectedPreset = "Llama 3.3 70B";

        Assert.Equal(70.0, vm.ParametersBillions);
        Assert.NotNull(vm.LlmResult);
        Assert.Equal(FitVerdict.PartialOffload, vm.LlmResult.FitVerdict);
        Assert.Equal(80, vm.LlmResult.TotalLayers);
        Assert.True(vm.LlmResult.GpuLayers > 0 && vm.LlmResult.GpuLayers < 80);
        Assert.Contains("Partial Offload", vm.VerdictSummaryText);
        Assert.Contains("Layers in VRAM", vm.OffloadSummaryText);
    }

    [Fact]
    public void PresetSwitching_DeepSeekR1_671B_On16GbVram_VerdictOomOrCpu()
    {
        var vm = new CanIRunItViewModel();

        vm.SelectedPreset = "DeepSeek R1 671B";

        Assert.Equal(671.0, vm.ParametersBillions);
        Assert.NotNull(vm.LlmResult);
        Assert.Equal(FitVerdict.OutOfMemory, vm.LlmResult.FitVerdict);
        Assert.Contains("Out of Memory", vm.VerdictSummaryText);
        Assert.Equal("🔴 Won't Fit (OOM)", vm.FitVerdictBadge?.BadgeText);
    }

    [Fact]
    public void SliderAdjustments_QuantizationAndContextLength_RecalculatesImmediately()
    {
        var vm = new CanIRunItViewModel();
        double initialWeightPercent = vm.ModelWeightsVramPercent;
        double initialKvPercent = vm.KvCacheVramPercent;

        // Changing quantization to FP16 should increase weight percent
        vm.SelectedQuantization = "FP16";
        Assert.True(vm.ModelWeightsVramPercent > initialWeightPercent);
        Assert.Equal("Custom", vm.SelectedPreset);

        // Changing context length to 65536 should increase KV cache percent
        vm.ContextLength = 65536;
        Assert.True(vm.KvCacheVramPercent > initialKvPercent);
    }

    [Fact]
    public void ModalitySwitching_ImageModality_CalculatesDiffusionFit()
    {
        var vm = new CanIRunItViewModel();

        vm.SelectImageModalityCommand.Execute(null);

        Assert.Equal("Image", vm.SelectedModality);
        Assert.NotNull(vm.DiffusionResult);
        Assert.Equal("Flux.1 Dev", vm.DiffusionResult.ModelName);
        Assert.Equal(FitVerdict.FullVram, vm.DiffusionResult.FitVerdict);
        Assert.Contains("s / image", vm.SpeedEstimationText);

        // Switch image preset to SD 1.5
        vm.SelectedImagePreset = "SD 1.5";
        Assert.NotNull(vm.DiffusionResult);
        Assert.Equal("SD 1.5", vm.DiffusionResult.ModelName);
        Assert.Equal(512, vm.SelectedImageResolution);
    }

    [Fact]
    public void ModalitySwitching_VideoModality_CalculatesVideoFit()
    {
        var vm = new CanIRunItViewModel();

        vm.SelectVideoModalityCommand.Execute(null);

        Assert.Equal("Video", vm.SelectedModality);
        Assert.NotNull(vm.VideoResult);
        Assert.Equal("Wan 2.2 14B", vm.VideoResult.ModelName);
        Assert.Contains("s / frame", vm.SpeedEstimationText);

        // Change frame count
        vm.VideoFrameCount = 129;
        Assert.NotNull(vm.VideoResult);
        Assert.True(vm.VideoResult.FrameContextMb > 5000);
    }

    [Fact]
    public void ModalitySwitching_AudioAnd3DModalities_CalculatesFit()
    {
        var vm = new CanIRunItViewModel();

        vm.SelectAudioModalityCommand.Execute(null);
        Assert.Equal("Audio", vm.SelectedModality);
        Assert.NotNull(vm.AudioResult);
        Assert.Equal("Kokoro TTS", vm.AudioResult.EngineName);
        Assert.Equal(FitVerdict.FullVram, vm.AudioResult.FitVerdict);
        Assert.Contains("Realtime", vm.SpeedEstimationText);

        vm.SelectThreeDModalityCommand.Execute(null);
        Assert.Equal("ThreeD", vm.SelectedModality);
        Assert.NotNull(vm.ThreeDResult);
        Assert.Equal("TRELLIS V2", vm.ThreeDResult.ModelName);
        Assert.Equal(FitVerdict.FullVram, vm.ThreeDResult.FitVerdict);
        Assert.Contains("s / mesh", vm.SpeedEstimationText);
    }

    [Fact]
    public void TelemetryUpdate_RefreshesHardwareAndRecalculates()
    {
        var vm = new CanIRunItViewModel();
        vm.SelectedPreset = "Llama 3.1 8B";
        vm.SelectedQuantization = "FP16"; // 8B FP16 requires ~16-17GB VRAM

        // On 16GB VRAM, might be PartialOffload or FullVram
        // Now update telemetry to 8GB VRAM (8192 MB)
        vm.UpdateHardwareTelemetry(8192, 8192, 32768, 32768, "NVIDIA GeForce RTX 3070");

        Assert.Equal("NVIDIA GeForce RTX 3070", vm.GpuName);
        Assert.Equal(8192.0, vm.TotalVramMb);
        Assert.Equal("8.0 GB VRAM", vm.VramText);
        Assert.NotNull(vm.LlmResult);
        Assert.Equal(FitVerdict.PartialOffload, vm.LlmResult.FitVerdict);
    }

    [Fact]
    public void UpdateHardwareTelemetry_WithTelemetryInfo_UpdatesState()
    {
        var vm = new CanIRunItViewModel();
        var info = new TelemetryInfo("RTX 3060 12GB", 12288.0, 10240.0, 2048.0, 65536.0, 48000.0);

        vm.UpdateHardwareTelemetry(info);

        Assert.Equal("RTX 3060 12GB", vm.GpuName);
        Assert.Equal(12288.0, vm.TotalVramMb);
        Assert.Equal(10240.0, vm.FreeVramMb);
        Assert.Equal(2048.0, vm.UsedVramMb);
        Assert.Equal(65536.0, vm.TotalRamMb);
        Assert.Equal(48000.0, vm.AvailableRamMb);
        Assert.Equal("12.0 GB VRAM", vm.VramText);
        Assert.Equal("64.0 GB RAM", vm.RamText);
    }

    [Fact]
    public void UpdateHardwareTelemetry_WithGpuTelemetryInfo_UpdatesState()
    {
        var vm = new CanIRunItViewModel();
        var gpuInfo = new GpuTelemetryInfo("NVIDIA GeForce RTX 4090", 4.0, 24.0, 20.0, 17);

        vm.UpdateHardwareTelemetry(gpuInfo);

        Assert.Equal("NVIDIA GeForce RTX 4090", vm.GpuName);
        Assert.Equal(24576.0, vm.TotalVramMb);
        Assert.Equal(20480.0, vm.FreeVramMb);
        Assert.Equal(4096.0, vm.UsedVramMb);
        Assert.Equal("24.0 GB VRAM", vm.VramText);
    }

    [Fact]
    public void InspectModel_LlmModel_PreselectsPresetOrCustomParameters()
    {
        var vm = new CanIRunItViewModel();

        vm.InspectModel("meta-llama/Llama-3.3-70B-Instruct-GGUF", "LLM");

        Assert.Equal("LLM", vm.SelectedModality);
        Assert.Equal(70.0, vm.ParametersBillions);
        Assert.NotNull(vm.LlmResult);
    }

    [Fact]
    public void InspectModel_DiffusionModel_PreselectsImagePreset()
    {
        var vm = new CanIRunItViewModel();

        vm.InspectModel("black-forest-labs/FLUX.1-dev", "Image");

        Assert.Equal("Image", vm.SelectedModality);
        Assert.Equal("Flux.1 Dev", vm.SelectedImagePreset);
        Assert.NotNull(vm.DiffusionResult);
    }

    [Fact]
    public void InspectModel_VideoModel_PreselectsVideoPreset()
    {
        var vm = new CanIRunItViewModel();

        vm.InspectModel("Wan-AI/Wan2.2-T2V-14B", "Video");

        Assert.Equal("Video", vm.SelectedModality);
        Assert.Equal("Wan 2.2 14B", vm.SelectedVideoPreset);
        Assert.NotNull(vm.VideoResult);
    }

    [Fact]
    public void InspectModel_AudioModel_PreselectsAudioPreset()
    {
        var vm = new CanIRunItViewModel();

        vm.InspectModel("hexgrad/Kokoro-82M", "Audio");

        Assert.Equal("Audio", vm.SelectedModality);
        Assert.Equal("Kokoro TTS", vm.SelectedAudioPreset);
        Assert.NotNull(vm.AudioResult);
    }

    [Fact]
    public void InspectModel_ThreeDModel_PreselectsThreeDPreset()
    {
        var vm = new CanIRunItViewModel();

        vm.InspectModel("JeffreyXiang/TRELLIS-image-large", "ThreeD");

        Assert.Equal("ThreeD", vm.SelectedModality);
        Assert.Equal("TRELLIS V2", vm.SelectedThreeDPreset);
        Assert.NotNull(vm.ThreeDResult);
    }

    [Fact]
    public void InspectModel_WithFileSizeBytes_CustomEvaluation()
    {
        var vm = new CanIRunItViewModel();

        // 4.5 GB file size = 4831838208 bytes
        vm.InspectModel("unregistered-quant.gguf", "LLM", fileSizeBytes: 4831838208L);

        Assert.Equal("LLM", vm.SelectedModality);
        Assert.NotNull(vm.LlmResult);
        Assert.NotNull(vm.FitVerdictBadge);
    }

    [Fact]
    public void AllPresetOptions_AvailableInCatalog()
    {
        var vm = new CanIRunItViewModel();

        // LLM presets
        Assert.Contains("Llama 3.3 70B", vm.AvailableLlmPresets);
        Assert.Contains("Llama 3.1 8B", vm.AvailableLlmPresets);
        Assert.Contains("Qwen 2.5 32B", vm.AvailableLlmPresets);
        Assert.Contains("Qwen 2.5 72B", vm.AvailableLlmPresets);
        Assert.Contains("DeepSeek R1 32B", vm.AvailableLlmPresets);
        Assert.Contains("DeepSeek R1 70B", vm.AvailableLlmPresets);
        Assert.Contains("DeepSeek R1 671B", vm.AvailableLlmPresets);
        Assert.Contains("Mistral Small 24B", vm.AvailableLlmPresets);
        Assert.Contains("Gemma 2 9B", vm.AvailableLlmPresets);
        Assert.Contains("Gemma 2 27B", vm.AvailableLlmPresets);
        Assert.Contains("Phi-4 14B", vm.AvailableLlmPresets);
        Assert.Contains("Custom", vm.AvailableLlmPresets);

        // Image presets
        Assert.Contains("Flux.1 Dev", vm.AvailableImagePresets);
        Assert.Contains("Flux.1 Schnell", vm.AvailableImagePresets);
        Assert.Contains("SDXL", vm.AvailableImagePresets);
        Assert.Contains("SD 3.5 Large", vm.AvailableImagePresets);
        Assert.Contains("SD 1.5", vm.AvailableImagePresets);
        Assert.Contains("Custom", vm.AvailableImagePresets);

        // Video presets
        Assert.Contains("Wan 2.2 14B", vm.AvailableVideoPresets);
        Assert.Contains("Wan 2.2 1.3B", vm.AvailableVideoPresets);
        Assert.Contains("LTX-Video", vm.AvailableVideoPresets);
        Assert.Contains("HunyuanVideo", vm.AvailableVideoPresets);

        // Audio presets
        Assert.Contains("Kokoro TTS", vm.AvailableAudioPresets);
        Assert.Contains("Faster-Whisper Large-v3-Turbo", vm.AvailableAudioPresets);
        Assert.Contains("AllTalk XTTS-v2", vm.AvailableAudioPresets);
        Assert.Contains("MusicGen Melody", vm.AvailableAudioPresets);

        // 3D presets
        Assert.Contains("TRELLIS V2", vm.AvailableThreeDPresets);
        Assert.Contains("Hunyuan3D-2", vm.AvailableThreeDPresets);
    }
}
