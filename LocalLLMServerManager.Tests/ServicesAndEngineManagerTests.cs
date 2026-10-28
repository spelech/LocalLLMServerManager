using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ServicesAndEngineManagerTests : IClassFixture<AppTestServerFixture>
{
    private readonly HttpClient _client;

    public ServicesAndEngineManagerTests(AppTestServerFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public void GpuTelemetryProvider_ParsesVariousTelemetrySources()
    {
        var (gpuName, totalVram, usedVram) = Program.GetGpuInfo();
        Assert.NotNull(gpuName);

        var regInfo = Program.GetGpuInfoFromRegistry();
        Assert.NotNull(regInfo.GpuName);

        var linuxInfo = Program.GetLinuxMemoryInfo();
        // May be null on Windows, should not throw
        if (OperatingSystem.IsLinux())
        {
            Assert.NotNull(linuxInfo);
        }

        var sampleNvidiaSmi = "NVIDIA GeForce RTX 4070 Ti SUPER, 16376, 1024";
        var parsed = Program.ParseNvidiaSmiOutput(sampleNvidiaSmi);
        Assert.NotNull(parsed);
        Assert.Contains("4070 Ti SUPER", parsed.Value.GpuName);
        Assert.True(parsed.Value.TotalVramBytes > 0);
        Assert.True(parsed.Value.UsedVramBytes > 0);
    }

    [Fact]
    public void GitUpdateService_IsValidBranchName_ValidatesCorrectly()
    {
        var service = new GitUpdateService();

        Assert.True(service.IsValidBranchName("main"));
        Assert.True(service.IsValidBranchName("feature/cool-stuff"));
        Assert.True(service.IsValidBranchName("fix_bug_123"));

        Assert.False(service.IsValidBranchName(""));
        Assert.False(service.IsValidBranchName(null!));
        Assert.False(service.IsValidBranchName("-invalid"));
        Assert.False(service.IsValidBranchName("/invalid"));
        Assert.False(service.IsValidBranchName(".invalid"));
        Assert.False(service.IsValidBranchName("branch.lock"));
        Assert.False(service.IsValidBranchName("branch/"));
        Assert.False(service.IsValidBranchName("branch..name"));
        Assert.False(service.IsValidBranchName("branch@{1}"));
        Assert.False(service.IsValidBranchName("branch//name"));
        Assert.False(service.IsValidBranchName("branch with space"));
        Assert.False(service.IsValidBranchName("branch~1"));
        Assert.False(service.IsValidBranchName("branch^2"));
        Assert.False(service.IsValidBranchName("branch:name"));
    }

    [Fact]
    public async Task GitUpdateService_RunCommandAsync_NonExistentApp_ReturnsFailure()
    {
        var service = new GitUpdateService();
        var (success, output, error) = await service.RunCommandAsync("non_existent_app_executable_12345.exe", new[] { "--version" }, Directory.GetCurrentDirectory());

        Assert.False(success);
        Assert.NotEmpty(error);
    }

    [Fact]
    public async Task AiEngineManager_Methods_HandleNonExistentPathsAndProcessesGracefully()
    {
        var manager = new AiEngineManager();
        var logger = NullLogger.Instance;

        Assert.Null(manager.ComfyProcess);
        Assert.Null(manager.ForgeProcess);

        bool comfyStarted = await manager.StartComfyUiAsync("non_existent_comfy_path.exe", logger);
        Assert.False(comfyStarted);

        bool forgeStarted = await manager.StartForgeAsync("non_existent_forge_path.exe", logger);
        Assert.False(forgeStarted);

        bool comfyStopped = await manager.StopComfyUiAsync(logger);
        Assert.True(comfyStopped);

        bool forgeStopped = await manager.StopForgeAsync(logger);
        Assert.True(forgeStopped);

        bool isRunning = manager.IsProcessRunning("non_existent_process_name_999");
        Assert.False(isRunning);
    }

    [Fact]
    public async Task AiEngineManager_AudioEngine_StartStop_HandlesGracefully()
    {
        var manager = new AiEngineManager();
        var logger = NullLogger.Instance;

        Assert.Null(manager.AudioProcess);

        bool started = await manager.StartAudioEngineAsync("non_existent_audio_path.exe", logger);
        Assert.False(started);

        bool stopped = await manager.StopAudioEngineAsync(logger);
        Assert.True(stopped);

        var startResult = await manager.StartEngineAsync("audio");
        Assert.False(startResult.Success);
        Assert.Equal("audio", startResult.Engine);

        var stopResult = await manager.StopEngineAsync("audio");
        Assert.True(stopResult.Success);
        Assert.Equal("audio", stopResult.Engine);
    }

    [Theory]
    [InlineData("forge")]
    [InlineData("sdforge")]
    [InlineData("comfyui")]
    [InlineData("comfy")]
    [InlineData("ollama")]
    [InlineData("unknown_engine_xyz")]
    public async Task AiEngineManager_StartEngineAsync_DispatchesExpectedEngine(string engineName)
    {
        var manager = new AiEngineManager();
        var result = await manager.StartEngineAsync(engineName);
        Assert.NotNull(result);
        if (engineName == "unknown_engine_xyz")
        {
            Assert.False(result.Success);
            Assert.Contains("Unsupported", result.Message);
        }
    }

    [Fact]
    public async Task AiEngineManager_Ollama_StartStop_HandlesGracefully()
    {
        var manager = new AiEngineManager();
        var logger = NullLogger.Instance;

        bool stopped = await manager.StopOllamaAsync(logger);
        Assert.True(stopped);

        var stopResult = await manager.StopEngineAsync("ollama");
        Assert.True(stopResult.Success);
        Assert.Equal("ollama", stopResult.Engine);
    }

    [Theory]
    [InlineData("forge")]
    [InlineData("comfyui")]
    [InlineData("ollama")]
    [InlineData("unknown_engine_xyz")]
    public async Task AiEngineManager_StopEngineAsync_DispatchesExpectedEngine(string engineName)
    {
        var manager = new AiEngineManager();
        var result = await manager.StopEngineAsync(engineName);
        Assert.NotNull(result);
        if (engineName == "unknown_engine_xyz")
        {
            Assert.False(result.Success);
            Assert.Contains("Unsupported", result.Message);
        }
    }

    [Fact]
    public async Task VramOrchestrator_RecordActivity_And_Tier1_Tier2_Eviction_WorksWithFakeTime()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var settings = new AppSettings(
            ModelCacheIdleTimeoutMinutes: 15,
            EngineHibernationTimeoutMinutes: 30,
            RamPressureThresholdPercent: 75
        );
        var settingsService = new FakeSettingsService(settings);
        var telemetryProvider = new FakeGpuTelemetryProvider(ramUsedPercent: 45);
        var engineManager = new FakeAiEngineManager();

        var orchestrator = new VramOrchestrator(
            httpClient,
            NullLogger<VramOrchestrator>.Instance,
            settingsService,
            telemetryProvider,
            engineManager,
            timeProvider
        );

        orchestrator.RecordActivity("ollama");
        orchestrator.RecordActivity("comfyui");
        orchestrator.RecordActivity("forge");
        orchestrator.RecordActivity("kokoro");

        // Advance 10m -> neither tier triggers
        timeProvider.Advance(TimeSpan.FromMinutes(10));
        var res10 = await orchestrator.EvaluateCacheLifetimesAsync();
        Assert.False(res10.OllamaUnloaded);
        Assert.False(res10.ComfyVramFreed);
        Assert.Empty(res10.HibernatedEngines);
        Assert.Empty(engineManager.StoppedEngines);

        // Advance another 6m (total 16m >= 15m) -> Tier 1 triggers (Ollama unload + ComfyUI /free)
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        var res16 = await orchestrator.EvaluateCacheLifetimesAsync();
        Assert.True(res16.OllamaUnloaded);
        Assert.True(res16.ComfyVramFreed);
        Assert.Empty(res16.HibernatedEngines);
        Assert.Empty(engineManager.StoppedEngines);
        Assert.Contains(handler.Requests, r => r.RequestUri!.ToString().Contains("11434/api/generate"));
        Assert.Contains(handler.Requests, r => r.RequestUri!.ToString().EndsWith("/free"));

        // Advance another 15m (total 31m >= 30m) -> Tier 2 triggers (comfy, forge, audio hibernated)
        timeProvider.Advance(TimeSpan.FromMinutes(15));
        var res31 = await orchestrator.EvaluateCacheLifetimesAsync();
        Assert.Contains("comfy", res31.HibernatedEngines);
        Assert.Contains("forge", res31.HibernatedEngines);
        Assert.Contains("audio", res31.HibernatedEngines);
        Assert.Contains("comfy", engineManager.StoppedEngines);
        Assert.Contains("forge", engineManager.StoppedEngines);
        Assert.Contains("audio", engineManager.StoppedEngines);
    }

    [Fact]
    public async Task VramOrchestrator_RamPressureAboveThreshold_EvictsImmediately()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var settings = new AppSettings(
            ModelCacheIdleTimeoutMinutes: 15,
            EngineHibernationTimeoutMinutes: 30,
            RamPressureThresholdPercent: 75
        );
        var settingsService = new FakeSettingsService(settings);
        var telemetryProvider = new FakeGpuTelemetryProvider(ramUsedPercent: 78); // >= 75%
        var engineManager = new FakeAiEngineManager();

        var orchestrator = new VramOrchestrator(
            httpClient,
            NullLogger<VramOrchestrator>.Instance,
            settingsService,
            telemetryProvider,
            engineManager,
            timeProvider
        );

        orchestrator.RecordActivity("ollama");
        orchestrator.RecordActivity("comfyui");
        orchestrator.RecordActivity("forge");
        orchestrator.RecordActivity("kokoro");

        // Advance 3m (> 2m idle) -> immediately evicts Tier 1 + Tier 2 due to RAM pressure
        timeProvider.Advance(TimeSpan.FromMinutes(3));
        var result = await orchestrator.EvaluateCacheLifetimesAsync();

        Assert.True(result.OllamaUnloaded);
        Assert.True(result.ComfyVramFreed);
        Assert.Contains("comfy", result.HibernatedEngines);
        Assert.Contains("forge", result.HibernatedEngines);
        Assert.Contains("audio", result.HibernatedEngines);
        Assert.Contains("RAM pressure", result.Reason);
        Assert.Equal(3, engineManager.StoppedEngines.Count);

        // Second consecutive call without new activity -> does not re-evict or re-stop
        var result2 = await orchestrator.EvaluateCacheLifetimesAsync();
        Assert.False(result2.OllamaUnloaded);
        Assert.False(result2.ComfyVramFreed);
        Assert.Empty(result2.HibernatedEngines);
        Assert.Equal("No eviction needed", result2.Reason);
        Assert.Equal(3, engineManager.StoppedEngines.Count);
    }

    [Fact]
    public async Task VramOrchestrator_PurgeAllCachesAsync_UnloadsAndStopsEngines()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var settings = new AppSettings();
        var settingsService = new FakeSettingsService(settings);
        var telemetryProvider = new FakeGpuTelemetryProvider(ramUsedPercent: 40);
        var engineManager = new FakeAiEngineManager();

        var orchestrator = new VramOrchestrator(
            httpClient,
            NullLogger<VramOrchestrator>.Instance,
            settingsService,
            telemetryProvider,
            engineManager,
            timeProvider
        );

        var result = await orchestrator.PurgeAllCachesAsync(stopIdleEngines: true);

        Assert.True(result.OllamaUnloaded);
        Assert.True(result.ComfyVramFreed);
        Assert.Contains("comfy", result.HibernatedEngines);
        Assert.Contains("forge", result.HibernatedEngines);
        Assert.Contains("audio", result.HibernatedEngines);
        Assert.Contains("comfy", engineManager.StoppedEngines);
        Assert.Contains("forge", engineManager.StoppedEngines);
        Assert.Contains("audio", engineManager.StoppedEngines);
        Assert.Contains(handler.Requests, r => r.RequestUri!.ToString().Contains("11434/api/generate"));
        Assert.Contains(handler.Requests, r => r.RequestUri!.ToString().EndsWith("/free"));
    }

    [Fact]
    public async Task MemoryEndpoints_GetMemory_And_PostPurge_ReturnExpectedPayloads()
    {
        var vramResponse = await _client.GetAsync("/api/gpu/vram");
        Assert.Equal(HttpStatusCode.OK, vramResponse.StatusCode);
        var vramJson = await vramResponse.Content.ReadAsStringAsync();
        var vramNode = JsonNode.Parse(vramJson);
        Assert.NotNull(vramNode);
        Assert.NotNull(vramNode["totalRamBytes"]);
        Assert.NotNull(vramNode["ramUsedPercent"]);
        Assert.Equal(15, vramNode["modelCacheIdleTimeoutMinutes"]?.GetValue<int>());
        Assert.Equal(30, vramNode["engineHibernationTimeoutMinutes"]?.GetValue<int>());
        Assert.Equal(75, vramNode["ramPressureThresholdPercent"]?.GetValue<int>());

        var memResponse = await _client.GetAsync("/api/hardware/memory");
        Assert.Equal(HttpStatusCode.OK, memResponse.StatusCode);
        var memJson = await memResponse.Content.ReadAsStringAsync();
        var memNode = JsonNode.Parse(memJson);
        Assert.NotNull(memNode);
        Assert.NotNull(memNode["totalRamBytes"]);
        Assert.NotNull(memNode["usedRamBytes"]);

        var purgeResponse = await _client.PostAsync("/api/memory/purge", null);
        Assert.Equal(HttpStatusCode.OK, purgeResponse.StatusCode);
        var purgeJson = await purgeResponse.Content.ReadAsStringAsync();
        var purgeNode = JsonNode.Parse(purgeJson);
        Assert.NotNull(purgeNode);
        Assert.Equal("purged", purgeNode["status"]?.GetValue<string>());
        Assert.NotNull(purgeNode["eviction"]);
        Assert.NotNull(purgeNode["memory"]);
    }

    private class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset startUtc) => _utcNow = startUtc;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var uri = request.RequestUri?.ToString() ?? "";

            if (uri.Contains("11434/api/ps"))
            {
                var json = "{\"models\":[{\"name\":\"llama3:latest\"}]}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }
            if (uri.Contains("11434/api/generate"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"response\":\"\"}", Encoding.UTF8, "application/json")
                });
            }
            if (uri.EndsWith("/free"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"status\":\"freed\"}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private class FakeSettingsService : ISettingsService
    {
        private readonly AppSettings _settings;
        public FakeSettingsService(AppSettings settings) => _settings = settings;
        public AppSettings LoadSettings() => _settings;
        public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);
        public void SaveSettings(AppSettings settings) { }
        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public string SettingsFilePath() => "settings.json";
    }

    private class FakeGpuTelemetryProvider : IGpuTelemetryProvider
    {
        private readonly int _ramUsedPercent;
        public FakeGpuTelemetryProvider(int ramUsedPercent = 45) => _ramUsedPercent = ramUsedPercent;

        public (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfo() =>
            ("NVIDIA GeForce RTX 4070 Ti SUPER", 16L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024);

        public (string GpuName, long TotalVramBytes, long UsedVramBytes)? GetLinuxMemoryInfo() => null;
        public (string GpuName, long TotalVramBytes, long UsedVramBytes)? ParseNvidiaSmiOutput(string output) => null;
        public (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfoFromRegistry() => GetGpuInfo();

        public SystemMemorySnapshot GetSystemMemoryInfo() =>
            new SystemMemorySnapshot(
                TotalRamBytes: 32L * 1024 * 1024 * 1024,
                UsedRamBytes: (long)(32L * 1024 * 1024 * 1024 * (_ramUsedPercent / 100.0)),
                AvailableRamBytes: (long)(32L * 1024 * 1024 * 1024 * ((100 - _ramUsedPercent) / 100.0)),
                RamUsedPercent: _ramUsedPercent,
                ProcessWorkingSetBytes: 500L * 1024 * 1024,
                NonPagedPoolBytes: 300L * 1024 * 1024
            );
    }

    private class FakeAiEngineManager : IAiEngineManager
    {
        public List<string> StoppedEngines { get; } = new();

        public Process? ComfyProcess => null;
        public Process? ForgeProcess => null;
        public Process? AudioProcess => null;
        public Process? OllamaProcess => null;

        public bool IsProcessRunning(string name) => false;
        public Task<bool> StartComfyUiAsync(string path, ILogger logger) => Task.FromResult(true);
        public Task<bool> StartForgeAsync(string path, ILogger logger) => Task.FromResult(true);
        public Task<bool> StartAudioEngineAsync(string path, ILogger logger) => Task.FromResult(true);
        public Task<bool> StartOllamaAsync(string path, ILogger logger) => Task.FromResult(true);

        public Task<bool> StopComfyUiAsync(ILogger logger)
        {
            StoppedEngines.Add("comfy");
            return Task.FromResult(true);
        }

        public Task<bool> StopForgeAsync(ILogger logger)
        {
            StoppedEngines.Add("forge");
            return Task.FromResult(true);
        }

        public Task<bool> StopAudioEngineAsync(ILogger logger)
        {
            StoppedEngines.Add("audio");
            return Task.FromResult(true);
        }

        public Task<bool> StopOllamaAsync(ILogger logger)
        {
            StoppedEngines.Add("ollama");
            return Task.FromResult(true);
        }

        public Task<EngineOperationResult> StartEngineAsync(string engine) =>
            Task.FromResult(new EngineOperationResult(true, engine, $"{engine} started"));

        public Task<EngineOperationResult> StopEngineAsync(string engine)
        {
            StoppedEngines.Add(engine);
            return Task.FromResult(new EngineOperationResult(true, engine, $"{engine} stopped"));
        }
    }
}
