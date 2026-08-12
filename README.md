# Local LLM Server Manager

> **v3.4.0** — A unified cross-platform application (.NET 10 + Avalonia UI & WebAssembly), System Tray app, background service/daemon, Model Context Protocol (MCP) AI API, visual orchestrator dashboard, and automated Playwright E2E testing framework to manage local Large Language Models (**Ollama**), Image Generation (**Stable Diffusion / Forge & ComfyUI**), and **3D Mesh Generation (TRELLIS V2 & Hunyuan3D v2)** on Windows, Linux, Mobile, and Web.

It tracks GPU VRAM usage in real time via NVML CUDA telemetry, profiles model capabilities, computes KV Cache memory footprints, integrates with the **Hugging Face Hub** to discover and pull GGUF models, connects to **CivitAI** to browse and download Stable Diffusion checkpoints directly to disk, features a **3D & ComfyUI Studio** with an interactive WebGL 3D canvas viewer, provides a **Unified Avalonia XAML WebAssembly (WASM)** interface across mobile and desktop browsers, and exposes a **Model Context Protocol (MCP) Server** (`/api/mcp/tools`) for AI assistants (Antigravity, Cursor, Claude).

![Dashboard Overview](docs/images/dashboard_desktop.png)

---

## 🖥️ User Interface Layout & Dashboard Structure

The application features a dark Fluent Avalonia UI theme (`#0F172A`) organized into modular tabs:

```
+-----------------------------------------------------------------------------------------+
| Local LLM Server Manager                                                                |
| GPU: NVIDIA GeForce RTX 4070 Ti SUPER -- 16 GB • Service Connected 🟢   [🔄 Refresh]    |
| GPU VRAM Allocation: 4.2 GB / 16.0 GB (26.3%)                                           |
| [========================-------------------------------------------------------------] |
+-----------------------------------------------------------------------------------------+
| [🦙 Installed Models] [🤗 Hugging Face Hub] [🎨 CivitAI Models] [📦 3D Studio] [⚙️ Settings] |
+-----------------------------------------------------------------------------------------+
| Ollama Local Model Library                                          [🧹 Unload All VRAM] |
|                                                                                         |
| +-------------------------------------------------------------------------------------+ |
| | qwen2.5-coder:7b                         [Coding] [4.7 GB]              Installed 🟢 | |
| +-------------------------------------------------------------------------------------+ |
| | llama3.2:latest                          [Chat] [2.0 GB]                Installed 🟢 | |
| +-------------------------------------------------------------------------------------+ |
|                                                                                         |
| Interactive KV Cache Calculator                             ~0.5 GB                     |
| [====================================------------------------------------------------]  |
| 8,192 tokens                                                                            |
+-----------------------------------------------------------------------------------------+
| LocalLLMServerManager v3.4.0 -- Unified WASM & Desktop UI         System Tray Enabled 🟢 |
+-----------------------------------------------------------------------------------------+
```

---

## 🌟 Key Features

### Native Desktop App & Services (Windows & Linux)
1. **Avalonia UI Native Dashboard** — Sleek Fluent dark desktop window presenting live VRAM usage, engine status cards, and one-click browser launch on Windows and Linux (X11 / Wayland).
2. **System Tray Integration** — Operates quietly in the notification area with right-click quick controls (Open Dashboard, View Health, Exit).
3. **Headless Background Services** — Runs headlessly on machine boot via Windows Service or Linux `systemd` daemon (`localllmmanager.service`).
4. **Automated Tray Attachment** — When a user logs in, the Avalonia System Tray app automatically attaches to the running background service instance.

