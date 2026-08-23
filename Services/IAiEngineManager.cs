using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LocalLLMServerManager.Services;

public record EngineOperationResult(
    bool Success,
    string Engine,
    string Message,
    int? Pid = null
);

public interface IAiEngineManager
{
    Process? ComfyProcess { get; }
    Process? ForgeProcess { get; }
    Process? AudioProcess { get; }

    bool IsProcessRunning(string name);
    Task<bool> StartComfyUiAsync(string executablePath, ILogger logger);
    Task<bool> StopComfyUiAsync(ILogger logger);
    Task<bool> StartForgeAsync(string executablePath, ILogger logger);
    Task<bool> StopForgeAsync(ILogger logger);
    Task<bool> StartAudioEngineAsync(string executablePath, ILogger logger);
    Task<bool> StopAudioEngineAsync(ILogger logger);

    Task<EngineOperationResult> StartEngineAsync(string engine);
    Task<EngineOperationResult> StopEngineAsync(string engine);
}
