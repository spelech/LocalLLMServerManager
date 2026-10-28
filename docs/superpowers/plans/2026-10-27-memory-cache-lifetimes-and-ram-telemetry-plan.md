# Memory & Cache Lifetime Management and Host RAM Telemetry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement two-tier idle cache eviction (`15m` model weight unload, `30m` child engine process hibernation, `75%` host RAM pressure relief) and expose real-time Host RAM, Process Working Set, and Windows Non-Paged Kernel Pool telemetry in a Unified Hardware Pill in the `34px` titlebar ribbon.

**Architecture:** Extend `AppSettings` and `SettingsViewModel` with cache lifetime parameters (`ModelCacheIdleTimeoutMinutes = 15`, `EngineHibernationTimeoutMinutes = 30`, `RamPressureThresholdPercent = 75`). Add OS-level physical RAM, working set, and non-paged pool telemetry to `IGpuTelemetryProvider` and `ITelemetryService`. Upgrade `VramOrchestrator` with activity timestamps, two-tier eviction, and a `MemoryLifecycleWatchdog` background `IHostedService`. Update `TelemetryRibbonControl` with a compact dual-bar Unified Hardware Pill (`VRAM` + `RAM`) and `SettingsTabControl` with a Memory & Cache Lifetimes card.

**Tech Stack:** .NET 10, C# 13, ASP.NET Core Minimal APIs, Avalonia UI 11, CommunityToolkit.Mvvm, xUnit, Playwright.

## Global Constraints

- Default Tier 1 Model Weight Idle Unload TTL: `ModelCacheIdleTimeoutMinutes = 15` (`0` = disabled).
- Default Tier 2 Child Engine Hibernation TTL: `EngineHibernationTimeoutMinutes = 30` (`0` = disabled).
- Default Host RAM Pressure Threshold: `RamPressureThresholdPercent = 75`.
- Preserve all backward-compatible JSON properties on `GET /api/gpu/vram` and `AppSettings`.
- Run `npm run lint` and `npx tsc --noEmit` after code changes.
- Verify UI changes with Playwright across both Chromium and Firefox (`npx playwright test`), including `page.reload()`, and visually inspect screenshots.

---

### Task 1: AppSettings & Host System Memory Telemetry (`IGpuTelemetryProvider` & `GpuTelemetryProvider`)

**Files:**
- Modify: `LocalLLMServerManager.Shared/Models/AppSettings.cs`
- Modify: `Services/IGpuTelemetryProvider.cs`
- Modify: `Services/GpuTelemetryProvider.cs`
- Test: `LocalLLMServerManager.Tests/HardwareEndpointsTests.cs`

**Interfaces:**
- Consumes: `IAiEngineManager` (optional process handles)
- Produces:
  - `AppSettings.ModelCacheIdleTimeoutMinutes` (`int`, default `15`)
  - `AppSettings.EngineHibernationTimeoutMinutes` (`int`, default `30`)
  - `AppSettings.RamPressureThresholdPercent` (`int`, default `75`)
  - `SystemMemorySnapshot` record (`TotalRamBytes`, `UsedRamBytes`, `AvailableRamBytes`, `RamUsedPercent`, `ProcessWorkingSetBytes`, `NonPagedPoolBytes`)
  - `IGpuTelemetryProvider.GetSystemMemoryInfo()`

- [ ] **Step 1: Write the failing test in `LocalLLMServerManager.Tests/HardwareEndpointsTests.cs`**

Add the following test methods to `LocalLLMServerManager.Tests/HardwareEndpointsTests.cs`:

```csharp
[Fact]
public void AppSettings_DefaultMemoryCacheSettings_AreConfigured()
{
    var settings = new AppSettings();
    Assert.Equal(15, settings.ModelCacheIdleTimeoutMinutes);
    Assert.Equal(30, settings.EngineHibernationTimeoutMinutes);
    Assert.Equal(75, settings.RamPressureThresholdPercent);
}

[Fact]
public void GpuTelemetryProvider_GetSystemMemoryInfo_ReturnsValidHostMetrics()
{
    var provider = new LocalLLMServerManager.Services.GpuTelemetryProvider();
    var snapshot = provider.GetSystemMemoryInfo();

    Assert.True(snapshot.TotalRamBytes > 0);
    Assert.True(snapshot.UsedRamBytes > 0);
    Assert.True(snapshot.AvailableRamBytes >= 0);
    Assert.InRange(snapshot.RamUsedPercent, 1, 100);
    Assert.True(snapshot.ProcessWorkingSetBytes > 0);
    Assert.True(snapshot.NonPagedPoolBytes >= 0);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~GetSystemMemoryInfo_ReturnsValidHostMetrics|FullyQualifiedName~AppSettings_DefaultMemoryCacheSettings_AreConfigured"`