### LLM Management (Ollama & Hugging Face Hub)
5. **Service Health Checks** — Real-time status indicators for Ollama (`11434`), Stable Diffusion / Forge (`7860`), and ComfyUI (`8188`).
6. **Cross-Platform VRAM Telemetry** — Reads GPU name and VRAM via NVML CUDA (`nvidia-smi`), Windows Registry, or Linux system memory (`/proc/meminfo`). Correctly reports e.g. *NVIDIA GeForce RTX 4070 Ti SUPER — 16 GB*.
7. **VRAM Usage Visualizer** — Stacked bar showing loaded-model VRAM vs free GPU memory.
8. **KV Cache Context Calculator** — Slide target token length (up to 32 K tokens) to preview weights + KV cache sizes and warn when context exceeds VRAM.
9. **Model Capabilities Profile** — Tags model families (Llama, Gemma, Qwen, Phi, Mistral, DeepSeek) with use-case badges (`Coding`, `Reasoning`, `Math`, `Chat`).
10. **Hugging Face Hub Integration** — Search GGUF repos, select quantization, inspect file sizes, and download with a live SSE progress stream.
11. **Ollama Library Quick-Pull** — Pre-populated cards for popular models (gemma2, llama3.2, qwen2.5-coder, phi3) with size estimates and one-click pull.
12. **Custom Pull** — Type any `user/model:tag` to pull an arbitrary Ollama model.
13. **Concurrent Model Preloading** — Trigger indefinite VRAM holds (`keep_alive: -1`) to run multiple models side-by-side.

![Ollama Installed Models](docs/images/dashboard_ollama.png)

![Hugging Face GGUF Search](docs/images/dashboard_huggingface.png)

### 3D Mesh & ComfyUI Generation (TRELLIS V2 / Hunyuan3D v2)
14. **ComfyUI Integration** — Proxy ComfyUI workflow execution, API requests, and WebSocket progress directly through port 5246.
15. **3D Mesh Generation** — Run TRELLIS V2 and Hunyuan3D v2 workflows for Image-to-3D and Text-to-3D mesh generation (.glb / .gltf).
16. **Interactive WebGL 3D Canvas** — Render generated 3D meshes natively in-browser using `<model-viewer>` with 360° orbital controls, wireframe toggles, lighting options, and GLB export.
17. **Bundled API Workflow Presets** — Ships with default ready-to-run API JSON templates for TRELLIS V2, Hunyuan3D v2, and FLUX/SDXL image generation.
18. **Engine Preference Switcher** — Easily set your preferred default image generator engine (Forge vs ComfyUI).

![3D Mesh & ComfyUI Studio](docs/images/dashboard_3d_studio.png)

### Stable Diffusion / Forge & CivitAI
19. **CivitAI Integration** — Search by name, type (Checkpoint / LoRA / Embedding / VAE / ControlNet), and sort order. Shows preview thumbnails, download counts, and star ratings.
20. **Direct-to-Disk Downloads** — Stream CivitAI files directly to disk with live progress bars.

![CivitAI SD Checkpoints](docs/images/dashboard_civitai.png)

### Application Settings & Engine Controls
21. **Configuration Dashboard** — Customize ports, engine directory paths, auto-unload thresholds, and telemetry refresh intervals.

![Application Settings](docs/images/dashboard_settings.png)

### Infrastructure & Reverse Proxy
22. **YARP Reverse Proxy** — Transparently proxies Ollama (`:11434`), Forge (`:7860`), and ComfyUI (`:8188`) traffic through a single endpoint (`:5246`).
23. **VRAM Orchestrator** — Auto-unloads active LLM models from GPU memory before heavy Stable Diffusion or ComfyUI 3D render jobs to prevent OOM errors.
24. **Background Engine Management** — UI controls to start/stop engines directly from the dashboard cleanly.
25. **Lazy Boot** — AI engines can now boot lazily on-demand when first requested, conserving system resources when idle.

---

## 🏛️ System Architecture

