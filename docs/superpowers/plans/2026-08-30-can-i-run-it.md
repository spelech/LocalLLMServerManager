# Can I Run It — Implementation Plan

> **Plan Document:** `docs/superpowers/plans/2026-08-30-can-i-run-it.md`  
> **Design Spec:** `docs/superpowers/specs/2026-08-30-can-i-run-it-design.md`  
> **Feature Branch:** `feature/canIrunIt`

---

## Global Constraints
- **Target Framework**: .NET 10 cross-platform (`net10.0`), Avalonia UI 12.1.1, WebAssembly browser runner.
- **Code Quality**: Clean test pass across all 312+ existing tests, 0 ESLint errors, 0 TypeScript compile errors.
- **Telemetry Grounding**: Strictly driven by the user's live physical GPU VRAM and System RAM telemetry from `ITelemetryService`.
- **Accuracy**: Mathematical models match llama.cpp / GGUF quantization tables, KV cache head dimensions, and Diffusion / DiT memory multipliers.

---

## Tasks

- [ ] **Task 1: Domain Models & Mathematical Sizing Engine**
  - **Brief**: Create `LocalLLMServerManager.Shared/Models/HardwareFitModels.cs` with `LlmFitRequest`, `LlmFitResult`, `DiffusionFitRequest`, `DiffusionFitResult`, `VideoFitRequest`, `VideoFitResult`, `AudioFitResult`, `ThreeDFitResult`, `QuickFitBadge`, and `FitVerdict` enum.
  - Implement `LocalLLMServerManager.Shared/Services/ICanIRunItService.cs` and `LocalLLMServerManager.Shared/Services/CanIRunItService.cs` supporting LLM weight + KV cache + CUDA overhead, layer offloading distribution, and diffusion/video/audio/3D sizing.
  - Write comprehensive unit tests in `LocalLLMServerManager.Tests/CanIRunItServiceTests.cs`.
  - **Tests**: `dotnet test --filter "FullyQualifiedName~CanIRunItServiceTests"`.

- [ ] **Task 2: Hardware REST API Endpoints**
  - **Brief**: Implement `Endpoints/HardwareEndpoints.cs` providing `GET /api/hardware/fit` and `POST /api/hardware/evaluate`.
  - Register `MapHardwareEndpoints(app)` in `Program.cs`.
  - Write integration tests in `LocalLLMServerManager.Tests/HardwareEndpointsTests.cs`.
  - **Tests**: `dotnet test --filter "FullyQualifiedName~HardwareEndpointsTests"`.

- [ ] **Task 3: CanIRunItViewModel & Reactive Calculations**
  - **Brief**: Create `LocalLLMServerManager.Shared/ViewModels/CanIRunItViewModel.cs` inheriting `ObservableObject`.
  - Wire live GPU / RAM properties from `ITelemetryService`.
  - Add modality selectors, preset catalog (DeepSeek R1, Llama 3.3, Qwen 2.5, Flux.1, Wan 2.2, Kokoro, etc.), context token slider (2K-128K), KV cache precision picker, and quantization options.
  - Write unit tests in `LocalLLMServerManager.Tests/CanIRunItViewModelTests.cs`.
  - **Tests**: `dotnet test --filter "FullyQualifiedName~CanIRunItViewModelTests"`.

- [ ] **Task 4: CanIRunItView.axaml Avalonia UI & Main Navigation Integration**
  - **Brief**: Create `LocalLLMServerManager.Shared/Views/CanIRunItView.axaml` and `.cs` styled with Matte Carbon theme (`#0F172A`).
  - Add Hardware Telemetry summary banner, stacked memory distribution bar, modality tabs, interactive sliders, and fit verdict card.
  - Register Tab #5 `[⚡ Can I Run It]` in `LocalLLMServerManager.Shared/Views/MainView.axaml` and `MainViewModel.cs`.
  - Write Avalonia headless interaction tests in `LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs`.
  - **Tests**: `dotnet test --filter "FullyQualifiedName~AvaloniaHeadlessInteractionTests"`.

- [ ] **Task 5: Ambient Compatibility Badges on Search & Library Cards**
  - **Brief**: Update `HuggingFaceSearchViewModel.cs`, `CivitaiSearchViewModel.cs`, and `OllamaLibraryViewModel.cs` to calculate `QuickFitBadge` for search/library items.
  - Update card XAML views (`HuggingFaceSearchView.axaml`, `CivitaiSearchView.axaml`, `OllamaLibraryView.axaml`) to display ambient compatibility pills.
  - Add click-to-inspect navigation command routing directly into `CanIRunItViewModel`.
  - Write unit tests verifying card badge computations.
  - **Tests**: `dotnet test --filter "FullyQualifiedName~SearchViewModel"`.

- [ ] **Task 6: Verification Suite, WASM Bundle Publish, Playwright E2E & PR**
  - **Brief**: Execute full verification suite (`npm run lint`, `npx tsc --noEmit`, and `dotnet test LocalLLMServerManager.sln`).
  - Publish WebAssembly bundle and sync to `wwwroot` and `C:\Program Files\LocalLLMServerManager\wwwroot`.
  - Update Playwright WASM browser tests in `LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs`.
  - Push branch and create Pull Request on GitHub.
