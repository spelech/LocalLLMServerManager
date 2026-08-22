# Model Context Protocol (MCP) Server & Installer Updates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a fully compliant Model Context Protocol (MCP) server over Streamable HTTP and SSE transports in ASP.NET Core, and upgrade Windows and Linux installer/update pipelines to support graceful in-place updates over existing running installations.

**Architecture:** ASP.NET Core Minimal API hosts the official `ModelContextProtocol.AspNetCore` server mapped to `/mcp`, dispatching tools (`get_gpu_vram`, `check_health`, `list_models`, `pull_model`, `unload_vram`, `start_engine`, `stop_engine`, `detect_tools`) via dependency injection to underlying services. Inno Setup, PowerShell, and Bash installers handle pre-install process detection/stopping, configuration preservation, and post-update service/tray recovery.

**Tech Stack:** .NET 10 LTS, `ModelContextProtocol.AspNetCore` (v2.2.0), `Microsoft.Extensions.AI.Abstractions`, Inno Setup 6, PowerShell, Bash, xUnit v3.

## Global Constraints
- Target Framework: `net10.0`
- Existing Minimal API endpoints (`/health`, `/api/gpu/vram`, `/api/settings`, `/api/models`, `/api/tools/detect`) must remain untouched and passing.
- Backward compatibility for `GET /api/mcp/tools` must be preserved.
- User configurations in `settings.json` must be preserved across in-place updates.
- All code changes must pass `npx tsc --noEmit` / linting (if applicable) and full test suite `dotnet test`.

---

### Task 1: Core MCP Tools Class (`Services/LocalLlmMcpTools.cs`)

**Files:**
- Create: `Services/LocalLlmMcpTools.cs`
- Modify: `LocalLLMServerManager.csproj`
- Test: `LocalLLMServerManager.Tests/McpServerIntegrationTests.cs`

**Interfaces:**
- Consumes:
  - `IGpuTelemetryProvider.GetTelemetryAsync()`
  - `IAiEngineManager.StartEngineAsync(string)` / `StopEngineAsync(string)`
  - `IOllamaModelService.GetInstalledModelsAsync()` / `PullModelAsync(string)`
  - `IToolDiscoveryService.DetectAllToolsAsync()`
- Produces:
  - `LocalLlmMcpTools` class annotated with `[McpServerToolType]` and `[McpServerTool]` exposing all 8 tool methods.

- [ ] **Step 1: Write unit tests for `LocalLlmMcpTools` in `LocalLLMServerManager.Tests/McpServerIntegrationTests.cs`**

```csharp
using System.Text.Json;
using System.Threading.Tasks;
using LocalLLMServerManager.Models;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using Moq;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class McpServerIntegrationTests
{
    [Fact]
    public async Task GetGpuVram_ReturnsTelemetryData()
    {
        var mockTelemetry = new Mock<IGpuTelemetryProvider>();
        mockTelemetry.Setup(t => t.GetTelemetryAsync())
            .ReturnsAsync(new GpuTelemetryResult("NVIDIA RTX 4090", 24576, 4096, 20480, 16.7));

        var mockEngine = new Mock<IAiEngineManager>();
        var mockOllama = new Mock<IOllamaModelService>();
        var mockDiscovery = new Mock<IToolDiscoveryService>();
        var mockHttp = new Mock<IHttpClientFactory>();

        var tools = new LocalLlmMcpTools(mockTelemetry.Object, mockEngine.Object, mockOllama.Object, mockDiscovery.Object, mockHttp.Object);
        var result = await tools.GetGpuVramAsync();

        Assert.Contains("RTX 4090", result);
        Assert.Contains("24576", result);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~McpServerIntegrationTests" -c Debug`
Expected: FAIL (type `LocalLlmMcpTools` not found).

- [ ] **Step 3: Implement `Services/LocalLlmMcpTools.cs`**

