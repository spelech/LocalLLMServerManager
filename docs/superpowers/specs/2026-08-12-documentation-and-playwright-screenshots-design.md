# Documentation & Playwright Real Screenshot Generator Design Specification

> **Date:** 2026-08-12  
> **Status:** Draft (Approved in Brainstorming)  
> **Target Version:** v3.4.0  

---

## 🎯 Goal
Overhaul the repository documentation ([`README.md`](file:///C:/Users/Alias/repos/LocalLLMServerManager/README.md), [`docs/ARCHITECTURE.md`](file:///C:/Users/Alias/repos/LocalLLMServerManager/docs/ARCHITECTURE.md), [`docs/DEVELOPMENT_GUIDE.md`](file:///C:/Users/Alias/repos/LocalLLMServerManager/docs/DEVELOPMENT_GUIDE.md), [`docs/USER_GUIDE.md`](file:///C:/Users/Alias/repos/LocalLLMServerManager/docs/USER_GUIDE.md)) to accurately reflect the current `v3.4.0` state of the project. Introduce an automated Playwright screenshot generator (`PlaywrightScreenshotGenerator.cs`) in `LocalLLMServerManager.Tests` to capture real, high-resolution PNG browser screenshots of the dark Fluent Avalonia WebAssembly UI (no AI-generated images) and embed them directly into the documentation.

---

## 📐 Architecture & Key Components

### 1. Automated Playwright Screenshot Generator
- **Location:** `LocalLLMServerManager.Tests/PlaywrightScreenshotGenerator.cs`
- **Fixture:** Uses `IClassFixture<AppTestServerFixture>` to spin up ASP.NET Core Kestrel in-memory.
- **Browser Config:** Launches Chromium headless with a `1280x800` viewport, `--use-gl=angle --use-angle=swiftshader --enable-webgl`.
- **Workflow:**
  1. Boot web host and navigate page to `AppTestServerFixture.TestBaseUrl`.
  2. Wait for `#out` canvas element and Avalonia WASM boot (5000ms delay).
  3. Capture high-resolution PNG screenshots for each tab view into `docs/images/`:
     - `docs/images/dashboard_desktop.png` (Overview & VRAM Telemetry header)
     - `docs/images/dashboard_ollama.png` (Ollama Installed Models)
     - `docs/images/dashboard_huggingface.png` (Hugging Face GGUF Search)
     - `docs/images/dashboard_civitai.png` (CivitAI SD Checkpoints Search)
     - `docs/images/dashboard_3d_studio.png` (3D Mesh & ComfyUI Studio)
     - `docs/images/dashboard_settings.png` (Settings Tab)

### 2. `README.md` Documentation Refresh
- **Version Bump:** Update to `v3.4.0` (Unified Avalonia XAML WebAssembly & Playwright E2E Release).
- **Embedded Screenshots:** Replace text ASCII art diagrams with real embedded markdown images (`![Dashboard Overview](docs/images/dashboard_desktop.png)`).
- **New Sections:**
  - **Docker & Container Deployment**: Detailed instructions for `Dockerfile` multi-stage build and `docker-compose.yml`.
  - **Playwright Automated E2E Testing**: How E2E browser tests verify zero 404 static asset errors and zero uncaught JS console errors.
  - **Static File Hosting & MIME Mappings**: Technical explanation of `.dat`, `.symbols`, `.wasm` static file handling in Kestrel.

### 3. Architecture Specification (`docs/ARCHITECTURE.md`)
- **Updated Mermaid Diagrams:**
  - Add Playwright E2E testing framework layer to system architecture diagram.
  - Add WebAssembly `AppBundle` & Kestrel `FileExtensionContentTypeProvider` pipeline.
  - Add Docker containerization lifecycle (`docker-compose.yml`).
- **Updated Matrices:**
  - Update `IAiEngineManager`, `ITelemetryService`, `IOllamaModelService`, and static file middleware mapping tables.

### 4. Developer & User Guides
- **`docs/DEVELOPMENT_GUIDE.md`**: Add instructions for running Playwright tests (`dotnet test --filter "FullyQualifiedName~PlaywrightWasmE2ETests"`), generating screenshots, and installing browser drivers (`pwsh LocalLLMServerManager.Tests/bin/Release/net10.0/playwright.ps1 install chromium`).
- **`docs/USER_GUIDE.md`**: Embed real Playwright UI screenshots to complement user feature walkthroughs.

---

## 🧪 Verification Strategy
1. Run `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" -c Release` to verify screenshot generation.
2. Confirm PNG files exist in `docs/images/` and are valid 1280x800 screenshots.
3. Validate markdown links and image renders across `README.md`, `docs/ARCHITECTURE.md`, `docs/DEVELOPMENT_GUIDE.md`, and `docs/USER_GUIDE.md`.
