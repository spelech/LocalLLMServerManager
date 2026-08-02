# Unified Avalonia UI Across Desktop & WebAssembly (WASM) Design Specification

**Date:** 2026-08-02  
**Status:** Approved  
**Version:** 2.1.0  

---

## 1. Executive Summary

Local LLM Server Manager requires a unified user interface across Windows Desktop and Mobile/Tablet Web environments without compromising the existing ASP.NET Core minimal APIs, YARP reverse proxy routes, or Model Context Protocol (MCP) server endpoints.

This design introduces a **Shared UI Architecture** where 100% of XAML views and ViewModels live in a shared class library (`LocalLLMServerManager.Shared`).
- **Windows Desktop Target**: Runs native DirectX/Skia rendering with Win32 System Tray icon integration.
- **Web & Mobile Target**: Compiles the shared XAML UI via `Avalonia.Browser` to WebAssembly (`WASM`), hosted directly in `wwwroot/` by ASP.NET Core on port `5246`.

---

## 2. Solution Architecture & Project Structure

The codebase is structured into three clear projects:

```
LocalLLMServerManager.sln
├── LocalLLMServerManager.Shared/            [Class Library: .NET 10.0]
│   ├── Assets/                              (App icons, logo assets)
│   ├── ViewModels/
│   │   ├── MainViewModel.cs                 (Core reactive state, tab state, responsive layout mode)
│   │   ├── ModelsViewModel.cs               (Ollama models, VRAM telemetry, KV cache calculator)
│   │   ├── HubViewModel.cs                  (Hugging Face & CivitAI search)
│   │   ├── EnginesViewModel.cs              (SD Forge & ComfyUI controls)
│   │   └── SettingsViewModel.cs             (LAN IP info, API config, System Tray settings)
│   ├── Views/
│   │   ├── MainView.axaml                   (Root responsive view with adaptive NavRail / BottomBar)
│   │   ├── ModelsView.axaml                 (Installed models grid & VRAM progress meter)
│   │   ├── HubView.axaml                    (GGUF search & model pull)
│   │   ├── EnginesView.axaml                (SD Forge & ComfyUI engine controls)
│   │   └── SettingsView.axaml               (Remote LAN URLs & preferences)
│   └── Services/
│       ├── IApiClient.cs                    (Abstraction for API calls)
│       └── ApiClient.cs                     (HttpClient implementation pointing to relative or absolute endpoints)
│
├── LocalLLMServerManager/                   [ASP.NET Core + Desktop Host: .NET 10.0]
│   ├── Program.cs                           (ASP.NET Core WebHost, minimal APIs, MCP endpoints, Avalonia desktop entrypoint)
│   ├── App.axaml / App.axaml.cs             (Avalonia Application lifetime & System Tray Icon)
│   ├── Views/MainWindow.axaml               (Desktop Window shell wrapping Shared MainView)
│   └── wwwroot/                             (Static web files served by ASP.NET Core, including compiled WASM bundle)
│
└── LocalLLMServerManager.Web/               [Avalonia.Browser App: .NET 10.0]
    ├── Program.cs                           (WASM entrypoint initializing Avalonia.Browser)
    ├── App.axaml / App.axaml.cs             (WASM App lifetime setting MainView as Root)
    └── index.html                           (HTML5 canvas container loading main.js and dotnet.wasm)
```

---

## 3. Adaptive Responsive XAML Design

To ensure optimal usability across smartphones, tablets, and desktop monitors, `MainView.axaml` adapts layout based on viewport width:

### Breakpoint Specifications:
1. **Mobile (`Width < 600px`)**:
   - Single-column vertical scroll view (`StackPanel` card layout).
   - Top compact telemetry header bar (GPU name & VRAM usage meter).
   - Bottom touch-friendly tab navigation bar (`ItemsControl` / `SegmentedControl`, minimum `44px` touch targets).
2. **Tablet (`600px <= Width <= 960px`)**:
   - Compact left icon navigation rail (`NavRail`).
   - Dual-column fluid card layout (`UniformGrid Columns="2"`).
3. **Desktop (`Width > 960px`)**:
   - Full left navigation sidebar with icons and text labels.
   - Multi-column grid layout, expanded Hugging Face / CivitAI hub search, and real-time interactive KV Cache Calculator slider.

---

## 4. API, MCP & Data Flow Integration

1. **Unified API Client (`ApiClient.cs`)**:
   - On Desktop: Direct HTTP or in-process calls to `http://127.0.0.1:5246`.
   - On WebAssembly: Relative HTTP calls (`/health`, `/api/gpu/vram`, `/api/models`), connecting back to the hosting ASP.NET Core server regardless of LAN IP or remote domain.
2. **MCP & Server Integrity**:
   - `/api/mcp/tools` endpoint remains 100% untouched and accessible to AI agents (Antigravity, Cursor, Claude).
   - YARP Reverse Proxy handles `/api/{**catch-all}` routing to Ollama (`:11434`), SD Forge (`:7860`), and ComfyUI (`:8188`).

---

## 5. Build & Deployment Pipeline

1. **WebAssembly Build**: `LocalLLMServerManager.Web` is compiled via `dotnet publish -c Release`. Output static assets (`main.js`, `dotnet.wasm`, HTML canvas runner) are copied into `LocalLLMServerManager/wwwroot/`.
2. **Inno Setup Installer**: `installer.iss` packages `LocalLLMServerManager.exe` and `wwwroot/` into `LocalLLMServerManager-v2.1.0-Setup.exe`.
3. **Verification & Testing**:
   - `dotnet test` executes all unit tests in `LocalLLMServerManager.Tests`.
   - TypeScript/JS linting: `node --check` and static bundle verification.
