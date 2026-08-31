# Task 3: CanIRunItViewModel & Reactive Calculations

## Description
Implement `CanIRunItViewModel` in `LocalLLMServerManager.Shared/ViewModels/CanIRunItViewModel.cs` using CommunityToolkit.Mvvm `ObservableObject` and `[ObservableProperty]`.

## Requirements
1. **Telemetry & Hardware State**:
   - Accepts `ITelemetryService?`, `ICanIRunItService?`, and `HttpClient?` in constructor (with default fallbacks).
   - Reactive properties for live hardware:
     - `GpuName` (e.g., "NVIDIA GeForce RTX 4070 Ti SUPER")
     - `TotalVramMb` (double), `FreeVramMb` (double), `UsedVramMb` (double)
     - `TotalRamMb` (double), `AvailableRamMb` (double)
     - `VramText` (e.g., "16.0 GB VRAM"), `RamText` (e.g., "32.0 GB RAM")
   - `UpdateHardwareTelemetry(TelemetryInfo info)` method to refresh values when telemetry polls.
2. **Modality State & Selector**:
   - `SelectedModality` (enum/string: `LLM`, `Image`, `Video`, `Audio`, `ThreeD`, default `LLM`).
   - RelayCommands to switch modalities: `SelectLlmModalityCommand`, `SelectImageModalityCommand`, etc.
3. **Model Presets & Sliders**:
   - **LLM**:
     - Presets: Llama 3.3 70B, Llama 3.1 8B, Qwen 2.5 32B, Qwen 2.5 72B, DeepSeek R1 32B, DeepSeek R1 70B, DeepSeek R1 671B, Mistral Small 24B, Gemma 2 9B, Gemma 2 27B, Phi-4 14B, Custom.
     - `SelectedPreset` (string)
     - `ParametersBillions` (double, slider 0.5 to 405.0)
     - `SelectedQuantization` (string: `Q2_K`, `Q3_K_M`, `Q4_K_M`, `Q5_K_M`, `Q6_K`, `Q8_0`, `FP16`, default `Q4_K_M`)
     - `ContextLength` (int, slider 2048 to 131072, step 1024 or standard increments)
     - `KvCachePrecision` (string: `FP16`, `Q8_0`, `Q4_0`, default `FP16`)
   - **Image Generation**:
     - Presets: Flux.1 Dev, Flux.1 Schnell, SDXL, SD 3.5 Large, SD 1.5, Custom.
     - `SelectedImageResolution` (string/int: 512, 768, 1024, 1536, default 1024)
     - `ImageBatchSize` (int, default 1)
   - **Video Generation**:
     - Presets: Wan 2.2 14B, Wan 2.2 1.3B, LTX-Video, HunyuanVideo.
     - `VideoFrameCount` (int: 49, 81, 97, 129, default 81)
     - `SelectedVideoResolution` (string: "480p", "720p", default "720p")
   - **Audio & 3D**:
     - Presets: Kokoro TTS, Faster-Whisper Large-v3-Turbo, AllTalk XTTS-v2, MusicGen Melody, TRELLIS V2, Hunyuan3D-2.
4. **Calculated Results & Visual Gauge**:
   - `Recalculate()` method triggered automatically on any property change (`OnPropertyChanged` or partial property change handlers).
   - `LlmResult` (`LlmFitResult`), `DiffusionResult`, `VideoResult`, `AudioResult`, `ThreeDResult`.
   - Visual Percentage Bar:
     - `ModelWeightsVramPercent` (0..100)
     - `KvCacheVramPercent` (0..100)
     - `OverheadVramPercent` (0..100)
     - `FreeVramPercent` (0..100)
   - `FitVerdictBadge` (`QuickFitBadge`)
   - `VerdictSummaryText` (e.g. "🟢 Fits 100% in VRAM — Blazing Fast GPU Acceleration (~42 tok/s)")
   - `OffloadSummaryText` (e.g. "36 / 36 Layers in VRAM (100% GPU)")
   - `RecommendationText` (e.g. "Optimal speed: Use Q4_K_M for full VRAM residency")
5. **Inspect Model Navigation Method**:
   - `InspectModel(string modelName, string modality, long? fileSizeBytes = null)`: Pre-selects matching preset or configures parameters to immediately evaluate the inspected model.
6. **Unit Tests in `LocalLLMServerManager.Tests/CanIRunItViewModelTests.cs`**:
   - Verify initial state and default calculation.
   - Verify preset switching (e.g., selecting "Llama 3.3 70B" updates parameters and triggers recalculation).
   - Verify slider adjustments (context length, quantization) update VRAM percentages and fit verdict.
   - Verify `InspectModel` correctly populates and recalculates.
   - Verify telemetry update refreshes available VRAM and re-evaluates fit.

## Constraints & TDD
- Write failing unit tests in `LocalLLMServerManager.Tests/CanIRunItViewModelTests.cs`.
- Implement `CanIRunItViewModel.cs`.
- Run `dotnet test --filter "FullyQualifiedName~CanIRunItViewModelTests"`.
- Run `npm run lint` and `npx tsc --noEmit`.
- Commit: `git commit -m "feat(viewmodel): implement CanIRunItViewModel with reactive sizing calculations and presets"`
- Write report to `.superpowers/sdd/2026-08-30-can-i-run-it/task-3-report.md`.