Expected: FAIL with compilation error (`ModelCacheIdleTimeoutMinutes` and `GetSystemMemoryInfo` not defined).

- [ ] **Step 3: Implement `AppSettings`, `SystemMemorySnapshot`, and `GpuTelemetryProvider.GetSystemMemoryInfo()`**

Update `LocalLLMServerManager.Shared/Models/AppSettings.cs`:

```csharp
namespace LocalLLMServerManager;

using System.Collections.Generic;
using LocalLLMServerManager.Shared.Models;

/// <summary>
/// Application settings persisted to settings.json next to the executable.
/// </summary>
public record AppSettings(
    string ForgeModelsPath = "",
    string ComfyUiUrl = "http://127.0.0.1:8188",
    string ThreeDModelsPath = "",
    string WorkflowsPath = "",
    string PreferredImageEngine = "comfy",
    string ComfyUiExecutablePath = "",
    string ForgeExecutablePath = "",
    string OllamaExecutablePath = "ollama",
    string ServiceName = "LocalLLMServerManager",
    string PublishOutputPath = "C:\\LocalLLMServerManager",
    string ComfyModelsPath = "",
    string AudioPath = "",
    string LanAccessUrl = "http://127.0.0.1:5246",
    string SelectedThemeStyle = "semi",
    string AudioEngineExecutablePath = "",
    string AudioEngineUrl = "http://127.0.0.1:8880",
    string PreferredAudioVoice = "af_heart",
    string VideoModelsPath = "",
    string VideoOutputPath = "",
    List<StudioPreset>? CustomPresets = null,
    bool AiAssistantEnabled = false,
    string AiAssistantEndpoint = "http://127.0.0.1:4000/v1",
    string AiAssistantApiKey = "",
    string AiAssistantModel = "google/gemini-2.5-flash",
    string AiAssistantCustomSystemPrompt = "",
    string AiAssistantPromptsDirectory = "",
    int ModelCacheIdleTimeoutMinutes = 15,
    int EngineHibernationTimeoutMinutes = 30,
    int RamPressureThresholdPercent = 75
);
```

Update `Services/IGpuTelemetryProvider.cs` to add `SystemMemorySnapshot` and `GetSystemMemoryInfo()`:

```csharp
using System;
using System.Threading.Tasks;

namespace LocalLLMServerManager.Services;

public record GpuTelemetryResult(
    string GpuName,
    long TotalVramMb,
    long UsedVramMb,
    long FreeVramMb,
    double UtilizationPercent
);

public record SystemMemorySnapshot(
    long TotalRamBytes,
    long UsedRamBytes,
    long AvailableRamBytes,
    int RamUsedPercent,
    long ProcessWorkingSetBytes,
    long NonPagedPoolBytes
);

public interface IGpuTelemetryProvider
{
    (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfo();
    (string GpuName, long TotalVramBytes, long UsedVramBytes)? GetLinuxMemoryInfo();
    (string GpuName, long TotalVramBytes, long UsedVramBytes)? ParseNvidiaSmiOutput(string output);
    (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfoFromRegistry();

    SystemMemorySnapshot GetSystemMemoryInfo()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        long totalBytes = gcInfo.TotalAvailableMemoryBytes > 0 ? gcInfo.TotalAvailableMemoryBytes : 32L * 1024 * 1024 * 1024;
        long usedBytes = gcInfo.MemoryLoadBytes > 0 ? gcInfo.MemoryLoadBytes : totalBytes / 2;
        long availBytes = Math.Max(0, totalBytes - usedBytes);
        int pct = totalBytes > 0 ? (int)Math.Clamp(Math.Round((double)usedBytes / totalBytes * 100.0), 0, 100) : 0;
        return new SystemMemorySnapshot(totalBytes, usedBytes, availBytes, pct, Environment.WorkingSet, 0);
    }

    Task<GpuTelemetryResult> GetTelemetryAsync()
    {
        var (gpuName, totalBytes, usedBytes) = GetGpuInfo();
        var totalMb = totalBytes / (1024 * 1024);
        var usedMb = usedBytes / (1024 * 1024);
        var freeMb = Math.Max(0, (totalBytes - usedBytes) / (1024 * 1024));
        var utilPercent = totalBytes > 0 ? Math.Round((double)usedBytes / totalBytes * 100.0, 1) : 0.0;
        return Task.FromResult(new GpuTelemetryResult(gpuName, totalMb, usedMb, freeMb, utilPercent));
    }
}
```

