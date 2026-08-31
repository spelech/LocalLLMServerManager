# Task 1: Domain Models & Mathematical Sizing Engine

## Description
Implement the core mathematical sizing engine and data models for hardware compatibility and performance analysis in `LocalLLMServerManager.Shared`.

## Files to Create/Update
1. `LocalLLMServerManager.Shared/Models/HardwareFitModels.cs`
   - `FitVerdict` enum (`FullVram`, `PartialOffload`, `CpuOnly`, `OutOfMemory`)
   - `LlmFitRequest` (ParametersBillions, Quantization, ContextLength, KvPrecision, AvailableVramMb, AvailableRamMb)
   - `LlmFitResult` (ModelWeightMb, KvCacheMb, TotalVramMb, GpuLayers, CpuLayers, TotalLayers, FitVerdict, EstimatedTokPerSec, RecommendationMessage)
   - `DiffusionFitRequest`, `DiffusionFitResult`
   - `VideoFitRequest`, `VideoFitResult`
   - `AudioFitResult`, `ThreeDFitResult`
   - `QuickFitBadge` (BadgeText, BadgeColorHex, Tooltip, FitVerdict)
2. `LocalLLMServerManager.Shared/Services/ICanIRunItService.cs`
   - Define interface methods for LLM, Diffusion, Video, Audio, 3D, and QuickFit evaluation.
3. `LocalLLMServerManager.Shared/Services/CanIRunItService.cs`
   - Implement accurate quantization bit tables (`Q2_K`: 2.65, `Q3_K_M`: 3.50, `Q4_K_M`: 4.50, `Q5_K_M`: 5.50, `Q6_K`: 6.60, `Q8_0`: 8.50, `FP16`: 16.00).
   - Implement KV cache formula with layer and head dimensions.
   - Implement layer offloading logic (GPU vs CPU layer count).
   - Implement speed estimation based on GPU bandwidth and compute.
   - Implement QuickFitBadge helper for model search results.
4. `LocalLLMServerManager.Tests/CanIRunItServiceTests.cs`
   - Unit tests covering:
     - Llama 3.3 70B on 16GB VRAM (verifying partial offload).
     - Qwen 2.5 32B on 24GB VRAM (verifying full VRAM fit).
     - DeepSeek R1 671B on 16GB VRAM (verifying CPU/RAM or OOM based on RAM).
     - Flux.1 Dev on 16GB VRAM.
     - Wan 2.2 14B Video on 16GB VRAM.
     - Kokoro TTS and Whisper STT memory footprints.
     - Quick fit badges across various model names and sizes.

## Constraints & TDD
- Write failing unit tests in `LocalLLMServerManager.Tests/CanIRunItServiceTests.cs` first.
- Implement the models and service to make all tests pass.
- Run `dotnet test --filter "FullyQualifiedName~CanIRunItServiceTests"`.
- Run `npm run lint` and `npx tsc --noEmit`.
- Commit changes: `git commit -m "feat(calculator): implement domain models and mathematical sizing engine for Can I Run It"`
- Write full report to `C:\Users\Alias\repos\LocalLLMServerManager\.superpowers\sdd\2026-08-30-can-i-run-it\task-1-report.md`.
