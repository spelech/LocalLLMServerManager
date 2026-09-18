# Windows Environment Validation & Handoff Guide

> **Version 3.12.1** | .NET 10 | Windows 10/11 x64 | Avalonia UI | Win32 Job Objects | Playwright E2E

This runbook provides step-by-step instructions and copy-paste ready PowerShell commands for validating **LocalLLMServerManager** on a native Windows workstation. It bridges the gap between headless Linux development and Windows native runtime capabilities.

---

## 📑 Table of Contents

1. [Environment Prerequisites](#1-environment-prerequisites)
2. [Win32 Job Object Process Lifecycle Validation](#2-win32-job-object-process-lifecycle-validation)
3. [Windows Registry GPU Scoring & NVML Telemetry](#3-windows-registry-gpu-scoring--nvml-telemetry)
4. [Playwright Chromium Setup & WASM E2E Testing](#4-playwright-chromium-setup--wasm-e2e-testing)
5. [Avalonia Native Desktop Launch & System Tray Validation](#5-avalonia-native-desktop-launch--system-tray-validation)
6. [Live AI Engine Integration Testing (Ollama / ComfyUI / Forge)](#6-live-ai-engine-integration-testing)
7. [Full Solution Verification Command Matrix](#7-full-solution-verification-command-matrix)
8. [Troubleshooting & Common Pitfalls](#8-troubleshooting--common-pitfalls)

---

## 1. Environment Prerequisites

Run the following checks in **PowerShell 7+** (`pwsh`) or **Windows PowerShell 5.1** running as Administrator where indicated.

### 1.1 Verify .NET 10 SDK

```powershell
# Verify .NET SDK version (must report 10.0.x or higher)
dotnet --version
dotnet --list-sdks
```

**Expected Output:**
```text
10.0.100 (or higher)
```

If .NET 10 is missing, install it via winget:
```powershell
winget install Microsoft.DotNet.SDK.10
```

### 1.2 Verify AI Backends

```powershell
# 1. Check Ollama CLI and daemon connectivity
ollama --version
Invoke-RestMethod -Uri "http://127.0.0.1:11434/api/version" -Method Get

# 2. Check NVIDIA GPU driver and CUDA availability
nvidia-smi

# 3. Verify Git and Python environments (for ComfyUI / Forge)
git --version
python --version
```

**Expected Output:**
- `ollama --version`: `ollama version is 0.5.x` (or newer)
- `Invoke-RestMethod`: returns JSON with `{"version": "0.x.x"}`
- `nvidia-smi`: displays GPU Model (e.g. RTX 4090, RTX 3080), Driver Version, and Total VRAM.

---

## 2. Win32 Job Object Process Lifecycle Validation

### 2.1 Technical Background

`LocalLLMServerManager` uses `Win32JobObject.cs` to bind spawned AI backend processes (Ollama, ComfyUI, Forge) to a Windows Kernel Job Object configured with `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` (`0x2000`).

When `LocalLLMServerManager` terminates (cleanly, via Task Manager, or crash), the Windows kernel guarantees that all child processes attached to the Job Object are immediately killed, preventing orphan Python or Ollama processes from locking VRAM or holding ports open.

### 2.2 Execute Unit Tests for JobObject

```powershell
# Run xUnit JobObject lifecycle tests
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj `
  --filter "FullyQualifiedName~JobObject" `
  -c Release `
  --logger "console;verbosity=detailed"
```

**Expected Output:**
```text
Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2
```

### 2.3 Interactive Orphan-Prevention Stress Test

Run this PowerShell test to prove that child processes terminated with the Job Object do not orphan:

```powershell
# Launch LocalLLMServerManager, start child processes, and verify termination upon exit
dotnet run --project LocalLLMServerManager.csproj

# In another PowerShell window:
# Verify in PowerShell that no orphan child processes remain after stopping LocalLLMServerManager:
Get-Process -Name ollama, python -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, CPU, WorkingSet64
```

**Expected Result:**
No orphaned `ollama` or `python` background child processes exist after application exit.

---

## 3. Windows Registry GPU Scoring & NVML Telemetry

### 3.1 Technical Background

`GpuTelemetryProvider.cs` executes a dual-strategy GPU detection algorithm:
1. **Primary (`nvidia-smi`):** Queries `--query-gpu=name,memory.total,memory.used --format=csv,noheader,nounits` for precise live VRAM readings.
2. **Fallback (Windows Registry Scoring):** Reads `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\000*` keys:
   - Evaluates `DriverDesc` and `ProviderName`.
   - Heuristic scores: NVIDIA (`score = 10`), AMD/Radeon (`score = 5`), Intel (`score = 0`), Basic Render/Virtual (`ignored`).
   - Parses `HardwareInformation.qwMemorySize` (64-bit QWORD) or `HardwareInformation.MemorySize` (32-bit DWORD / binary).

### 3.2 Verify Windows Display Adapter Registry Entries

Run this command in PowerShell to inspect your installed GPU entries:

```powershell
Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\000*" | `
  Select-Object DriverDesc, ProviderName, "HardwareInformation.qwMemorySize", "HardwareInformation.MemorySize" | `
  Format-Table -AutoSize
```

**Sample Output:**
```text
DriverDesc               ProviderName HardwareInformation.qwMemorySize HardwareInformation.MemorySize
----------               ------------ -------------------------------- ------------------------------
NVIDIA GeForce RTX 4090  NVIDIA                            25757220864                     1073741824
```

### 3.3 Validate Telemetry Endpoint Live via HTTP

Start the server and query the `/api/gpu/vram` endpoint:

```powershell
# Query local VRAM telemetry endpoint
$gpu = Invoke-RestMethod -Uri "http://127.0.0.1:5246/api/gpu/vram" -Method Get
$gpu | Format-List
```

**Expected Output:**
```text
gpuName : NVIDIA GeForce RTX 4090
used    : 2.4
total   : 24.0
free    : 21.6
pct     : 10
```

---

## 4. Playwright Chromium Setup & WASM E2E Testing

### 4.1 Technical Background

End-to-End browser validation runs against the compiled WebAssembly build hosted in-process via Kestrel (`AppTestServerFixture`). Playwright launches headless Chromium with WebGL rendering flags (`--use-gl=angle --use-angle=swiftshader --enable-webgl`) to render the full Avalonia WASM Canvas and capture documentation screenshots.

### 4.2 Step 1: Build Test Assembly

```powershell
dotnet build LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj -c Release
```

### 4.3 Step 2: Install Playwright Chromium Drivers

```powershell
# Install Playwright browser dependencies for net10.0
pwsh LocalLLMServerManager.Tests/bin/Release/net10.0/playwright.ps1 install chromium
```

**Expected Output:**
```text
Downloading Chromium 133.0.x (playwright build vxxxx)...
Chromium downloaded to C:\Users\<User>\AppData\Local\ms-playwright\chromium-xxxx
```

### 4.4 Step 3: Execute Playwright WASM E2E Test Suite

```powershell
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj `
  --filter "FullyQualifiedName~PlaywrightWasmE2ETests" `
  -c Release `
  --logger "console;verbosity=detailed"
```

**Expected Output:**
```text
Passed! - Failed: 0, Passed: 6, Skipped: 0, Total: 6
```

### 4.5 Step 4: Run Automated Documentation Screenshot Generator

```powershell
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj `
  --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" `
  -c Release
```

**Verification:**
Check `docs/images/` for freshly captured PNGs:
- `docs/images/dashboard_desktop.png`
- `docs/images/dashboard_ollama.png`
- `docs/images/dashboard_huggingface.png`
- `docs/images/dashboard_civitai.png`
- `docs/images/dashboard_settings.png`
- `docs/images/dashboard_3d_studio.png`

```powershell
Get-ChildItem docs/images/*.png | Select-Object Name, Length, LastWriteTime
```

---

## 5. Avalonia Native Desktop Launch & System Tray Validation

### 5.1 Technical Background

The native Windows application (`LocalLLMServerManager.csproj`) runs an Avalonia 11.2 desktop interface backed by an embedded ASP.NET Core Kestrel server on port `5246`.

Key desktop features:
- **Glassmorphic Fluent / Semi Dark Themes** with dynamic runtime style switching (`App.SetThemeStyle`).
- **Windows System Tray Icon** with context menu.
- **Window Minimize-to-Tray** and persistence (`ShutdownMode.OnExplicitShutdown`).

### 5.2 Launch Native Desktop App

```powershell
# Run native desktop application
dotnet run --project LocalLLMServerManager.csproj -c Release
```

### 5.3 Manual Validation Checklist

| Area | Action | Expected Behavior | Status |
| :--- | :--- | :--- | :--- |
| **Window Frame** | Launch application | Window opens centered with Dark Glassmorphism, 1280x800 minimum bounds. | [ ] |
| **Theme Switcher** | Go to Settings -> Toggle Theme (Semi / Fluent) | Palette transitions immediately between Semi Theme and Fluent Theme without crash. | [ ] |
| **System Tray Icon** | Look at Windows taskbar notification area | Purple/Dark brain icon appears in system tray. | [ ] |
| **Tray Context Menu** | Right-click system tray icon | Menu shows: `Open Dashboard`, `Open Web UI`, `Exit`. | [ ] |
| **Minimize to Tray** | Close window via `[X]` or minimize | Window hides from taskbar; process continues running in tray. | [ ] |
| **Restore from Tray** | Double click tray icon or select `Open Dashboard` | MainWindow restores to foreground with state preserved. | [ ] |
| **Web UI Action** | Click `Open Web UI` from tray menu | Default browser opens `http://127.0.0.1:5246`. | [ ] |
| **Exit Action** | Click `Exit` from tray menu | Application shuts down cleanly and removes tray icon. | [ ] |

---

## 6. Live AI Engine Integration Testing

When Ollama, ComfyUI, or Stable Diffusion Forge are installed and running locally, execute live integration smoke tests.

### 6.1 Run Live Integration Smoke Tests

```powershell
# Execute live integration tests (connects to live http://127.0.0.1:11434 if present)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj `
  --filter "FullyQualifiedName~LiveExternalProviderIntegrationTests" `
  -c Release `
  --logger "console;verbosity=detailed"
```

**Expected Output:**
```text
Passed: Live_OllamaVersion_ReturnsDaemonVersion
Passed: Live_OllamaTags_ReturnsInstalledModels
Passed: Live_OllamaPs_ReturnsRunningProcessList
```

### 6.2 Test VRAM Eviction Live via PowerShell

```powershell
# 1. Verify running models in Ollama
Invoke-RestMethod -Uri "http://127.0.0.1:11434/api/ps" -Method Get

# 2. Trigger VRAM unload through LocalLLMServerManager proxy
$unloadResult = Invoke-RestMethod -Uri "http://127.0.0.1:5246/api/models/unload" -Method Post -ContentType "application/json" -Body "{}"
$unloadResult | Format-List

# 3. Verify VRAM freed in nvidia-smi
nvidia-smi --query-gpu=memory.used,memory.free --format=csv
```

---

## 7. Full Solution Verification Command Matrix

Execute this master test sequence before completing final Windows validation:

```powershell
# 1. Clean build all configurations
dotnet clean
dotnet build LocalLLMServerManager.sln -c Release

# 2. Run unit & mock integration test suite (530+ tests)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj `
  --filter "FullyQualifiedName!~Playwright" `
  -c Release

# 3. Run Playwright WASM E2E tests
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj `
  --filter "FullyQualifiedName~PlaywrightWasmE2ETests" `
  -c Release

# 4. Generate updated documentation screenshots
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj `
  --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" `
  -c Release

# 5. Build Windows Inno Setup installer (if Inno Setup 6 installed)
if (Test-Path "C:\Program Files (x86)\Inno Setup 6\ISCC.exe") {
    pwsh scripts/build_release.ps1
}
```

---

## 8. Troubleshooting & Common Pitfalls

### 8.1 Playwright Execution Policy Error
**Symptom:** `playwright.ps1 cannot be loaded because running scripts is disabled on this system.`  
**Fix:**
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned
```

### 8.2 Port 5246 Already in Use
**Symptom:** `System.IO.IOException: Failed to bind to address http://127.0.0.1:5246: address already in use.`  
**Fix:**
```powershell
# Find and terminate existing LocalLLMServerManager instance
$portProc = Get-NetTCPConnection -LocalPort 5246 -ErrorAction SilentlyContinue
if ($portProc) {
    Stop-Process -Id $portProc.OwningProcess -Force
}
```

### 8.3 Hybrid Laptops (iGPU + dGPU) VRAM Reporting
**Symptom:** `GpuTelemetryProvider` reports Intel Iris Xe or AMD Radeon Graphics instead of NVIDIA RTX.  
**Fix:**
- Ensure `nvidia-smi` is on system `PATH` (`C:\Windows\System32` or `C:\Program Files\NVIDIA Corporation\NVSMI`).
- Windows Settings -> System -> Display -> Graphics -> Add `LocalLLMServerManager.exe` -> Set to **High Performance (NVIDIA GPU)**.
