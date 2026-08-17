# Flexible Tool Paths, Ecosystem Auto-Discovery & Configurable Onboarding Design

- **Date:** 2026-08-17
- **Topic:** Configurable Application & Model Paths, AI Tool Auto-Discovery, Desktop File/Folder Pickers, Parameterized Scripts, and Comprehensive Test Coverage
- **Status:** Approved / In Review

---

## 1. Overview & Problem Statement

`LocalLLMServerManager` historically assumed fixed folder structures (such as `D:\AI` or `%APPDATA%\AI`) for external AI runtimes (ComfyUI, Stable Diffusion WebUI Forge / Automatic1111, Ollama) and their associated model weights, checkpoints, LoRAs, workflows, and 3D outputs.

Because these external runtimes are independently installed and managed by users, the application must:
1. Eliminate hardcoded assumptions like `D:\AI` or static drive paths.
2. Automatically discover where AI tools and model directories are installed according to standard ecosystem conventions.
3. Provide intuitive in-app configuration with native folder and file browsing dialogs (`IStorageProvider`) and real-time path validation status in both the Desktop Avalonia UI and Web Dashboard.
4. Parameterize and modernize helper scripts (`setup_ai_tools.ps1`, `download_models.ps1`, `install_comfy_nodes.ps1`, Python scripts) so they sync with `settings.json`, accept CLI arguments, and fallback gracefully to standard locations.
5. Maintain >90% code test coverage, update all documentation, and bump the application version.

---

## 2. Ecosystem Standard Paths & Auto-Discovery Architecture

### 2.1 Standard Discovery Targets

| Tool / Component | Ecosystem Standard & Default Locations | Discovery Mechanism |
| :--- | :--- | :--- |
| **Ollama** | • Binary: `%LOCALAPPDATA%\Programs\Ollama\ollama.exe`<br>• Models: `%USERPROFILE%\.ollama\models`<br>• Env: `OLLAMA_MODELS` | 1. Check `PATH` (`where.exe ollama` / `Get-Command ollama`)<br>2. Scan `%LOCALAPPDATA%\Programs\Ollama\ollama.exe`<br>3. Inspect `OLLAMA_MODELS` env variable |
| **ComfyUI** | • Desktop App: `%LOCALAPPDATA%\Programs\ComfyUI`<br>• Portable: `%USERPROFILE%\ComfyUI`, `%USERPROFILE%\ComfyUI_windows_portable`, `C:\ComfyUI`, `D:\ComfyUI`<br>• Binary / Batch: `run_nvidia_gpu.bat`, `run_cpu.bat`, or `python_embeded\python.exe main.py`<br>• Models: `<comfy_root>\models\` (`checkpoints`, `loras`, `unet`, `vae`, `clip`)<br>• Custom Nodes: `<comfy_root>\custom_nodes\`<br>• Workflows: `<comfy_root>\user\default\workflows` or `<comfy_root>\workflows` | 1. Check active processes (`python.exe` running ComfyUI)<br>2. Check `%LOCALAPPDATA%\Programs\ComfyUI`<br>3. Scan standard root directories across available fixed drives (`C:\`, `D:\`, `E:\`) and `%USERPROFILE%`<br>4. Validate presence of `run_nvidia_gpu.bat` or `main.py` |
| **SD WebUI Forge / A1111** | • Roots: `%USERPROFILE%\stable-diffusion-webui-forge`, `%USERPROFILE%\stable-diffusion-webui`, `C:\stable-diffusion-webui-forge`, `C:\SD_Forge`, `D:\SD_Forge`<br>• Runner: `webui-user.bat`, `webui.bat`<br>• Models: `<forge_root>\models\Stable-diffusion`, `<forge_root>\models\Lora` | 1. Check active processes<br>2. Scan standard directories across fixed drives and `%USERPROFILE%`<br>3. Validate `webui-user.bat` and `models\Stable-diffusion` directory |
| **Shared Outputs & Workflows** | • 3D Outputs: `%USERPROFILE%\Documents\3D_Outputs` or app-relative `output_3d`<br>• Workflows: `<comfy_root>\workflows` or app-relative `Workflows/` | Scan ComfyUI install or default to user documents / app directories |

---

## 3. Backend Architecture & Services

### 3.1 `IToolDiscoveryService` & `ToolDiscoveryService`

A new backend service `LocalLLMServerManager.Services.IToolDiscoveryService` added to dependency injection:

```csharp
namespace LocalLLMServerManager.Services;

public interface IToolDiscoveryService
{
    Task<DiscoveredToolsResult> DetectAllToolsAsync();
    DiscoveredToolInfo DetectOllama();
    DiscoveredToolInfo DetectComfyUi();
    DiscoveredToolInfo DetectForge();
    PathValidationResult ValidatePath(string? path, PathTargetType targetType);
}

public record DiscoveredToolInfo(
    bool IsInstalled,
    string? ExecutablePath,
    string? RootDirectory,
    string? ModelsDirectory,
    string? WorkflowsDirectory,
    string StatusMessage
);

