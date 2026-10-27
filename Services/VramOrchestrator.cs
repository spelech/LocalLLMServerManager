using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalLLMServerManager;

public record CacheEvictionResult(
    bool OllamaUnloaded,
    bool ComfyVramFreed,
    IReadOnlyList<string> HibernatedEngines,
    string Reason
);

public class VramOrchestrator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VramOrchestrator> _logger;
    private readonly ISettingsService? _settingsService;
    private readonly IGpuTelemetryProvider? _telemetryProvider;
    private readonly IAiEngineManager? _engineManager;
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastActivityUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _tier1Evicted = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _tier2Hibernated = new(StringComparer.OrdinalIgnoreCase);

    public VramOrchestrator(HttpClient httpClient, ILogger<VramOrchestrator> logger)
        : this(httpClient, logger, null, null, null, null)
    {
    }

    [ActivatorUtilitiesConstructor]
    public VramOrchestrator(
        HttpClient httpClient,
        ILogger<VramOrchestrator> logger,
        ISettingsService? settingsService = null,
        IGpuTelemetryProvider? telemetryProvider = null,
        IAiEngineManager? engineManager = null,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settingsService = settingsService;
        _telemetryProvider = telemetryProvider;
        _engineManager = engineManager;
        _timeProvider = timeProvider ?? TimeProvider.System;

        var now = _timeProvider.GetUtcNow();
        _lastActivityUtc["ollama"] = now;
        _lastActivityUtc["comfy"] = now;
        _lastActivityUtc["forge"] = now;
        _lastActivityUtc["audio"] = now;
    }

    public static string NormalizeEngineKey(string? engine)
    {
        var normalized = engine?.Trim().ToLowerInvariant() ?? "";
        if (normalized == "comfyui" || normalized == "comfy") return "comfy";
        if (normalized == "forge" || normalized == "sdforge") return "forge";
        if (normalized == "audio" || normalized == "kokoro" || normalized == "alltalk" || normalized == "tts") return "audio";
        if (normalized == "ollama") return "ollama";
        return normalized;
    }

    public void RecordActivity(string engine)
    {
        var key = NormalizeEngineKey(engine);
        if (!string.IsNullOrEmpty(key))
        {
            var now = _timeProvider.GetUtcNow();
            _lastActivityUtc[key] = now;
            _tier1Evicted[key] = false;
            _tier2Hibernated[key] = false;
            _logger.LogDebug("Recorded activity for engine '{Engine}' at {Timestamp}", key, now);
        }
    }

    public DateTimeOffset? GetLastActivityUtc(string engine)
    {
        var key = NormalizeEngineKey(engine);
        return _lastActivityUtc.TryGetValue(key, out var dt) ? dt : null;
    }

    public async Task<bool> IsOllamaHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(1.5));
            var response = await _httpClient.GetAsync("http://127.0.0.1:11434/", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsForgeHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(1.5));
            var response = await _httpClient.GetAsync("http://127.0.0.1:7860/sdapi/v1/progress", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsComfyUiHealthyAsync(string comfyUrl = "http://127.0.0.1:8188", CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(1.5));
            var baseUrl = comfyUrl.TrimEnd('/');
            var response = await _httpClient.GetAsync($"{baseUrl}/system_stats", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task EnsureVramForImageGenerationAsync()
    {
        await UnloadOllamaModelsAsync("Image Generation");
    }

    public async Task EnsureVramForComfyUiAsync()
    {
        await UnloadOllamaModelsAsync("ComfyUI Workflows (Video / 3D / Image)");
    }

    public async Task<bool> FreeComfyUiVramAsync(string comfyUrl = "http://127.0.0.1:8188", CancellationToken cancellationToken = default)
    {
        try
        {
            var baseUrl = comfyUrl.TrimEnd('/');
            var payload = new { free_memory = true, unload_models = true };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{baseUrl}/free", content, cancellationToken);
            _logger.LogInformation("Triggered ComfyUI VRAM unload / free memory.");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call ComfyUI free memory API.");
            return false;
        }
    }

    public async Task<bool> UnloadOllamaModelsAsync(string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            var psResponse = await _httpClient.GetAsync("http://127.0.0.1:11434/api/ps", cancellationToken);
            if (!psResponse.IsSuccessStatusCode) return false;

            var psContent = await psResponse.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonNode.Parse(psContent);
            var models = json?["models"]?.AsArray();

            if (models != null && models.Count > 0)
            {
                _logger.LogInformation($"Active LLM found in VRAM. Issuing unload command to free up VRAM for {reason}.");

                foreach (var model in models)
                {
                    var modelName = model?["name"]?.ToString();
                    if (!string.IsNullOrEmpty(modelName))
                    {
                        var payload = new { model = modelName, keep_alive = 0 };
                        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                        await _httpClient.PostAsync("http://127.0.0.1:11434/api/generate", content, cancellationToken);
                        _logger.LogInformation($"Unloaded model: {modelName}");
                    }
                }

                if (_timeProvider == TimeProvider.System)
                {
                    await Task.Delay(1500, cancellationToken); // Give the system time to clear VRAM
                }

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to orchestrate VRAM for {reason}.");
            return false;
        }
    }

    public async Task<CacheEvictionResult> EvaluateCacheLifetimesAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService?.LoadSettings() ?? new AppSettings();
        var sysMem = _telemetryProvider?.GetSystemMemoryInfo();
        var now = _timeProvider.GetUtcNow();

        int modelCacheTimeoutMinutes = settings.ModelCacheIdleTimeoutMinutes;
        int engineHibernateTimeoutMinutes = settings.EngineHibernationTimeoutMinutes;
        int ramPressureThreshold = settings.RamPressureThresholdPercent;
        int ramUsedPct = sysMem?.RamUsedPercent ?? 0;

        bool isRamPressure = ramPressureThreshold > 0 && ramUsedPct >= ramPressureThreshold;
        var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl;

        bool ollamaUnloaded = false;
        bool comfyVramFreed = false;
        var hibernatedEngines = new List<string>();

        if (isRamPressure)
        {
            var twoMinutes = TimeSpan.FromMinutes(2);

            var ollamaIdle = now - (_lastActivityUtc.TryGetValue("ollama", out var oLast) ? oLast : now);
            if (ollamaIdle >= twoMinutes)
            {
                ollamaUnloaded = await UnloadOllamaModelsAsync($"RAM pressure ({ramUsedPct}% >= {ramPressureThreshold}%)", cancellationToken);
                _tier1Evicted["ollama"] = true;
            }

            var comfyIdle = now - (_lastActivityUtc.TryGetValue("comfy", out var cLast) ? cLast : now);
            if (comfyIdle >= twoMinutes)
            {
                comfyVramFreed = await FreeComfyUiVramAsync(comfyUrl, cancellationToken);
                _tier1Evicted["comfy"] = true;
            }

            if (_engineManager != null)
            {
                if (comfyIdle >= twoMinutes)
                {
                    await _engineManager.StopComfyUiAsync(_logger);
                    hibernatedEngines.Add("comfy");
                    _tier2Hibernated["comfy"] = true;
                }

                var forgeIdle = now - (_lastActivityUtc.TryGetValue("forge", out var fLast) ? fLast : now);
                if (forgeIdle >= twoMinutes)
                {
                    await _engineManager.StopForgeAsync(_logger);
                    hibernatedEngines.Add("forge");
                    _tier2Hibernated["forge"] = true;
                }

                var audioIdle = now - (_lastActivityUtc.TryGetValue("audio", out var aLast) ? aLast : now);
                if (audioIdle >= twoMinutes)
                {
                    await _engineManager.StopAudioEngineAsync(_logger);
                    hibernatedEngines.Add("audio");
                    _tier2Hibernated["audio"] = true;
                }
            }

            var reason = $"RAM pressure relief ({ramUsedPct}% >= {ramPressureThreshold}%)";
            return new CacheEvictionResult(ollamaUnloaded, comfyVramFreed, hibernatedEngines, reason);
        }

        // Tier 1: Model weight unload (default 15m)
        if (modelCacheTimeoutMinutes > 0)
        {
            var tier1Ttl = TimeSpan.FromMinutes(modelCacheTimeoutMinutes);

            var ollamaIdle = now - (_lastActivityUtc.TryGetValue("ollama", out var oLast) ? oLast : now);
            if (ollamaIdle >= tier1Ttl && (!_tier1Evicted.TryGetValue("ollama", out var oEvicted) || !oEvicted))
            {
                ollamaUnloaded = await UnloadOllamaModelsAsync($"Tier 1 idle timeout ({ollamaIdle.TotalMinutes:F0}m >= {modelCacheTimeoutMinutes}m)", cancellationToken);
                _tier1Evicted["ollama"] = true;
            }

            var comfyIdle = now - (_lastActivityUtc.TryGetValue("comfy", out var cLast) ? cLast : now);
            if (comfyIdle >= tier1Ttl && (!_tier1Evicted.TryGetValue("comfy", out var cEvicted) || !cEvicted))
            {
                comfyVramFreed = await FreeComfyUiVramAsync(comfyUrl, cancellationToken);
                _tier1Evicted["comfy"] = true;
            }
        }

        // Tier 2: Child engine process hibernation (default 30m)
        if (engineHibernateTimeoutMinutes > 0 && _engineManager != null)
        {
            var tier2Ttl = TimeSpan.FromMinutes(engineHibernateTimeoutMinutes);

            var comfyIdle = now - (_lastActivityUtc.TryGetValue("comfy", out var cLast) ? cLast : now);
            if (comfyIdle >= tier2Ttl && (!_tier2Hibernated.TryGetValue("comfy", out var cHib) || !cHib))
            {
                await _engineManager.StopComfyUiAsync(_logger);
                hibernatedEngines.Add("comfy");
                _tier2Hibernated["comfy"] = true;
            }

            var forgeIdle = now - (_lastActivityUtc.TryGetValue("forge", out var fLast) ? fLast : now);
            if (forgeIdle >= tier2Ttl && (!_tier2Hibernated.TryGetValue("forge", out var fHib) || !fHib))
            {
                await _engineManager.StopForgeAsync(_logger);
                hibernatedEngines.Add("forge");
                _tier2Hibernated["forge"] = true;
            }

            var audioIdle = now - (_lastActivityUtc.TryGetValue("audio", out var aLast) ? aLast : now);
            if (audioIdle >= tier2Ttl && (!_tier2Hibernated.TryGetValue("audio", out var aHib) || !aHib))
            {
                await _engineManager.StopAudioEngineAsync(_logger);
                hibernatedEngines.Add("audio");
                _tier2Hibernated["audio"] = true;
            }
        }

        string evictionReason;
        if (ollamaUnloaded || comfyVramFreed || hibernatedEngines.Count > 0)
        {
            var parts = new List<string>();
            if (ollamaUnloaded) parts.Add("Ollama weights unloaded");
            if (comfyVramFreed) parts.Add("ComfyUI VRAM freed");
            if (hibernatedEngines.Count > 0) parts.Add($"Hibernated: {string.Join(", ", hibernatedEngines)}");
            evictionReason = string.Join("; ", parts);
        }
        else
        {
            evictionReason = "No eviction needed";
        }

        return new CacheEvictionResult(ollamaUnloaded, comfyVramFreed, hibernatedEngines, evictionReason);
    }

    public async Task<CacheEvictionResult> PurgeAllCachesAsync(bool stopIdleEngines = false, CancellationToken cancellationToken = default)
    {
        var settings = _settingsService?.LoadSettings() ?? new AppSettings();
        var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl;

        bool ollamaUnloaded = await UnloadOllamaModelsAsync("Manual memory purge", cancellationToken);
        bool comfyFreed = await FreeComfyUiVramAsync(comfyUrl, cancellationToken);

        _tier1Evicted["ollama"] = true;
        _tier1Evicted["comfy"] = true;

        var hibernatedEngines = new List<string>();
        if (stopIdleEngines && _engineManager != null)
        {
            await _engineManager.StopComfyUiAsync(_logger);
            hibernatedEngines.Add("comfy");
            _tier2Hibernated["comfy"] = true;

            await _engineManager.StopForgeAsync(_logger);
            hibernatedEngines.Add("forge");
            _tier2Hibernated["forge"] = true;

            await _engineManager.StopAudioEngineAsync(_logger);
            hibernatedEngines.Add("audio");
            _tier2Hibernated["audio"] = true;
        }

        return new CacheEvictionResult(ollamaUnloaded, comfyFreed, hibernatedEngines, "Manual cache purge");
    }
}
