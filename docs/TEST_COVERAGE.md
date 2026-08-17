# Test Coverage & Quality Assurance Specification

> **LocalLLMServerManager v3.5.0** | Target: `.NET 10 LTS` | Avalonia UI & WebAssembly | Microsoft Playwright

This document provides a comprehensive audit of all unit, integration, mock server, and end-to-end (E2E) Playwright browser automation tests across the **LocalLLMServerManager** codebase on both **Windows** and **Linux** environments.

---

## 📊 Executive Summary & Test Metrics

```
+-----------------------------------------------------------------------------------------+
| TOTAL TESTS EXECUTED : 171                                                              |
| PASSED               : 171 (100.0%)                                                     |
| FAILED               : 0   (0.0%)                                                       |
| SKIPPED              : 0   (0.0%)                                                       |
| TEST FIXTURE FILES   : 29                                                               |
| TARGET RUNTIMES      : Windows 11 x64, Linux x64 (systemd, X11, Wayland), Chromium Headless|
+-----------------------------------------------------------------------------------------+
```

### Testing Frameworks & Tooling
* **Test Runner**: [xUnit.net v3](https://xunit.net/) (`xunit.v3` 3.2.2)
* **Headless UI Testing**: `Avalonia.Headless.XUnit` (12.1.1)
* **Mocking & Isolation**: `Moq` (4.20.72)
* **Browser E2E Automation**: `Microsoft.Playwright` (1.50.0) with Chromium WebGL SwiftShader emulation
* **Code Coverage Instrumentation**: `coverlet.collector` (10.0.1)

---

## 🧩 Chunked Test Execution Architecture

To eliminate port contention and process memory race conditions during local and CI/CD test runs on Windows and Linux, the test suite is partitioned into five targeted execution chunks:

```
                  ┌─────────────────────────────────────────────────────────┐
                  │                 LocalLLMServerManager.Tests             │
                  │                        (171 Tests)                      │
                  └────────────────────────────┬────────────────────────────┘
                                               │
         ┌──────────────────┬──────────────────┼──────────────────┬──────────────────┐
         ▼                  ▼                  ▼                  ▼                  ▼
  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐   ┌──────────────┐   ┌──────────────┐
  │   Chunk 1    │   │   Chunk 2    │   │   Chunk 3    │   │   Chunk 4    │   │   Chunk 5    │
  │  ViewModels  │   │  Services    │   │  Endpoints   │   │  Playwright  │   │  Screenshot  │
  │  & Settings  │   │  & Tool Disc │   │  & Workflows │   │   WASM E2E   │   │  Generator   │
  │  (50 Tests)  │   │  (81 Tests)  │   │  (76 Tests)  │   │   (1 Test)   │   │   (1 Test)   │
  └──────────────┘   └──────────────┘   └──────────────┘   └──────────────┘   └──────────────┘
```

### Execution Commands

```bash
# Chunk 1: ViewModels & Core Settings (50 tests)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~ViewModel|FullyQualifiedName~AppSettings|FullyQualifiedName~BrowserLauncher" -c Release --nologo

# Chunk 2: Services, Tool Discovery, VRAM Orchestrator & Static Files (81 tests)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~Services|FullyQualifiedName~VramOrchestrator|FullyQualifiedName~StaticFile|FullyQualifiedName~ToolDiscovery" -c Release --nologo

# Chunk 3: Endpoints, Mock Servers, Discovery Endpoints & Workflow Performance (76 tests)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~Endpoint|FullyQualifiedName~MockServer|FullyQualifiedName~WorkflowPerformance|FullyQualifiedName~DiscoveryEndpoints" -c Release --nologo

# Chunk 4: Playwright WebAssembly Browser E2E Tests (1 test)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightWasmE2ETests" -c Release --nologo

# Chunk 5: Playwright Automated Documentation Screenshot Generator (1 test)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" -c Release --nologo
```

---

## 🗺️ Component-by-Component Test Mapping

| Layer / Component | Source File(s) | Primary Test File(s) | Verified Capabilities & Assertions |
|---|---|---|---|
| **Host Bootstrapper** | `Program.cs` | `ProgramEndpointsAndServicesTests.cs`<br>`ServerInfrastructureAndAppTests.cs` | ASP.NET Core Kestrel initialization, DI service container configuration, port binding, command line flag parsing (`--service`, `--headless`). |
| **System Tray & Desktop Lifecycle** | `App.axaml.cs`<br>`LocalLLMServerManager.csproj` | `AvaloniaAppAndWindowCoverageTests.cs`<br>`MainWindowUiTests.cs` | Avalonia desktop app builder, system tray icon lifecycle, tray menu commands (Open Dashboard, View Health, Exit), desktop window auto-attachment to background service. |
| **AI Engine Manager** | `Services/AiEngineManager.cs` | `ServicesAndEngineManagerCoverageTests.cs`<br>`CoverageThresholdTargetedPushTests.cs` | Process lifecycle for Ollama (`11434`), Forge (`7860`), ComfyUI (`8188`), process health monitoring, graceful shutdown, Win32 Job Object memory caps. |
| **Tool Discovery Service** | `Services/ToolDiscoveryService.cs`<br>`Interfaces/IToolDiscoveryService.cs` | `ToolDiscoveryServiceTests.cs` | Multi-drive filesystem scanning for Ollama, ComfyUI, and SD Forge installations, path validation (`Valid`, `NotFound`, `Invalid`), environment variable expansion. |
| **GPU Telemetry Provider** | `Services/GpuTelemetryProvider.cs` | `ProgramEndpointsAndServicesTests.cs`<br>`ServicesAndEngineManagerCoverageTests.cs`<br>`DeepCoveragePushTests.cs` | Cross-platform GPU VRAM telemetry reading NVML CUDA (`nvidia-smi`), Windows Registry fallback, and Linux `/proc/meminfo` fallback. |
| **VRAM Orchestrator** | `Services/VramOrchestrator.cs` | `VramOrchestratorTests.cs`<br>`DeepCoveragePushTests.cs`<br>`ReachNinetyPercentCoverageTests.cs` | Proactive memory orchestration: sends `keep_alive: 0` to Ollama before Stable Diffusion or ComfyUI generation, ComfyUI `/free` call, Forge progress polling. |
| **Settings Service** | `Services/SettingsService.cs`<br>`LocalLLMServerManager.Shared/Models/AppSettings.cs` | `AppSettingsTests.cs`<br>`ProgramEndpointsAndServicesTests.cs`<br>`CoverageThresholdTargetedPushTests.cs` | Persistent JSON settings storage (`appsettings.json` / `settings.json`), environment variable expansion (`%APPDATA%`), default fallback handling. |
| **Git Update Service** | `Services/GitUpdateService.cs` | `CoverageThresholdTargetedPushTests.cs` | Git fetch, pull, and checkout command execution with error handling for in-app self-updates. |
| **Win32 Job Object** | `Services/Win32JobObject.cs` | `ServicesAndEngineManagerCoverageTests.cs`<br>`DeepCoveragePushTests.cs` | Windows Win32 Job Object memory quota limits and child process termination on parent exit; graceful no-op on Linux. |
| **Health API** | `Endpoints/HealthEndpoints.cs` | `ProgramEndpointsAndServicesTests.cs`<br>`EndpointRegistrationCoverageTests.cs` | `GET /health` returns HTTP 200 OK, engine health states, and version `3.5.0`. |
| **Discovery API** | `Endpoints/DiscoveryEndpoints.cs` | `DiscoveryEndpointsTests.cs`<br>`EndpointRegistrationCoverageTests.cs` | `POST /api/tools/detect` (scans drives and returns detected tools), `POST /api/tools/validate-path` (dynamically checks file/directory validity). |
| **Engine API** | `Endpoints/EngineEndpoints.cs` | `ProgramEndpointsAndServicesTests.cs`<br>`EndpointRegistrationCoverageTests.cs` | `GET /api/gpu/vram`, `GET /api/settings`, `POST /api/settings`, `/api/comfy/*`, `/api/forge/*`. |
| **Model Proxy API** | `Endpoints/ModelProxyEndpoints.cs` | `ProgramEndpointsAndServicesTests.cs`<br>`OllamaAndHfMockServerTests.cs`<br>`EndpointRegistrationCoverageTests.cs` | `GET /api/models`, `GET /api/ollama/ps`, `GET /api/hf/search`, `GET /api/hf/download`, `GET /api/civitai/search`, `GET /api/civitai/download`. |
| **Workflow API** | `Endpoints/WorkflowEndpoints.cs` | `WorkflowPerformanceTests.cs`<br>`EndpointRegistrationCoverageTests.cs` | `GET /api/comfy/workflows` (preset discovery), `GET /api/3d/files` (GLB/GLTF mesh inspection and serving). |
| **MCP API** | `Endpoints/McpEndpoints.cs` | `ProgramEndpointsAndServicesTests.cs`<br>`EndpointRegistrationCoverageTests.cs` | Model Context Protocol JSON-RPC 2.0 endpoint (`POST /api/mcp/tools`) for AI assistant tool discovery (`tools/list`) and invocation (`tools/call`). |
| **Root ViewModel** | `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs` | `MainViewModelCoverageTests.cs`<br>`MainWindowViewModelTests.cs`<br>`ReachNinetyPercentCoverageTests.cs` | Master coordinator ViewModel aggregating sub-ViewModels, tab switching, global health polling, toast notifications, VRAM unload triggering. |
| **Telemetry ViewModel** | `LocalLLMServerManager.Shared/ViewModels/TelemetryViewModel.cs` | `MainViewModelCoverageTests.cs`<br>`NinetyPercentThresholdTests.cs` | Reactive GPU VRAM percentage calculation, stacked bar allocation, GPU model formatting. |
| **Ollama Library ViewModel** | `LocalLLMServerManager.Shared/ViewModels/OllamaLibraryViewModel.cs` | `MainViewModelCoverageTests.cs`<br>`FinalPushTo90CoverageTests.cs`<br>`CoverageThresholdTargetedPushTests.cs` | Installed Ollama models, capability profiling (`Coding`, `Reasoning`, `Math`, `Chat`), interactive KV Cache calculator (up to 32K tokens), model pull SSE stream parsing. |
| **Hugging Face ViewModel** | `LocalLLMServerManager.Shared/ViewModels/HuggingFaceSearchViewModel.cs` | `MainViewModelCoverageTests.cs`<br>`SearchServicesCoverageTests.cs` | GGUF repository search, branch quantization tree parsing (Q4_K_M, Q5_K_M, Q8_0, FP16), download progress tracking. |
| **CivitAI ViewModel** | `LocalLLMServerManager.Shared/ViewModels/CivitaiSearchViewModel.cs` | `MainViewModelCoverageTests.cs`<br>`SearchServicesCoverageTests.cs` | CivitAI model gallery, search filters (Checkpoint, LoRA, VAE, ControlNet), download manager. |
| **Settings ViewModel** | `LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs` | `SettingsViewModelCoverageTests.cs`<br>`MainViewModelCoverageTests.cs`<br>`AppSettingsTests.cs` | Settings editor for directory paths, URLs, preferred engine toggle, auto-detect tools, file/folder pickers, and real-time status indicators. |
| **Shared Services** | `LocalLLMServerManager.Shared/Services/*` | `SearchServicesCoverageTests.cs`<br>`BrowserLauncherTests.cs`<br>`LiveExternalProviderIntegrationTests.cs` | `CivitaiSearchService`, `HuggingFaceSearchService`, `OllamaModelService`, `TelemetryService`, `ToastService`, `BrowserLauncher`, `ToolDiscoveryService`. |
| **Avalonia XAML Views** | `LocalLLMServerManager.Shared/Views/*` | `MainWindowUiTests.cs`<br>`AvaloniaAppAndWindowCoverageTests.cs` | Fluent dark theme controls (`MainView`, `TelemetryHeaderControl`, `OllamaModelsTabControl`, `HuggingFaceTabControl`, `CivitaiTabControl`, `EngineStudioTabControl`, `SettingsTabControl`). |
| **Static Assets & WASM** | `Program.cs`<br>`LocalLLMServerManager.Web/*` | `StaticFileMimeTypeTests.cs`<br>`PlaywrightWasmE2ETests.cs` | Kestrel static file provider MIME mappings (`.wasm`, `.dat`, `.json`, `.js`, `.css`, `.png`, `.glb`), WebAssembly browser runtime initialization. |
| **Playwright Browser E2E** | `LocalLLMServerManager.Web/wwwroot/*` | `PlaywrightWasmE2ETests.cs` | Headless Chromium boots WebAssembly client, validates `#out` DOM container, asserts 0 unhandled console errors, 0 404s on `_framework` assets. |
| **Screenshot Generator** | `docs/images/*` | `PlaywrightScreenshotGenerator.cs` | Navigates all 5 tabs in headless Chromium with WebGL SwiftShader, captures crisp screenshots to `docs/images/`, asserts visual distinctness. |

---

## 🪟🐧 Cross-Platform Test Validation Matrix

| Platform / Capability | Windows 11 x64 | Linux (Ubuntu / Debian / Fedora) | Verification Details |
|---|---|---|---|
| **Process Management** | ✅ Win32 Job Objects | ✅ Linux Process Group / Signals | Verified in `ServicesAndEngineManagerCoverageTests` & `DeepCoveragePushTests`. Win32 memory caps active on Windows; graceful fallback on Linux. |
| **Tool Discovery** | ✅ Multi-Drive Roots (`C:`, `D:`, `E:`) | ✅ Standard Roots (`/opt`, `~/.ollama`) | Verified in `ToolDiscoveryServiceTests` & `DiscoveryEndpointsTests`. Scans all connected drive partitions and standard paths. |
| **GPU Telemetry** | ✅ NVML (`nvidia-smi`) + Registry | ✅ NVML (`nvidia-smi`) + `/proc/meminfo` | Verified in `GpuTelemetryProvider`. Returns accurate VRAM bytes and GPU model name across OS boundaries. |
| **Browser Launching** | ✅ `explorer.exe` / `cmd` | ✅ `xdg-open` | Verified in `BrowserLauncherTests`. Tests verify cross-platform shell execution and fallback handling. |
| **Background Daemon** | ✅ Windows Service | ✅ Linux `systemd` daemon | Verified in `ProgramEndpointsAndServicesTests` with `--service` and `--headless` flags. |
| **Static File MIME Routing** | ✅ Kestrel Custom Content Types | ✅ Kestrel Custom Content Types | Verified in `StaticFileMimeTypeTests`. Correctly resolves `.wasm` (`application/wasm`) and `.glb` (`model/gltf-binary`). |
| **Playwright Automation** | ✅ Headless Chromium + SwiftShader | ✅ Headless Chromium + SwiftShader | Verified in `PlaywrightWasmE2ETests` and `PlaywrightScreenshotGenerator`. |

---

## 📁 Test Fixture Inventory (All 29 Test Files)

| # | Test Class File | Primary Focus | Test Count |
|---|---|---|---|
| 1 | `AppSettingsTests.cs` | AppSettings default values, environment variable expansion, JSON serialization | 6 |
| 2 | `AvaloniaAppAndWindowCoverageTests.cs` | Avalonia desktop app builder, main window layout, and control initialization | 3 |
| 3 | `BrowserLauncherTests.cs` | Cross-platform URL launching (Windows explorer / Linux xdg-open) | 12 |
| 4 | `CoverageThresholdTargetedPushTests.cs` | Deep coverage for Services, Endpoints, GitUpdateService, and ViewModels | 6 |
| 5 | `DeepCoveragePushTests.cs` | Win32 Job Object Linux fallback, Engine Manager and VRAM edge cases | 7 |
| 6 | `DiscoveryEndpointsTests.cs` | Tool detection and path validation REST Minimal API endpoints | 8 |
| 7 | `EndToEndSystemTests.cs` | Full stack Kestrel Minimal API + YARP reverse proxy system integration | 3 |
| 8 | `EndpointRegistrationCoverageTests.cs` | Route registration verification for all Minimal API endpoint modules | 1 |
| 9 | `FinalPushTo90CoverageTests.cs` | Boundary condition coverage and cancellation token propagation | 5 |
| 10 | `FinalPushTo90PercentThresholdTests.cs` | ViewModel edge cases and SSE streaming data parsing | 2 |
| 11 | `LiveExternalProviderIntegrationTests.cs` | Live integration and fallback for Hugging Face and CivitAI endpoints | 6 |
| 12 | `MainViewModelCoverageTests.cs` | MainViewModel coordinator reactivity, tabs, and toast dispatcher | 10 |
| 13 | `MainWindowUiTests.cs` | UI DataContext bindings, UserControl hierarchy, and XAML styles | 1 |
| 14 | `MainWindowViewModelTests.cs` | MainWindow ViewModel lifecycle and command bindings | 1 |
| 15 | `NinetyPercentThresholdTests.cs` | Comprehensive branch coverage for error recovery and null safety | 2 |
| 16 | `OllamaAndHfMockServerTests.cs` | Mock HTTP servers simulating Ollama API and Hugging Face Hub | 1 |
| 17 | `PlaywrightScreenshotGenerator.cs` | Automated Playwright documentation screenshot generator across all 5 tabs | 1 |
| 18 | `PlaywrightWasmE2ETests.cs` | Playwright E2E browser automation verifying WebAssembly client in Chromium | 1 |
| 19 | `ProgramEndpointsAndServicesTests.cs` | Kestrel Minimal API integration tests (`/health`, `/api/gpu/vram`, `/api/mcp/tools`, `/api/settings`) | 52 |
| 20 | `ReachNinetyPercentCoverageTests.cs` | Health check online/offline branches, VRAM unload, streaming progress | 3 |
| 21 | `SearchServicesCoverageTests.cs` | Unit tests for CivitaiSearchService, HuggingFaceSearchService, OllamaModelService | 4 |
| 22 | `ServerInfrastructureAndAppTests.cs` | DI container resolution, logging infrastructure, and server lifecycle | 1 |
| 23 | `ServicesAndEngineManagerCoverageTests.cs` | Engine process management, Win32 Job Objects, GpuTelemetryProvider | 3 |
| 24 | `SettingsViewModelCoverageTests.cs` | SettingsViewModel auto-detect tools, file/folder pickers, and path validation | 10 |
| 25 | `StaticFileMimeTypeTests.cs` | MIME type provider verification for WASM and 3D GLB assets | 2 |
| 26 | `TestAppBuilder.cs` | Headless Avalonia application builder infrastructure | 1 |
| 27 | `ToolDiscoveryServiceTests.cs` | Multi-drive tool discovery, path validation, and environment expansion | 12 |
| 28 | `VramOrchestratorTests.cs` | VRAM Orchestrator pre-generation memory clearing and health probes | 7 |
| 29 | `WorkflowPerformanceTests.cs` | ComfyUI workflow JSON loading, parsing, and GLB export profiling | 1 |
| **Total** | | | **171 Tests** |

---

## 🛠️ Best Practices for Developers & Contributors

1. **Always run tests in chunks** when validating locally to avoid concurrent port collisions on mock HTTP servers.
2. **Ensure Chromium binaries are installed** before running Playwright tests:
   ```powershell
   pwsh LocalLLMServerManager.Tests/bin/Release/net10.0/playwright.ps1 install chromium
   ```
3. **Clean up orphaned processes** if a test host is forcefully terminated:
   ```powershell
   Get-Process -Name "*LocalLLMServerManager*", "*testhost*" -ErrorAction SilentlyContinue | Stop-Process -Force
   ```
