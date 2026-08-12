# LocalLLMServerManager — Developer & Contributor Guide

> **Version 3.4.0** | .NET 10 | Avalonia UI | ASP.NET Core Minimal API | YARP Reverse Proxy | Playwright E2E Browser Automation

This guide provides a comprehensive overview of how to build, develop, extend, and test **LocalLLMServerManager**. It covers the codebase layout, MVVM architecture, Avalonia XAML control composition, styling design tokens, Minimal API endpoints, dependency injection, Playwright E2E browser testing, and automated screenshot generation.

---

## 📋 Prerequisites & Tools

To develop and build **LocalLLMServerManager**, ensure you have installed:

1. **[.NET 10 SDK](https://dotnet.microsoft.com/download)** — Target framework `net10.0`.
2. **IDE / Editor**:
   - Visual Studio 2022 / VS Code with **Avalonia for Visual Studio** extension.
   - Antigravity / C# Dev Kit extension.
3. **PowerShell 7+ / Bash** — For running build, test, and installer packaging scripts.
4. *(Optional)* **[Inno Setup 6](https://jrsoftware.org/isinfo.php)** — Required if compiling the Windows executable installer (`scripts/installer.iss`).

---

## 📁 Repository Directory Structure

The solution is organized into modular projects following clean architecture guidelines:

```
LocalLLMServerManager/
├── Program.cs                               # ASP.NET Core Minimal API Host & DI Container Bootstrapper
├── Endpoints/                               # Route Extension Modules (Minimal API Map Extensions)
│   ├── EngineEndpoints.cs                   # /api/gpu/vram, /api/settings, /api/comfy/*, /api/forge/*
│   ├── HealthEndpoints.cs                   # /health healthcheck endpoint
│   ├── McpEndpoints.cs                      # /api/mcp/tools Model Context Protocol JSON-RPC API
│   ├── ModelProxyEndpoints.cs               # /api/models, /api/ollama/ps, /api/hf/*, /api/civitai/*
│   └── WorkflowEndpoints.cs                # /api/comfy/workflows, /api/3d/files
├── Services/                                # Concrete Server Infrastructure Services
│   ├── AiEngineManager.cs                   # Managed Process Lifecycle & Win32 Job Object Memory Caps
│   ├── GitUpdateService.cs                  # Self-Update Git Commands (fetch, checkout, pull)
│   ├── GpuTelemetryProvider.cs              # NVML nvidia-smi & Linux /proc/meminfo telemetry provider
│   ├── SettingsService.cs                   # JSON Settings file storage service
│   ├── VramOrchestrator.cs                  # Memory Orchestrator (prevents OOM when switching engines)
│   └── Win32JobObject.cs                    # Win32 Process Management & Cleanup Job Object
├── LocalLLMServerManager.Shared/            # Cross-Platform Shared UI Library (Desktop + WASM)
│   ├── Interfaces/                          # Standalone Interfaces for Dependency Injection
│   │   ├── ICivitaiSearchService.cs
│   │   ├── IHuggingFaceSearchService.cs
│   │   ├── IOllamaModelService.cs
│   │   └── ITelemetryService.cs
│   ├── Models/                              # Data DTOs & Models
│   │   └── AppSettings.cs
│   ├── Services/                            # Shared UI Services
│   │   ├── CivitaiSearchService.cs
│   │   ├── HuggingFaceSearchService.cs
│   │   ├── OllamaModelService.cs
│   │   ├── TelemetryService.cs
│   │   ├── ToastService.cs                  # Global Toast Notification Service
│   │   └── BrowserLauncher.cs               # Cross-platform URL launcher
│   ├── ViewModels/                          # MVVM ViewModels
│   │   ├── MainViewModel.cs                 # Root ViewModel Coordinator
│   │   ├── TelemetryViewModel.cs            # Telemetry & VRAM gauge sub-ViewModel
│   │   ├── OllamaLibraryViewModel.cs        # Ollama library & KV cache sub-ViewModel
│   │   ├── HuggingFaceSearchViewModel.cs    # Hugging Face Hub search sub-ViewModel
│   │   ├── CivitaiSearchViewModel.cs        # CivitAI search sub-ViewModel
│   │   └── SettingsViewModel.cs             # App settings sub-ViewModel
│   └── Views/                               # Avalonia XAML UI Views & Controls
│       ├── MainView.axaml                   # Root Coordinator View
│       └── Controls/                        # Modular SOLID UserControls
│           ├── TelemetryHeaderControl.axaml
│           ├── OllamaModelsTabControl.axaml
│           ├── HuggingFaceTabControl.axaml
│           ├── CivitaiTabControl.axaml
│           ├── EngineStudioTabControl.axaml
│           └── SettingsTabControl.axaml
├── LocalLLMServerManager.Web/                # Avalonia WebAssembly (WASM) Project
│   ├── App.axaml
│   └── Program.cs
└── LocalLLMServerManager.Tests/              # Automated Test Suite (133 Unit, Integration & E2E Tests)
    ├── PlaywrightWasmE2ETests.cs             # Playwright E2E WebAssembly Browser Automation Tests
    ├── PlaywrightScreenshotGenerator.cs       # Automated Documentation PNG Screenshot Generator
    └── AppTestServerFixture.cs               # WebApplication Kestrel Test Host Fixture
```

---

## 🎨 UI Design Tokens & Styling Architecture

The application uses a **Fluent Dark Theme** built with Avalonia XAML styles and curated design tokens.

### Color Palette

| Token Name | Hex Code | Use Case |
|---|---|---|
| `BackgroundPrimary` | `#0F172A` | Application background & canvas |
| `CardBackground` | `#1E293B` | UserControl borders, tab content, cards |
| `BorderBrush` | `#334155` | Subtle container borders & dividers |
| `AccentPrimary` | `#38BDF8` | Primary highlights, GPU VRAM bar, primary action buttons |
| `AccentSecondary` | `#EC4899` | CivitAI branding, Stable Diffusion action buttons |
| `AccentPurple` | `#A855F7` | ComfyUI 3D Studio accents |
| `StatusGreen` | `#22C55E` | Online badges, installed status indicators, save confirmation |
| `TextPrimary` | `#F8FAFC` | Main headings & primary text |
| `TextMuted` | `#94A3B8` | Subtitles, labels, secondary metadata |

### Creating New UserControls (SOLID SRP Pattern)

When adding a new feature or tab to the Avalonia desktop/WASM UI:
1. Create a dedicated `.axaml` UserControl under `LocalLLMServerManager.Shared/Views/Controls/`.
2. Create the matching code-behind `.axaml.cs` file inheriting from `UserControl`.
3. Strongly type the control's DataContext via `x:DataType="vm:YourSubViewModel"`.
4. Embed the control inside `MainView.axaml` using the `controls:` namespace.

#### Example Control Markup:
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:LocalLLMServerManager.Shared.ViewModels"
             x:Class="LocalLLMServerManager.Shared.Views.Controls.CustomFeatureControl"
             x:DataType="vm:CustomFeatureViewModel">
    <Border Background="#1E293B" CornerRadius="8" BorderBrush="#334155" BorderThickness="1" Padding="16">
        <TextBlock Text="{Binding HeaderText}" Foreground="#F8FAFC" FontSize="16" FontWeight="Bold"/>
    </Border>
</UserControl>
```

---

## 🧱 MVVM Pattern & CommunityToolkit MVVM

The project leverages `CommunityToolkit.Mvvm` for clean, decoupled reactivity.

### Rules for ViewModels
1. **Single Responsibility**: Keep feature logic inside dedicated sub-ViewModels (`TelemetryViewModel`, `OllamaLibraryViewModel`, `HuggingFaceSearchViewModel`, `CivitaiSearchViewModel`, `SettingsViewModel`).
2. **Root ViewModel Coordination**: `MainViewModel` aggregates sub-ViewModels and forwards properties for backward compatibility.
3. **Use Source Generators**: Use `[ObservableProperty]` for properties and `[RelayCommand]` for actions.

#### ViewModel Pattern Example:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class CustomFeatureViewModel : ObservableObject
{
    [ObservableProperty]
    private string _headerText = "Feature Ready";

    [RelayCommand]
    public void ExecuteAction()
    {
        HeaderText = "Action Triggered!";
    }
}
```

---

## ⚡ ASP.NET Core Minimal API & Extension Endpoints

Backend HTTP routes are modularized using C# extension methods on `WebApplication`.

### Adding a New API Endpoint Module

1. Create a static class under `LocalLLMServerManager/Endpoints/CustomEndpoints.cs`.
2. Define a `MapCustomEndpoints(this WebApplication app)` extension method.
3. Register the extension method call inside `Program.cs`.

```csharp
namespace LocalLLMServerManager.Endpoints;

public static class CustomEndpoints
{
    public static void MapCustomEndpoints(this WebApplication app)
    {
        app.MapGet("/api/custom/status", (ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();
            return Results.Ok(new { status = "Ready", path = settings.ForgeModelsPath });
        });
    }
}
```

---

## 🧪 Testing & Quality Assurance Guidelines

All code changes must maintain **100% test pass rate** across unit, integration, and Playwright E2E browser tests.

### 1. Running Unit & Integration Tests
```bash
# Build and run the entire test suite in Release configuration
dotnet test LocalLLMServerManager.sln --nologo -c Release
```

### 2. Playwright Chromium Driver Setup
Playwright E2E testing requires the Chromium browser driver binaries. Run the following command after building the test assembly:
```powershell
# Install Playwright Chromium browser binary
pwsh LocalLLMServerManager.Tests/bin/Release/net10.0/playwright.ps1 install chromium
```

### 3. Executing Playwright E2E Browser Tests
To execute end-to-end browser automation tests targeting the WebAssembly web application hosted via Kestrel Minimal API:
```bash
# Run Playwright E2E WASM browser tests
dotnet test --filter "FullyQualifiedName~PlaywrightWasmE2ETests" -c Release
```

### 4. Automated Documentation Screenshot Generator (`PlaywrightScreenshotGenerator.cs`)
The project includes an automated Playwright screenshot generator (`PlaywrightScreenshotGenerator.cs`) that launches headless Chromium with WebGL rendering flags (`--use-gl=angle --use-angle=swiftshader --enable-webgl`), boots the WebAssembly host fixture (`AppTestServerFixture`), navigates each dashboard tab, and outputs crisp PNG screenshots directly to `docs/images/`:

- `docs/images/dashboard_desktop.png` — Overview & VRAM Monitor (Tab 1)
- `docs/images/dashboard_ollama.png` — Ollama Installed Models (Tab 1)
- `docs/images/dashboard_huggingface.png` — Hugging Face Hub Model Search (Tab 2)
- `docs/images/dashboard_civitai.png` — CivitAI Stable Diffusion Asset Manager (Tab 3)
- `docs/images/dashboard_3d_studio.png` — 3D & ComfyUI Studio WebGL Canvas (Tab 4)
- `docs/images/dashboard_settings.png` — App Settings & Configuration (Tab 5)

To re-generate all user guide screenshots automatically:
```bash
# Execute automated screenshot generator
dotnet test --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" -c Release
```

### Coverage Rule
- Always execute `dotnet test LocalLLMServerManager.sln --nologo -c Release` before committing or tagging releases.
- Ensure no orphaned background processes remain after running tests.
