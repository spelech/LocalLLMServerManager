# Test Coverage & Quality Assurance Specification

> **LocalLLMServerManager v3.5.0** | Target: `.NET 10 LTS` | Avalonia UI & WebAssembly | Microsoft Playwright

This document provides a comprehensive audit of all unit, integration, mock server, and end-to-end (E2E) Playwright browser automation tests across the **LocalLLMServerManager** codebase on both **Windows** and **Linux** environments.

---

## 📊 Executive Summary & Test Metrics

```
+-----------------------------------------------------------------------------------------+
| TOTAL TESTS EXECUTED : 174                                                              |
| PASSED               : 173 (99.4%)                                                      |
| SKIPPED              : 1   (Playwright screenshot generator on-demand)                  |
| FAILED               : 0   (0.0%)                                                       |
| TEST FIXTURE CLASSES : 20                                                               |
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
                  │                        (174 Tests)                      │
                  └────────────────────────────┬────────────────────────────┘
                                               │
         ┌──────────────────┬──────────────────┼──────────────────┬──────────────────┐
         ▼                  ▼                  ▼                  ▼                  ▼
  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐   ┌──────────────┐   ┌──────────────┐
  │   Chunk 1    │   │   Chunk 2    │   │   Chunk 3    │   │   Chunk 4    │   │   Chunk 5    │
  │  ViewModels  │   │  Services    │   │  Endpoints   │   │  MCP Server  │   │  Playwright  │
  │  & Settings  │   │  & Discovery │   │  & Workflows │   │  & Tools     │   │   WASM E2E   │
  │  (37 Tests)  │   │  (46 Tests)  │   │  (67 Tests)  │   │  (22 Tests)  │   │   (2 Tests)  │
  └──────────────┘   └──────────────┘   └──────────────┘   └──────────────┘   └──────────────┘
```

### Execution Commands

```bash
# Chunk 1: ViewModels, Settings & UI (37 tests)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~ViewModel|FullyQualifiedName~AppSettings|FullyQualifiedName~AvaloniaUi|FullyQualifiedName~MainWindowUi" -c Release --nologo

# Chunk 2: Services, Tool Discovery, VRAM Orchestrator & Static Files (46 tests)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~Services|FullyQualifiedName~VramOrchestrator|FullyQualifiedName~StaticFile|FullyQualifiedName~ToolDiscovery|FullyQualifiedName~BrowserLauncher" -c Release --nologo

# Chunk 3: Endpoints, System, Mock Servers & Workflows (67 tests)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~ServerEndpoints|FullyQualifiedName~DiscoveryEndpoints|FullyQualifiedName~EndToEndSystem|FullyQualifiedName~LiveExternal|FullyQualifiedName~WorkflowPerformance" -c Release --nologo

# Chunk 4: Model Context Protocol (MCP) Integration Tests (22 tests)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~McpServerIntegrationTests" -c Release --nologo

# Chunk 5: Playwright WebAssembly Browser E2E & Screenshot Generator (2 tests)
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~Playwright" -c Release --nologo
```

---

## 🗺️ Component-by-Component Test Mapping