Update `Services/GpuTelemetryProvider.cs` to implement `GetSystemMemoryInfo()` using `GlobalMemoryStatusEx` and `GetPerformanceInfo` on Windows, `/proc/meminfo` on Linux, and `GC.GetGCMemoryInfo()` fallback, plus summing active child process working sets from `IAiEngineManager`:

```csharp
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LocalLLMServerManager.Services;

public class GpuTelemetryProvider : IGpuTelemetryProvider
{
    private readonly IAiEngineManager? _engineManager;

    public GpuTelemetryProvider(IAiEngineManager? engineManager = null)
    {
        _engineManager = engineManager;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PERFORMANCE_INFORMATION
    {
        public uint cb;
        public nuint CommitTotal;
        public nuint CommitLimit;
        public nuint CommitPeak;
        public nuint PhysicalTotal;
        public nuint PhysicalAvailable;
        public nuint SystemCache;
        public nuint KernelTotal;
        public nuint KernelPaged;
        public nuint KernelNonpaged;
        public nuint PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

    public SystemMemorySnapshot GetSystemMemoryInfo()
    {
        long totalBytes = 0;
        long availBytes = 0;
        long nonPagedPoolBytes = 0;

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref memStatus) && memStatus.ullTotalPhys > 0)
                {
                    totalBytes = (long)memStatus.ullTotalPhys;
                    availBytes = (long)memStatus.ullAvailPhys;
                }
            }
            catch { }

            try
            {
                if (GetPerformanceInfo(out var perfInfo, (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>()))
                {
                    nonPagedPoolBytes = (long)((ulong)perfInfo.KernelNonpaged * (ulong)perfInfo.PageSize);
                }
            }
            catch { }
        }
        else if (OperatingSystem.IsLinux())
        {
            try
            {
                if (File.Exists("/proc/meminfo"))
                {
                    long totalKb = 0;
                    long availKb = 0;
                    long sunreclaimKb = 0;
                    foreach (var line in File.ReadLines("/proc/meminfo"))
                    {
                        if (line.StartsWith("MemTotal:"))
                        {
                            var val = line.Split(':', StringSplitOptions.TrimEntries)[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                            long.TryParse(val, out totalKb);
                        }
                        else if (line.StartsWith("MemAvailable:"))
                        {
                            var val = line.Split(':', StringSplitOptions.TrimEntries)[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                            long.TryParse(val, out availKb);
                        }
                        else if (line.StartsWith("SUnreclaim:"))
                        {
                            var val = line.Split(':', StringSplitOptions.TrimEntries)[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                            long.TryParse(val, out sunreclaimKb);
                        }
                    }
                    if (totalKb > 0)
                    {
                        totalBytes = totalKb * 1024;
                        availBytes = availKb * 1024;
                        nonPagedPoolBytes = sunreclaimKb * 1024;
                    }
                }
            }
            catch { }
        }

        if (totalBytes <= 0)
        {
            var gcInfo = GC.GetGCMemoryInfo();
            totalBytes = gcInfo.TotalAvailableMemoryBytes > 0 ? gcInfo.TotalAvailableMemoryBytes : 32L * 1024 * 1024 * 1024;
            long gcUsed = gcInfo.MemoryLoadBytes > 0 ? gcInfo.MemoryLoadBytes : totalBytes / 2;
            availBytes = Math.Max(0, totalBytes - gcUsed);
        }

        long usedBytes = Math.Max(0, totalBytes - availBytes);
        int ramUsedPct = totalBytes > 0
            ? (int)Math.Clamp(Math.Round((double)usedBytes / totalBytes * 100.0), 0, 100)
            : 0;

        long workingSetBytes = Environment.WorkingSet;
        if (_engineManager != null)
        {
            workingSetBytes += GetSafeProcessWorkingSet(_engineManager.ComfyProcess);
            workingSetBytes += GetSafeProcessWorkingSet(_engineManager.ForgeProcess);
            workingSetBytes += GetSafeProcessWorkingSet(_engineManager.AudioProcess);
            workingSetBytes += GetSafeProcessWorkingSet(_engineManager.OllamaProcess);
        }

        return new SystemMemorySnapshot(
            TotalRamBytes: totalBytes,
            UsedRamBytes: usedBytes,
            AvailableRamBytes: availBytes,
            RamUsedPercent: ramUsedPct,
            ProcessWorkingSetBytes: workingSetBytes,
            NonPagedPoolBytes: nonPagedPoolBytes
        );
    }

    private static long GetSafeProcessWorkingSet(Process? proc)
    {
        if (proc == null) return 0;
        try
        {
            if (proc.HasExited) return 0;
            proc.Refresh();
            return proc.WorkingSet64;
        }
        catch
        {
            return 0;
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~GetSystemMemoryInfo_ReturnsValidHostMetrics|FullyQualifiedName~AppSettings_DefaultMemoryCacheSettings_AreConfigured"`
Expected: PASS

