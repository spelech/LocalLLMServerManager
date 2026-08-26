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
        var ffmpegTask = Task.Run(DetectFFmpeg);
        var pythonTask = Task.Run(DetectPythonEnvironment);

        await Task.WhenAll(ollamaTask, comfyTask, forgeTask, audioTask, ffmpegTask, pythonTask);

        var ollama = await ollamaTask;
        var comfy = await comfyTask;
        var forge = await forgeTask;
        var audio = await audioTask;
        var ffmpeg = await ffmpegTask;
        var python = await pythonTask;

        var suggested3D = comfy.ModelsDirectory ?? forge.ModelsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI", "3D");
        var suggestedWorkflows = comfy.WorkflowsDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI", "Workflows");

        return new DiscoveredToolsResult(
            Ollama: ollama,
            ComfyUi: comfy,
            Forge: forge,
            SuggestedThreeDPath: suggested3D,
            SuggestedWorkflowsPath: suggestedWorkflows,
            AudioEngine: audio,
            FFmpeg: ffmpeg,
            PythonEnvironment: python
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
        // 1. Check running processes
        try
        {
            var runningComfy = Process.GetProcessesByName("Comfy Desktop")
                .Concat(Process.GetProcessesByName("ComfyUI"))
                .Concat(Process.GetProcessesByName("comfy"))
                .FirstOrDefault();
            if (runningComfy != null)
            {
                try
                {
                    var procPath = runningComfy.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(procPath) && File.Exists(procPath))
                    {
                        var dir = Path.GetDirectoryName(procPath)!;
                        var modelsDir = FindDirectory(dir, "models", "ComfyUI/models");
                        var workflowsDir = FindDirectory(dir, "user/default/workflows", "ComfyUI/user/default/workflows", "workflows", "ComfyUI/workflows");
                        return new DiscoveredToolInfo(
                            IsInstalled: true,
                            ExecutablePath: procPath,
                            RootDirectory: dir,
                            ModelsDirectory: modelsDir,
                            WorkflowsDirectory: workflowsDir,
                            StatusMessage: $"Discovered running ComfyUI at {procPath}"
                        );
                    }
                }
                catch
                {
                    // Ignore process module inspection errors
                }
            }
        }
        catch
        {
            // Ignore process enumeration failures
        }

        var targetSubdirs = new[]
        {
            "ComfyUI_windows_portable",
            "ComfyUI",
            "comfyui",
            "ComfyUI_windows_portable_nvidia_cu121_or_cpu",
            "comfy",
            "Comfy Desktop",
            "comfy-desktop",
            "ComfyDesktop"
        };

        var runnerNames = new[]
        {
            "run_nvidia_gpu_fast_fp16_accumulation.bat",
            "run_nvidia_gpu.bat",
            "run_directml.bat",
            "run_cpu.bat",
            "run.bat",
            "run.sh",
            "start.sh",
            "main.py",
            "Comfy Desktop.exe",
            "comfy-desktop.exe",
            "ComfyUI.exe",
            "comfy.exe"
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
            "webui.sh",
            "webui-user.sh",
            "run.bat",
            "run.sh",
            "start.sh",
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
            "kokoro-fastapi",
            "Kokoro-FastAPI",
            "alltalk_tts",
            "alltalk",
            "AllTalk",
            "kokoro",
            "Kokoro",
            "tts",
            "TTS"
        };

        var runnerNames = new[]
        {
            "run.bat",
            "start.bat",
            "run.sh",
            "start.sh",
            "main.py",
            "app.py",
            "api.py"
        };

        foreach (var baseRoot in _searchRoots)
        {
            if (!Directory.Exists(baseRoot))
                continue;

            var candidateDirectories = new List<string>();
            var audioEnginesDir = Path.Combine(baseRoot, "audio", "engines");
            var audioDir = Path.Combine(baseRoot, "audio");

            foreach (var sub in targetSubdirs)
            {
                var dirInEngines = ResolveActualDirectory(audioEnginesDir, sub);
                if (dirInEngines != null && !candidateDirectories.Contains(dirInEngines))
                    candidateDirectories.Add(dirInEngines);

                var dirInAudio = ResolveActualDirectory(audioDir, sub);
                if (dirInAudio != null && !candidateDirectories.Contains(dirInAudio))
                    candidateDirectories.Add(dirInAudio);

                var dirInRoot = ResolveActualDirectory(baseRoot, sub);
                if (dirInRoot != null && !candidateDirectories.Contains(dirInRoot))
                    candidateDirectories.Add(dirInRoot);
            }

            if (Directory.Exists(audioEnginesDir) && !candidateDirectories.Contains(audioEnginesDir))
                candidateDirectories.Add(audioEnginesDir);
            if (Directory.Exists(audioDir) && !candidateDirectories.Contains(audioDir))
                candidateDirectories.Add(audioDir);
            if (!candidateDirectories.Contains(baseRoot))
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
                                                    Directory.Exists(Path.Combine(dir, "custom_voices")) ||
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
                    var modelsDir = FindDirectory(dir, "voices", "custom_voices", "models", "voice_models");
                    if (modelsDir == null)
                    {
                        var parent = Path.GetDirectoryName(dir);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            if (Directory.Exists(Path.Combine(parent, "custom_voices")))
                                modelsDir = Path.Combine(parent, "custom_voices");
                            else if (Directory.Exists(Path.Combine(parent, "voices")))
                                modelsDir = Path.Combine(parent, "voices");
                            else
                            {
                                var grandParent = Path.GetDirectoryName(parent);
                                if (!string.IsNullOrEmpty(grandParent))
                                {
                                    if (Directory.Exists(Path.Combine(grandParent, "custom_voices")))
                                        modelsDir = Path.Combine(grandParent, "custom_voices");
                                    else if (Directory.Exists(Path.Combine(grandParent, "voices")))
                                        modelsDir = Path.Combine(grandParent, "voices");
                                }
                            }
                        }
                    }

                    if (modelsDir == null)
                    {
                        var audioCustom = Path.Combine(baseRoot, "audio", "custom_voices");
                        if (Directory.Exists(audioCustom))
                            modelsDir = audioCustom;
                    }

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

        var kokoroCli = FindExecutableOnPath(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "kokoro-fastapi.exe" : "kokoro-fastapi");
        if (!string.IsNullOrEmpty(kokoroCli))
        {
            return new DiscoveredToolInfo(
                IsInstalled: true,
                ExecutablePath: kokoroCli,
                RootDirectory: Path.GetDirectoryName(kokoroCli),
                ModelsDirectory: null,
                WorkflowsDirectory: null,
                StatusMessage: $"Discovered Kokoro TTS CLI at {kokoroCli}"
            );
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

    public DiscoveredToolInfo DetectFFmpeg()
    {
        var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ffmpeg.exe" : "ffmpeg";
        string? exePath = null;

        // 1. Check custom search roots & common portable locations first
        foreach (var root in _searchRoots)
        {
            var candidates = new[]
            {
                Path.Combine(root, binaryName),
                Path.Combine(root, "bin", binaryName),
                Path.Combine(root, "FFmpeg", "bin", binaryName),
                Path.Combine(root, "ffmpeg", "bin", binaryName),
                Path.Combine(root, "ffmpeg", binaryName),
                Path.Combine(root, "Microsoft", "WinGet", "Links", binaryName)
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

        // 2. Check PATH
        if (string.IsNullOrEmpty(exePath))
        {
            exePath = FindExecutableOnPath(binaryName);
        }

        // 3. Check running processes
        if (string.IsNullOrEmpty(exePath))
        {
            try
            {
                var processes = Process.GetProcessesByName("ffmpeg");
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
                        // Ignore process module inspection errors
                    }
                }
            }
            catch
            {
                // Ignore process enumeration failures
            }
        }

        // 4. Check OS-specific standard directories
        if (string.IsNullOrEmpty(exePath))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                var winCandidates = new[]
                {
                    !string.IsNullOrEmpty(localAppData) ? Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "ffmpeg.exe") : null,
                    !string.IsNullOrEmpty(progFiles) ? Path.Combine(progFiles, "FFmpeg", "bin", "ffmpeg.exe") : null,
                    !string.IsNullOrEmpty(userProfile) ? Path.Combine(userProfile, "scoop", "shims", "ffmpeg.exe") : null,
                    @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
                    @"C:\FFmpeg\bin\ffmpeg.exe"
                };

                foreach (var candidate in winCandidates)
                {
                    if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                    {
                        exePath = candidate;
                        break;
                    }
                }
            }
            else
            {
                var posixCandidates = new[]
                {
                    "/usr/bin/ffmpeg",
                    "/usr/local/bin/ffmpeg",
                    "/snap/bin/ffmpeg",
                    "/opt/ffmpeg/bin/ffmpeg"
                };

                foreach (var candidate in posixCandidates)
                {
                    if (File.Exists(candidate))
                    {
                        exePath = candidate;
                        break;
                    }
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
                StatusMessage: "FFmpeg not detected"
            );
        }

        var hardwareAccelerators = new List<string>();
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "-encoders",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            if (proc.WaitForExit(1500))
            {
                if (output.Contains("nvenc", StringComparison.OrdinalIgnoreCase)) hardwareAccelerators.Add("NVENC (CUDA)");
                if (output.Contains("qsv", StringComparison.OrdinalIgnoreCase)) hardwareAccelerators.Add("Intel QuickSync (QSV)");
                if (output.Contains("vaapi", StringComparison.OrdinalIgnoreCase)) hardwareAccelerators.Add("VAAPI");
                if (output.Contains("amf", StringComparison.OrdinalIgnoreCase)) hardwareAccelerators.Add("AMD AMF");
            }
        }
        catch
        {
            // Ignore execution failure
        }

        var hwText = hardwareAccelerators.Count > 0 ? $" (Hardware: {string.Join(", ", hardwareAccelerators)})" : "";

        return new DiscoveredToolInfo(
            IsInstalled: true,
            ExecutablePath: exePath,
            RootDirectory: Path.GetDirectoryName(exePath),
            ModelsDirectory: null,
            WorkflowsDirectory: null,
            StatusMessage: $"Discovered FFmpeg at {exePath}{hwText}"
        );
    }

    public DiscoveredToolInfo DetectPythonEnvironment()
    {
        var pythonNames = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "python.exe", "python3.exe", "python" }
            : new[] { "python3", "python" };

        string? exePath = null;

        // 1. Check custom search roots & standard virtualenvs first
        foreach (var root in _searchRoots)
        {
            var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[]
                {
                    Path.Combine(root, "python.exe"),
                    Path.Combine(root, "Scripts", "python.exe"),
                    Path.Combine(root, "venv", "Scripts", "python.exe"),
                    Path.Combine(root, ".venv", "Scripts", "python.exe"),
                    Path.Combine(root, "python_embeded", "python.exe"),
                    Path.Combine(root, "python", "python.exe")
                }
                : new[]
                {
                    Path.Combine(root, "bin", "python3"),
                    Path.Combine(root, "bin", "python"),
                    Path.Combine(root, "venv", "bin", "python3"),
                    Path.Combine(root, "venv", "bin", "python"),
                    Path.Combine(root, ".venv", "bin", "python3"),
                    Path.Combine(root, ".venv", "bin", "python"),
                    Path.Combine(root, "python3"),
                    Path.Combine(root, "python")
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

        // 2. Check PATH
        if (string.IsNullOrEmpty(exePath))
        {
            foreach (var name in pythonNames)
            {
                exePath = FindExecutableOnPath(name);
                if (!string.IsNullOrEmpty(exePath))
                    break;
            }
        }

        // 3. Check running processes
        if (string.IsNullOrEmpty(exePath))
        {
            try
            {
                var processes = Process.GetProcessesByName("python")
                    .Concat(Process.GetProcessesByName("python3"))
                    .Concat(Process.GetProcessesByName("uvicorn"));

                foreach (var proc in processes)
                {
                    try
                    {
                        var procPath = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(procPath) && File.Exists(procPath))
                        {
                            exePath = procPath;
                            break;
                        }
                    }
                    catch
                    {
                        // Ignore process module inspection errors
                    }
                }
            }
            catch
            {
                // Ignore process enumeration failures
            }
        }

        // 4. Check OS-specific standard locations
        if (string.IsNullOrEmpty(exePath))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

                var candidates = new List<string>();
                if (!string.IsNullOrEmpty(localAppData))
                {
                    var pyDir = Path.Combine(localAppData, "Programs", "Python");
                    if (Directory.Exists(pyDir))
                    {
                        candidates.AddRange(Directory.GetFiles(pyDir, "python.exe", SearchOption.AllDirectories));
                    }
                }

                if (!string.IsNullOrEmpty(progFiles))
                {
                    var pyDir = Path.Combine(progFiles, "Python");
                    if (Directory.Exists(pyDir))
                    {
                        candidates.AddRange(Directory.GetFiles(pyDir, "python.exe", SearchOption.AllDirectories));
                    }
                }

                exePath = candidates.FirstOrDefault(File.Exists);
            }
            else
            {
                var posixCandidates = new[]
                {
                    "/usr/bin/python3",
                    "/usr/local/bin/python3",
                    "/usr/bin/python",
                    "/opt/venv/bin/python3",
                    "/opt/venv/bin/python"
                };

                foreach (var candidate in posixCandidates)
                {
                    if (File.Exists(candidate))
                    {
                        exePath = candidate;
                        break;
                    }
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
                StatusMessage: "Python environment not detected"
            );
        }

        var missingAudioPackages = new List<string>();
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "-c \"import sys; [print(m) for m in ['kokoro_onnx','soundfile','fastapi','uvicorn','openai'] if __import__('importlib.util').util.find_spec(m) is None]\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            if (proc.WaitForExit(2000))
            {
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                missingAudioPackages.AddRange(lines);
            }
        }
        catch
        {
            // Ignore inspection failures
        }

        var pkgStatus = missingAudioPackages.Count == 0
            ? " (Audio TTS packages installed)"
            : $" (Missing audio packages: {string.Join(", ", missingAudioPackages)})";

        return new DiscoveredToolInfo(
            IsInstalled: true,
            ExecutablePath: exePath,
            RootDirectory: Path.GetDirectoryName(exePath),
            ModelsDirectory: null,
            WorkflowsDirectory: null,
            StatusMessage: $"Discovered Python at {exePath}{pkgStatus}"
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

    private static string? ResolveActualDirectory(string parentDir, string subName)
    {
        if (!Directory.Exists(parentDir))
            return null;

        try
        {
            var match = Directory.GetDirectories(parentDir)
                .FirstOrDefault(d => string.Equals(Path.GetFileName(d), subName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }
        catch
        {
            // Ignore access errors
        }

        var direct = Path.Combine(parentDir, subName);
        return Directory.Exists(direct) ? direct : null;
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
            roots.Add(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\" : "/");
            roots.Add(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\AI" : "/AI");
        }

        // User profile & AppData
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            roots.Add(userProfile);
            roots.Add(Path.Combine(userProfile, "AI"));
            roots.Add(Path.Combine(userProfile, ".local", "share"));
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
            roots.Add(Path.Combine(localAppData, "Microsoft", "WinGet", "Links"));
        }

        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(progFiles))
        {
            roots.Add(progFiles);
        }

        var progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(progFilesX86))
        {
            roots.Add(progFilesX86);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            roots.Add("/opt");
            roots.Add("/opt/AI");
            roots.Add("/srv");
            roots.Add("/srv/AI");
            roots.Add("/data");
            roots.Add("/data/AI");
            roots.Add("/mnt");
            roots.Add("/usr/local");
            roots.Add("/var/lib");
        }

        return roots.Where(Directory.Exists).ToList();
    }
}
