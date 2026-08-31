# Task 2: Hardware REST API Endpoints

## Description
Implement the REST Minimal API endpoints for hardware compatibility evaluation in `Endpoints/HardwareEndpoints.cs` and wire them into `Program.cs`.

## Files to Create/Update
1. `Endpoints/HardwareEndpoints.cs`
   - Implement `MapHardwareEndpoints(this IEndpointRouteBuilder app)`
   - Route `GET /api/hardware/fit`:
     - Query parameters: `modality` (string, optional, default "llm"), `params` (double, default 8), `quant` (string, optional, default "q4_k_m"), `context` (int, default 8192), `kv_prec` (string, optional, default "fp16"), `model_name` (string, optional), `size_bytes` (long?, optional).
     - Inspects current system telemetry via `ITelemetryService` (or uses provided query overrides if present).
     - Returns JSON representation of `LlmFitResult` (or `QuickFitBadge` / other modality results).
   - Route `POST /api/hardware/evaluate`:
     - Accepts JSON payload with `LlmFitRequest`, `DiffusionFitRequest`, or `VideoFitRequest`.
     - Returns evaluated fit result.
2. `Program.cs`
   - Register `builder.Services.AddSingleton<ICanIRunItService, CanIRunItService>();`
   - Call `app.MapHardwareEndpoints();` alongside other endpoint mappings.
3. `LocalLLMServerManager.Tests/HardwareEndpointsTests.cs`
   - Integration tests using `AppTestServerFixture` verifying:
     - `GET /api/hardware/fit?params=70&quant=q4_k_m&context=8192` returns 200 OK with expected JSON structure.
     - `POST /api/hardware/evaluate` returns 200 OK with valid fit calculation.
     - Invalid query parameters return sensible error responses or defaults without throwing 500.

## Constraints & TDD
- Write failing integration tests in `LocalLLMServerManager.Tests/HardwareEndpointsTests.cs`.
- Implement `Endpoints/HardwareEndpoints.cs` and wire in `Program.cs`.
- Run `dotnet test --filter "FullyQualifiedName~HardwareEndpointsTests"`.
- Run `npm run lint` and `npx tsc --noEmit`.
- Commit: `git commit -m "feat(api): add /api/hardware/fit and /api/hardware/evaluate endpoints"`
- Write report to `.superpowers/sdd/2026-08-30-can-i-run-it/task-2-report.md`.
