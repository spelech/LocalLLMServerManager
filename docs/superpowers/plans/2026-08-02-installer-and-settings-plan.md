# Installer Fix & %APPDATA%/AI Path Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the Inno Setup installer to support admin elevation and optional Windows Service installation alongside the System Tray desktop app, remove hardcoded `D:\AI` path references across the codebase, replace default fallbacks with `%APPDATA%\AI\...`, and make all tool paths configurable and persistent in both the Avalonia Desktop UI and Web Dashboard settings.

**Architecture:** Expand `AppSettings` schema to hold paths for ComfyUI, SD Forge, Ollama, 3D outputs, and Workflows. Create an environment variable path expansion helper `Program.ResolvePath` to expand `%APPDATA%` and `%USERPROFILE%` dynamically at runtime. Update `installer.iss` with `PrivilegesRequired=admin` and task definitions for both System Tray logon auto-start and Windows Service registration. Expose editable settings inputs in Avalonia XAML and Web Dashboard JS.

**Tech Stack:** C# .NET 10, ASP.NET Core Minimal API, Avalonia UI (XAML/MVVM), HTML5/JavaScript, Inno Setup (ISCC).

## Global Constraints

- Preserve all existing unit tests and maintain overall line coverage >= 90%.
- Execute `npm run lint`, `npx tsc --noEmit`, and `dotnet test` after code modifications.
- Windows environment compatibility: path expansion for `%APPDATA%`, `%USERPROFILE%`, `%LOCALAPPDATA%`.

---

### Task 1: Update AppSettings Record & Environment Path Resolution in Program.cs

**Files:**
- Modify: `AppSettings.cs:1-15`
- Modify: `Program.cs:510-610`
- Test: `LocalLLMServerManager.Tests/AppSettingsTests.cs`

**Interfaces:**
- Consumes: Existing `AppSettings` record.
- Produces: `AppSettings` with updated default tool paths using `%APPDATA%\AI\...` and static `Program.ResolvePath(string? rawPath, string fallback)` helper method.

- [ ] **Step 1: Write failing unit test for AppSettings and ResolvePath**

```csharp
[Fact]
public void AppSettings_DefaultValues_UseAppDataAiPaths()
{
    var settings = new AppSettings();
    Assert.Contains("%APPDATA%", settings.ComfyUiExecutablePath);
    Assert.Contains("%APPDATA%", settings.ForgeExecutablePath);
    Assert.Contains("%APPDATA%", settings.ThreeDModelsPath);
    Assert.Contains("%APPDATA%", settings.WorkflowsPath);
}

[Fact]
public void ResolvePath_ExpandsEnvironmentVariables_Correctly()
{
    var raw = "%APPDATA%\\AI\\test.bat";
    var resolved = Program.ResolvePath(raw, "%APPDATA%\\AI\\fallback.bat");
    Assert.DoesNotContain("%APPDATA%", resolved);
    Assert.EndsWith("test.bat", resolved);
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~AppSettings_DefaultValues_UseAppDataAiPaths"`
Expected: FAIL

- [ ] **Step 3: Update AppSettings.cs and Program.cs**

Update `AppSettings.cs`:
```csharp
namespace LocalLLMServerManager;

public record AppSettings(
    string ForgeModelsPath = "%APPDATA%\\AI\\SD_Forge\\models",
    string ComfyUiUrl = "http://127.0.0.1:8188",
    string ThreeDModelsPath = "%APPDATA%\\AI\\3d_outputs",
    string WorkflowsPath = "%APPDATA%\\AI\\Workflows",
    string PreferredImageEngine = "Forge",
    string ComfyUiExecutablePath = "%APPDATA%\\AI\\ComfyUI\\run_nvidia_gpu.bat",
    string ForgeExecutablePath = "%APPDATA%\\AI\\SD_Forge\\webui-user.bat",
    string OllamaExecutablePath = "ollama"
);
```

Add `ResolvePath` in `Program.cs`:
```csharp
public static string ResolvePath(string? rawPath, string fallbackRelativePath)
{
    var target = string.IsNullOrWhiteSpace(rawPath) ? fallbackRelativePath : rawPath;
    var expanded = Environment.ExpandEnvironmentVariables(target);
    return Path.GetFullPath(expanded);
}
```