```csharp
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Services;

[McpServerToolType]
public sealed class LocalLlmMcpTools
{
    private readonly IGpuTelemetryProvider _telemetryProvider;
    private readonly IAiEngineManager _engineManager;
    private readonly IOllamaModelService _ollamaModelService;
    private readonly IToolDiscoveryService _toolDiscoveryService;
    private readonly IHttpClientFactory _httpClientFactory;

    public LocalLlmMcpTools(
        IGpuTelemetryProvider telemetryProvider,
        IAiEngineManager engineManager,
        IOllamaModelService ollamaModelService,
        IToolDiscoveryService toolDiscoveryService,
        IHttpClientFactory httpClientFactory)
    {
        _telemetryProvider = telemetryProvider;
        _engineManager = engineManager;
        _ollamaModelService = ollamaModelService;
        _toolDiscoveryService = toolDiscoveryService;
        _httpClientFactory = httpClientFactory;
    }

    [McpServerTool, Description("Get real-time GPU VRAM allocation, total memory, used memory, and GPU hardware name via NVML CUDA.")]
    public async Task<string> GetGpuVramAsync()
    {
        var telemetry = await _telemetryProvider.GetTelemetryAsync();
        return JsonSerializer.Serialize(telemetry, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Check real-time health and connectivity of Ollama, Stable Diffusion Forge, and ComfyUI backend ports.")]
    public async Task<string> CheckHealthAsync()
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(2);

        async Task<object> CheckPort(string url)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var resp = await client.GetAsync(url);
                sw.Stop();
                return new { online = resp.IsSuccessStatusCode, status = (int)resp.StatusCode, latencyMs = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                return new { online = false, error = ex.Message };
            }
        }

        var results = new
        {
            ollama = await CheckPort("http://127.0.0.1:11434/"),
            sdForge = await CheckPort("http://127.0.0.1:7860/"),
            comfyUi = await CheckPort("http://127.0.0.1:8188/system_stats")
        };

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("List all installed Ollama LLM models, quantization formats, and memory/disk footprint.")]
    public async Task<string> ListModelsAsync()
    {
        var models = await _ollamaModelService.GetInstalledModelsAsync();
        return JsonSerializer.Serialize(models, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Trigger a model pull from the Ollama library or Hugging Face repository.")]
    public async Task<string> PullModelAsync([Description("Model identifier, e.g. 'llama3.2:latest' or 'qwen2.5-coder:7b'")] string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return JsonSerializer.Serialize(new { success = false, error = "modelName is required" });

        var started = await _ollamaModelService.PullModelAsync(modelName);
        return JsonSerializer.Serialize(new { success = started, modelName, message = started ? "Model pull initiated" : "Failed to initiate pull" });
    }

    [McpServerTool, Description("Unload all LLM models currently residing in GPU VRAM to free memory for diffusion or 3D workflows.")]
    public async Task<string> UnloadVramAsync()
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var payload = new StringContent("{\"model\":\"\",\"keep_alive\":0}", System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("http://127.0.0.1:11434/api/generate", payload);
            return JsonSerializer.Serialize(new { success = response.IsSuccessStatusCode, status = (int)response.StatusCode, message = "VRAM unload requested" });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [McpServerTool, Description("Start an AI backend engine process ('forge' or 'comfyui').")]
    public async Task<string> StartEngineAsync([Description("Target engine: 'forge' or 'comfyui'")] string engine)
    {
        var result = await _engineManager.StartEngineAsync(engine);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Gracefully terminate an AI backend engine process ('forge' or 'comfyui').")]
    public async Task<string> StopEngineAsync([Description("Target engine: 'forge' or 'comfyui'")] string engine)
    {
        var result = await _engineManager.StopEngineAsync(engine);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool, Description("Scan system drives and PATH for installed Ollama, ComfyUI, and SD Forge directories.")]
    public async Task<string> DetectToolsAsync()
    {
        var discovered = await _toolDiscoveryService.DetectAllToolsAsync();
        return JsonSerializer.Serialize(discovered, new JsonSerializerOptions { WriteIndented = true });
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~McpServerIntegrationTests" -c Debug`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/LocalLlmMcpTools.cs LocalLLMServerManager.csproj LocalLLMServerManager.Tests/McpServerIntegrationTests.cs
git commit -m "feat(mcp): implement LocalLlmMcpTools suite with 8 tools"
```

---

### Task 2: ASP.NET Core Kestrel Endpoint Registration (`Endpoints/McpEndpoints.cs` & `Program.cs`)

**Files:**
- Modify: `Endpoints/McpEndpoints.cs`
- Modify: `Program.cs`
- Test: `LocalLLMServerManager.Tests/McpServerIntegrationTests.cs`

**Interfaces:**
- Consumes:
  - `LocalLlmMcpTools`
  - `builder.Services.AddMcpServer().WithHttpTransport().WithTools<LocalLlmMcpTools>()`
  - `app.MapMcp("/mcp")`
- Produces:
  - Working `/mcp` route for standard MCP agents
  - Backwards-compatible `GET /api/mcp/tools` route

- [ ] **Step 1: Add integration tests for MCP endpoints**

In `LocalLLMServerManager.Tests/McpServerIntegrationTests.cs`, add:
```csharp
[Fact]
public async Task LegacyMcpToolsEndpoint_ReturnsAllToolMetadata()
{
    using var appFixture = new AppTestServerFixture();
    var client = appFixture.CreateClient();

    var response = await client.GetAsync("/api/mcp/tools");
    Assert.True(response.IsSuccessStatusCode);

    var json = await response.Content.ReadAsStringAsync();
    Assert.Contains("get_gpu_vram", json);
    Assert.Contains("check_health", json);
    Assert.Contains("list_models", json);
    Assert.Contains("pull_model", json);
    Assert.Contains("unload_vram", json);
    Assert.Contains("start_engine", json);
    Assert.Contains("stop_engine", json);
    Assert.Contains("detect_tools", json);
    Assert.Contains("/mcp", json);
}
```

- [ ] **Step 2: Update `Endpoints/McpEndpoints.cs` and `Program.cs`**

In `Endpoints/McpEndpoints.cs`:
```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class McpEndpoints
{
    public static void MapMcpEndpoints(this WebApplication app)
    {
        // Standard Model Context Protocol (MCP) Streamable HTTP & SSE endpoint
        app.MapMcp("/mcp");

        // Backwards-compatible discovery endpoint
        app.MapGet("/api/mcp/tools", () => Results.Ok(new
        {
            protocol = "mcp",
            version = "2024-11-05",
            endpoint = "/mcp",
            tools = new[]
            {
                new { name = "get_gpu_vram", description = "Get real-time GPU VRAM allocation, total memory, used memory, and GPU hardware name via NVML CUDA." },
                new { name = "check_health", description = "Check real-time health and connectivity of Ollama, Stable Diffusion Forge, and ComfyUI backend ports." },
                new { name = "list_models", description = "List all installed Ollama LLM models, quantization formats, and memory/disk footprint." },
                new { name = "pull_model", description = "Trigger a model pull from the Ollama library or Hugging Face repository." },
                new { name = "unload_vram", description = "Unload all LLM models currently residing in GPU VRAM to free memory for diffusion or 3D workflows." },
                new { name = "start_engine", description = "Start an AI backend engine process ('forge' or 'comfyui')." },
                new { name = "stop_engine", description = "Gracefully terminate an AI backend engine process ('forge' or 'comfyui')." },
                new { name = "detect_tools", description = "Scan system drives and PATH for installed Ollama, ComfyUI, and SD Forge directories." }
            }
        }));
    }
}
```

In `Program.cs`:
```csharp
// Register MCP Server
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<LocalLlmMcpTools>();
```

- [ ] **Step 3: Run integration test to verify endpoint**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~McpServerIntegrationTests" -c Debug`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Endpoints/McpEndpoints.cs Program.cs LocalLLMServerManager.Tests/McpServerIntegrationTests.cs
git commit -m "feat(mcp): map /mcp streamable HTTP endpoint and register MCP tools in DI"
```

---

### Task 3: Comprehensive MCP Integration Tests

**Files:**
- Modify: `LocalLLMServerManager.Tests/McpServerIntegrationTests.cs`
- Modify: `LocalLLMServerManager.Tests/LiveExternalProviderIntegrationTests.cs`

- [ ] **Step 1: Write comprehensive tool invocation and schema tests in `McpServerIntegrationTests.cs`**

Test:
- `CheckHealthAsync` executes and returns structured JSON
- `ListModelsAsync` returns model array
- `UnloadVramAsync` returns status
- `StartEngineAsync` & `StopEngineAsync` return engine responses
- `DetectToolsAsync` returns discovered tools result

- [ ] **Step 2: Run all tests in `LocalLLMServerManager.Tests`**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj -c Debug`
Expected: 100% PASS with 0 failures.

