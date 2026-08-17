# Windows Testing & Verification Design Specification

> **Date:** 2026-08-16  
> **Target Version:** v3.4.0  
> **Platform:** Windows 11 (win-x64) | .NET 10 | Avalonia UI | Playwright | Inno Setup  

---

## 1. Overview & Objectives

This design specification establishes the end-to-end testing, validation, and release handoff verification pipeline for **LocalLLMServerManager v3.4.0** on native Windows. It ensures that the application meets all stability, documentation, packaging, and runtime requirements prior to release deployment.

### Target Objectives
1. **Automated Test Suite Verification:** 100% pass rate across all 137 unit and integration tests.
2. **Playwright WASM Browser E2E:** Verify WebAssembly client boots cleanly in headless Chromium without 404 static asset errors or uncaught JavaScript exceptions.
3. **Automated Documentation Visual Assets:** Execute the Playwright screenshot generator to capture and update all 6 dashboard tab PNG screenshots in `docs/images/`.
4. **Self-Contained Release Packaging:** Publish self-contained `win-x64` binaries, create distribution ZIP archives, and compile the Inno Setup executable installer.
5. **Native Runtime & Service Smoke Verification:** Verify CLI startup, `--service` background daemon mode, `/health` endpoint response, and clean process teardown.

---

## 2. Architecture & Components

```
+-----------------------------------------------------------------------------------+
|                           Windows Testing Pipeline                                |
+-----------------------------------------------------------------------------------+
|                                                                                   |
|  [ Stage 1: Unit & Integration Tests ]                                            |
|    └─ 137 Tests: Endpoints, ViewModels, Services, Win32 Job Object, Telemetry     |
|                                                                                   |
|  [ Stage 2: Playwright WASM E2E & Screenshots ]                                   |
|    ├─ PlaywrightWasmE2ETests (Zero 404s, Zero JS Console Errors)                  |
|    └─ PlaywrightScreenshotGenerator (WebGL Chromium -> 6 PNGs in docs/images/)     |
|                                                                                   |
|  [ Stage 3: Windows Native Packaging & Installer ]                               |
|    ├─ Avalonia WASM Publish -> wwwroot/                                           |
|    ├─ Self-Contained win-x64 Publish -> publish/                                  |
|    ├─ Portable ZIP Archive -> dist/LocalLLMServerManager-v3.4.0-win-x64.zip       |
|    └─ Inno Setup Compilation -> dist/LocalLLMServerManager-v3.4.0-Setup.exe       |
|                                                                                   |
|  [ Stage 4: Native Runtime & Service Smoke Test ]                                |
|    ├─ CLI Help & Version validation                                               |
|    ├─ Background --service Mode Execution                                         |
|    ├─ /health HTTP 200 Probe Verification                                         |
|    └─ Graceful Teardown (Zero orphaned processes/locks)                          |
|                                                                                   |
+-----------------------------------------------------------------------------------+
```

---

## 3. Detailed Pipeline Stages

### Stage 1: Unit & Integration Test Suite
- **Command:** `dotnet test LocalLLMServerManager.sln --nologo -c Release`
- **Scope:**
  - Minimal API Endpoints (`/health`, `/api/gpu/vram`, `/api/settings`, `/api/models`, `/api/mcp/tools`, `/api/comfy/workflows`, `/api/3d/files`).
  - MVVM ViewModels (`MainViewModel`, `TelemetryViewModel`, `OllamaLibraryViewModel`, `HuggingFaceSearchViewModel`, `CivitaiSearchViewModel`, `SettingsViewModel`).
  - Infrastructure Services (`AiEngineManager`, `GpuTelemetryProvider`, `VramOrchestrator`, `Win32JobObject`, `SettingsService`).
- **Success Criteria:** Total 137 tests, 0 failed, 0 skipped.

### Stage 2: Playwright WASM E2E & Screenshot Generation
- **Commands:**
  1. `pwsh LocalLLMServerManager.Tests/bin/Release/net10.0/playwright.ps1 install chromium`
  2. `dotnet test --filter "FullyQualifiedName~PlaywrightWasmE2ETests" -c Release`
  3. `dotnet test --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" -c Release`
- **Scope:**
  - Asserts all WASM static assets (`.dat`, `.symbols`, `.wasm`, `.boot.json`) load with HTTP 200.
  - Asserts zero unhandled JS exceptions during DOM mounting.
  - Renders and saves 6 high-resolution PNGs to `docs/images/`:
    - `dashboard_desktop.png` (Overview & VRAM Monitor)
    - `dashboard_ollama.png` (Ollama Installed Models)
    - `dashboard_huggingface.png` (Hugging Face Model Search)
    - `dashboard_civitai.png` (CivitAI Asset Manager)
    - `dashboard_3d_studio.png` (3D / ComfyUI WebGL Canvas)
    - `dashboard_settings.png` (Application Settings)
- **Success Criteria:** Tests pass; all 6 PNG files are updated with non-zero size.

### Stage 3: Windows Native Packaging & Installer Compilation
- **Command:** `pwsh scripts/build_release.ps1`
- **Scope:**
  - Build & compile Avalonia WebAssembly and sync to `wwwroot/`.
  - Compile self-contained `win-x64` executable to `publish/`.
  - Package ZIP archive to `dist/LocalLLMServerManager-v3.4.0-win-x64.zip`.
  - Compile Inno Setup installer to `dist/LocalLLMServerManager-v3.4.0-Setup.exe`.
- **Success Criteria:** `dist/` contains both the `.zip` portable package and `.exe` installer.

### Stage 4: Native Runtime & Service Smoke Verification
- **Commands & Checks:**
  1. Validate binary help flag: `.\publish\LocalLLMServerManager.exe --help`
  2. Launch background service instance: `.\publish\LocalLLMServerManager.exe --service`
  3. Probe health endpoint: `Invoke-RestMethod http://127.0.0.1:5246/health`
  4. Validate response: `status: "Healthy"`, `version: "3.4.0"`
  5. Terminate service instance cleanly.
- **Success Criteria:** Health probe returns 200 OK with expected JSON body; process exits cleanly without lingering locks.

---

## 4. Quality Gates & Error Handling

1. **Process Lock Prevention:** Pre-check and kill any lingering `LocalLLMServerManager`, `LocalLLMServerManager.Tests`, or `testhost` processes before file operations.
2. **Port Arbitration:** Ensure TCP port `5246` is released before spinning up server instances.
3. **Artifact Integrity:** Validate file sizes for all generated binaries, ZIPs, and images.
