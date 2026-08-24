using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LocalLLMServerManager.Services;

public class ToolDiscoveryService : IToolDiscoveryService
{
    private readonly IReadOnlyList<string> _searchRoots;
    private readonly string? _ollamaModelsOverride;

    public ToolDiscoveryService(IReadOnlyList<string>? searchRoots = null, string? ollamaModelsOverride = null)
    {
        _searchRoots = searchRoots ?? GetDefaultSearchRoots();
        _ollamaModelsOverride = ollamaModelsOverride;
    }

    public async Task<DiscoveredToolsResult> DetectAllToolsAsync()
    {
        var ollamaTask = Task.Run(DetectOllama);
        var comfyTask = Task.Run(DetectComfyUi);
        var forgeTask = Task.Run(DetectForge);
        var audioTask = Task.Run(DetectAudioEngine);

        await Task.WhenAll(ollamaTask, comfyTask, forgeTask, audioTask);

        var ollama = await ollamaTask;
        var comfy = await comfyTask;
        var forge = await forgeTask;
        var audio = await audioTask;

        var suggested3D = comfy.ModelsDirectory ?? forge.ModelsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI", "3D");
        var suggestedWorkflows = comfy.WorkflowsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI", "Workflows");

        return new DiscoveredToolsResult(
            Ollama: ollama,
            ComfyUi: comfy,
            Forge: forge,
            SuggestedThreeDPath: suggested3D,
            SuggestedWorkflowsPath: suggestedWorkflows,
            AudioEngine: audio
        );
    }

    public DiscoveredToolInfo DetectOllama()
    {
        // 1. Check PATH
        var pathExe = FindExecutableOnPath(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ollama.exe" : "ollama");
        string? exePath = pathExe;

        // 2. Check running processes
        if (string.IsNullOrEmpty(exePath))
        {
            try
            {
                var processes = Process.GetProcessesByName("ollama");
                if (processes.Length > 0)
                {
                    try
                    {
                        var procPath = processes[0].MainModule?.FileName;
                        if (!string.IsNullOrEmpty(procPath) && File.Exists(procPath))
                        {
                            exePath = procPath;
                        }
                    }
                    catch
                    {
                        // Ignore process inspection access restrictions
                    }
                }
            }
            catch
            {
                // Ignore process enumeration failures
            }
        }

        // 3. Check custom search roots & local app data
        if (string.IsNullOrEmpty(exePath))
        {
            var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ollama.exe" : "ollama";
            foreach (var root in _searchRoots)
            {
                var candidates = new[]
                {
                    Path.Combine(root, binaryName),
                    Path.Combine(root, "Programs", "Ollama", binaryName),
                    Path.Combine(root, "Ollama", binaryName),
                    Path.Combine(root, "ollama", binaryName)
                };

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        exePath = candidate;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(exePath))
                    break;
            }
        }

        // 4. Check standard AppData location on Windows
        if (string.IsNullOrEmpty(exePath) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                var defaultPath = Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe");
                if (File.Exists(defaultPath))
                {
                    exePath = defaultPath;
                }
            }
        }

        if (string.IsNullOrEmpty(exePath))
        {
            return new DiscoveredToolInfo(
                IsInstalled: false,
                ExecutablePath: null,
                RootDirectory: null,
                ModelsDirectory: null,
                WorkflowsDirectory: null,
                StatusMessage: "Ollama not detected"
            );
        }

        var rootDir = Path.GetDirectoryName(exePath);
        var modelsDir = ResolveOllamaModelsDirectory();

