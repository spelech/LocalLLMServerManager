using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class VramOrchestratorTests
{
    [Fact]
    public async Task IsOllamaHealthyAsync_ReturnsTrue_WhenApiReturnsSuccess()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Ollama is running")
            });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        var isHealthy = await orchestrator.IsOllamaHealthyAsync();

        Assert.True(isHealthy);
    }

    [Fact]
    public async Task IsOllamaHealthyAsync_ReturnsFalse_WhenApiFails()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        var isHealthy = await orchestrator.IsOllamaHealthyAsync();

        Assert.False(isHealthy);
    }

    [Fact]
    public async Task IsForgeHealthyAsync_ReturnsTrue_WhenProgressEndpointReturns200()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/sdapi/v1/progress")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        var isHealthy = await orchestrator.IsForgeHealthyAsync();

        Assert.True(isHealthy);
    }

    [Fact]
    public async Task IsComfyUiHealthyAsync_ReturnsTrue_WhenSystemStatsReturns200()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/system_stats")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        var isHealthy = await orchestrator.IsComfyUiHealthyAsync("http://127.0.0.1:8188");

        Assert.True(isHealthy);
    }

    [Fact]
    public async Task FreeComfyUiVramAsync_SendsPostToFreeEndpoint_WhenHealthy()
    {
        var capturedRequests = new System.Collections.Generic.List<HttpRequestMessage>();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequests.Add(req))
            .ReturnsAsync(() => new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        var result = await orchestrator.FreeComfyUiVramAsync("http://127.0.0.1:8188");

        Assert.True(result);
        Assert.Equal(2, capturedRequests.Count);
        Assert.Equal(HttpMethod.Get, capturedRequests[0].Method);
        Assert.Equal("http://127.0.0.1:8188/system_stats", capturedRequests[0].RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, capturedRequests[1].Method);
        Assert.Equal("http://127.0.0.1:8188/free", capturedRequests[1].RequestUri?.ToString());
    }

    [Fact]
    public async Task FreeComfyUiVramAsync_DoesNotPostToFree_WhenComfyUiIsOffline()
    {
        var capturedRequests = new System.Collections.Generic.List<HttpRequestMessage>();

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequests.Add(req))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        var result = await orchestrator.FreeComfyUiVramAsync("http://127.0.0.1:8188");

        Assert.False(result);
        Assert.Single(capturedRequests);
        Assert.Equal(HttpMethod.Get, capturedRequests[0].Method);
        Assert.Equal("http://127.0.0.1:8188/system_stats", capturedRequests[0].RequestUri?.ToString());
        Assert.DoesNotContain(capturedRequests, r => r.RequestUri!.ToString().EndsWith("/free"));
    }

    [Fact]
    public async Task EvaluateCacheLifetimesAsync_ExecutesEvictionUnderRamPressure_EvenAfterTier1EvictionLatch()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                var uri = req.RequestUri?.ToString() ?? "";
                if (uri.Contains("11434/api/ps"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"models\":[{\"name\":\"llama3:latest\"}]}")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"status\":\"ok\"}")
                };
            });

        var settingsMock = new Mock<LocalLLMServerManager.Shared.Interfaces.ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(
            ModelCacheIdleTimeoutMinutes: 15,
            EngineHibernationTimeoutMinutes: 30,
            RamPressureThresholdPercent: 75
        ));

        int currentRamPct = 40;
        var telemetryMock = new Mock<LocalLLMServerManager.Services.IGpuTelemetryProvider>();
        telemetryMock.Setup(t => t.GetSystemMemoryInfo()).Returns(() => new LocalLLMServerManager.Services.SystemMemorySnapshot(
            TotalRamBytes: 32L * 1024 * 1024 * 1024,
            UsedRamBytes: 16L * 1024 * 1024 * 1024,
            AvailableRamBytes: 16L * 1024 * 1024 * 1024,
            RamUsedPercent: currentRamPct,
            ProcessWorkingSetBytes: 256L * 1024 * 1024,
            NonPagedPoolBytes: 128L * 1024 * 1024
        ));

        var engineMock = new Mock<LocalLLMServerManager.Services.IAiEngineManager>();
        var orchestrator = new VramOrchestrator(
            new HttpClient(handlerMock.Object),
            NullLogger<VramOrchestrator>.Instance,
            settingsMock.Object,
            telemetryMock.Object,
            engineMock.Object,
            timeProvider
        );

        // 1. Advance 16m under normal RAM (40%) -> Tier 1 eviction runs and sets _tier1Evicted = true
        timeProvider.Advance(TimeSpan.FromMinutes(16));
        var tier1Result = await orchestrator.EvaluateCacheLifetimesAsync();
        Assert.True(tier1Result.OllamaUnloaded);
        Assert.True(tier1Result.ComfyVramFreed);

        // 2. Now RAM pressure spikes (85% >= 75%) without RecordActivity being called -> must NOT be latched out
        currentRamPct = 85;
        timeProvider.Advance(TimeSpan.FromMinutes(3));
        var pressureResult = await orchestrator.EvaluateCacheLifetimesAsync();
        Assert.True(pressureResult.OllamaUnloaded);
        Assert.True(pressureResult.ComfyVramFreed);
    }

    [Fact]
    public async Task EvaluateCacheLifetimesAsync_RehibernatesEngine_WhenProcessBecomesActiveAgainAfterHibernation()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"models\":[]}")
            });

        var settingsMock = new Mock<LocalLLMServerManager.Shared.Interfaces.ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(
            ModelCacheIdleTimeoutMinutes: 15,
            EngineHibernationTimeoutMinutes: 30,
            RamPressureThresholdPercent: 75
        ));

        var telemetryMock = new Mock<LocalLLMServerManager.Services.IGpuTelemetryProvider>();
        telemetryMock.Setup(t => t.GetSystemMemoryInfo()).Returns(new LocalLLMServerManager.Services.SystemMemorySnapshot(
            TotalRamBytes: 32L * 1024 * 1024 * 1024,
            UsedRamBytes: 12L * 1024 * 1024 * 1024,
            AvailableRamBytes: 20L * 1024 * 1024 * 1024,
            RamUsedPercent: 40,
            ProcessWorkingSetBytes: 256L * 1024 * 1024,
            NonPagedPoolBytes: 128L * 1024 * 1024
        ));

        using var activeCurrentProcess = System.Diagnostics.Process.GetCurrentProcess();
        System.Diagnostics.Process? mockComfyProcess = null;

        var engineMock = new Mock<LocalLLMServerManager.Services.IAiEngineManager>();
        engineMock.Setup(e => e.ComfyProcess).Returns(() => mockComfyProcess);
        engineMock.Setup(e => e.StopComfyUiAsync(It.IsAny<Microsoft.Extensions.Logging.ILogger>())).ReturnsAsync(true);
        engineMock.Setup(e => e.StopForgeAsync(It.IsAny<Microsoft.Extensions.Logging.ILogger>())).ReturnsAsync(true);
        engineMock.Setup(e => e.StopAudioEngineAsync(It.IsAny<Microsoft.Extensions.Logging.ILogger>())).ReturnsAsync(true);

        var orchestrator = new VramOrchestrator(
            new HttpClient(handlerMock.Object),
            NullLogger<VramOrchestrator>.Instance,
            settingsMock.Object,
            telemetryMock.Object,
            engineMock.Object,
            timeProvider
        );

        // 1. Advance 31m -> initial Tier 2 hibernation runs and sets _tier2Hibernated["comfy"] = true
        timeProvider.Advance(TimeSpan.FromMinutes(31));
        var firstHibernation = await orchestrator.EvaluateCacheLifetimesAsync();
        Assert.Contains("comfy", firstHibernation.HibernatedEngines);
        engineMock.Verify(e => e.StopComfyUiAsync(It.IsAny<Microsoft.Extensions.Logging.ILogger>()), Times.Once);

        // 2. ComfyUI process becomes active again externally without RecordActivity being called
        mockComfyProcess = activeCurrentProcess;
        timeProvider.Advance(TimeSpan.FromMinutes(31));
        var secondHibernation = await orchestrator.EvaluateCacheLifetimesAsync();
        Assert.Contains("comfy", secondHibernation.HibernatedEngines);
        engineMock.Verify(e => e.StopComfyUiAsync(It.IsAny<Microsoft.Extensions.Logging.ILogger>()), Times.Exactly(2));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset startUtc) => _utcNow = startUtc;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    [Fact]
    public async Task EnsureVramForImageGenerationAsync_ExecutesCleanly()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() => new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        await orchestrator.EnsureVramForImageGenerationAsync();
    }

    [Fact]
    public async Task EnsureVramForComfyUiAsync_ExecutesCleanly()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(() => new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        await orchestrator.EnsureVramForComfyUiAsync();
    }
}
