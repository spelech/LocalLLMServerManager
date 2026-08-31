# Task 3 Report: CanIRunItViewModel & Reactive Calculations

## 1. Overview
Implemented `CanIRunItViewModel` in `LocalLLMServerManager.Shared/ViewModels/CanIRunItViewModel.cs` using CommunityToolkit.Mvvm `ObservableObject` and `[ObservableProperty]`. The ViewModel provides reactive hardware telemetry integration, modality switching, preset catalogs across LLM/Image/Video/Audio/3D domains, memory breakdown percentage bar computations, fit verdict generation, and cross-tab `InspectModel` routing.

## 2. Changes Made

### 2.1 Domain Model Additions (`LocalLLMServerManager.Shared/Models/HardwareFitModels.cs`)
- Added `TelemetryInfo` record to encapsulate GPU VRAM and System RAM state snapshots (`GpuName`, `TotalVramMb`, `FreeVramMb`, `UsedVramMb`, `TotalRamMb`, `AvailableRamMb`).

### 2.2 CanIRunItViewModel (`LocalLLMServerManager.Shared/ViewModels/CanIRunItViewModel.cs`)
- **Hardware Telemetry**:
  - Properties: `GpuName`, `TotalVramMb`, `FreeVramMb`, `UsedVramMb`, `TotalRamMb`, `AvailableRamMb`, `VramText`, `RamText`.
  - Methods: `UpdateHardwareTelemetry(TelemetryInfo info)`, `UpdateHardwareTelemetry(GpuTelemetryInfo info)`, `UpdateHardwareTelemetry(double totalVramMb, double freeVramMb, double totalRamMb, double availableRamMb, string? gpuName = null)`, and `RefreshTelemetryAsync(...)`.
- **Modality State**:
  - `SelectedModality` (`LLM`, `Image`, `Video`, `Audio`, `ThreeD`).
  - RelayCommands: `SelectLlmModalityCommand`, `SelectImageModalityCommand`, `SelectVideoModalityCommand`, `SelectAudioModalityCommand`, `SelectThreeDModalityCommand`, `Select3DModalityCommand`, `SelectModalityCommand`.
- **Preset Catalogs & Interactive Sliders**:
  - **LLM**: `AvailableLlmPresets` (Llama 3.3 70B, Llama 3.1 8B, Qwen 2.5 32B/72B, DeepSeek R1 32B/70B/671B, Mistral Small 24B, Gemma 2 9B/27B, Phi-4 14B, Custom), `ParametersBillions`, `SelectedQuantization`, `ContextLength` (2K-128K), `KvCachePrecision`.
  - **Image**: `AvailableImagePresets` (Flux.1 Dev/Schnell, SDXL, SD 3.5 Large, SD 1.5, Custom), `SelectedImageResolution` (512, 768, 1024, 1536), `ImageBatchSize`, `SelectedImageQuantization`.
  - **Video**: `AvailableVideoPresets` (Wan 2.2 14B/1.3B, LTX-Video, HunyuanVideo), `VideoFrameCount` (49, 81, 97, 129), `SelectedVideoResolution` ("480p", "720p"), `SelectedVideoQuantization`.
  - **Audio & 3D**: `AvailableAudioPresets` (Kokoro TTS, Faster-Whisper Large-v3-Turbo, AllTalk XTTS-v2, MusicGen Melody), `AvailableThreeDPresets` (TRELLIS V2, Hunyuan3D-2).
- **Reactive Results & Visual Distribution Gauge**:
  - Automatically triggers `Recalculate()` upon any property change.
  - Generates `LlmResult`, `DiffusionResult`, `VideoResult`, `AudioResult`, `ThreeDResult`.
  - Computes stacked memory percentages: `ModelWeightsVramPercent`, `KvCacheVramPercent`, `OverheadVramPercent`, `FreeVramPercent`.
  - Sets `FitVerdictBadge`, `VerdictSummaryText`, `OffloadSummaryText`, `RecommendationText`, and `SpeedEstimationText`.
- **Cross-Tab Inspection**:
  - `InspectModel(string modelName, string modality, long? fileSizeBytes = null)`: Matches preset or extracts parameter/quantization specs, sets active modality, and triggers instant recalculation.

### 2.3 Unit Tests (`LocalLLMServerManager.Tests/CanIRunItViewModelTests.cs`)
- Implemented 17 unit tests verifying:
  - Default initialization and calculation accuracy.
  - Preset switching across LLM, Image, Video, Audio, and 3D.
  - Slider adjustments and custom quantization overrides.
  - Telemetry updates and dynamic memory ceiling recalculation.
  - `InspectModel` parsing and cross-tab navigation prefill.
  - Completeness of available preset catalogs.

## 3. Verification & Test Results
1. **TDD Failure Verification**: Ran `dotnet test LocalLLMServerManager.sln --filter "FullyQualifiedName~CanIRunItViewModelTests"` before ViewModel creation; confirmed compiler failure.
2. **ViewModel Test Pass**: Ran `dotnet test LocalLLMServerManager.sln --filter "FullyQualifiedName~CanIRunItViewModelTests"` after implementation; **17/17 passed (0 failed)**.
3. **Full Solution Test Pass**: Ran `dotnet test LocalLLMServerManager.sln` across entire project; **384 passed, 0 failed, 1 skipped**.
4. **Tooling Verification**:
   - `npm run lint` — passed with 0 errors.
   - `npx tsc --noEmit` — passed with 0 errors.

## 4. Commits Created
- `c83987b`: `feat(viewmodel): implement CanIRunItViewModel with reactive sizing calculations and presets`
