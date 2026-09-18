using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AiAppToolsTests
{
    private readonly Mock<IGpuTelemetryProvider> _mockTelemetry = new();
    private readonly Mock<IAiEngineManager> _mockEngine = new();
    private readonly Mock<IOllamaModelService> _mockOllama = new();
    private readonly Mock<ISettingsService> _mockSettings = new();
    private readonly Mock<ICanIRunItService> _mockCanIRunItService = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();

    private AiAppTools CreateTools(HttpMessageHandler? handler = null, AppSettings? customSettings = null)
    {
        var httpHandler = handler ?? new MockHttpMessageHandler(HttpStatusCode.OK, "{}");
        var client = new HttpClient(httpHandler);
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        _mockSettings.Setup(s => s.LoadSettings()).Returns(customSettings ?? new AppSettings());

        return new AiAppTools(
            _mockTelemetry.Object,
            _mockEngine.Object,
            _mockOllama.Object,
            _mockSettings.Object,
            _mockCanIRunItService.Object,
            _mockHttpFactory.Object
        );
    }

    [Fact]
    public async Task GetGpuVramTelemetry_ReturnsValidJsonTelemetry()
    {
        _mockTelemetry.Setup(t => t.GetTelemetryAsync())
            .ReturnsAsync(new GpuTelemetryResult("NVIDIA RTX 4090", 24576, 4096, 20480, 16.7));

        var tools = CreateTools();
        var json = await tools.GetGpuVramTelemetryAsync();

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("NVIDIA RTX 4090", json);
        Assert.Contains("24576", json);
    }

    [Fact]
    public async Task ListInstalledModels_ReturnsJsonModelList()
    {
        _mockOllama.Setup(o => o.GetInstalledModelsAsync())
            .ReturnsAsync(new List<OllamaModelItem>
            {
                new("llama3.2:latest", "4.7 GB", "General", "#00FF00", true)
            });

        var tools = CreateTools();
        var json = await tools.ListInstalledModelsAsync();

        Assert.Contains("llama3.2:latest", json);
        Assert.Contains("4.7 GB", json);
    }

    [Fact]
    public async Task StartAiEngine_CallsEngineManagerAndReturnsResult()
    {
        _mockEngine.Setup(e => e.StartEngineAsync("comfyui"))
            .ReturnsAsync(new EngineOperationResult(true, "comfyui", "Started"));

        var tools = CreateTools();
        var json = await tools.StartAiEngineAsync("comfyui");

        Assert.Contains("comfyui", json);
        Assert.Contains("Started", json);
    }

    [Fact]
    public async Task StopAiEngine_CallsEngineManagerAndReturnsResult()
    {
        _mockEngine.Setup(e => e.StopEngineAsync("forge"))
            .ReturnsAsync(new EngineOperationResult(true, "forge", "Stopped"));

        var tools = CreateTools();
        var json = await tools.StopAiEngineAsync("forge");

        Assert.Contains("forge", json);
        Assert.Contains("Stopped", json);
    }

    [Fact]
    public async Task GetAppSettings_ReturnsMaskedApiKey()
    {
        var tools = CreateTools(customSettings: new AppSettings(
            AiAssistantApiKey: "secret-production-key-12345",
            PreferredImageEngine: "comfy"
        ));
        var json = await tools.GetAppSettingsAsync();

        Assert.Contains("******", json);
        Assert.DoesNotContain("secret-production-key-12345", json);
        Assert.Contains("comfy", json);
    }

    [Fact]
    public async Task UpdateAppSetting_UpdatesKnownKeyAndPersists()
    {
        var initial = new AppSettings(PreferredImageEngine: "forge");
        _mockSettings.Setup(s => s.LoadSettings()).Returns(initial);

        AppSettings? saved = null;
        _mockSettings.Setup(s => s.SaveSettings(It.IsAny<AppSettings>()))
            .Callback<AppSettings>(s => saved = s);

        var tools = CreateTools();
        var json = await tools.UpdateAppSettingAsync("PreferredImageEngine", "comfy");

        Assert.Contains("true", json);
        Assert.NotNull(saved);
        Assert.Equal("comfy", saved!.PreferredImageEngine);
    }

    [Fact]
    public async Task CalculateHardwareFit_CallsCanIRunItService()
    {
        _mockTelemetry.Setup(t => t.GetTelemetryAsync())
            .ReturnsAsync(new GpuTelemetryResult("NVIDIA RTX 4090", 24576, 4096, 20480, 16.7));

        _mockCanIRunItService.Setup(c => c.EvaluateLlmFit(It.IsAny<LlmFitRequest>()))
            .Returns(new LlmFitResult(
                ModelWeightMb: 16000,
                KvCacheMb: 1000,
                OverheadMb: 600,
                TotalVramMb: 17600,
                TotalRamMb: 0,
                GpuLayers: 64,
                CpuLayers: 0,
                TotalLayers: 64,
                FitVerdict: FitVerdict.FullVram,
                EstimatedTokPerSec: 25.0,
                RecommendationMessage: "100% in GPU VRAM"
            ));

        var tools = CreateTools();
        var json = await tools.CalculateHardwareFitAsync("llama3.3:70b", parametersBillions: 70.0, quantization: "Q4_K_M");

        Assert.Contains("FullVram", json);
        Assert.Contains("100% in GPU VRAM", json);
    }

    [Fact]
    public async Task QueryAppDocumentation_ReturnsMatchingProcedures()
    {
        var tools = CreateTools();
        var json = await tools.QueryAppDocumentationAsync("image");

        Assert.False(string.IsNullOrWhiteSpace(json));
        Assert.Contains("Image", json, StringComparison.OrdinalIgnoreCase);
    }
}
