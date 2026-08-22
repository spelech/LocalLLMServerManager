# Model Context Protocol (MCP) Server & Installer Update Support Design

## 1. Overview & Goals

LocalLLMServerManager currently manages local Large Language Models (Ollama), Image Generation (Stable Diffusion / Forge), and 3D Mesh Generation (ComfyUI / TRELLIS / Hunyuan3D). To enable seamless automation by AI agents (e.g. Claude Desktop, Cursor, Antigravity) and ensure hassle-free application upgrades for end-users, this project will:
1. Implement a fully compliant **Model Context Protocol (MCP)** server over Streamable HTTP and SSE transports using the official `ModelContextProtocol.AspNetCore` package.
2. Upgrade all **installer and update pipelines** (Inno Setup Windows installer `.iss`, PowerShell scripts, and Linux installation scripts) to support graceful in-place updates over existing running installations without file locks or configuration loss.

---

## 2. Architecture & Components

```
+-------------------------------------------------------------------------------+
| AI Agents (Claude Desktop, Cursor, Antigravity, Open WebUI)                  |
+-------------------------------------------------------------------------------+
                                    |
                                    | Streamable HTTP & SSE Transport (JSON-RPC 2.0)
                                    v
+-------------------------------------------------------------------------------+
| LocalLLMServerManager Kestrel Host (:5246)                                    |
|                                                                               |
|   /mcp (Standard MCP Streamable HTTP / SSE Endpoint)                          |
|   /api/mcp/tools (Backward-Compatible Tool List)                              |
|                                                                               |
|   +-------------------------------------------------------------------------+ |
|   | LocalLlmMcpTools ([McpServerToolType])                                  |
|   |   - get_gpu_vram()       - list_models()      - start_engine(engine)    |
|   |   - check_health()       - pull_model(name)   - stop_engine(engine)     |
|   |   - unload_vram()        - detect_tools()                               |
|   +-------------------------------------------------------------------------+ |
|                                   |                                           |
|          +------------------------+------------------------+                  |
|          v                        v                        v                  |
|   IGpuTelemetryProvider   IAiEngineManager     IOllamaModelService /          |
|                                                IToolDiscoveryService          |
+-------------------------------------------------------------------------------+
```

---

## 3. Detailed Specifications

### 3.1 Model Context Protocol (MCP) Server

#### Dependencies & Configuration
* Reference `ModelContextProtocol.AspNetCore` (v2.2.0) in `LocalLLMServerManager.csproj`.
* Register MCP server services in `Program.cs`:
  ```csharp
  builder.Services.AddMcpServer()
      .WithHttpTransport()
      .WithTools<LocalLlmMcpTools>();
  ```
* Map endpoint in Kestrel routing pipeline:
  ```csharp
  app.MapMcp("/mcp");
  ```
* Retain and enhance `Endpoints/McpEndpoints.cs` to serve legacy discovery requests at `GET /api/mcp/tools`.

#### Tool Definitions (`Services/LocalLlmMcpTools.cs`)
All tools are registered with `[McpServerTool]` and descriptive `[Description]` attributes for agent schema discovery:

1. **`get_gpu_vram()`**
   * **Description**: "Get real-time GPU VRAM allocation, total memory, used memory, and GPU hardware name via NVML CUDA."
   * **Delegates To**: `IGpuTelemetryProvider.GetTelemetryAsync()`
   * **Returns**: JSON object containing GPU name, total MB, used MB, free MB, and utilization percentage.

2. **`check_health()`**
   * **Description**: "Check real-time health and connectivity of Ollama, Stable Diffusion Forge, and ComfyUI backend ports."
   * **Delegates To**: HTTP health checks on `11434`, `7860`, and `8188`.
   * **Returns**: Status map with boolean online flags and response latencies.

3. **`list_models()`**
   * **Description**: "List all installed Ollama LLM models, quantization formats, and memory/disk footprint."
   * **Delegates To**: `IOllamaModelService.GetInstalledModelsAsync()`
   * **Returns**: Array of installed model objects (name, size, digest, modified date).