---

### Task 2: Two-Tier Idle Cache Eviction, RAM Pressure Relief & `MemoryLifecycleWatchdog` (`VramOrchestrator`)

**Files:**
- Modify: `Services/VramOrchestrator.cs`
- Create: `Services/MemoryLifecycleWatchdog.cs`
- Modify: `Endpoints/EngineEndpoints.cs`
- Modify: `Program.cs`
- Test: `LocalLLMServerManager.Tests/ServicesAndEngineManagerTests.cs`

**Interfaces:**
- Consumes: `ISettingsService`, `IGpuTelemetryProvider`, `IAiEngineManager`, `TimeProvider`
- Produces:
  - `VramOrchestrator.RecordActivity(string engine)`
  - `VramOrchestrator.GetLastActivityUtc(string engine)`
  - `VramOrchestrator.EvaluateCacheLifetimesAsync(CancellationToken)`
  - `VramOrchestrator.PurgeAllCachesAsync(bool stopIdleEngines = false, CancellationToken cancellationToken = default)`
  - `MemoryLifecycleWatchdog` (`BackgroundService`)
  - `POST /api/memory/purge` and `GET /api/hardware/memory` endpoints

- [ ] **Step 1: Write the failing tests for Tier 1, Tier 2, RAM Pressure, and Purge in `LocalLLMServerManager.Tests/ServicesAndEngineManagerTests.cs`**

Add tests verifying:
1. `RecordActivity` updates last activity timestamp for normalized engine names (`"ollama"`, `"comfy"`, `"forge"`, `"audio"`).
2. `EvaluateCacheLifetimesAsync` triggers Tier 1 model weight unload (`keep_alive = 0` to Ollama and `/free` to ComfyUI) after `15` minutes of inactivity.
3. `EvaluateCacheLifetimesAsync` triggers Tier 2 engine process hibernation (`StopComfyUiAsync`, `StopForgeAsync`, `StopAudioEngineAsync`) after `30` minutes of inactivity.
4. `EvaluateCacheLifetimesAsync` triggers immediate Tier 1 + Tier 2 eviction when `RamUsedPercent >= 75%` (`RamPressureThresholdPercent`).
5. `POST /api/memory/purge` triggers `PurgeAllCachesAsync` and returns `200 OK` with memory telemetry.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~VramOrchestrator_EvaluateCacheLifetimes"`
Expected: FAIL with compilation error.

- [ ] **Step 3: Implement `VramOrchestrator` cache lifetime methods, `MemoryLifecycleWatchdog`, and API endpoints**