- [ ] **Step 3: Commit**

```bash
git add LocalLLMServerManager.Tests/McpServerIntegrationTests.cs LocalLLMServerManager.Tests/LiveExternalProviderIntegrationTests.cs
git commit -m "test(mcp): add comprehensive integration tests for MCP tools and endpoints"
```

---

### Task 4: Inno Setup Windows Installer In-Place Update Support (`scripts/installer.iss`)

**Files:**
- Modify: `scripts/installer.iss`

**Requirements:**
- Handle existing installations gracefully.
- Add `CloseApplications=yes` and `RestartApplications=yes`.
- In `[Code]`, detect running service and stop it (`net.exe stop LocalLLMServerManager`) and terminate running tray processes.
- Mark `settings.json` with `Flags: onlyifdoesntexist uninsneveruninstall` so existing user settings are untouched during upgrades.
- In `[Run]`, reconfigure service if it exists (`sc config ...`) and start it (`net start LocalLLMServerManager`).

- [ ] **Step 1: Update `scripts/installer.iss` with update and lifecycle directives**

Update `scripts/installer.iss` with:
1. `CloseApplications=yes`
2. `RestartApplications=yes`
3. `[Files]` flag `onlyifdoesntexist` for `settings.json`
4. `[Code]` pre-install function `PrepareToInstall` stopping running service and tray app
5. Service reconfiguration in `[Run]`

