# LocalLLMServerManager — System Architecture & Component Design

> **v3.5.0 Architecture Specification & Mermaid Diagrams**

This document provides a visual and structural blueprint of **LocalLLMServerManager**, detailing its component decomposition, MVVM hierarchy, Minimal API route modules, Dependency Injection lifecycle, Model Context Protocol (MCP) AI integration, VRAM orchestration flow, WebAssembly static asset pipeline, Playwright E2E testing layer, and Docker containerization architecture.

---

## 🏛️ High-Level System Architecture

```mermaid
graph TD
    subgraph AIAssistantLayer["AI Assistant & Autonomous Agent Layer"]
        ClaudeAgent["Claude Desktop / Cursor / Antigravity / Agents"]
    end

    subgraph TestAndAutomationLayer["E2E Test & Browser Automation Layer"]
        E2E_Playwright["Playwright Browser Test Runner (PlaywrightWasmE2ETests)"]
        DocGen["Automated Screenshot Generator (PlaywrightScreenshotGenerator)"]
    end

    subgraph DesktopAndWebLayer["User Interface Layer (Desktop & WebAssembly)"]
        UI_Desktop["Desktop Window (Avalonia UI X11/Wayland/Win32)"]
        UI_Tray["System Tray Notification Icon & Menu"]
        UI_WASM["WebAssembly Browser App (WASM :5246)"]
        UI_Web["Web Studio & 3D WebGL Canvas (wwwroot)"]
    end

    subgraph DockerContainer["Docker Container & Service Host Environment"]
        subgraph HostLayer["ASP.NET Core Minimal API Host (:5246)"]
            ProgramHost["Program.cs (Host & DI Container)"]
            
            subgraph WasmPipeline["WASM & Static File Pipeline"]
                WasmProvider["FileExtensionContentTypeProvider (.wasm, .json, .dat)"]
                AppBundle["AppBundle Static Web Hosting (/ & /_framework/*)"]
            end

            subgraph Endpoints["Route Extension Modules"]
                E_Health["HealthEndpoints (/health)"]
                E_Proxy["ModelProxyEndpoints (/api/models, /api/hf/*, /api/civitai/*)"]
                E_Engine["EngineEndpoints (/api/gpu/vram, /api/settings, /api/comfy/*, /api/forge/*)"]
                E_Workflow["WorkflowEndpoints (/api/comfy/workflows, /api/3d/files)"]
                E_Disc["DiscoveryEndpoints (/api/tools/detect, /api/tools/validate-path)"]
                E_MCP["McpEndpoints (/mcp Streamable HTTP / SSE, /api/mcp/tools)"]
            end

            subgraph CoreServices["Application Services (DI Container)"]
                S_MCP["LocalLlmMcpTools (8 MCP AI Tools)"]
                S_Disc["ToolDiscoveryService (Multi-Drive Auto-Discovery)"]
                S_VRAM["VramOrchestrator"]
                S_EngineMgr["AiEngineManager (Win32 JobObject / Linux Process)"]
                S_Telemetry["GpuTelemetryProvider (nvidia-smi / Linux proc)"]
                S_Settings["SettingsService (settings.json)"]
                S_Git["GitUpdateService (Git Commands)"]
            end

            YARP["YARP Reverse Proxy Engine"]
        end

        subgraph ExternalEngineLayer["Managed Engine Processes"]
            OllamaEngine["Ollama Server (:11434)"]
            ForgeEngine["Stable Diffusion Forge (:7860)"]
            ComfyEngine["ComfyUI 3D Studio (:8188)"]
        end
    end

    ClaudeAgent -->|JSON-RPC 2.0 /mcp & /api/mcp/tools| E_MCP
    E_MCP --> S_MCP
    S_MCP --> S_Telemetry
    S_MCP --> S_EngineMgr
    S_MCP --> S_Disc

    E2E_Playwright -->|Headless Chromium WebGL| UI_WASM
    DocGen -->|Capture PNG Screenshots| UI_WASM

    UI_Desktop -->|HTTP REST & WS| ProgramHost
    UI_WASM -->|HTTP REST & Static Files| WasmProvider
    WasmProvider --> AppBundle
    UI_Tray -->|Process IPC| ProgramHost

    ProgramHost --> WasmPipeline
    ProgramHost --> Endpoints
    Endpoints --> CoreServices
    ProgramHost --> YARP

    YARP -->|Proxy /v1/chat| OllamaEngine
    YARP -->|Proxy /sdapi| ForgeEngine
    YARP -->|Proxy /comfyapi| ComfyEngine

    S_VRAM -->|Unload VRAM HTTP| OllamaEngine
    S_EngineMgr -->|Process Control| ForgeEngine
    S_EngineMgr -->|Process Control| ComfyEngine
```