| Layer / Component | Source File(s) | Primary Test File(s) | Verified Capabilities & Assertions |
|---|---|---|---|
| **Host Bootstrapper** | `Program.cs` | `ServerEndpointsTests.cs`<br>`ServerInfrastructureAndAppTests.cs` | ASP.NET Core Kestrel initialization, DI service container configuration, port binding, command line flag parsing (`--service`, `--headless`). |
| **System Tray & Desktop Lifecycle** | `App.axaml.cs`<br>`LocalLLMServerManager.csproj` | `AvaloniaUiTests.cs`<br>`MainWindowUiTests.cs` | Avalonia desktop app builder, system tray icon lifecycle, tray menu commands (Open Dashboard, View Health, Exit), desktop window auto-attachment to background service. |
| **AI Engine Manager** | `Services/AiEngineManager.cs` | `ServicesAndEngineManagerTests.cs` | Process lifecycle for Ollama (`11434`), Forge (`7860`), ComfyUI (`8188`), process health monitoring, graceful shutdown, Win32 Job Object memory caps. |
| **Tool Discovery Service** | `Services/ToolDiscoveryService.cs`<br>`Interfaces/IToolDiscoveryService.cs` | `ToolDiscoveryServiceTests.cs` | Multi-drive filesystem scanning for Ollama, ComfyUI, and SD Forge installations, path validation (`Valid`, `NotFound`, `Invalid`), environment variable expansion. |
| **GPU Telemetry Provider** | `Services/GpuTelemetryProvider.cs` | `ServerEndpointsTests.cs`<br>`ServicesAndEngineManagerTests.cs` | Cross-platform GPU VRAM telemetry reading NVML CUDA (`nvidia-smi`), Windows Registry fallback, and Linux `/proc/meminfo` fallback. |
| **VRAM Orchestrator** | `Services/VramOrchestrator.cs` | `VramOrchestratorTests.cs`<br>`MainViewModelTests.cs` | Proactive memory orchestration: sends `keep_alive: 0` to Ollama before Stable Diffusion or ComfyUI generation, ComfyUI `/free` call, Forge progress polling. |
| **Settings Service** | `Services/SettingsService.cs`<br>`LocalLLMServerManager.Shared/Models/AppSettings.cs` | `AppSettingsTests.cs`<br>`ServerEndpointsTests.cs` | Persistent JSON settings storage (`appsettings.json` / `settings.json`), environment variable expansion (`%APPDATA%`), default fallback handling. |
| **Git Update Service** | `Services/GitUpdateService.cs` | `ServicesAndEngineManagerTests.cs`<br>`ServerEndpointsTests.cs` | Git branch validation, fetch, pull, and checkout command execution with error handling for in-app self-updates. |
| **Win32 Job Object** | `Services/Win32JobObject.cs` | `ServerInfrastructureAndAppTests.cs`<br>`ServicesAndEngineManagerTests.cs` | Windows Win32 Job Object memory quota limits and child process termination on parent exit; graceful no-op on Linux. |
| **Health API** | `Endpoints/HealthEndpoints.cs` | `ServerEndpointsTests.cs` | `GET /health` returns HTTP 200 OK, engine health states, and version `3.5.0`. |
| **Discovery API** | `Endpoints/DiscoveryEndpoints.cs` | `DiscoveryEndpointsTests.cs` | `POST /api/tools/detect` (scans drives and returns detected tools), `POST /api/tools/validate-path` (dynamically checks file/directory validity). |
| **Engine API** | `Endpoints/EngineEndpoints.cs` | `ServerEndpointsTests.cs` | `GET /api/gpu/vram`, `GET /api/settings`, `POST /api/settings`, `/api/comfy/*`, `/api/forge/*`. |
| **Model Proxy API** | `Endpoints/ModelProxyEndpoints.cs` | `ServerEndpointsTests.cs`<br>`LiveExternalProviderIntegrationTests.cs` | `GET /api/models`, `GET /api/ollama/ps`, `GET /api/hf/search`, `GET /api/hf/download`, `GET /api/civitai/search`, `GET /api/civitai/download`. |
| **Workflow API** | `Endpoints/WorkflowEndpoints.cs` | `WorkflowPerformanceTests.cs` | `GET /api/comfy/workflows` (preset discovery), `GET /api/3d/files` (GLB/GLTF mesh inspection and serving). |
| **MCP API & Tool Suite** | `Services/LocalLlmMcpTools.cs`<br>`Endpoints/McpEndpoints.cs`<br>`Program.cs` | `McpServerIntegrationTests.cs` | Streamable HTTP / SSE MCP server mapped to `/mcp`, JSON-RPC 2.0 dispatch, legacy discovery endpoint (`GET /api/mcp/tools`), and all 8 AI automation tools. |
| **Root ViewModel** | `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs` | `MainViewModelTests.cs` | Master coordinator ViewModel aggregating sub-ViewModels, tab switching, global health polling, toast notifications, VRAM unload triggering. |
| **Telemetry ViewModel** | `LocalLLMServerManager.Shared/ViewModels/TelemetryViewModel.cs` | `MainViewModelTests.cs` | Reactive GPU VRAM percentage calculation, stacked bar allocation, GPU model formatting. |
| **Ollama Library ViewModel** | `LocalLLMServerManager.Shared/ViewModels/OllamaLibraryViewModel.cs` | `MainViewModelTests.cs` | Installed Ollama models, capability profiling (`Coding`, `Reasoning`, `Math`, `Chat`), interactive KV Cache calculator (up to 32K tokens), model pull SSE stream parsing. |
| **Hugging Face ViewModel** | `LocalLLMServerManager.Shared/ViewModels/HuggingFaceSearchViewModel.cs` | `MainViewModelTests.cs`<br>`SearchServicesTests.cs` | GGUF repository search, branch quantization tree parsing (Q4_K_M, Q5_K_M, Q8_0, FP16), download progress tracking. |
| **CivitAI ViewModel** | `LocalLLMServerManager.Shared/ViewModels/CivitaiSearchViewModel.cs` | `MainViewModelTests.cs`<br>`SearchServicesTests.cs` | CivitAI model gallery, search filters (Checkpoint, LoRA, VAE, ControlNet), download manager. |
| **Settings ViewModel** | `LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs` | `SettingsViewModelTests.cs`<br>`MainViewModelTests.cs`<br>`AppSettingsTests.cs` | Settings editor for directory paths, URLs, preferred engine toggle, auto-detect tools, file/folder pickers, and real-time status indicators. |
| **Shared Services** | `LocalLLMServerManager.Shared/Services/*` | `SearchServicesTests.cs`<br>`BrowserLauncherTests.cs`<br>`LiveExternalProviderIntegrationTests.cs` | `CivitaiSearchService`, `HuggingFaceSearchService`, `OllamaModelService`, `TelemetryService`, `ToastService`, `BrowserLauncher`, `ToolDiscoveryService`. |
| **Avalonia XAML Views** | `LocalLLMServerManager.Shared/Views/*` | `MainWindowUiTests.cs`<br>`AvaloniaUiTests.cs` | Fluent dark theme controls (`MainView`, `TelemetryHeaderControl`, `OllamaModelsTabControl`, `HuggingFaceTabControl`, `CivitaiTabControl`, `EngineStudioTabControl`, `SettingsTabControl`). |
| **Static Assets & WASM** | `Program.cs`<br>`LocalLLMServerManager.Web/*` | `StaticFileMimeTypeTests.cs`<br>`PlaywrightWasmE2ETests.cs` | Kestrel static file provider MIME mappings (`.wasm`, `.dat`, `.json`, `.js`, `.css`, `.png`, `.glb`), WebAssembly browser runtime initialization. |
| **Playwright Browser E2E** | `LocalLLMServerManager.Web/wwwroot/*` | `PlaywrightWasmE2ETests.cs` | Headless Chromium boots WebAssembly client, validates `#out` DOM container, asserts 0 unhandled console errors, 0 404s on `_framework` assets. |
| **Screenshot Generator** | `docs/images/*` | `PlaywrightScreenshotGenerator.cs` | Navigates all 5 tabs in headless Chromium with WebGL SwiftShader, captures crisp screenshots to `docs/images/`, asserts visual distinctness. |
| **Installer & In-Place Updates** | `scripts/installer.iss`<br>`scripts/install.ps1`<br>`scripts/update.ps1`<br>`scripts/install_linux.sh` | Verified via automated syntax & script lifecycle verification | Pre-install process termination (Windows Service / tray app / systemd), configuration preservation (`settings.json`), post-install reconfiguration and restart. |