public record DiscoveredToolsResult(
    DiscoveredToolInfo Ollama,
    DiscoveredToolInfo ComfyUi,
    DiscoveredToolInfo Forge,
    string SuggestedThreeDPath,
    string SuggestedWorkflowsPath
);

public enum PathTargetType
{
    Executable,
    Directory
}

public record PathValidationResult(bool Exists, bool IsValid, string? ErrorMessage);
```

### 3.2 `AppSettings` Model Modernization

Remove hardcoded fallback paths. Paths default to empty strings, triggering dynamic resolution via `ToolDiscoveryService` when unconfigured:

```csharp
namespace LocalLLMServerManager;

public record AppSettings(
    string ComfyUiExecutablePath = "",
    string ForgeExecutablePath = "",
    string OllamaExecutablePath = "ollama",
    string ForgeModelsPath = "",
    string ComfyModelsPath = "",
    string ThreeDModelsPath = "",
    string WorkflowsPath = "",
    string ComfyUiUrl = "http://127.0.0.1:8188",
    string PreferredImageEngine = "comfy",
    string LanAccessUrl = "http://127.0.0.1:5246",
    string SelectedThemeStyle = "semi",
    string ServiceName = "LocalLLMServerManager",
    string PublishOutputPath = "C:\\LocalLLMServerManager"
);
```

### 3.3 New Endpoints (`Endpoints/DiscoveryEndpoints.cs`)

* `GET /api/system/tools/detect`: Runs the full discovery scan and returns `DiscoveredToolsResult`.
* `POST /api/system/tools/apply-detected`: Automatically updates any unset/missing paths in `settings.json` with detected paths.
* `POST /api/system/tools/validate`: Validates an array of paths for existence and execution safety.

---

## 4. Desktop UI (Avalonia) & Web Dashboard

### 4.1 Avalonia Desktop UI (`SettingsTabControl.axaml` & `SettingsViewModel.cs`)

1. **Auto-Detect Button**: A prominent `"🔍 Auto-Detect Installed Tools"` button in the Settings view.
2. **File and Folder Pickers**:
   - `Browse File...` button next to Executables: Opens `IStorageProvider.OpenFilePickerAsync` with filters for `.bat`, `.cmd`, and `.exe`.
   - `Browse Folder...` button next to Directories: Opens `IStorageProvider.OpenFolderPickerAsync`.
3. **Live Status Indicators**: Visual status pills next to each path:
   - 🟢 `Verified` (Path exists on disk)
   - ⚠️ `Missing` (Path does not exist on disk)
   - 🔍 `Auto-Discovered` (Suggested from standard location)

### 4.2 Web Dashboard

1. **Auto-Discovery Card**: Shows detected Ollama, ComfyUI, and Forge paths with one-click "Apply Detected Paths".
2. **Path Status Badges**: Visual indicator icons next to all configured path inputs.

---

## 5. Helper Script & Installer Modernization

### 5.1 PowerShell Scripts (`setup_ai_tools.ps1`, `download_models.ps1`, `install_comfy_nodes.ps1`, `fix_comfy.ps1`)

* Parameterize with standard arguments:
  ```powershell
  param(
      [string]$TargetDir = "",
      [string]$ModelsDir = "",
      [switch]$Interactive
  )
  ```
* Priority:
  1. CLI parameters (`-TargetDir`, `-ModelsDir`).
  2. `settings.json` values if present.
  3. Standard ecosystem defaults (`$env:USERPROFILE\ComfyUI`, `$env:USERPROFILE\models`).
  4. Interactive prompt if `-Interactive` is specified.
* `extra_model_paths.yaml` in ComfyUI will be dynamically written using the resolved models directory.

### 5.2 Python Scripts (`download_all.py`, `hf_download.py`)

* Add `argparse` with `--output-dir` and `--models-dir` flags, falling back to `settings.json` or `~/models`.

### 5.3 Inno Setup Installer (`installer.iss`)

* Maintain clean portable directory `{autopf}\LocalLLMServerManager` without hardcoding dependency directories.

---

## 6. Automated Testing & Verification Strategy

1. **Unit Tests (`LocalLLMServerManager.Tests`)**:
   - `ToolDiscoveryServiceTests`: Test discovery with mocked filesystem/environment, PATH resolution, and fallback handling.
   - `AppSettingsTests`: Verify default values, serialization, and dynamic path resolution.
   - `DiscoveryEndpointsTests`: Test `GET /api/system/tools/detect`, `POST /api/system/tools/apply-detected`, and `POST /api/system/tools/validate`.
   - `SettingsViewModelTests`: Test auto-detect commands, browse file/folder commands, validation states, and saving settings.
2. **Coverage Goal**:
   - Maintain total project code coverage above 90%.
3. **Documentation & Versioning**:
   - Bump version to `3.5.0` in `LocalLLMServerManager.csproj`, `Shared.csproj`, `installer.iss`, `README.md`.
   - Update `REQUIREMENTS.md`, `DEVELOPMENT_GUIDE.md`, `TEST_COVERAGE.md`.
   - Ensure `npm run lint` and `npx tsc --noEmit` / `dotnet test` all pass cleanly.