Update `Services/VramOrchestrator.cs`:
- Add optional constructor dependencies (`ISettingsService? settingsService = null`, `IGpuTelemetryProvider? telemetryProvider = null`, `IAiEngineManager? engineManager = null`, `TimeProvider? timeProvider = null`) while preserving the existing 2-parameter constructor `VramOrchestrator(HttpClient httpClient, ILogger<VramOrchestrator> logger)` for backward compatibility with existing tests.
- Track `_lastActivityUtc` (`ConcurrentDictionary<string, DateTimeOffset>`) and `_tier1Evicted` (`ConcurrentDictionary<string, bool>`).
- Implement `RecordActivity(string engine)`, `GetLastActivityUtc(string engine)`, `EvaluateCacheLifetimesAsync(CancellationToken)`, and `PurgeAllCachesAsync(bool stopEngines = false, CancellationToken cancellationToken = default)`.

Create `Services/MemoryLifecycleWatchdog.cs`:
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LocalLLMServerManager.Services;

public sealed class MemoryLifecycleWatchdog : BackgroundService
{
    private readonly VramOrchestrator _orchestrator;
    private readonly ILogger<MemoryLifecycleWatchdog> _logger;
    private readonly TimeSpan _checkInterval;

    public MemoryLifecycleWatchdog(
        VramOrchestrator orchestrator,
        ILogger<MemoryLifecycleWatchdog> logger,
        TimeSpan? checkInterval = null)
    {
        _orchestrator = orchestrator;
        _logger = logger;
        _checkInterval = checkInterval ?? TimeSpan.FromSeconds(30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_checkInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _orchestrator.EvaluateCacheLifetimesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in MemoryLifecycleWatchdog loop.");
        }
    }
}
```

Update `Endpoints/EngineEndpoints.cs`:
- Extend `GET /api/gpu/vram` to include `totalRamBytes`, `usedRamBytes`, `availableRamBytes`, `totalRamGb`, `usedRamGb`, `ramUsedPercent`, `processWorkingSetMb`, `nonPagedPoolMb`, `modelCacheIdleTimeoutMinutes`, `engineHibernationTimeoutMinutes`, `ramPressureThresholdPercent`.
- Add `GET /api/hardware/memory` returning the full GPU + System RAM + Cache Lifetime status.
- Add `POST /api/memory/purge` invoking `orchestrator.PurgeAllCachesAsync(stopEngines: true)` and returning updated memory metrics.

Update `Program.cs`:
- Register `builder.Services.AddHostedService<MemoryLifecycleWatchdog>();`.
- In the orchestration middleware (`app.Use`), stamp `orchestrator.RecordActivity(...)` when requests hit Ollama (`/api/generate`, `/api/chat`, `/v1/chat`), Forge (`/sdapi`, `/v1/images`), ComfyUI (`/comfyapi`, `/api/comfy/prompt`), or Audio (`/v1/audio`).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~VramOrchestrator|FullyQualifiedName~EngineEndpoints"`
Expected: PASS

---

### Task 3: Shared Telemetry Service, Unified Hardware Pill UI & Settings Cache Lifetime Controls

