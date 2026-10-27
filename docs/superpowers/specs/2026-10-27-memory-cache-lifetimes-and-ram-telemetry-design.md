# Memory & Cache Lifetime Management and Host RAM Telemetry Design

**Date:** 2026-10-27  
**Related Issues:** [#97](https://github.com/spelech/LocalLLMServerManager/issues/97), [#98](https://github.com/spelech/LocalLLMServerManager/issues/98)

---

## 1. Problem Statement & Motivation

During extended operation of `LocalLLMServerManager`, backend AI engines (`Ollama`, `ComfyUI`, `SD Forge`, `Kokoro TTS`) retain multi-gigabyte model weights in memory-mapped files (`mmap`), shared IPC sections, and native CUDA/DirectML host-pinned buffers (`Non-Paged Pool`).

Live system diagnostics demonstrated:
- **25.00 GB / 31.72 GB (78.8%)** host RAM in use while `LocalLLMServerManager` reported only **~1.85 GB** in its private working set.
- Terminating `LocalLLMServerManager` immediately reclaimed **12.29 GB** of physical RAM and **~870 MB** of Windows Non-Paged Kernel Pool memory.
- The UI telemetry ribbon only monitored GPU VRAM via `nvidia-smi`, providing zero visibility into host system RAM, process working sets, or kernel driver pool consumption.
- `VramOrchestrator` only unloaded models reactively when switching between conflicting GPU workloads, lacking time-based idle cache eviction or host RAM pressure relief.

---

## 2. Goals & Non-Goals

### Goals
1. **Two-Tier Idle Cache Eviction ([#97](https://github.com/spelech/LocalLLMServerManager/issues/97)):**
   - **Tier 1 (Weight Unload — Default `15` minutes):** Automatically unload idle model weights from VRAM and host RAM (`keep_alive: 0` for Ollama, `POST /free` with `unload_models: true, free_memory: true` for ComfyUI) after `ModelCacheIdleTimeoutMinutes` of inactivity.
   - **Tier 2 (Engine Hibernation — Default `30` minutes):** Automatically stop dormant child engine processes (`ComfyUI`, `SD Forge`, `Kokoro TTS`) after `EngineHibernationTimeoutMinutes` of inactivity.
   - **Host RAM Pressure Relief (Default `75%` threshold):** When host physical RAM usage reaches or exceeds `RamPressureThresholdPercent` (`75%`), immediately trigger Tier 1 weight unloads and Tier 2 hibernation of idle background engines (`> 2` minutes since last activity).
2. **Host RAM, Process Working Set & Kernel Pool Telemetry ([#98](https://github.com/spelech/LocalLLMServerManager/issues/98)):**
   - Extend `IGpuTelemetryProvider` and `ITelemetryService` to query host physical RAM (`TotalRamBytes`, `UsedRamBytes`, `AvailableRamBytes`, `RamUsedPercent`), application + child engine working set (`ProcessWorkingSetBytes`), and Windows Non-Paged Kernel Pool (`NonPagedPoolBytes`).
   - Expose these metrics via `GET /api/gpu/vram` (backward-compatible) and `GET /api/hardware/memory`, plus a manual `POST /api/memory/purge` endpoint.
3. **Unified Hardware Pill & Settings UI:**
   - Upgrade the center pill in `TelemetryRibbonControl` (`34px` titlebar ribbon) into a compact **Unified Hardware Pill** displaying dual progress bars (`VRAM` and `RAM`), color-coded pressure states, a rich breakdown tooltip, and a one-click **Purge Memory Cache** button.
   - Add a **Memory & Cache Lifetimes** configuration section in `SettingsTabControl` bound to `AppSettings`.

### Non-Goals
- Modifying internal memory allocators inside third-party Python binaries (`ComfyUI`, `Forge`, `Kokoro`).
- Re-architecting UI bitmap converters ([#99](https://github.com/spelech/LocalLLMServerManager/issues/99)), which is deferred unless needed later.

---

## 3. Architecture & Components

### 3.1 Configuration (`AppSettings.cs` & `SettingsViewModel.cs`)
Extend `AppSettings` record with backward-compatible defaults:
```csharp
int ModelCacheIdleTimeoutMinutes = 15,
int EngineHibernationTimeoutMinutes = 30,
int RamPressureThresholdPercent = 75
```
- `0` disables automatic timeout for either tier (`Never`).
- `SettingsViewModel` exposes observable properties (`ModelCacheIdleTimeoutMinutes`, `EngineHibernationTimeoutMinutes`, `RamPressureThresholdPercent`) and preset options for the Settings UI.

### 3.2 Two-Tier Eviction & Activity Tracking (`VramOrchestrator.cs` & `MemoryLifecycleWatchdog.cs`)
1. **Activity Tracking in `VramOrchestrator`:**
   - Maintain a thread-safe `ConcurrentDictionary<string, DateTimeOffset>` mapping normalized engine keys (`"ollama"`, `"comfy"`, `"forge"`, `"audio"`) to their last active UTC timestamp, plus a `ConcurrentDictionary<string, bool>` tracking whether Tier 1 weight eviction has already been performed since the last activity.
   - Inject `TimeProvider` (defaulting to `TimeProvider.System`) so unit tests can deterministically advance time without `Task.Delay`.
   - Expose `RecordActivity(string engine)` and `GetEngineIdleStates()`.
   - Record activity automatically in:
     - Reverse-proxy and orchestration middleware in `Program.cs` (`/api/generate`, `/api/chat`, `/v1/chat`, `/sdapi/*`, `/v1/images/*`, `/comfyapi/*`, `/api/comfy/prompt`, `/v1/audio/*`).
     - MCP tool invocations in `LocalLlmMcpTools.cs`.
2. **Evaluation Method (`EvaluateCacheLifetimesAsync`):**
   - Queries current settings via `ISettingsService` and host memory telemetry via `IGpuTelemetryProvider`.
   - **RAM Pressure Check:** If `RamUsedPercent >= settings.RamPressureThresholdPercent` (default `75%`):
     - Immediately unloads Ollama models (`UnloadOllamaModelsAsync("Host RAM pressure threshold exceeded")`) and frees ComfyUI memory (`FreeComfyUiVramAsync`).
     - Stops any running managed child engine (`ComfyUI`, `Forge`, `Audio`) that has been idle for at least 2 minutes.
   - **Tier 1 Check (`ModelCacheIdleTimeoutMinutes > 0`, default `15m`):**
     - If `"ollama"` idle duration $\ge$ `ModelCacheIdleTimeoutMinutes`, call `UnloadOllamaModelsAsync("Idle cache TTL expired")` and mark Tier 1 evicted.
     - If `"comfy"` idle duration $\ge$ `ModelCacheIdleTimeoutMinutes`, call `FreeComfyUiVramAsync(settings.ComfyUiUrl)` and mark Tier 1 evicted.
   - **Tier 2 Check (`EngineHibernationTimeoutMinutes > 0`, default `30m`):**
     - For `"comfy"`, `"forge"`, and `"audio"`, if the process is running and idle duration $\ge$ `EngineHibernationTimeoutMinutes`, stop the engine via `IAiEngineManager` (`StopComfyUiAsync`, `StopForgeAsync`, `StopAudioEngineAsync`).
3. **Manual Purge (`PurgeAllCachesAsync` & `POST /api/memory/purge`):**
   - Immediately unloads all Ollama models, calls ComfyUI `/free`, and optionally stops idle engines if `stopEngines = true`, returning a JSON summary of reclaimed state.
4. **Background Hosted Service (`MemoryLifecycleWatchdog`):**
   - Implements `BackgroundService` (`IHostedService`) registered in `Program.CreateWebApplication`.
   - Runs a `PeriodicTimer(TimeSpan.FromSeconds(30))` invoking `VramOrchestrator.EvaluateCacheLifetimesAsync(stoppingToken)`.

### 3.3 Host RAM, Process Working Set & Kernel Pool Telemetry (`GpuTelemetryProvider.cs` & `TelemetryService.cs`)
1. **`SystemMemoryInfo` Model (`LocalLLMServerManager.Shared.Interfaces`):**
   ```csharp
   public record SystemMemoryInfo(
       long TotalRamBytes,
       long UsedRamBytes,
       long AvailableRamBytes,
       int RamUsedPercent,
       long ProcessWorkingSetBytes,
       long NonPagedPoolBytes);
   ```
2. **OS Collection in `GpuTelemetryProvider`:**
   - **Windows:**
     - P/Invoke `GlobalMemoryStatusEx` (`kernel32.dll`) for accurate physical `ullTotalPhys` and `ullAvailPhys`.
     - P/Invoke `GetPerformanceInfo` (`psapi.dll`) for `KernelNonpaged` pages $\times$ `PageSize`.
   - **Linux:**
     - Parse `/proc/meminfo` for `MemTotal`, `MemAvailable`, and `SUnreclaim` (kernel slab unreclaimable / non-paged equivalent).
   - **Fallback:**
     - `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` and `Environment.WorkingSet`.
   - **Process Working Set:**
     - Sum `Environment.WorkingSet` of the current process plus any active child processes tracked by `IAiEngineManager` (`ComfyProcess`, `ForgeProcess`, `AudioProcess`, `OllamaProcess`).
3. **API Contract (`GET /api/gpu/vram` & `GET /api/hardware/memory`):**
   - `GET /api/gpu/vram` preserves existing fields (`gpuName`, `vramBytes`, `usedVramBytes`, `vramGB`) and adds:
     - `totalRamBytes`, `usedRamBytes`, `availableRamBytes`, `totalRamGb`, `usedRamGb`, `ramUsedPercent`, `processWorkingSetMb`, `nonPagedPoolMb`, `modelCacheIdleTimeoutMinutes`, `engineHibernationTimeoutMinutes`, `ramPressureThresholdPercent`.
   - `GET /api/hardware/memory` returns the same rich memory & cache lifetime snapshot.
   - `POST /api/memory/purge` triggers `VramOrchestrator.PurgeAllCachesAsync` and returns updated memory telemetry.

### 3.4 UI: Unified Hardware Pill (`TelemetryRibbonControl.axaml` & `TelemetryViewModel.cs`)
1. **`TelemetryViewModel` Additions:**
   - Observable properties:
     - `RamUsedGb`, `RamTotalGb`, `RamPercentage`, `RamStatusText` (e.g., `"12.7 / 31.7 GB (40%)"`), `RamCompactText` (e.g., `"12.7/31.7G"`), `VramCompactText` (e.g., `"4.2/16.0G"`).
     - `ProcessWorkingSetMb`, `NonPagedPoolMb`.
     - `RamBarColor`:
       - `< 75%`: `#38BDF8` / `{StaticResource PrimaryBrush}`
       - `75% – 89%` (at or above pressure threshold): `#F59E0B` (Amber warning)
       - `>= 90%`: `#EF4444` (Red critical)
     - `HardwareTooltipText`: Multi-line summary showing GPU Name, VRAM (`Used / Total`), System RAM (`Used / Total`), App + Engine Working Set (`MB`), Kernel Non-Paged Pool (`MB`), and Cache Lifetime policy (`15m Unload / 30m Hibernate / 75% Pressure`).
   - `PurgeMemoryCommand` (`[RelayCommand]`): Calls `POST /api/memory/purge`, refreshes telemetry, and shows a toast notification.
2. **`TelemetryRibbonControl.axaml` Layout:**
   - In `Grid.Column="1"`, render a single `matte-pill` containing:
     - GPU icon + truncated GPU name (`MaxWidth="110"`).
     - **VRAM micro-gauge:** label `"VRAM"`, `36px` `ProgressBar`, and `VramCompactText`.
     - Subtle vertical divider (`1px` `GlassBorderBrush`).
     - **RAM micro-gauge:** label `"RAM"`, `36px` `ProgressBar` (bound to `RamBarColor`), and `RamCompactText`.
     - **Purge Memory button** (`ribbon-icon-btn` with broom/sparkle icon) bound to `PurgeMemoryCommand` right next to Refresh in `Grid.Column="2"`.

### 3.5 Settings UI (`SettingsTabControl.axaml`)
Add a **Memory & Cache Lifetimes** section card with three controls:
- **Model Weight Idle Unload TTL (minutes):** `NumericUpDown` / preset selector (default `15`).
- **Engine Process Hibernation TTL (minutes):** `NumericUpDown` / preset selector (default `30`).
- **System RAM Pressure Threshold (%):** `Slider` / `NumericUpDown` (`50%–95%`, default `75%`).

---

## 4. Error Handling & Edge Cases
- All native P/Invoke calls (`GlobalMemoryStatusEx`, `GetPerformanceInfo`) are guarded by `OperatingSystem.IsWindows()` and `try/catch` blocks with managed `GC.GetGCMemoryInfo()` fallbacks.
- Child process `WorkingSet64` access handles `InvalidOperationException` (process exited mid-query) without throwing.
- Setting `ModelCacheIdleTimeoutMinutes = 0` or `EngineHibernationTimeoutMinutes = 0` cleanly disables time-based eviction for that tier while still allowing manual purge.

---

## 5. Verification Strategy
1. **Unit & Integration Tests (`LocalLLMServerManager.Tests`):**
   - `VramOrchestratorCacheLifetimeTests`: Verify Tier 1 weight unload at 15 minutes, Tier 2 engine hibernation at 30 minutes, immediate eviction when `RamUsedPercent >= 75%`, and manual `POST /api/memory/purge`.
   - `GpuTelemetryProviderMemoryTests`: Verify `GetSystemMemoryInfo()` returns valid non-zero physical RAM, working set, and non-paged pool metrics on Windows/Linux.
   - `TelemetryViewModelTests`: Verify `RamPercentage`, `RamBarColor` transitions (`<75%`, `>=75%`, `>=90%`), `HardwareTooltipText`, and `PurgeMemoryCommand`.
2. **Linting & Typechecking:**
   - Run `npm run lint` and `npx tsc --noEmit`.
3. **Browser E2E & Visual Verification:**
   - Run Playwright tests across both Chromium and Firefox (`npx playwright test`), including `page.reload()`.
   - Capture and visually inspect screenshots of the updated titlebar Unified Hardware Pill and Settings Memory & Cache Lifetimes card.
