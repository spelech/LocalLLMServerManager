using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalLLMServerManager.Services;

public sealed class MemoryLifecycleWatchdog : BackgroundService
{
    private readonly VramOrchestrator _orchestrator;
    private readonly ILogger<MemoryLifecycleWatchdog> _logger;
    private readonly TimeSpan _checkInterval;

    public MemoryLifecycleWatchdog(
        VramOrchestrator orchestrator,
        ILogger<MemoryLifecycleWatchdog> logger,
        TimeSpan? checkInterval = null)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_checkInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _orchestrator.EvaluateCacheLifetimesAsync(stoppingToken);
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
