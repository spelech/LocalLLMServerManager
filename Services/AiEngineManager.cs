using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace LocalLLMServerManager.Services;

public class AiEngineManager : IAiEngineManager
{
    private static Process? _comfyProcess;
    private static Process? _forgeProcess;
    private static readonly JobObject AiEnginesJob = new();

    public Process? ComfyProcess => _comfyProcess;
    public Process? ForgeProcess => _forgeProcess;

    public bool IsProcessRunning(string name)
    {
        try
        {
            var processes = Process.GetProcessesByName(name);
            return processes.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> StartComfyUiAsync(string executablePath, ILogger logger)
    {
        try
        {
            if (_comfyProcess != null && !_comfyProcess.HasExited) return Task.FromResult(true);

            var expandedPath = Environment.ExpandEnvironmentVariables(executablePath);
            if (!File.Exists(expandedPath))
            {
                logger.LogWarning("ComfyUI executable path does not exist: {Path}", expandedPath);
                return Task.FromResult(false);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = expandedPath,
                WorkingDirectory = Path.GetDirectoryName(expandedPath),
                UseShellExecute = true,
                CreateNoWindow = false
            };

            _comfyProcess = Process.Start(startInfo);
            if (_comfyProcess != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AiEnginesJob.AddProcess(_comfyProcess);
            }

            logger.LogInformation("Started ComfyUI engine process PID {Pid}", _comfyProcess?.Id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start ComfyUI process");
            return Task.FromResult(false);
        }
    }

    public Task<bool> StopComfyUiAsync(ILogger logger)
    {
        try
        {
            if (_comfyProcess != null && !_comfyProcess.HasExited)
            {
                _comfyProcess.Kill(true);
                _comfyProcess = null;
                logger.LogInformation("Stopped ComfyUI process");
                return Task.FromResult(true);
            }

            var processes = Process.GetProcessesByName("python");
            foreach (var p in processes)
            {
                try
                {
                    if (p.MainWindowTitle.Contains("ComfyUI", StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill(true);
                    }
                }
                catch { }
            }
            _comfyProcess = null;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop ComfyUI process");
            return Task.FromResult(false);
        }
    }

    public Task<bool> StartForgeAsync(string executablePath, ILogger logger)
    {
        try
        {
            if (_forgeProcess != null && !_forgeProcess.HasExited) return Task.FromResult(true);

            var expandedPath = Environment.ExpandEnvironmentVariables(executablePath);
            if (!File.Exists(expandedPath))
            {
                logger.LogWarning("SD Forge executable path does not exist: {Path}", expandedPath);
                return Task.FromResult(false);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = expandedPath,
                WorkingDirectory = Path.GetDirectoryName(expandedPath),
                UseShellExecute = true,
                CreateNoWindow = false
            };

            _forgeProcess = Process.Start(startInfo);
            if (_forgeProcess != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AiEnginesJob.AddProcess(_forgeProcess);
            }

            logger.LogInformation("Started SD Forge process PID {Pid}", _forgeProcess?.Id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start SD Forge process");
            return Task.FromResult(false);
        }
    }

    public Task<bool> StopForgeAsync(ILogger logger)
    {
        try
        {
            if (_forgeProcess != null && !_forgeProcess.HasExited)
            {
                _forgeProcess.Kill(true);
                _forgeProcess = null;
                logger.LogInformation("Stopped SD Forge process");
                return Task.FromResult(true);
            }
            _forgeProcess = null;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop SD Forge process");
            return Task.FromResult(false);
        }
    }
}