4. **`pull_model(string modelName)`**
   * **Description**: "Trigger a model pull from the Ollama library or Hugging Face repository."
   * **Delegates To**: `IOllamaModelService.PullModelAsync(modelName)`
   * **Returns**: Initiation confirmation status.

5. **`unload_vram()`**
   * **Description**: "Unload all LLM models currently residing in GPU VRAM to free memory for diffusion or 3D workflows."
   * **Delegates To**: `VramOrchestrator.UnloadAllLlmModelsAsync()` (or Ollama keep_alive: 0 call).
   * **Returns**: VRAM unload result with freed status.

6. **`start_engine(string engine)`**
   * **Description**: "Start an AI backend engine process ('forge', 'comfyui', or 'ollama')."
   * **Delegates To**: `IAiEngineManager.StartEngineAsync(engine)`
   * **Returns**: Process start status and PID if successful.

7. **`stop_engine(string engine)`**
   * **Description**: "Gracefully terminate an AI backend engine process ('forge' or 'comfyui')."
   * **Delegates To**: `IAiEngineManager.StopEngineAsync(engine)`
   * **Returns**: Termination confirmation.

8. **`detect_tools()`**
   * **Description**: "Scan system drives and PATH for installed Ollama, ComfyUI, and SD Forge directories."
   * **Delegates To**: `IToolDiscoveryService.DetectAllToolsAsync()`
   * **Returns**: Auto-discovered executable paths and model directories.

---

### 3.2 Installer In-Place Update & Process Lifecycle

#### Inno Setup (`scripts/installer.iss`)
1. **Application & Service Stoppage**:
   * Add `CloseApplications=yes` and `RestartApplications=yes`.
   * Add Pascal script in `[Code]` checking if `LocalLLMServerManager` service exists and is running. If running, execute `net stop LocalLLMServerManager` before file copying.
   * Terminate any active `LocalLLMServerManager.exe` desktop tray processes.
2. **Settings Preservation**:
   * Configure `settings.json` file entry with `Flags: onlyifdoesntexist uninsneveruninstall` so existing user settings are untouched during upgrades.
3. **Post-Install Reconfiguration & Startup**:
   * If service already exists, reconfigure executable path and start service.
   * If service is newly selected, register service and start it.
   * Launch the updated tray application if desktop launch is selected.

#### PowerShell Scripts (`scripts/install.ps1`, `scripts/update.ps1`)
1. Pre-check running processes:
   * Detect and stop Windows Service `LocalLLMServerManager` if running.
   * Gracefully terminate running tray processes (`Get-Process -Name LocalLLMServerManager | Stop-Process -Force`).
2. Preserve `settings.json` during publish/copy.
3. Restart Windows Service and re-launch tray app after update.

#### Linux Installation Script (`scripts/install_linux.sh`)
1. Detect running systemd service `localllmmanager.service`.
2. Stop service prior to binary updates.
3. Preserve existing configuration files in `/etc/localllmmanager/` or user config.
4. Reload systemd daemons and restart service.

---

## 4. Testing & Verification Plan

1. **Unit & Integration Tests (`LocalLLMServerManager.Tests/McpServerIntegrationTests.cs`)**:
   * Test MCP protocol `initialize` handshake returning server capabilities and metadata.
   * Test `tools/list` schema validation asserting all 8 tools are present with JSON Schema properties.
   * Test `tools/call` for `get_gpu_vram`, `check_health`, `list_models`, `unload_vram`, `detect_tools`, `start_engine`, `stop_engine`.
   * Test legacy `GET /api/mcp/tools` backwards compatibility.
2. **Installer & Update Verification**:
   * Verify PowerShell `update.ps1` and `install.ps1` process shutdown and settings preservation logic.
   * Verify Inno Setup script compilation and directive validation.
3. **Build & Regression Suite**:
   * Run `dotnet test` to confirm all existing and new tests pass with 0 errors.