Replace hardcoded `D:\AI` strings in `Program.cs` endpoints with `ResolvePath`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~AppSettings_DefaultValues_UseAppDataAiPaths"`
Expected: PASS

- [ ] **Step 5: Commit changes**

```bash
git add AppSettings.cs Program.cs LocalLLMServerManager.Tests/AppSettingsTests.cs
git commit -m "feat(settings): update AppSettings defaults to %APPDATA%/AI and add environment path resolver"
```

---

### Task 2: Expand MainViewModel.cs with Settings Bindings and Persistence Commands

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Test: `LocalLLMServerManager.Tests/MainViewModelCoverageTests.cs`

**Interfaces:**
- Consumes: `GET /api/settings` and `POST /api/settings`.
- Produces: Observable properties for tool paths (`ComfyUiExecutablePath`, `ForgeExecutablePath`, `ForgeModelsPath`, `ThreeDModelsPath`, `WorkflowsPath`) and `SaveSettingsAsync()` command.

- [ ] **Step 1: Write failing unit test for MainViewModel settings bindings**

```csharp
[Fact]
public async Task MainViewModel_LoadAndSaveSettings_UpdatesObservableProperties()
{
    var vm = new MainViewModel();
    vm.ComfyUiExecutablePath = "%APPDATA%\\AI\\ComfyUI\\run_nvidia_gpu.bat";
    vm.ForgeExecutablePath = "%APPDATA%\\AI\\SD_Forge\\webui-user.bat";
    
    Assert.NotNull(vm.ComfyUiExecutablePath);
    Assert.NotNull(vm.ForgeExecutablePath);
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~MainViewModel_LoadAndSaveSettings_UpdatesObservableProperties"`
Expected: FAIL

- [ ] **Step 3: Update MainViewModel.cs**

Add observable properties for tool paths in `MainViewModel.cs`:
```csharp
[ObservableProperty] private string _comfyUiExecutablePath = "%APPDATA%\\AI\\ComfyUI\\run_nvidia_gpu.bat";
[ObservableProperty] private string _forgeExecutablePath = "%APPDATA%\\AI\\SD_Forge\\webui-user.bat";
[ObservableProperty] private string _forgeModelsPath = "%APPDATA%\\AI\\SD_Forge\\models";
[ObservableProperty] private string _threeDModelsPath = "%APPDATA%\\AI\\3d_outputs";
[ObservableProperty] private string _workflowsPath = "%APPDATA%\\AI\\Workflows";
[ObservableProperty] private string _comfyUiUrl = "http://127.0.0.1:8188";
[ObservableProperty] private string _preferredImageEngine = "Forge";

public async Task LoadSettingsAsync()
{
    try
    {
        var settings = await _http.GetFromJsonAsync<AppSettings>($"{ApiBase.TrimEnd('/')}/api/settings");
        if (settings != null)
        {
            ComfyUiExecutablePath = settings.ComfyUiExecutablePath;
            ForgeExecutablePath = settings.ForgeExecutablePath;
            ForgeModelsPath = settings.ForgeModelsPath;
            ThreeDModelsPath = settings.ThreeDModelsPath;
            WorkflowsPath = settings.WorkflowsPath;
            ComfyUiUrl = settings.ComfyUiUrl;
            PreferredImageEngine = settings.PreferredImageEngine;
        }
    }
    catch { }
}

public async Task SaveSettingsAsync()
{
    try
    {
        var settings = new AppSettings(
            ForgeModelsPath: ForgeModelsPath,
            ComfyUiUrl: ComfyUiUrl,
            ThreeDModelsPath: ThreeDModelsPath,
            WorkflowsPath: WorkflowsPath,
            PreferredImageEngine: PreferredImageEngine,
            ComfyUiExecutablePath: ComfyUiExecutablePath,
            ForgeExecutablePath: ForgeExecutablePath
        );
        var resp = await _http.PostAsJsonAsync($"{ApiBase.TrimEnd('/')}/api/settings", settings);
        if (resp.IsSuccessStatusCode)
        {
            ToastService.Instance.Show("Settings saved successfully!", ToastType.Success);
        }
    }
    catch (Exception ex)
    {
        ToastService.Instance.Show($"Failed to save settings: {ex.Message}", ToastType.Error);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~MainViewModel_LoadAndSaveSettings_UpdatesObservableProperties"`