```
                  +----------------------------------------------+
                  |  Desktop Session (User Logon - Win/Linux)    |
                  |  - Avalonia UI System Tray Icon / Window     |
                  |  - Native XAML Dark Dashboard Window         |
                  |  - Auto-Attaches to local server (:5246)     |
                  +----------------------+-----------------------+
                                         | REST / HTTP (:5246)
                                         v
+-----------------------------------------------------------------------------------+
|  Local HTTP Server & Reverse Proxy Host                                           |
|  - ASP.NET Core Web API + YARP Reverse Proxy (:5246)                              |
|  - VRAM Orchestrator & Process Management                                         |
|  - Responsive Web Dashboard & WebGL 3D Studio (wwwroot)                           |
+------------------------------------+----------------------------------------------+
                                     |
                                     v
                  +-----------------------------------+
                  | Managed Processes                 |
                  | - Ollama (:11434)                 |
                  | - SD Forge (:7860)                |
                  | - ComfyUI (:8188)                 |
                  +-----------------------------------+
```

### Dual-Session Lifecycle
* **Headless Background Service Mode**: Machine boots -> `LocalLLMServerManager --service` starts automatically before user logon (Windows Service or Linux `systemd` daemon). Hosts Web API, YARP proxy, and VRAM orchestrator headlessly on `http://127.0.0.1:5246`.
* **User Desktop Session**: User signs in -> `LocalLLMServerManager` desktop app starts, probes `:5246/health`, and automatically attaches to the running background service instance.

---

## 🎭 Playwright Automated E2E Browser Testing

LocalLLMServerManager includes automated end-to-end (E2E) browser testing built on `Microsoft.Playwright` and xUnit. The test suite spins up an in-memory ASP.NET Core server (`AppTestServerFixture`) and launches headless Chromium with WebAssembly and WebGL flags (`--use-gl=angle --use-angle=swiftshader --enable-webgl`) to validate application behavior in real browser engines.

### Key Capabilities
- **WASM Bundle & Static File Validation**: Listens for HTTP responses to verify zero `404 Not Found` errors when serving Avalonia WASM `.dll`, `.dat`, `.wasm`, and `.boot.json` assets.
- **Console Error Trap**: Monitors browser console output to ensure zero uncaught JavaScript errors occur during WASM startup and canvas rendering.
- **WebGL 3D Canvas Initialization**: Confirms the `<canvas id="out">` element is initialized and rendered with non-zero dimensions.
- **Automated Screenshot Generation**: `PlaywrightScreenshotGenerator` navigates the dark Fluent UI dashboard and captures real 1280x800 PNG screenshots stored in `docs/images/`.

### Running Playwright Tests
```bash
# Install Playwright browser drivers (Chromium)
pwsh LocalLLMServerManager.Tests/bin/Release/net10.0/playwright.ps1 install chromium

# Run all Playwright WASM E2E tests
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightWasmE2ETests" -c Release

# Run automated screenshot generator
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" -c Release
```

---

## 🐳 Docker & Container Deployment

LocalLLMServerManager can be containerized using Docker for seamless deployment on server infrastructure or home lab setups.

### Multi-Stage `Dockerfile`
```dockerfile
# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["LocalLLMServerManager.csproj", "./"]
COPY ["LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj", "LocalLLMServerManager.Shared/"]
COPY ["LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj", "LocalLLMServerManager.Web/"]
RUN dotnet restore "LocalLLMServerManager.csproj"
COPY . .
RUN dotnet publish "LocalLLMServerManager.csproj" -c Release -o /app/publish

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 5246
ENV ASPNETCORE_URLS=http://+:5246
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LocalLLMServerManager.dll", "--service"]
```

### `docker-compose.yml`
```yaml
version: '3.8'

services:
  localllmservermanager:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: localllmservermanager
    ports:
      - "5246:5246"
    volumes:
      - ./data:/app/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5246
    restart: unless-stopped
```

### Running with Docker CLI
```bash
# Build Docker image
docker build -t localllmservermanager:v3.4.0 .

# Run container exposing port 5246
docker run -d -p 5246:5246 --name localllmservermanager localllmservermanager:v3.4.0

# Or start using Docker Compose
docker-compose up -d
```

---

## 🌐 WebAssembly Static Asset Hosting & Kestrel MIME Mappings