---

## 🪟🐧 Cross-Platform Test Validation Matrix

| Platform / Capability | Windows 11 x64 | Linux (Ubuntu / Debian / Fedora) | Verification Details |
|---|---|---|---|
| **Process Management** | ✅ Win32 Job Objects | ✅ Linux Process Group / Signals | Verified in `ServicesAndEngineManagerTests`. Win32 memory caps active on Windows; graceful fallback on Linux. |
| **Tool Discovery** | ✅ Multi-Drive Roots (`C:`, `D:`, `E:`) | ✅ Standard Roots (`/opt`, `~/.ollama`) | Verified in `ToolDiscoveryServiceTests` & `DiscoveryEndpointsTests`. Scans all connected drive partitions and standard paths. |
| **GPU Telemetry** | ✅ NVML (`nvidia-smi`) + Registry | ✅ NVML (`nvidia-smi`) + `/proc/meminfo` | Verified in `GpuTelemetryProvider`. Returns accurate VRAM bytes and GPU model name across OS boundaries. |
| **Browser Launching** | ✅ `explorer.exe` / `cmd` | ✅ `xdg-open` | Verified in `BrowserLauncherTests`. Tests verify cross-platform shell execution and fallback handling. |
| **Background Daemon** | ✅ Windows Service | ✅ Linux `systemd` daemon | Verified in `ServerEndpointsTests` and `ServerInfrastructureAndAppTests` with `--service` and `--headless` flags. |
| **Static File MIME Routing** | ✅ Kestrel Custom Content Types | ✅ Kestrel Custom Content Types | Verified in `StaticFileMimeTypeTests`. Correctly resolves `.wasm` (`application/wasm`) and `.glb` (`model/gltf-binary`). |
| **MCP Server & AI Tools** | ✅ Streamable HTTP & SSE (`/mcp`) | ✅ Streamable HTTP & SSE (`/mcp`) | Verified in `McpServerIntegrationTests` with all 8 tools, DI container resolution, and legacy endpoint. |
| **In-Place Upgrades** | ✅ Inno Setup (`.iss`) & `update.ps1` | ✅ `install_linux.sh` | Verified in installer scripts with pre-install process termination, settings preservation, and post-update service restart. |
| **Playwright Automation** | ✅ Headless Chromium + SwiftShader | ✅ Headless Chromium + SwiftShader | Verified in `PlaywrightWasmE2ETests` and `PlaywrightScreenshotGenerator`. |