---

## 🧱 SOLID MVVM Control & ViewModel Hierarchy

The UI layer is structured into single-responsibility Avalonia `UserControl`s and matching child `ObservableObject` ViewModels.

```mermaid
graph TD
    subgraph Views["Avalonia XAML View Layer"]
        MV["MainView.axaml (Coordinator View)"]
        THC["TelemetryHeaderControl.axaml"]
        OMC["OllamaModelsTabControl.axaml"]
        HFC["HuggingFaceTabControl.axaml"]
        CTC["CivitaiTabControl.axaml"]
        ESC["EngineStudioTabControl.axaml"]
        STC["SettingsTabControl.axaml"]
    end

    subgraph ViewModels["Reactive MVVM ViewModel Layer"]
        MVM["MainViewModel (Root Coordinator)"]
        TVM["TelemetryViewModel"]
        OVM["OllamaLibraryViewModel"]
        HVM["HuggingFaceSearchViewModel"]
        CVM["CivitaiSearchViewModel"]
        SVM["SettingsViewModel"]
    end

    subgraph SharedServices["Shared UI Services & Interfaces"]
        ITS["ITelemetryService -> TelemetryService"]
        IOS["IOllamaModelService -> OllamaModelService"]
        IHS["IHuggingFaceSearchService -> HuggingFaceSearchService"]
        ICS["ICivitaiSearchService -> CivitaiSearchService"]
        IDS["IToolDiscoveryService -> ToolDiscoveryService"]
        TS["ToastService (Global Banner Notifications)"]
    end

    MV --> THC
    MV --> OMC
    MV --> HFC
    MV --> CTC
    MV --> ESC
    MV --> STC

    MVM --> TVM
    MVM --> OVM
    MVM --> HVM
    MVM --> CVM
    MVM --> SVM

    THC -.->|DataContext| TVM
    OMC -.->|DataContext| OVM
    HFC -.->|DataContext| HVM
    CTC -.->|DataContext| CVM
    ESC -.->|DataContext| MVM
    STC -.->|DataContext| SVM

    TVM --> ITS
    OVM --> IOS
    HVM --> IHS
    CVM --> ICS
    SVM --> IDS
    OVM --> TS
    CVM --> TS
    SVM --> TS
```

---

## 🔄 VRAM Orchestration & Engine Switch Sequence

When a user or external client requests an Image Generation or 3D Mesh job while an LLM model is consuming GPU VRAM, the `VramOrchestrator` automatically frees VRAM to prevent Out-Of-Memory (OOM) crashes.

```mermaid
sequenceDiagram
    autonumber
    actor Client as User / AI Agent / Web Client
    participant Proxy as YARP / Minimal API Middleware
    participant Orch as VramOrchestrator
    participant Ollama as Ollama API (:11434)
    participant Engine as ComfyUI / Forge Engine

    Client->>Proxy: POST /sdapi/v1/txt2img (or /api/comfy/prompt)
    Proxy->>Orch: EnsureVramForImageGenerationAsync()
    Orch->>Ollama: POST /api/generate (keep_alive: 0, prompt: "")
    Ollama-->>Orch: 200 OK (Model unmounted from VRAM)
    Orch->>Engine: Probe Health / Check Running Status
    alt Engine not running
        Orch->>Engine: Start Process (Win32 JobObject)
        Orch-->>Orch: Wait for HTTP Health Readiness
    end
    Proxy->>Engine: Forward Image / 3D Render Request
    Engine-->>Client: Return Generated Image / GLB Mesh Output
```

---

## 🛠️ Interface & Service Mapping Matrix