**Files:**
- Modify: `LocalLLMServerManager.Shared/Interfaces/ITelemetryService.cs`
- Modify: `LocalLLMServerManager.Shared/Services/TelemetryService.cs`
- Modify: `LocalLLMServerManager.Shared/Models/EngineStatusCardModel.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/TelemetryViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/TelemetryRibbonControl.axaml`
- Modify: `LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/SettingsViewModelTests.cs`
- Test: `LocalLLMServerManager.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `GET /api/gpu/vram`, `POST /api/memory/purge`, `GET /api/settings`, `POST /api/settings`
- Produces:
  - `GpuTelemetryInfo` optional/defaulted properties: `UsedRamGb`, `TotalRamGb`, `RamPercent`, `ProcessWorkingSetMb`, `NonPagedPoolMb`
  - `TelemetryViewModel`: `RamUsedGb`, `RamTotalGb`, `RamPercentage`, `RamStatusText`, `RamCompactText`, `VramCompactText`, `ProcessWorkingSetMb`, `NonPagedPoolMb`, `RamBarColor`, `HardwareTooltipText`, `PurgeMemoryCommand`
  - `SettingsViewModel`: `ModelCacheIdleTimeoutMinutes`, `EngineHibernationTimeoutMinutes`, `RamPressureThresholdPercent`

- [ ] **Step 1: Write failing unit tests for `TelemetryViewModel` RAM properties & `SettingsViewModel` cache lifetime persistence**

Add unit tests verifying:
1. `TelemetryViewModel` updates `RamUsedGb`, `RamTotalGb`, `RamPercentage`, `RamCompactText`, `VramCompactText`, `HardwareTooltipText`, and shifts `RamBarColor` to Amber (`#F59E0B`) at `>= 75%` and Red (`#EF4444`) at `>= 90%`.
2. `TelemetryViewModel.PurgeMemoryCommand` posts to `/api/memory/purge` and refreshes telemetry.
3. `SettingsViewModel` loads and saves `ModelCacheIdleTimeoutMinutes` (`15`), `EngineHibernationTimeoutMinutes` (`30`), and `RamPressureThresholdPercent` (`75`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~TelemetryViewModel"`
Expected: FAIL with compilation error.

- [ ] **Step 3: Implement `TelemetryService`, `TelemetryViewModel`, `TelemetryRibbonControl.axaml`, `SettingsViewModel`, and `SettingsTabControl.axaml`**

1. Update `GpuTelemetryInfo` in `LocalLLMServerManager.Shared/Interfaces/ITelemetryService.cs` with optional default parameters:
```csharp
public record GpuTelemetryInfo(
    string GpuName,
    double UsedVramGb,
    double TotalVramGb,
    double FreeVramGb,
    int Percent,
    double UsedRamGb = 0.0,
    double TotalRamGb = 32.0,
    int RamPercent = 0,
    double ProcessWorkingSetMb = 0.0,
    double NonPagedPoolMb = 0.0
);
```
2. Update `TelemetryService.QueryGpuVramAsync` to parse both camelCase and PascalCase fields (`usedVramGb` / `UsedVramBytes`, `totalVramGb` / `VramBytes` / `VramGB`, `usedRamGb`, `totalRamGb`, `ramUsedPercent`, `processWorkingSetMb`, `nonPagedPoolMb`).
3. Update `TelemetryViewModel.cs` with `RamUsedGb`, `RamTotalGb`, `RamPercentage`, `RamStatusText`, `RamCompactText`, `VramCompactText`, `ProcessWorkingSetMb`, `NonPagedPoolMb`, `RamBarColor`, `HardwareTooltipText`, and `PurgeMemoryCommand`.
4. Update `TelemetryRibbonControl.axaml` center pill (`Grid.Column="1"`) to display the **Unified Hardware Pill** with dual compact progress bars for `VRAM` and `RAM` plus the `PurgeMemoryCommand` icon button in `Grid.Column="2"`.
5. Update `SettingsViewModel.cs` and `SettingsTabControl.axaml` (under the Engines & Paths pane) to include the **Memory & Cache Lifetimes** configuration card with `ModelCacheIdleTimeoutMinutes`, `EngineHibernationTimeoutMinutes`, and `RamPressureThresholdPercent`.

- [ ] **Step 4: Run unit tests to verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`
Expected: PASS

---

### Task 4: Linting, Typechecking, Playwright Browser E2E & Visual Screenshot Inspection

**Files:**
- Modify: `e2e` / Playwright tests if applicable
- Verify: Full test suite, `npm run lint`, `npx tsc --noEmit`, `npx playwright test`, and visual inspection

- [ ] **Step 1: Run linting and typechecking**

Run:
```powershell
npm run lint
npx tsc --noEmit
```
Expected: 0 errors.

- [ ] **Step 2: Run full .NET test suite**

Run:
```powershell
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj
```
Expected: All tests pass.

- [ ] **Step 3: Run Playwright browser tests across Chromium and Firefox & inspect screenshots**

Run:
```powershell
npx playwright test
```
Verify hard reload (`page.reload()`) and visually inspect captured screenshots via `read_file` to confirm the Unified Hardware Pill in the 34px titlebar ribbon and the Memory & Cache Lifetimes card in Settings have clean alignment, no badge clipping, and proper layout.
