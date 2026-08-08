using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LocalLLMServerManager.Services;

public interface IAiEngineManager
{
    Process? ComfyProcess { get; }
    Process? ForgeProcess { get; }

    bool IsProcessRunning(string name);
    Task<bool> StartComfyUiAsync(string executablePath, ILogger logger);
    Task<bool> StopComfyUiAsync(ILogger logger);
    Task<bool> StartForgeAsync(string executablePath, ILogger logger);
    Task<bool> StopForgeAsync(ILogger logger);
}
