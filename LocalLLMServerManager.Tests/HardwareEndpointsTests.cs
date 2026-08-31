using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalLLMServerManager.Shared.Models;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class HardwareEndpointsTests : IClassFixture<AppTestServerFixture>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public HardwareEndpointsTests(AppTestServerFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetHardwareFit_DefaultLlm_Returns200AndLlmFitResult()
    {
        var response = await _client.GetAsync("/api/hardware/fit?params=70&quant=q4_k_m&context=8192&vram_mb=16384&ram_mb=65536");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LlmFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(80, result.TotalLayers);
        Assert.Equal(FitVerdict.PartialOffload, result.FitVerdict);
        Assert.True(result.GpuLayers > 0);
        Assert.True(result.CpuLayers > 0);
        Assert.True(result.ModelWeightMb > 0);
        Assert.True(result.KvCacheMb > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.RecommendationMessage));
    }

    [Fact]
    public async Task GetHardwareFit_ExplicitVramAndRam_FullVramFit()
    {
        var response = await _client.GetAsync("/api/hardware/fit?params=32&quant=q4_k_m&context=4096&vram_mb=24576&ram_mb=32768");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LlmFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(64, result.TotalLayers);
        Assert.Equal(64, result.GpuLayers);
        Assert.Equal(0, result.CpuLayers);
        Assert.Equal(FitVerdict.FullVram, result.FitVerdict);
    }

    [Fact]
    public async Task GetHardwareFit_DiffusionModality_ReturnsDiffusionFitResult()
    {
        var response = await _client.GetAsync("/api/hardware/fit?modality=diffusion&model_name=Flux.1+Dev&quant=FP8&context=1024&vram_mb=16384&ram_mb=32768");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DiffusionFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Contains("Flux", result.ModelName);
        Assert.True(result.BaseModelMb > 0);
        Assert.True(result.EstimatedSecondsPerImage > 0.0);
    }

    [Fact]
    public async Task GetHardwareFit_VideoModality_ReturnsVideoFitResult()
    {
        var response = await _client.GetAsync("/api/hardware/fit?modality=video&model_name=Wan+2.2+14B&quant=FP8&vram_mb=16384&ram_mb=32768");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<VideoFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Contains("Wan", result.ModelName);
        Assert.True(result.DiTModelMb > 0);
        Assert.True(result.EstimatedSecondsPerFrame > 0.0);
    }

    [Fact]
    public async Task GetHardwareFit_AudioModality_ReturnsAudioFitResult()
    {
        var response = await _client.GetAsync("/api/hardware/fit?modality=audio&model_name=Kokoro&vram_mb=8192&ram_mb=16384");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AudioFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("Kokoro", result.EngineName);
        Assert.Equal(FitVerdict.FullVram, result.FitVerdict);
    }

    [Fact]
    public async Task GetHardwareFit_ThreeDModality_ReturnsThreeDFitResult()
    {
        var response = await _client.GetAsync("/api/hardware/fit?modality=3d&model_name=TRELLIS&vram_mb=16384&ram_mb=32768");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ThreeDFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("TRELLIS", result.ModelName);
        Assert.Equal(FitVerdict.FullVram, result.FitVerdict);
    }

    [Fact]
    public async Task GetHardwareFit_BadgeModality_ReturnsQuickFitBadge()
    {
        var response = await _client.GetAsync("/api/hardware/fit?modality=badge&model_name=llama3:8b&size_bytes=4500000000&vram_mb=16384&ram_mb=32768");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<QuickFitBadge>(JsonOptions);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.BadgeText));
        Assert.StartsWith("#", result.BadgeColorHex);
    }

    [Fact]
    public async Task GetHardwareFit_InvalidQueryParam_DoesNotThrow500()
    {
        var response = await _client.GetAsync("/api/hardware/fit?params=invalid_number&context=invalid_int");
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        // Either 200 OK (with fallback defaults) or 400 Bad Request
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostHardwareEvaluate_LlmFitRequest_Returns200AndLlmFitResult()
    {
        var request = new LlmFitRequest(
            ParametersBillions: 70.0,
            Quantization: "Q4_K_M",
            ContextLength: 8192,
            KvPrecision: "FP16",
            AvailableVramMb: 16384,
            AvailableRamMb: 65536
        );

        var response = await _client.PostAsJsonAsync("/api/hardware/evaluate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<LlmFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(FitVerdict.PartialOffload, result.FitVerdict);
        Assert.Equal(80, result.TotalLayers);
    }

    [Fact]
    public async Task PostHardwareEvaluate_DiffusionFitRequest_Returns200AndDiffusionFitResult()
    {
        var request = new DiffusionFitRequest(
            ModelName: "Flux.1 Dev",
            Quantization: "FP8",
            Resolution: 1024,
            AvailableVramMb: 16384,
            AvailableRamMb: 32768
        );

        var response = await _client.PostAsJsonAsync("/api/hardware/evaluate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DiffusionFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("Flux.1 Dev", result.ModelName);
        Assert.True(result.EstimatedSecondsPerImage > 0.0);
    }

    [Fact]
    public async Task PostHardwareEvaluate_VideoFitRequest_Returns200AndVideoFitResult()
    {
        var request = new VideoFitRequest(
            ModelName: "Wan 2.2 14B",
            Quantization: "FP8",
            FrameCount: 49,
            Resolution: 720,
            AvailableVramMb: 16384,
            AvailableRamMb: 32768
        );

        var response = await _client.PostAsJsonAsync("/api/hardware/evaluate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<VideoFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("Wan 2.2 14B", result.ModelName);
    }

    [Fact]
    public async Task PostHardwareEvaluate_AudioRequest_Returns200AndAudioFitResult()
    {
        var payload = new
        {
            modality = "audio",
            engineName = "Kokoro",
            availableVramMb = 8192,
            availableRamMb = 16384
        };

        var response = await _client.PostAsJsonAsync("/api/hardware/evaluate", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AudioFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("Kokoro", result.EngineName);
        Assert.Equal(FitVerdict.FullVram, result.FitVerdict);
    }

    [Fact]
    public async Task PostHardwareEvaluate_ThreeDRequest_Returns200AndThreeDFitResult()
    {
        var payload = new
        {
            modality = "3d",
            modelName = "TRELLIS",
            availableVramMb = 16384,
            availableRamMb = 32768
        };

        var response = await _client.PostAsJsonAsync("/api/hardware/evaluate", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ThreeDFitResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal("TRELLIS", result.ModelName);
        Assert.Equal(FitVerdict.FullVram, result.FitVerdict);
    }

    [Fact]
    public async Task PostHardwareEvaluate_BadgeRequest_Returns200AndQuickFitBadge()
    {
        var payload = new
        {
            modality = "badge",
            modelName = "Qwen2.5-7B",
            fileSizeBytes = 4500000000L,
            availableVramMb = 16384,
            availableRamMb = 32768
        };

        var response = await _client.PostAsJsonAsync("/api/hardware/evaluate", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<QuickFitBadge>(JsonOptions);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.BadgeText));
    }

    [Fact]
    public async Task PostHardwareEvaluate_InvalidJson_Returns400BadRequest()
    {
        var content = new StringContent("{ invalid_json: true", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/hardware/evaluate", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostHardwareEvaluate_InvalidContentType_Returns400BadRequest()
    {
        var content = new StringContent("plain text body", System.Text.Encoding.UTF8, "text/plain");
        var response = await _client.PostAsync("/api/hardware/evaluate", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
