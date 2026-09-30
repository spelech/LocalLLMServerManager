using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace LocalLLMServerManager.Services;

public class AiEngineManager : IAiEngineManager
{
    private static Process? _comfyProcess;
    private static Process? _forgeProcess;
    private static Process? _audioProcess;
    private static Process? _ollamaProcess;
    private static readonly JobObject AiEnginesJob = new();

    public Process? ComfyProcess => _comfyProcess;
    public Process? ForgeProcess => _forgeProcess;
    public Process? AudioProcess => _audioProcess;
    public Process? OllamaProcess => _ollamaProcess;

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

            var expandedPath = ResolveComfyExecutable(executablePath);
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

            var expandedPath = ResolveForgeExecutable(executablePath);
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

    public Task<bool> StartAudioEngineAsync(string executablePath, ILogger logger)
    {
        try
        {
            if (_audioProcess != null && !_audioProcess.HasExited) return Task.FromResult(true);

            var expandedPath = ResolveAudioExecutable(executablePath);
            ProcessStartInfo startInfo;

            if (expandedPath.TrimStart().StartsWith("docker", StringComparison.OrdinalIgnoreCase))
            {
                var parts = expandedPath.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var dockerExe = parts[0];
                var args = parts.Length > 1 ? parts[1] : "";

                startInfo = new ProcessStartInfo
                {
                    FileName = dockerExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else
            {
                if (!File.Exists(expandedPath))
                {
                    logger.LogWarning("Audio Engine executable path does not exist: {Path}", expandedPath);
                    return Task.FromResult(false);
                }

                if (expandedPath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
                {
                    var pyExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "python" : "python3";
                    startInfo = new ProcessStartInfo
                    {
                        FileName = pyExe,
                        Arguments = $"\"{expandedPath}\"",
                        WorkingDirectory = Path.GetDirectoryName(expandedPath),
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
                else
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = expandedPath,
                        WorkingDirectory = Path.GetDirectoryName(expandedPath),
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                }
            }

            _audioProcess = Process.Start(startInfo);
            if (_audioProcess != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AiEnginesJob.AddProcess(_audioProcess);
            }

            logger.LogInformation("Started Audio Engine process PID {Pid}", _audioProcess?.Id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Audio Engine process");
            return Task.FromResult(false);
        }
    }

    public Task<bool> StopAudioEngineAsync(ILogger logger)
    {
        try
        {
            if (_audioProcess != null && !_audioProcess.HasExited)
            {
                _audioProcess.Kill(true);
                _audioProcess = null;
                logger.LogInformation("Stopped Audio Engine process");
                return Task.FromResult(true);
            }

            var processes = Process.GetProcessesByName("python");
            foreach (var p in processes)
            {
                try
                {
                    if (p.MainWindowTitle.Contains("kokoro", StringComparison.OrdinalIgnoreCase) ||
                        p.MainWindowTitle.Contains("alltalk", StringComparison.OrdinalIgnoreCase) ||
                        p.MainWindowTitle.Contains("audio", StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill(true);
                    }
                }
                catch { }
            }
            _audioProcess = null;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop Audio Engine process");
            return Task.FromResult(false);
        }
    }

    public Task<bool> StartOllamaAsync(string executablePath, ILogger logger)
    {
        try
        {
            if (_ollamaProcess != null && !_ollamaProcess.HasExited) return Task.FromResult(true);
            if (IsProcessRunning("ollama"))
            {
                logger.LogInformation("Ollama process is already running");
                return Task.FromResult(true);
            }

            var resolvedPath = ResolveOllamaExecutable(executablePath);
            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedPath,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var workingDir = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
            {
                startInfo.WorkingDirectory = workingDir;
            }

            _ollamaProcess = Process.Start(startInfo);
            if (_ollamaProcess != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AiEnginesJob.AddProcess(_ollamaProcess);
            }

            logger.LogInformation("Started Ollama server process PID {Pid}", _ollamaProcess?.Id);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Ollama process");
            return Task.FromResult(false);
        }
    }

    public static string ResolveOllamaExecutable(string? executablePath = null)
    {
        if (!string.IsNullOrWhiteSpace(executablePath) && !string.Equals(executablePath.Trim(), "ollama", StringComparison.OrdinalIgnoreCase))
        {
            return Environment.ExpandEnvironmentVariables(executablePath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidate = Path.Combine(localApp, "Programs", "Ollama", "ollama.exe");
            if (File.Exists(candidate)) return candidate;

            var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var progCandidate = Path.Combine(progFiles, "Ollama", "ollama.exe");
            if (File.Exists(progCandidate)) return progCandidate;
        }

        return "ollama";
    }

    public static string ResolveComfyExecutable(string? executablePath = null)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return Environment.ExpandEnvironmentVariables(executablePath);
        }

        try
        {
            var discovered = new ToolDiscoveryService().DetectComfyUi();
            if (discovered.IsInstalled && !string.IsNullOrWhiteSpace(discovered.ExecutablePath) && File.Exists(discovered.ExecutablePath))
            {
                return discovered.ExecutablePath;
            }
        }
        catch { }

        return @"C:\AI\ComfyUI\run_nvidia_gpu.bat";
    }

    public static string ResolveForgeExecutable(string? executablePath = null)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return Environment.ExpandEnvironmentVariables(executablePath);
        }

        try
        {
            var discovered = new ToolDiscoveryService().DetectForge();
            if (discovered.IsInstalled && !string.IsNullOrWhiteSpace(discovered.ExecutablePath) && File.Exists(discovered.ExecutablePath))
            {
                return discovered.ExecutablePath;
            }
        }
        catch { }

        return @"C:\AI\webui\webui-user.bat";
    }

    public static string ResolveAudioExecutable(string? executablePath = null)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return Environment.ExpandEnvironmentVariables(executablePath);
        }

        try
        {
            var discovered = new ToolDiscoveryService().DetectAudioEngine();
            if (discovered.IsInstalled && !string.IsNullOrWhiteSpace(discovered.ExecutablePath) && File.Exists(discovered.ExecutablePath))
            {
                return discovered.ExecutablePath;
            }
        }
        catch { }

        return @"C:\AI\Kokoro-FastAPI\main.py";
    }

    public Task<bool> StopOllamaAsync(ILogger logger)
    {
        try
        {
            if (_ollamaProcess != null && !_ollamaProcess.HasExited)
            {
                _ollamaProcess.Kill(true);
                _ollamaProcess = null;
                logger.LogInformation("Stopped managed Ollama process");
            }

            var processes = Process.GetProcessesByName("ollama");
            foreach (var p in processes)
            {
                try
                {
                    p.Kill(true);
                }
                catch { }
            }

            var appProcesses = Process.GetProcessesByName("ollama_app");
            foreach (var p in appProcesses)
            {
                try
                {
                    p.Kill(true);
                }
                catch { }
            }

            _ollamaProcess = null;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop Ollama process");
            return Task.FromResult(false);
        }
    }

    public async Task<EngineOperationResult> StartEngineAsync(string engine)
    {
        var normalized = engine?.Trim().ToLowerInvariant() ?? "";
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        if (normalized == "forge" || normalized == "sdforge")
        {
            var settings = new SettingsService().LoadSettings();
            var execPath = ResolveForgeExecutable(settings.ForgeExecutablePath);
            var success = await StartForgeAsync(execPath, logger);
            return new EngineOperationResult(success, "forge", success ? "SD Forge Started" : "Failed to start SD Forge", _forgeProcess?.Id);
        }
        else if (normalized == "comfyui" || normalized == "comfy")
        {
            var settings = new SettingsService().LoadSettings();
            var execPath = ResolveComfyExecutable(settings.ComfyUiExecutablePath);
            var success = await StartComfyUiAsync(execPath, logger);
            return new EngineOperationResult(success, "comfyui", success ? "ComfyUI Started" : "Failed to start ComfyUI", _comfyProcess?.Id);
        }
        else if (normalized == "ollama")
        {
            var settings = new SettingsService().LoadSettings();
            var execPath = ResolveOllamaExecutable(settings.OllamaExecutablePath);
            var success = await StartOllamaAsync(execPath, logger);
            return new EngineOperationResult(success, "ollama", success ? "Ollama Started" : "Failed to start Ollama", _ollamaProcess?.Id);
        }
        else if (normalized == "audio" || normalized == "kokoro" || normalized == "alltalk" || normalized == "tts")
        {
            var settings = new SettingsService().LoadSettings();
            var execPath = string.IsNullOrWhiteSpace(settings.AudioEngineExecutablePath) ? @"C:\AI\Kokoro-FastAPI\main.py" : settings.AudioEngineExecutablePath;
            var success = await StartAudioEngineAsync(execPath, logger);
            return new EngineOperationResult(success, "audio", success ? "Audio Engine Started" : "Failed to start Audio Engine", _audioProcess?.Id);
        }

        return new EngineOperationResult(false, engine ?? "unknown", $"Unsupported engine: {engine}");
    }

    public async Task<EngineOperationResult> StopEngineAsync(string engine)
    {
        var normalized = engine?.Trim().ToLowerInvariant() ?? "";
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        if (normalized == "forge" || normalized == "sdforge")
        {
            var success = await StopForgeAsync(logger);
            return new EngineOperationResult(success, "forge", success ? "SD Forge Stopped" : "Failed to stop SD Forge");
        }
        else if (normalized == "comfyui" || normalized == "comfy")
        {
            var success = await StopComfyUiAsync(logger);
            return new EngineOperationResult(success, "comfyui", success ? "ComfyUI Stopped" : "Failed to stop ComfyUI");
        }
        else if (normalized == "ollama")
        {
            var success = await StopOllamaAsync(logger);
            return new EngineOperationResult(success, "ollama", success ? "Ollama Stopped" : "Failed to stop Ollama");
        }
        else if (normalized == "audio" || normalized == "kokoro" || normalized == "alltalk" || normalized == "tts")
        {
            var success = await StopAudioEngineAsync(logger);
            return new EngineOperationResult(success, "audio", success ? "Audio Engine Stopped" : "Failed to stop Audio Engine");
        }

        return new EngineOperationResult(false, engine ?? "unknown", $"Unsupported engine: {engine}");
    }
}
