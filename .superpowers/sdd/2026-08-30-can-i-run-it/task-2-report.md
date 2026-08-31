# Task 2 Report: Hardware REST API Endpoints

## 1. Overview
Successfully implemented the REST Minimal API endpoints for hardware compatibility and performance analysis in `Endpoints/HardwareEndpoints.cs` and wired them into `Program.cs`.

## 2. Changes Made

### 2.1 Hardware Endpoints (`Endpoints/HardwareEndpoints.cs`)
- Implemented `MapHardwareEndpoints(this WebApplication app)`.
- **`GET /api/hardware/fit`**:
  - Parses query string parameters: `modality` (default `"llm"`), `params` (default `8.0`), `quant` (default `"Q4_K_M"`), `context` (default `8192`), `kv_prec` (default `"FP16"`), `model_name`, `size_bytes`, `vram_mb`, `ram_mb`.
  - Integrates with `IGpuTelemetryProvider` and `GC.GetGCMemoryInfo()` for live system VRAM and RAM telemetry with safe defaults.
  - Dynamically evaluates and returns `LlmFitResult`, `DiffusionFitResult`, `VideoFitResult`, `AudioFitResult`, `ThreeDFitResult`, or `QuickFitBadge` depending on modality.
- **`POST /api/hardware/evaluate`**:
  - Validates JSON content type and parses request payloads safely.
  - Evaluates polymorphic requests for `LlmFitRequest`, `DiffusionFitRequest`, `VideoFitRequest`, Audio, 3D, and quick fit badges.
  - Returns `400 Bad Request` on malformed JSON or invalid content types without throwing unhandled `500` server errors.

### 2.2 Application Wiring (`Program.cs`)
- Registered `builder.Services.AddSingleton<ICanIRunItService, CanIRunItService>();` in the DI container.
- Registered endpoint routing via `app.MapHardwareEndpoints();`.

### 2.3 Integration Tests (`LocalLLMServerManager.Tests/HardwareEndpointsTests.cs`)
- Created 16 comprehensive integration tests using `AppTestServerFixture`:
  - `GetHardwareFit_DefaultLlm_Returns200AndLlmFitResult`
  - `GetHardwareFit_ExplicitVramAndRam_FullVramFit`
  - `GetHardwareFit_DiffusionModality_ReturnsDiffusionFitResult`
  - `GetHardwareFit_VideoModality_ReturnsVideoFitResult`
  - `GetHardwareFit_AudioModality_ReturnsAudioFitResult`
  - `GetHardwareFit_ThreeDModality_ReturnsThreeDFitResult`
  - `GetHardwareFit_BadgeModality_ReturnsQuickFitBadge`
  - `GetHardwareFit_InvalidQueryParam_DoesNotThrow500`
  - `PostHardwareEvaluate_LlmFitRequest_Returns200AndLlmFitResult`
  - `PostHardwareEvaluate_DiffusionFitRequest_Returns200AndDiffusionFitResult`
  - `PostHardwareEvaluate_VideoFitRequest_Returns200AndVideoFitResult`
  - `PostHardwareEvaluate_AudioRequest_Returns200AndAudioFitResult`
  - `PostHardwareEvaluate_ThreeDRequest_Returns200AndThreeDFitResult`
  - `PostHardwareEvaluate_BadgeRequest_Returns200AndQuickFitBadge`
  - `PostHardwareEvaluate_InvalidJson_Returns400BadRequest`
  - `PostHardwareEvaluate_InvalidContentType_Returns400BadRequest`

## 3. Verification & Test Results
1. **TDD Failure Verification**: Ran `dotnet test LocalLLMServerManager.sln --filter "FullyQualifiedName~HardwareEndpointsTests"` before implementation; verified all 16 tests failed with 404 (NotFound).
2. **Endpoint Test Pass**: Ran `dotnet test LocalLLMServerManager.sln --filter "FullyQualifiedName~HardwareEndpointsTests"` after implementation; **16/16 passed (0 failed)**.
3. **Full Test Suite Pass**: Ran `dotnet test LocalLLMServerManager.sln` across entire project; **367 passed, 0 failed, 1 skipped**.
4. **Tooling & Typecheck**:
   - `npm run lint` — passed with 0 errors.
   - `npx tsc --noEmit` — passed with 0 errors.

## 4. Commits Created
- `280628d`: `feat(api): add /api/hardware/fit and /api/hardware/evaluate endpoints`