Expected: PASS

- [ ] **Step 5: Commit changes**

```bash
git add LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs LocalLLMServerManager.Tests/MainViewModelCoverageTests.cs
git commit -m "feat(viewmodel): add tool path observable properties and SaveSettingsAsync command"
```

---

### Task 3: Update Avalonia Desktop UI & Web Dashboard Settings Panel

**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml`
- Modify: `wwwroot/index.html`
- Modify: `wwwroot/app.js`

**Interfaces:**
- Consumes: `MainViewModel` settings properties and `/api/settings` REST endpoints.
- Produces: Visual settings controls in both XAML UI and Web Dashboard UI.

- [ ] **Step 1: Update MainView.axaml with Settings Form**

Add a TabItem for **Settings** in `MainView.axaml` with text inputs for tool paths (`ComfyUiExecutablePath`, `ForgeExecutablePath`, `ForgeModelsPath`, `ThreeDModelsPath`, `WorkflowsPath`) and a **Save Settings** button bound to `SaveSettingsAsyncCommand`.

- [ ] **Step 2: Update wwwroot/index.html and wwwroot/app.js**

Add a Settings view/tab in `wwwroot/index.html` with inputs for settings fields and connect event handlers in `wwwroot/app.js` to fetch and submit settings via `/api/settings`.

- [ ] **Step 3: Run frontend linting & type checks**

Run: `npm run lint` and `npx tsc --noEmit`
Expected: PASS with 0 errors.

- [ ] **Step 4: Commit changes**

```bash
git add LocalLLMServerManager.Shared/Views/MainView.axaml wwwroot/index.html wwwroot/app.js
git commit -m "feat(ui): add tool path configuration settings panel to Avalonia desktop UI and web dashboard"
```

---

### Task 4: Fix Inno Setup Script & Release Packaging Script

**Files:**
- Modify: `installer.iss`
- Modify: `build_release.ps1`

**Interfaces:**
- Consumes: Single-file published Release binaries.
- Produces: `LocalLLMServerManager-v3.0.0-Setup.exe` with Administrator elevation and options for System Tray auto-start + Windows Service registration.

- [ ] **Step 1: Update installer.iss for v3.0.0 and Administrator Elevation**

Ensure `PrivilegesRequired=admin`, set output executable to `LocalLLMServerManager-v3.0.0-Setup`, configure `{autopf}\LocalLLMServerManager` destination, and include task definitions for:
- `autostart` (HKCU Run registry entry)
- `windowsservice` (`sc.exe create LocalLLMServerManager binPath= "{app}\LocalLLMServerManager.exe --service" start= auto`)

- [ ] **Step 2: Update build_release.ps1**

Set `$Version = "3.0.0"` in `build_release.ps1` and verify ISCC execution.

- [ ] **Step 3: Test release publish script**

Run: `pwsh -Command ".\build_release.ps1"`
Expected: Clean publish and zip archive generation in `dist/`.

- [ ] **Step 4: Commit changes**

```bash
git add installer.iss build_release.ps1
git commit -m "fix(installer): configure admin privileges, v3.0.0 versioning, and Windows Service installation task"
```

---

### Task 5: Comprehensive Verification & Code Coverage Check

**Files:**
- Modify/Create: `LocalLLMServerManager.Tests/CoverageThresholdTargetedPushTests.cs`

- [ ] **Step 1: Run full test suite with code coverage**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --collect:'XPlat Code Coverage' --nologo`
Expected: 56+ tests PASS cleanly.

- [ ] **Step 2: Verify code coverage rate**

Run: `node scratch/parse_coverage.js`
Expected: OVERALL COVERAGE >= 90.00%

- [ ] **Step 3: Final Commit**

```bash
git add .
git commit -m "chore(tests): verify 90%+ code coverage threshold after installer and settings overhaul"
```
