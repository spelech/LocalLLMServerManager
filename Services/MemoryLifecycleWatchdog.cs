using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalLLMServerManager.Services;

public sealed class MemoryLifecycleWatchdog : BackgroundService
{
    private readonly VramOrchestrator _orchestrator;
    private readonly ILogger<MemoryLifecycleWatchdog> _logger;
    private readonly IGpuTelemetryProvider? _telemetryProvider;
    private readonly TimeSpan _checkInterval;
    private readonly TimeProvider _timeProvider;

    public MemoryLifecycleWatchdog(
        VramOrchestrator orchestrator,
        ILogger<MemoryLifecycleWatchdog> logger,
        TimeSpan? checkInterval,
        TimeProvider? timeProvider = null)
        : this(orchestrator, logger, null, checkInterval, timeProvider)
    {
    }

    [ActivatorUtilitiesConstructor]
    public MemoryLifecycleWatchdog(
        VramOrchestrator orchestrator,
        ILogger<MemoryLifecycleWatchdog> logger,
        IGpuTelemetryProvider? telemetryProvider = null,
        TimeSpan? checkInterval = null,
        TimeProvider? timeProvider = null)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _telemetryProvider = telemetryProvider;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(30);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_checkInterval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _orchestrator.EvaluateCacheLifetimesAsync(stoppingToken);

                    var memInfo = (_telemetryProvider ?? new GpuTelemetryProvider()).GetSystemMemoryInfo();
                    if (memInfo.ProcessWorkingSetBytes > 512L * 1024 * 1024 ||
                        GC.GetTotalMemory(false) > 256L * 1024 * 1024 ||
                        memInfo.RamUsedPercent >= 75.0)
                    {
                        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                        GC.WaitForPendingFinalizers();
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error during memory cache evaluation in watchdog loop.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in MemoryLifecycleWatchdog loop.");
        }
    }
}