To host Avalonia XAML WebAssembly applications directly within ASP.NET Core Kestrel without runtime loading errors, `Program.cs` configures a custom `FileExtensionContentTypeProvider` for static files.

### Configured MIME Mappings
```csharp
var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".dat"] = "application/octet-stream";
contentTypeProvider.Mappings[".symbols"] = "application/octet-stream";
contentTypeProvider.Mappings[".wasm"] = "application/wasm";
contentTypeProvider.Mappings[".clr"] = "application/octet-stream";
contentTypeProvider.Mappings[".pdb"] = "application/octet-stream";
contentTypeProvider.Mappings[".boot.json"] = "application/json";

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});
```

### Technical Benefits
- **WebAssembly Compatibility**: Ensures `.wasm` files are served with `application/wasm` headers required by web browsers for WebAssembly compilation.
- **Managed Assembly & Data Stream Support**: `.dat`, `.clr`, and `.pdb` static files are served as `application/octet-stream`, preventing 404/415 media type rejection by ASP.NET Core middleware.
- **Fallback Type Handling**: `ServeUnknownFileTypes = true` prevents missing static file headers when Avalonia WASM requests dynamic assembly blobs or metadata files.

---

## 📱 Mobile Responsiveness & Cross-Device Compatibility

The Web Dashboard features a responsive CSS layout engine:
* **Mobile Viewport Optimization**: Dynamically adjusts cards, status badges, search bars, and navigation tabs to single-column flex layouts on mobile devices (< 768px).
* **Zero Element Overlap**: Grid systems automatically collapse into stacked cards with full touch target support for phones and tablets.
* **Responsive 3D Studio**: The WebGL 3D Mesh viewer (`<model-viewer>`) automatically resizes canvas bounds and supports touch gesture orbit controls.

---

## 📚 Guides & Documentation

- [Developer & Contributor Guide](docs/DEVELOPMENT_GUIDE.md) — Comprehensive guide on project layout, SOLID Avalonia XAML controls, design tokens, MVVM pattern, Minimal API endpoints, and testing.
- [System Architecture & Mermaid Diagrams](docs/ARCHITECTURE.md) — Visual architecture blueprints, component hierarchy, VRAM orchestration sequence diagrams, and service mapping matrices.
- [ComfyUI & 3D Mesh Generation Setup Guide](docs/COMFYUI_AND_3D_GUIDE.md) — How to configure ComfyUI, install 3D nodes (TRELLIS V2 / Hunyuan3D v2), and export custom workflow presets.
- [Linux Caddy Proxy & Open WebUI / LibreChat Integration Guide](docs/CADDY_OPENWEBUI_SETUP.md) — How to expose LocalLLMServerManager via Caddy reverse proxy to Open WebUI and LibreChat clients.

---

## 📦 Versioning Convention

We use **MAJOR.MINOR.PATCH** (SemVer):

| Version | What changed |
|---------|-------------|
| `1.0.0` | Initial release — dashboard, VRAM bar, HF search, Ollama pull, YARP proxy, Windows Service |
| `1.1.0` | CivitAI search tab with model type / sort filters and preview thumbnails |
| `1.2.0` | Forge models directory config, direct-to-disk CivitAI downloads with SSE progress, persistent `settings.json` |
| `1.3.0` | Migration to .NET 10 LTS target framework and updated dependencies |
| `1.4.0` | ComfyUI integration, 3D Mesh Studio (TRELLIS V2 / Hunyuan3D v2), interactive WebGL 3D viewer, preferred engine toggle |
| `1.5.0` | Lazy boot for AI engines, process job objects, and UI controls for background engine management |
| `2.0.0` | Major architecture update — Avalonia UI desktop shell, system tray icon, pre-logon Windows Service boot & logon tray attachment |
| `3.0.0` | Avalonia WebAssembly (Wasm) integration, 3D Canvas Studio, unified multi-platform interface |
| `3.1.0` | Cross-Platform Linux support, Linux release scripts (`build_release.sh`), systemd service installer (`install_linux.sh`), `.desktop` launcher, NVML & `/proc/meminfo` VRAM telemetry, and SSH remote workflow support |
| `3.2.0` | Fixed WASM launcher script routing, added `/api/models` backend proxy, updated high-res 32-bit icon, added end-to-end integration tests, and completed repo housekeeping |
| `3.3.0` | Major architecture refactoring — decomposed Program.cs and MainViewModel into modular interfaces, services, and endpoint route extensions |
| `3.4.0` | Added Playwright automated E2E browser testing, real WebAssembly UI screenshot generator, Docker containerization support, and Kestrel WASM static asset MIME type mappings |

