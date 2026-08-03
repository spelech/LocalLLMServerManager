# Design Specification: Installer Fix, Path Configuration & UI Settings Persistence

**Date**: 2026-08-02  
**Version**: 3.0.0  
**Status**: Proposed / Pending User Review  

---

## 1. Overview & Objectives

This specification defines the design for fixing the installer and making external tool paths fully configurable across the **LocalLLMServerManager** ecosystem.

### Key Deliverables:
1. **Installer Enhancements (`installer.iss` / `build_release.ps1`)**:
   - Elevates installer to Administrator privileges (`PrivilegesRequired=admin`).
   - Interactive installation directory picker (`{autopf}\LocalLLMServerManager` default).
   - Bundles options for both **Desktop System Tray Application (with logon auto-start)** and **Background Windows Service (starts on system boot)**.
2. **Path Configuration & `%APPDATA%\AI` Fallbacks**:
   - Removes all hardcoded `D:\AI` path references.
   - Sets standard defaults relative to `%APPDATA%\AI` (e.g., `%APPDATA%\AI\ComfyUI\run_nvidia_gpu.bat`, `%APPDATA%\AI\SD_Forge\webui-user.bat`, `%APPDATA%\AI\3d_outputs`, `%APPDATA%\AI\Workflows`).
   - Resolves system environment variables (`%APPDATA%`, `%USERPROFILE%`, `%LOCALAPPDATA%`) dynamically at runtime.
3. **UI Settings & Persistence (`settings.json`)**:
   - Expands `AppSettings.cs` record and REST API (`/api/settings`).
   - Edits and persists path settings in both **Avalonia Desktop UI** (`MainView.axaml` + `MainViewModel.cs`) and **Web Dashboard UI** (`wwwroot/index.html` + `wwwroot/app.js`).

---

## 2. Architecture & Data Structures

### 2.1 `AppSettings.cs` Schema
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

### 2.2 Environment Variable Path Resolver (`Program.cs`)
```csharp
public static string ResolvePath(string? rawPath, string fallbackRelativePath)
{
    if (string.IsNullOrWhiteSpace(rawPath))
    {
        rawPath = fallbackRelativePath;
    }
    var expanded = Environment.ExpandEnvironmentVariables(rawPath);
    return Path.GetFullPath(expanded);
}
```

---

## 3. Installer Architecture (`installer.iss`)

```ini
[Setup]
AppId={{D1A39E4C-6721-4E12-A349-8F8D58014E7B}
AppName=Local LLM Server Manager
AppVersion=3.0.0
AppPublisher=LocalLLMServerManager Team
DefaultDirName={autopf}\LocalLLMServerManager
DefaultGroupName=Local LLM Server Manager
PrivilegesRequired=admin
OutputBaseFilename=LocalLLMServerManager-v3.0.0-Setup
OutputDir=dist
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Auto-start System Tray App on user login"; GroupDescription: "System Integration"
Name: "windowsservice"; Description: "Install background Windows Service (starts automatically on system boot)"; GroupDescription: "System Integration"; Flags: checkedonce

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "LocalLLMServerManagerTray"; ValueData: """{app}\LocalLLMServerManager.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "sc.exe"; Parameters: "create LocalLLMServerManager binPath= """"{app}\LocalLLMServerManager.exe"" --service"" start= auto displayName= ""Local LLM Server Manager"""; Tasks: windowsservice; Flags: runhidden
Filename: "net.exe"; Parameters: "start LocalLLMServerManager"; Tasks: windowsservice; Flags: runhidden
Filename: "{app}\LocalLLMServerManager.exe"; Description: "{cm:LaunchProgram,Local LLM Server Manager}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "net.exe"; Parameters: "stop LocalLLMServerManager"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete LocalLLMServerManager"; Flags: runhidden
```

---

## 4. UI Settings Management

### 4.1 Avalonia Desktop UI (`MainView.axaml`)
- Adds a **Settings Tab** displaying inputs for:
  - ComfyUI Executable Path
  - SD Forge Executable Path
  - SD Forge Models Path
  - 3D Outputs Path
  - Workflows Preset Path
  - Preferred Engine (`Forge` / `ComfyUI`)
- Includes **Save Settings** button with instant feedback toast (`ToastService`).

### 4.2 Web Dashboard UI (`wwwroot/index.html` + `wwwroot/app.js`)
- Adds a **Settings Drawer / Modal** in the top navigation bar.
- Queries `GET /api/settings` on load and posts changes to `POST /api/settings`.

---

## 5. Verification & Test Plan

1. **Unit & Integration Tests**:
   - Add tests for `AppSettings` default path resolution with `%APPDATA%`.
   - Update `ProgramEndpointsAndServicesTests.cs` to verify `/api/settings` read/write.
   - Run `dotnet test` and confirm all unit tests pass with >= 90% code coverage.
2. **Build Verification**:
   - Run `build_release.ps1` and verify clean compilation.
   - Confirm published self-contained output in `publish/` and `dist/`.
