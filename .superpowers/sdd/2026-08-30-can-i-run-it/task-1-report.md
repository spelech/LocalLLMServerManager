# Task 1 Report: Domain Models & Mathematical Sizing Engine

**Status:** DONE  
**Date:** 2026-08-30  
**Feature Branch:** `feature/canIrunIt`  
**Commit:** `9cada83` — `feat(calculator): implement domain models and mathematical sizing engine for Can I Run It`

---

## 1. Summary of Changes

Implemented the complete mathematical sizing and hardware compatibility evaluation engine for LocalLLMServerManager according to the Can I Run It specification.

### 1.1 Domain Models (`LocalLLMServerManager.Shared/Models/HardwareFitModels.cs`)
- `FitVerdict` enum: `FullVram`, `PartialOffload`, `CpuOnly`, `OutOfMemory`.
- `LlmFitRequest`: Encapsulates parameter counts (billions), quantization string (`Q4_K_M`, `FP16`, `Q8_0`, etc.), context token length, KV cache precision, available GPU VRAM (MB), available System RAM (MB), and optional architectural overrides (layer count, KV head count, head dimension).
- `LlmFitResult`: Detailed breakdown including `ModelWeightMb`, `KvCacheMb`, `OverheadMb`, `TotalVramMb`, `TotalRamMb`, `GpuLayers`, `CpuLayers`, `TotalLayers`, `FitVerdict`, `EstimatedTokPerSec`, and actionable `RecommendationMessage`.
- `DiffusionFitRequest` & `DiffusionFitResult`: Sizing for image generation models (Flux.1, SDXL, SD 3.5, SD 1.5) with base weights, text encoders (CLIP/T5), VAE buffers, and resolution-scaled latent memory.
- `VideoFitRequest` & `VideoFitResult`: Sizing for video diffusion models (Wan 2.2 14B/1.3B, LTX-Video, HunyuanVideo) scaling with frame count and resolution.
- `AudioFitResult` & `ThreeDFitResult`: Footprint models for TTS/STT engines (Kokoro, Whisper, XTTS-v2, MusicGen) and 3D generation models (TRELLIS V2, Hunyuan3D-2).
- `QuickFitBadge`: Ambient visual pill metadata (`BadgeText`, `BadgeColorHex`, `Tooltip`, `FitVerdict`) for search and library cards.

### 1.2 Service Interface (`LocalLLMServerManager.Shared/Services/ICanIRunItService.cs`)
- `LlmFitResult EvaluateLlmFit(LlmFitRequest request)`
- `DiffusionFitResult EvaluateDiffusionFit(DiffusionFitRequest request)`
- `VideoFitResult EvaluateVideoFit(VideoFitRequest request)`
- `AudioFitResult EvaluateAudioFit(string engineName, long vramMb, long ramMb)`
- `ThreeDFitResult Evaluate3DFit(string modelName, long vramMb, long ramMb)`
- `QuickFitBadge EvaluateQuickFit(string modelName, long? fileSizeBytes, string modality, long vramMb, long ramMb)`

### 1.3 Mathematical Sizing Engine (`LocalLLMServerManager.Shared/Services/CanIRunItService.cs`)
- **Quantization Table**: Precise bits-per-weight mapping (`Q2_K`: 2.65, `Q3_K_M`: 3.50, `Q4_K_M`: 4.50, `Q5_K_M`: 5.50, `Q6_K`: 6.60, `Q8_0`: 8.50, `FP8`: 8.00, `FP16`/`BF16`: 16.00, `FP32`: 32.00).
- **KV Cache Calculation**: Exact byte allocation formula $2 \times N_{layers} \times N_{kv\_heads} \times D_{head} \times C_{context} \times (B_{kv} / 8)$ supporting FP16, Q8_0, and Q4_0 precision modes.
- **Layer Offload Allocation**: Evaluates remaining GPU VRAM after KV cache and CUDA runtime overhead (600MB) to dynamically compute optimal GPU vs. CPU layer count and CPU RAM footprint.
- **Speed & RT Estimations**: Modeled based on GPU memory bandwidth (~800 GB/s) vs. System RAM bandwidth (~40 GB/s) and PCIe transfer penalties under partial offload.
- **QuickFit Evaluator**: Ambient evaluator parsing model tags, sizes, and quantizations for instant compatibility badging.

---

## 2. Test Verification & Results

- **TDD Workflow Followed**:
  1. Wrote unit tests in `LocalLLMServerManager.Tests/CanIRunItServiceTests.cs`.
  2. Executed `dotnet test --filter "FullyQualifiedName~CanIRunItServiceTests"` and verified compilation/test failures.
  3. Implemented domain models and service engine.
  4. Executed `dotnet test --filter "FullyQualifiedName~CanIRunItServiceTests"` — **40/40 passed (0 failed)**.
- **Full Solution Verification**:
  - `dotnet test LocalLLMServerManager.sln`: **351 passed, 0 failed, 1 skipped** (Duration: 1m 4s).
  - `npm run lint`: **0 errors, clean pass**.
  - `npx tsc --noEmit`: **0 errors, clean pass**.

---

## 3. Commits

- `9cada83` — `feat(calculator): implement domain models and mathematical sizing engine for Can I Run It`