---

## 📁 Test Fixture Inventory (All 20 Test Files)

| # | Test Class File | Primary Focus | Test Count |
|---|---|---|---|
| 1 | `AppSettingsTests.cs` | AppSettings default values, environment variable expansion, JSON serialization | 6 |
| 2 | `AvaloniaUiTests.cs` | Avalonia desktop app builder, main window layout, and control initialization | 3 |
| 3 | `BrowserLauncherTests.cs` | Cross-platform URL launching (Windows explorer / Linux xdg-open) | 12 |
| 4 | `DiscoveryEndpointsTests.cs` | Tool detection and path validation REST Minimal API endpoints | 8 |
| 5 | `EndToEndSystemTests.cs` | Full stack Kestrel Minimal API + YARP reverse proxy system integration | 3 |
| 6 | `LiveExternalProviderIntegrationTests.cs` | Live integration and fallback for Hugging Face and CivitAI endpoints | 6 |
| 7 | `MainViewModelTests.cs` | MainViewModel coordinator reactivity, tabs, toast dispatcher, and VRAM unload | 13 |
| 8 | `MainWindowUiTests.cs` | UI DataContext bindings, UserControl hierarchy, and XAML styles | 1 |
| 9 | `McpServerIntegrationTests.cs` | Model Context Protocol streamable HTTP `/mcp` endpoint, 8 tools, DI resolution, and error branches | 22 |
| 10 | `PlaywrightScreenshotGenerator.cs` | Automated Playwright documentation screenshot generator across all 5 tabs | 1 |
| 11 | `PlaywrightWasmE2ETests.cs` | Playwright E2E browser automation verifying WebAssembly client in Chromium | 1 |
| 12 | `SearchServicesTests.cs` | Unit tests for CivitaiSearchService, HuggingFaceSearchService, OllamaModelService | 4 |
| 13 | `ServerEndpointsTests.cs` | Kestrel Minimal API integration tests (`/health`, `/api/gpu/vram`, `/api/settings`, path safety) | 55 |
| 14 | `ServerInfrastructureAndAppTests.cs` | DI container resolution, logging infrastructure, and Win32 Job Object lifecycle | 1 |
| 15 | `ServicesAndEngineManagerTests.cs` | Engine process management, GitUpdateService, and GpuTelemetryProvider parsing | 4 |
| 16 | `SettingsViewModelTests.cs` | SettingsViewModel auto-detect tools, file/folder pickers, and path validation | 10 |
| 17 | `StaticFileMimeTypeTests.cs` | MIME type provider verification for WASM and 3D GLB assets | 2 |
| 18 | `ToolDiscoveryServiceTests.cs` | Multi-drive tool discovery, path validation, and environment expansion | 14 |
| 19 | `VramOrchestratorTests.cs` | VRAM Orchestrator pre-generation memory clearing and health probes | 7 |
| 20 | `WorkflowPerformanceTests.cs` | ComfyUI workflow JSON loading, parsing, and GLB export profiling | 1 |
| **Total** | | | **174 Tests** |

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