| Interface | Implementation | Lifetime | Responsibility |
|---|---|---|---|
| `IAiEngineManager` | `AiEngineManager` | Singleton | Spawns, monitors, and terminates ComfyUI & Forge process trees via Win32 Job Objects |
| `IToolDiscoveryService` | `ToolDiscoveryService` | Singleton | Scans system drives and PATH to detect Ollama, ComfyUI, and SD Forge; validates paths |
| `IGitUpdateService` | `GitUpdateService` | Singleton | Validates branch names, executes `git fetch`, `git checkout`, and `git pull` |
| `IGpuTelemetryProvider` | `GpuTelemetryProvider` | Singleton | Queries hardware VRAM via `nvidia-smi` CLI, Linux `/proc/meminfo`, or Windows Registry scoring |
| `ISettingsService` | `SettingsService` | Singleton | Handles thread-safe JSON serialization for `settings.json` |
| `ITelemetryService` | `TelemetryService` | Singleton | Queries `/api/gpu/vram` and engine health endpoints for UI ViewModels |
| `IOllamaModelService` | `OllamaModelService` | Singleton | Loads installed models, capabilities, and executes VRAM unload API calls |
| `IHuggingFaceSearchService` | `HuggingFaceSearchService` | Singleton | Queries Hugging Face Hub API for GGUF model repositories and quantization files |
| `ICivitaiSearchService` | `CivitaiSearchService` | Singleton | Queries CivitAI REST API for Stable Diffusion checkpoints, LoRAs, and ratings |
| `LocalLlmMcpTools` | `LocalLlmMcpTools` | Scoped / MCP | Implements 8 standard Model Context Protocol tools for AI agent automation |
| `IContentTypeProvider` | `FileExtensionContentTypeProvider` | Singleton | Configures WASM MIME mapping (`.wasm`, `.dat`, `.json`) for static file hosting |
| `IBrowser` / `IPage` | `PlaywrightWasmE2ETests` / `PlaywrightScreenshotGenerator` | Test Lifecycle | Headless Chromium automation for E2E integration testing and screenshot generation |
| Container Orchestration | `Dockerfile` / `docker-compose.yml` | Container Runtime | Multi-stage Docker packaging, port mapping (`5246`), and volume mounting |
| Installer & Lifecycle | Inno Setup / PowerShell / Bash | Install Runtime | Manages pre-stop of active processes, non-destructive config upgrades, and post-update restarts |

---

## 🎯 Architecture-to-Requirements Mapping

Each architectural subsystem maps directly to standardized requirement specifications defined in **[Software Requirements Specification & RTM](REQUIREMENTS.md)** and verified in **[Test Coverage Specification](TEST_COVERAGE.md)**:

| Architecture Layer | Subsystem / Component | Requirement Domain | Key Requirement IDs |
|---|---|---|---|
| **Host & Middleware** | ASP.NET Core Kestrel Host, YARP Proxy, Settings | Core Infrastructure | `CORE-001` .. `CORE-008` |
| **LLM Management** | Ollama Model Service, KV Cache Calculator | LLM & Ollama | `LLM-001` .. `LLM-008` |
| **Model Hubs** | Hugging Face Hub Service, CivitAI Service | Model Repositories | `HUB-001` .. `HUB-003`, `DIFF-001` .. `DIFF-005` |
| **3D & Studio** | ComfyUI Proxy, 3D Mesh Studio, WebGL Viewer | 3D Generation | `3D-001` .. `3D-006` |
| **Hardware & Memory** | GPU Telemetry Provider, VRAM Orchestrator | Telemetry & Memory | `VRAM-001` .. `VRAM-005` |
| **AI Assistant API** | Model Context Protocol Streamable HTTP & SSE | MCP Integration | `MCP-001` .. `MCP-004` |
| **Installer & Lifecycle**| Inno Setup, PowerShell & Linux Installers | Installer & Upgrades | `INST-001` .. `INST-004` |
| **Tool Discovery** | Multi-Drive Scanner & Path Validation | Tool Discovery | `DISC-001` .. `DISC-005` |
| **Desktop & Web UI** | Avalonia XAML Controls, MVVM Layer, WASM App | User Interface | `UI-001` .. `UI-004`, `WASM-001` .. `WASM-003` |
| **Quality & Automation**| Playwright Harness, Screenshot Generator | Test & Automation | `E2E-001` .. `E2E-003` |