- [ ] **Step 2: Verify `scripts/installer.iss` syntax and directives**

- [ ] **Step 3: Commit**

```bash
git add scripts/installer.iss
git commit -m "feat(installer): add in-place update and service lifecycle management to Inno Setup"
```

---

### Task 5: PowerShell and Linux Update Scripts (`scripts/install.ps1`, `scripts/update.ps1`, `scripts/install_linux.sh`)

**Files:**
- Modify: `scripts/install.ps1`
- Modify: `scripts/update.ps1`
- Modify: `scripts/install_linux.sh`

- [ ] **Step 1: Enhance `scripts/install.ps1` and `scripts/update.ps1`**
  - Detect running Windows Service `LocalLLMServerManager` and stop it before publish/copy.
  - Detect and kill running `LocalLLMServerManager.exe` tray app to prevent file lock errors.
  - Preserve existing `settings.json` (backup and restore if target exists).
  - Restart service and relaunch tray app post-install.

- [ ] **Step 2: Enhance `scripts/install_linux.sh`**
  - Check `systemctl is-active --quiet localllmmanager`.
  - Stop service if active prior to binary copy.
  - Preserve user settings.
  - Reload systemd and restart service.

- [ ] **Step 3: Test PowerShell and shell script routines**

- [ ] **Step 4: Commit**

```bash
git add scripts/install.ps1 scripts/update.ps1 scripts/install_linux.sh
git commit -m "feat(scripts): enhance PowerShell and Linux installers with graceful update support"
```

---

### Task 6: Documentation, Requirements Traceability & Full Verification

**Files:**
- Modify: `README.md`
- Modify: `docs/REQUIREMENTS.md`
- Modify: `docs/TEST_COVERAGE.md`
- Modify: `docs/ARCHITECTURE.md`

- [ ] **Step 1: Update documentation and requirements matrix**
  - Update `docs/REQUIREMENTS.md` with `MCP-001`, `MCP-002`, `MCP-003` requirements and tests.
  - Update `README.md` highlighting the `/mcp` server and in-place installer update features.
  - Update `docs/TEST_COVERAGE.md`.

- [ ] **Step 2: Run full build and test suite**

Run: `dotnet test -c Release`
Run: `npm run lint` and `npx tsc --noEmit` (if web frontend changes exist)

- [ ] **Step 3: Commit**

```bash
git add README.md docs/REQUIREMENTS.md docs/TEST_COVERAGE.md docs/ARCHITECTURE.md
git commit -m "docs: document MCP server endpoints, tools, and in-place installer updates"
```
