# Flexible Tool Paths, Ecosystem Auto-Discovery & Configurable Onboarding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform LocalLLMServerManager from hardcoded folder assumptions (e.g. `D:\AI`) into a fully configurable, user-friendly platform with ecosystem auto-discovery for AI tools (Ollama, ComfyUI, SD Forge/A1111), native Desktop folder/file pickers, real-time path validation badges, parameterized helper scripts, comprehensive unit tests (>90% coverage), and documentation updates for a v3.5.0 release.

**Architecture:** Introduce `IToolDiscoveryService` / `ToolDiscoveryService` to scan standard ecosystem locations across `%LOCALAPPDATA%`, `%USERPROFILE%`, drive roots, and system `PATH`. Modernize `AppSettings` to remove hardcoded paths and provide dynamic resolution. Expose discovery and validation endpoints in ASP.NET Core, wire up `IStorageProvider` file/folder dialogs and visual status badges in the Avalonia UI & Web Dashboard, and update all helper scripts to read from `settings.json` and accept CLI parameters.

**Tech Stack:** C# .NET 9.0, Avalonia UI 11.x, CommunityToolkit.Mvvm, ASP.NET Core Minimal APIs, xUnit, PowerShell, Inno Setup.

## Global Constraints

- Never hardcode fixed drive paths or arbitrary folder names like `D:\AI` or `C:\AI`.
- Maintain >90% code coverage across `LocalLLMServerManager.Tests`.
- Run `npm run lint` and `npx tsc --noEmit` / `dotnet test` to ensure zero regressions.
- Version bumped to `3.5.0` across project files and documentation.

---

### Task 1: `IToolDiscoveryService` and `ToolDiscoveryService` Implementation & Unit Tests

**Files:**
- Create: `Services/IToolDiscoveryService.cs`
- Create: `Services/ToolDiscoveryService.cs`
- Test: `LocalLLMServerManager.Tests/ToolDiscoveryServiceTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public interface IToolDiscoveryService
  {
      Task<DiscoveredToolsResult> DetectAllToolsAsync();
      DiscoveredToolInfo DetectOllama();
      DiscoveredToolInfo DetectComfyUi();
      DiscoveredToolInfo DetectForge();
      PathValidationResult ValidatePath(string? path, PathTargetType targetType);
  }
  public record DiscoveredToolInfo(bool IsInstalled, string? ExecutablePath, string? RootDirectory, string? ModelsDirectory, string? WorkflowsDirectory, string StatusMessage);
  public record DiscoveredToolsResult(DiscoveredToolInfo Ollama, DiscoveredToolInfo ComfyUi, DiscoveredToolInfo Forge, string SuggestedThreeDPath, string SuggestedWorkflowsPath);
  public enum PathTargetType { Executable, Directory }
  public record PathValidationResult(bool Exists, bool IsValid, string? ErrorMessage);
  ```

- [ ] **Step 1: Write unit tests for `ToolDiscoveryService`**

```csharp
// LocalLLMServerManager.Tests/ToolDiscoveryServiceTests.cs
using System.IO;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ToolDiscoveryServiceTests
{
    [Fact]
    public void ValidatePath_NullOrEmpty_ReturnsInvalid()
    {
        var service = new ToolDiscoveryService();
        var result = service.ValidatePath("", PathTargetType.Directory);
        Assert.False(result.Exists);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePath_ExistingDirectory_ReturnsValid()
    {
        var service = new ToolDiscoveryService();
        var tempDir = Directory.GetCurrentDirectory();
        var result = service.ValidatePath(tempDir, PathTargetType.Directory);
        Assert.True(result.Exists);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task DetectAllToolsAsync_ReturnsNonEmptyResult()
    {
        var service = new ToolDiscoveryService();
        var result = await service.DetectAllToolsAsync();
        Assert.NotNull(result);
        Assert.NotNull(result.Ollama);
        Assert.NotNull(result.ComfyUi);
        Assert.NotNull(result.Forge);
        Assert.False(string.IsNullOrWhiteSpace(result.SuggestedThreeDPath));
        Assert.False(string.IsNullOrWhiteSpace(result.SuggestedWorkflowsPath));
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~ToolDiscoveryServiceTests"`
Expected: FAIL (types do not exist yet).