        return new DiscoveredToolInfo(
            IsInstalled: true,
            ExecutablePath: exePath,
            RootDirectory: rootDir,
            ModelsDirectory: modelsDir,
            WorkflowsDirectory: null,
            StatusMessage: $"Discovered Ollama at {exePath}"
        );
    }

    public DiscoveredToolInfo DetectComfyUi()
    {
        var targetSubdirs = new[]
        {
            "ComfyUI_windows_portable",
            "ComfyUI",
            "comfyui",
            "ComfyUI_windows_portable_nvidia_cu121_or_cpu",
            "comfy"
        };

        var runnerNames = new[]
        {
            "run_nvidia_gpu_fast_fp16_accumulation.bat",
            "run_nvidia_gpu.bat",
            "run_directml.bat",
            "run_cpu.bat",
            "run.bat",
            "main.py"
        };

        foreach (var baseRoot in _searchRoots)
        {
            var candidateDirectories = new List<string>();
            foreach (var sub in targetSubdirs)
            {
                candidateDirectories.Add(Path.Combine(baseRoot, sub));
            }
            candidateDirectories.Add(baseRoot);

            foreach (var dir in candidateDirectories)
            {
                if (!Directory.Exists(dir))
                    continue;

                string? foundRunner = null;
                foreach (var runner in runnerNames)
                {
                    var runnerPath = Path.Combine(dir, runner);
                    if (File.Exists(runnerPath))
                    {
                        // If it's a generic run.bat, ensure directory has ComfyUI signatures
                        if (runner.Equals("run.bat", StringComparison.OrdinalIgnoreCase))
                        {
                            var hasComfySignature = File.Exists(Path.Combine(dir, "main.py")) ||
                                                    File.Exists(Path.Combine(dir, "ComfyUI", "main.py")) ||
                                                    Directory.Exists(Path.Combine(dir, "comfy")) ||
                                                    Directory.Exists(Path.Combine(dir, "ComfyUI")) ||
                                                    File.Exists(Path.Combine(dir, "execution.py"));
                            if (!hasComfySignature)
                                continue;
                        }

                        foundRunner = runnerPath;
                        break;
                    }
                }

                // If not found in portable root, check nested ComfyUI subfolder
                var nestedComfy = Path.Combine(dir, "ComfyUI");
                if (foundRunner == null && Directory.Exists(nestedComfy))
                {
                    var mainPy = Path.Combine(nestedComfy, "main.py");
                    if (File.Exists(mainPy))
                    {
                        foundRunner = mainPy;
                    }
                }

                if (foundRunner != null)
                {
                    var modelsDir = FindDirectory(dir, "models", "ComfyUI/models");
                    if (modelsDir == null)
                    {
                        var parentModels = Path.Combine(dir, "..", "comfy_models");
                        if (Directory.Exists(parentModels))
                            modelsDir = Path.GetFullPath(parentModels);
                    }

                    var workflowsDir = FindDirectory(dir, "user/default/workflows", "ComfyUI/user/default/workflows", "workflows", "ComfyUI/workflows");

                    return new DiscoveredToolInfo(
                        IsInstalled: true,
                        ExecutablePath: foundRunner,
                        RootDirectory: dir,
                        ModelsDirectory: modelsDir,
                        WorkflowsDirectory: workflowsDir,
                        StatusMessage: $"Discovered ComfyUI at {foundRunner}"
                    );
                }
            }
        }

        return new DiscoveredToolInfo(
            IsInstalled: false,
            ExecutablePath: null,
            RootDirectory: null,
            ModelsDirectory: null,
            WorkflowsDirectory: null,
            StatusMessage: "ComfyUI not detected"
        );
    }

    public DiscoveredToolInfo DetectForge()
    {
        var targetSubdirs = new[]
        {
            "SD_Forge",
            "sd_forge",
            "SD-Forge",
            "sd-forge",
            "webui_forge_cu121_torch231",
            "stable-diffusion-webui-forge",
            "webui_forge",
            "Forge",
            "forge",
            "stable-diffusion-webui",
            "webui"
        };

        var runnerNames = new[]
        {
            "webui-user.bat",
            "webui.bat",
            "run.bat",
            "launch.py",
            "update.bat"
        };

        foreach (var baseRoot in _searchRoots)
        {
            var candidateDirectories = new List<string>();
            foreach (var sub in targetSubdirs)
            {
                candidateDirectories.Add(Path.Combine(baseRoot, sub));
            }
            candidateDirectories.Add(baseRoot);

            foreach (var dir in candidateDirectories)
            {
                if (!Directory.Exists(dir))
                    continue;

                string? foundRunner = null;
                foreach (var runner in runnerNames)
                {
                    var runnerPath = Path.Combine(dir, runner);
                    if (File.Exists(runnerPath))
                    {
                        // If it's a generic run.bat or update.bat, ensure directory has WebUI / Forge signatures
                        if (runner.Equals("run.bat", StringComparison.OrdinalIgnoreCase) || runner.Equals("update.bat", StringComparison.OrdinalIgnoreCase))
                        {
                            var hasForgeSignature = File.Exists(Path.Combine(dir, "webui-user.bat")) ||
                                                    File.Exists(Path.Combine(dir, "webui.bat")) ||
                                                    File.Exists(Path.Combine(dir, "webui.py")) ||
                                                    File.Exists(Path.Combine(dir, "launch.py")) ||
                                                    Directory.Exists(Path.Combine(dir, "modules_forge")) ||
                                                    Directory.Exists(Path.Combine(dir, "modules")) ||
                                                    Directory.Exists(Path.Combine(dir, "webui"));
                            if (!hasForgeSignature)
                                continue;
                        }

                        foundRunner = runnerPath;
                        break;
                    }
                }

                if (foundRunner != null)
                {
                    var modelsDir = FindDirectory(dir, "models/Stable-diffusion", "webui/models/Stable-diffusion", "models", "webui/models");

                    return new DiscoveredToolInfo(
                        IsInstalled: true,
                        ExecutablePath: foundRunner,
                        RootDirectory: dir,
                        ModelsDirectory: modelsDir,
                        WorkflowsDirectory: null,
                        StatusMessage: $"Discovered SD Forge at {foundRunner}"
                    );
                }
            }
        }

        return new DiscoveredToolInfo(
            IsInstalled: false,
            ExecutablePath: null,
            RootDirectory: null,
            ModelsDirectory: null,
            WorkflowsDirectory: null,
            StatusMessage: "SD Forge not detected"
        );
    }

    public DiscoveredToolInfo DetectAudioEngine()
    {
        var targetSubdirs = new[]
        {
            "Kokoro-FastAPI",
            "kokoro-fastapi",
            "Kokoro",
            "kokoro",
            "AllTalk",
            "alltalk",
            "alltalk_tts",
            "tts",
            "TTS"
        };

        var runnerNames = new[]
        {
            "main.py",
            "app.py",
            "api.py",
            "start.bat",
            "run.bat",
            "start.sh"
        };

        foreach (var baseRoot in _searchRoots)
        {
            var candidateDirectories = new List<string>();
            foreach (var sub in targetSubdirs)
            {
                candidateDirectories.Add(Path.Combine(baseRoot, sub));
            }
            candidateDirectories.Add(baseRoot);

            foreach (var dir in candidateDirectories)
            {
                if (!Directory.Exists(dir))
                    continue;

                string? foundRunner = null;
                foreach (var runner in runnerNames)
                {
                    var runnerPath = Path.Combine(dir, runner);
                    if (File.Exists(runnerPath))
                    {
                        if (runner.Equals("run.bat", StringComparison.OrdinalIgnoreCase) || runner.Equals("start.bat", StringComparison.OrdinalIgnoreCase))
                        {
                            var hasAudioSignature = File.Exists(Path.Combine(dir, "main.py")) ||
                                                    File.Exists(Path.Combine(dir, "app.py")) ||
                                                    File.Exists(Path.Combine(dir, "api.py")) ||
                                                    Directory.Exists(Path.Combine(dir, "voices")) ||
                                                    Directory.Exists(Path.Combine(dir, "models"));
                            if (!hasAudioSignature)
                                continue;
                        }

                        foundRunner = runnerPath;
                        break;
                    }
                }

                if (foundRunner != null)
                {
                    var modelsDir = FindDirectory(dir, "voices", "models", "voice_models");

                    return new DiscoveredToolInfo(
                        IsInstalled: true,
                        ExecutablePath: foundRunner,
                        RootDirectory: dir,
                        ModelsDirectory: modelsDir,
                        WorkflowsDirectory: null,
                        StatusMessage: $"Discovered Audio Engine at {foundRunner}"
                    );
                }
            }
        }

        var dockerExe = FindExecutableOnPath(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "docker.exe" : "docker");
        if (!string.IsNullOrEmpty(dockerExe))
        {
            return new DiscoveredToolInfo(
                IsInstalled: true,
                ExecutablePath: "docker run -d -p 8880:8880 ghcr.io/resemble-ai/kokoro-fastapi",
                RootDirectory: null,
                ModelsDirectory: null,
                WorkflowsDirectory: null,
                StatusMessage: "Docker detected for Audio Engine container"
            );
        }

        return new DiscoveredToolInfo(
            IsInstalled: false,
            ExecutablePath: null,
            RootDirectory: null,
            ModelsDirectory: null,
            WorkflowsDirectory: null,
            StatusMessage: "Audio Engine not detected"
        );
    }

    public PathValidationResult ValidatePath(string? path, PathTargetType targetType)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new PathValidationResult(false, false, "Path cannot be empty.");
        }

        string expanded;
        try
        {
            expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        }
        catch (Exception ex)
        {
            return new PathValidationResult(false, false, $"Invalid path format: {ex.Message}");
        }

        if (targetType == PathTargetType.Executable)
        {
            if (File.Exists(expanded))
            {
                return new PathValidationResult(true, true, null);
            }

            // Check if bare command exists on PATH
            if (!expanded.Contains(Path.DirectorySeparatorChar) && !expanded.Contains(Path.AltDirectorySeparatorChar))
            {
                var onPath = FindExecutableOnPath(expanded);
                if (!string.IsNullOrEmpty(onPath))
                {
                    return new PathValidationResult(true, true, null);
                }
            }

            return new PathValidationResult(false, false, $"File not found: {expanded}");
        }

        if (targetType == PathTargetType.Directory)
        {
            if (Directory.Exists(expanded))
            {
                return new PathValidationResult(true, true, null);
            }

            return new PathValidationResult(false, false, $"Directory not found: {expanded}");
        }

        return new PathValidationResult(false, false, "Unknown target type.");
    }

    private string? ResolveOllamaModelsDirectory()
    {
        if (!string.IsNullOrEmpty(_ollamaModelsOverride) && Directory.Exists(_ollamaModelsOverride))
        {
            return _ollamaModelsOverride;
        }

        var envModels = Environment.GetEnvironmentVariable("OLLAMA_MODELS");
        if (!string.IsNullOrWhiteSpace(envModels) && Directory.Exists(envModels))
        {
            return envModels;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            var defaultModels = Path.Combine(userProfile, ".ollama", "models");
            if (Directory.Exists(defaultModels))
            {
                return defaultModels;
            }
        }

        return null;
    }

    private static string? FindExecutableOnPath(string executableName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : new[] { string.Empty };

        foreach (var dir in paths)
        {
            try
            {
                if (!Directory.Exists(dir))
                    continue;

                var candidate = Path.Combine(dir, executableName);
                if (File.Exists(candidate))
                    return candidate;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Path.HasExtension(executableName))
                {
                    foreach (var ext in extensions)
                    {
                        var withExt = Path.Combine(dir, executableName + ext);
                        if (File.Exists(withExt))
                            return withExt;
                    }
                }
            }
            catch
            {
                // Ignore directory access failures in PATH scan
            }
        }

        return null;
    }

    private static string? FindDirectory(string baseDir, params string[] relativeCandidates)
    {
        foreach (var rel in relativeCandidates)
        {
            var normRel = rel.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var candidate = Path.Combine(baseDir, normRel);
            if (Directory.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private static List<string> GetDefaultSearchRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Drives
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                roots.Add(drive.RootDirectory.FullName);
                roots.Add(Path.Combine(drive.RootDirectory.FullName, "AI"));
            }
        }
        catch
        {
            roots.Add(@"C:\");
            roots.Add(@"C:\AI");
        }

        // User profile & AppData
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            roots.Add(userProfile);
            roots.Add(Path.Combine(userProfile, "AI"));
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            roots.Add(Path.Combine(appData, "AI"));
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            roots.Add(Path.Combine(localAppData, "AI"));
            roots.Add(Path.Combine(localAppData, "Programs"));
        }

        return roots.Where(Directory.Exists).ToList();
    }
}