---

## 🚀 Installation & Downloads

### Option 1: Official Windows Installer (.exe)
Download the latest `LocalLLMServerManager-v3.4.0-Setup.exe` from the [GitHub Releases](https://github.com/spelech/LocalLLMServerManager/releases) page.
* Includes an installation wizard with options for:
  * 🟢 **Install Windows Service** (Headless pre-logon machine boot)
  * 🟢 **Auto-Start System Tray App** on user login
  * 🟢 **Desktop & Start Menu Shortcuts**

### Option 2: Linux Automated Installation Script (`install_linux.sh`)
Clone the repository on Linux and run:
```bash
sudo ./install_linux.sh
```
* Installs the app binary to `/usr/local/share/LocalLLMServerManager`
* Symlinks binary to `/usr/local/bin/localllmmanager`
* Registers the **systemd service** (`localllmmanager.service`) for background autostart
* Installs desktop launcher (`localllmmanager.desktop`) in your application menu

### Option 3: Standalone Portable (.zip / .tar.gz)
Download `LocalLLMServerManager-v3.4.0-win-x64.zip` or `LocalLLMServerManager-v3.4.0-linux-x64.tar.gz` from Releases, extract, and run executable. Includes bundled runtime — no .NET SDK required!

### Option 4: Building Release Packages Locally
- **Windows:** Run `.\build_release.ps1`
- **Linux:** Run `./build_release.sh`
Output artifacts will be generated in `dist/`.

---

## 🌐 Remote SSH Viewing & Port Forwarding

To work with LocalLLMServerManager on a remote Linux machine over SSH:
1. Connect over SSH with local port forwarding:
   ```bash
   ssh -L 5246:localhost:5246 user@your-linux-host
   ```
2. Run the application in headless service mode on the remote host:
   ```bash
   dotnet run -- --service
   # or manage via systemd: sudo systemctl start localllmmanager
   ```
3. Open **`http://localhost:5246`** in your local browser to access 100% of the UI features (VRAM monitor, Hugging Face search, CivitAI downloader, 3D WebGL viewer) at full speed with zero lag over SSH.

---

## ⚙️ Service Control Commands

### Linux (systemd)
```bash
# Start Service
sudo systemctl start localllmmanager

# Stop Service
sudo systemctl stop localllmmanager

# Check Status
sudo systemctl status localllmmanager
```

### Windows (Service Control)
Open PowerShell as **Administrator**:
```powershell
# Start Service
Start-Service -Name "LocalLLMServerManager"

# Stop Service
Stop-Service -Name "LocalLLMServerManager"

# Service Status
Get-Service -Name "LocalLLMServerManager"
```

If running directly:
```cmd
C:\LocalLLMServerManager\LocalLLMServerManager.exe
```
Dashboard available at **http://localhost:5246/**

---

## 🔧 Prerequisites

- **[Ollama](https://ollama.com/)** — Local LLM inference runtime
- **[Stable Diffusion WebUI Forge](https://github.com/lllyasviel/stable-diffusion-webui-forge)** *(optional)* — SD image generation backend
- **[ComfyUI](https://github.com/comfyanonymous/ComfyUI)** *(optional)* — Node-based 3D mesh & image generation backend
- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10)** *(optional)* — Only required if compiling from source code