- [ ] **Step 3: Implement `IToolDiscoveryService` and `ToolDiscoveryService`**

Implement discovery logic inspecting `PATH`, `%LOCALAPPDATA%\Programs\Ollama`, `%USERPROFILE%`, drive roots (`C:\`, `D:\`), and process names for Ollama, ComfyUI, and SD Forge.

- [ ] **Step 4: Run tests and verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~ToolDiscoveryServiceTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/IToolDiscoveryService.cs Services/ToolDiscoveryService.cs LocalLLMServerManager.Tests/ToolDiscoveryServiceTests.cs
git commit -m "feat: add IToolDiscoveryService and discovery unit tests"
```

---

### Task 2: Modernize `AppSettings.cs` & Dynamic Fallback Resolution

**Files:**
- Modify: `LocalLLMServerManager.Shared/Models/AppSettings.cs`
- Modify: `Program.cs`
- Modify: `Services/SettingsService.cs`
- Modify: `LocalLLMServerManager.Tests/AppSettingsTests.cs`

**Interfaces:**
- Consumes: `IToolDiscoveryService`
- Produces: Modernized `AppSettings` record with dynamic resolution.

- [ ] **Step 1: Update failing tests for `AppSettings`**

Update `AppSettingsTests.cs` to verify that `AppSettings` defaults do not contain hardcoded `D:\AI` or `%APPDATA%\AI` strings and that dynamic resolution functions properly.

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~AppSettingsTests"`

- [ ] **Step 3: Update `AppSettings.cs` and `Program.cs`**

Remove hardcoded fallback strings from `AppSettings.cs`. Register `IToolDiscoveryService` in `Program.cs` DI container. Update `ResolvePath` and `SettingsService`.

- [ ] **Step 4: Run tests and verify pass**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~AppSettingsTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Models/AppSettings.cs Program.cs Services/SettingsService.cs LocalLLMServerManager.Tests/AppSettingsTests.cs
git commit -m "feat: modernize AppSettings with dynamic discovery fallbacks"
```

---

### Task 3: Discovery & Path Validation REST Endpoints

**Files:**
- Create: `Endpoints/DiscoveryEndpoints.cs`
- Modify: `Program.cs`
- Test: `LocalLLMServerManager.Tests/DiscoveryEndpointsTests.cs`

**Interfaces:**
- Consumes: `IToolDiscoveryService`, `ISettingsService`
- Produces: `GET /api/system/tools/detect`, `POST /api/system/tools/apply-detected`, `POST /api/system/tools/validate`

- [ ] **Step 1: Write failing endpoint integration tests**

```csharp
// LocalLLMServerManager.Tests/DiscoveryEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class DiscoveryEndpointsTests : IClassFixture<AppTestServerFixture>
{
    private readonly HttpClient _client;
    public DiscoveryEndpointsTests(AppTestServerFixture fixture) => _client = fixture.Client;

    [Fact]
    public async Task Get_DetectTools_ReturnsSuccessAndPayload()
    {
        var response = await _client.GetAsync("/api/system/tools/detect");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DiscoveredToolsResult>();
        Assert.NotNull(result);
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~DiscoveryEndpointsTests"`

- [ ] **Step 3: Implement `Endpoints/DiscoveryEndpoints.cs` and map in `Program.cs`**

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~DiscoveryEndpointsTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Endpoints/DiscoveryEndpoints.cs Program.cs LocalLLMServerManager.Tests/DiscoveryEndpointsTests.cs
git commit -m "feat: add REST endpoints for tool discovery and path validation"
```

---

### Task 4: Avalonia Desktop UI: File/Folder Pickers, Auto-Detect & Status Indicators

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml.cs`
- Test: `LocalLLMServerManager.Tests/SettingsViewModelCoverageTests.cs`

**Interfaces:**
- Consumes: `IStorageProvider` via Avalonia top-level window, Discovery Endpoints.
- Produces: Commands for `AutoDetectToolsCommand`, `BrowseComfyExecutableCommand`, `BrowseForgeExecutableCommand`, `BrowseForgeModelsCommand`, `BrowseThreeDModelsCommand`, `BrowseWorkflowsCommand`, and status indicator properties.

- [ ] **Step 1: Write ViewModel unit tests for discovery and browsing commands**

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelCoverageTests"`

- [ ] **Step 3: Implement `SettingsViewModel.cs` commands and update `SettingsTabControl.axaml` with browse buttons and status badges**

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~SettingsViewModel"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml.cs LocalLLMServerManager.Tests/SettingsViewModelCoverageTests.cs
git commit -m "feat: add desktop file/folder pickers, auto-detect command, and status badges"
```

---

### Task 5: Web Dashboard Auto-Discovery & Validation

**Files:**
- Modify: `wwwroot/index.html` (or settings scripts in `wwwroot/`)
- Verify static assets and formatting.

- [ ] **Step 1: Add Auto-Discovery card and path status indicators to the Web Dashboard Settings tab**
- [ ] **Step 2: Connect frontend triggers to `GET /api/system/tools/detect` and `POST /api/system/tools/apply-detected`**
- [ ] **Step 3: Validate lint and typechecking**
- [ ] **Step 4: Commit**

```bash
git add wwwroot/
git commit -m "feat: integrate tool auto-discovery and validation into web dashboard"
```

---

### Task 6: Parameterize & Modernize Helper Scripts

**Files:**
- Modify: `scripts/setup_ai_tools.ps1`
- Modify: `scripts/download_models.ps1`
- Modify: `scripts/download_media_models.ps1`
- Modify: `scripts/install_comfy_nodes.ps1`
- Modify: `scripts/fix_comfy.ps1`
- Modify: `scripts/download_all.py`
- Modify: `scripts/hf_download.py`

- [ ] **Step 1: Refactor `setup_ai_tools.ps1` to accept `-TargetDir` and `-ModelsDir`, read `settings.json`, and dynamically generate `extra_model_paths.yaml`**
- [ ] **Step 2: Refactor `download_models.ps1` and `download_media_models.ps1` to accept `-ModelsDir` and read from `settings.json`**
- [ ] **Step 3: Refactor `install_comfy_nodes.ps1` and `fix_comfy.ps1` to parameterize ComfyUI directory**
- [ ] **Step 4: Update Python scripts with `argparse` for customizable destinations**
- [ ] **Step 5: Commit**

```bash
git add scripts/
git commit -m "feat: parameterize helper scripts to read settings.json and accept CLI arguments"
```

---

### Task 7: Documentation, Version Bump (v3.5.0), and Full Test & Build Verification

**Files:**
- Modify: `LocalLLMServerManager.csproj`
- Modify: `LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`
- Modify: `scripts/installer.iss`
- Modify: `README.md`
- Modify: `docs/REQUIREMENTS.md`
- Modify: `docs/DEVELOPMENT_GUIDE.md`
- Modify: `docs/TEST_COVERAGE.md`

- [ ] **Step 1: Bump project version to `3.5.0` in all `.csproj` and installer files**
- [ ] **Step 2: Update documentation to detail auto-discovery, custom folder configuration, and script parameterization**
- [ ] **Step 3: Run full test suite and typechecking**

```bash
dotnet test
npm run lint
npx tsc --noEmit
```

- [ ] **Step 4: Commit**

```bash
git add LocalLLMServerManager.csproj LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj scripts/installer.iss README.md docs/
git commit -m "docs: bump version to 3.5.0 and document flexible path configuration"
```
