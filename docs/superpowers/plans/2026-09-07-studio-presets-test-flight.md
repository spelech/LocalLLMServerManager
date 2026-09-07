# Studio Presets, Test Flight & Rich Stage Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide an intuitive, foolproof studio experience for Image, Video, and Audio/TTS generation featuring curated & custom presets, real-time hardware fit pre-flight checks, a guided diagnostic test flight modal, and rich 4-stage generation progress with collapsible engine logs.

**Architecture:** Implement a modular `StudioPresetService` and hardware fit estimator in `LocalLLMServerManager.Shared`. Build reusable Avalonia controls (`StudioPresetBarControl`, `GenerationStageTrackerControl`, `TestFlightModalControl`) shared across desktop and WASM web clients. Wire them into `EngineStudioTabControl` and `SettingsTabControl`.

**Tech Stack:** .NET 10 / C#, Avalonia UI (Desktop & Web WASM), CommunityToolkit.Mvvm, xUnit for unit tests, TypeScript / ESLint for tooling validation.

## Global Constraints
- Target Framework: .NET 10.0 (`net10.0`).
- Cross-platform: Must compile and run identically on Avalonia Desktop and Browser WASM.
- Quality Gates: Always run `npm run lint`, `npx tsc --noEmit`, and `dotnet test` before marking work complete.
- Minor version bump required upon completion.

---

### Task 1: Core Models & StudioPresetService with Unit Tests

**Files:**
- Create: `LocalLLMServerManager.Shared/Models/StudioPresetModels.cs`
- Create: `LocalLLMServerManager.Shared/Interfaces/IStudioPresetService.cs`
- Create: `LocalLLMServerManager.Shared/Services/StudioPresetService.cs`
- Modify: `LocalLLMServerManager.Shared/Models/AppSettings.cs`
- Create: `LocalLLMServerManager.Tests/StudioPresetServiceTests.cs`

**Interfaces:**
- Produces:
  - `StudioModality` enum (`Image`, `Video`, `Audio`)
  - `StudioPreset` record (`Id`, `Name`, `Description`, `Modality`, `WorkflowOrEngine`, `Width`, `Height`, `FrameCount`, `Fps`, `DurationSeconds`, `VoiceProfile`, `SamplePrompt`, `NegativePrompt`, `IsBuiltIn`)
  - `IStudioPresetService` (`GetPresets(StudioModality modality)`, `SavePreset(StudioPreset preset)`, `DeletePreset(string id)`, `DuplicatePreset(string id)`, `ExportJson()`, `ImportJson(string json)`, `ResetToDefaults()`)

- [ ] **Step 1: Write the failing unit tests for StudioPresetService**

Create `LocalLLMServerManager.Tests/StudioPresetServiceTests.cs`:
```csharp
using System.Linq;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StudioPresetServiceTests
{
    [Fact]
    public void GetPresets_ReturnsBuiltInDefaults_ForVideoImageAudio()
    {
        var service = new StudioPresetService();
        var videoPresets = service.GetPresets(StudioModality.Video);
        var imagePresets = service.GetPresets(StudioModality.Image);
        var audioPresets = service.GetPresets(StudioModality.Audio);

        Assert.NotEmpty(videoPresets);
        Assert.Contains(videoPresets, p => p.Name.Contains("480p", System.StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(imagePresets);
        Assert.NotEmpty(audioPresets);
    }

    [Fact]
    public void SaveAndGetCustomPreset_WorksCorrectly()
    {
        var service = new StudioPresetService();
        var custom = new StudioPreset
        {
            Name = "Custom 4K Video",
            Modality = StudioModality.Video,
            Width = 3840,
            Height = 2160,
            FrameCount = 60,
            Fps = 30
        };

        service.SavePreset(custom);
        var videoPresets = service.GetPresets(StudioModality.Video);

        Assert.Contains(videoPresets, p => p.Name == "Custom 4K Video" && p.Width == 3840);
    }

    [Fact]
    public void DeletePreset_RemovesCustom_DoesNotRemoveBuiltIn()
    {
        var service = new StudioPresetService();
        var custom = new StudioPreset
        {
            Name = "Temporary Preset",
            Modality = StudioModality.Image
        };
        service.SavePreset(custom);
        Assert.Contains(service.GetPresets(StudioModality.Image), p => p.Name == "Temporary Preset");

        var deleted = service.DeletePreset(custom.Id);
        Assert.True(deleted);
        Assert.DoesNotContain(service.GetPresets(StudioModality.Image), p => p.Name == "Temporary Preset");

        var builtIn = service.GetPresets(StudioModality.Video).First(p => p.IsBuiltIn);
        var deletedBuiltIn = service.DeletePreset(builtIn.Id);
        Assert.False(deletedBuiltIn);
    }

    [Fact]
    public void ExportAndImportJson_PreservesCustomPresets()
    {
        var service = new StudioPresetService();
        service.SavePreset(new StudioPreset { Name = "ExportTest", Modality = StudioModality.Audio });
        var json = service.ExportJson();

        var newService = new StudioPresetService();
        newService.ImportJson(json);

        Assert.Contains(newService.GetPresets(StudioModality.Audio), p => p.Name == "ExportTest");
    }
}
```

- [ ] **Step 2: Run test to verify it fails to compile/run**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StudioPresetServiceTests"`

- [ ] **Step 3: Implement StudioPresetModels, IStudioPresetService, and StudioPresetService**

Implement `StudioPresetModels.cs`, `IStudioPresetService.cs`, `StudioPresetService.cs`, and update `AppSettings.cs` with `List<StudioPreset> CustomPresets { get; set; } = new();`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StudioPresetServiceTests"`

- [ ] **Step 5: Commit changes**

```bash
git add LocalLLMServerManager.Shared/Models/StudioPresetModels.cs LocalLLMServerManager.Shared/Interfaces/IStudioPresetService.cs LocalLLMServerManager.Shared/Services/StudioPresetService.cs LocalLLMServerManager.Shared/Models/AppSettings.cs LocalLLMServerManager.Tests/StudioPresetServiceTests.cs
git commit -m "feat: add StudioPresetService and data models with tests"
```

---

### Task 2: Hardware Fit Pre-Flight Estimation

**Files:**
- Modify: `LocalLLMServerManager.Shared/Interfaces/ICanIRunItService.cs`
- Modify: `LocalLLMServerManager.Shared/Services/CanIRunItService.cs`
- Modify: `LocalLLMServerManager.Tests/CanIRunItServiceTests.cs`

**Interfaces:**
- Produces:
  - `StudioHardwareFit EstimateStudioHardwareFit(StudioModality modality, int width, int height, int frameCount, string workflow, double freeVramMb, double totalVramMb)`
  - `StudioHardwareFit` record (`FitBadge`, `EstimatedVramMb`, `StatusText`, `RecommendedPresetName`, `RequiresLlmUnload`)

- [ ] **Step 1: Write failing unit test for Studio Hardware Fit estimation**

Add tests to `LocalLLMServerManager.Tests/CanIRunItServiceTests.cs`:
```csharp
[Fact]
public void EstimateStudioHardwareFit_CalculatesAccurately()
{
    var service = new CanIRunItService();
    // 480p Video on 16GB GPU with 12GB Free -> Ready
    var fit = service.EstimateStudioHardwareFit(StudioModality.Video, 832, 480, 48, "wan2.2", 12000, 16000);
    Assert.Equal("Ready", fit.StatusText);
    Assert.False(fit.RequiresLlmUnload);

    // 720p Video with only 4GB Free on 12GB GPU -> Requires LLM unload
    var fitTight = service.EstimateStudioHardwareFit(StudioModality.Video, 1280, 720, 48, "wan2.2", 4000, 12000);
    Assert.True(fitTight.RequiresLlmUnload);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~EstimateStudioHardwareFit"`

- [ ] **Step 3: Implement EstimateStudioHardwareFit in CanIRunItService**

Implement heuristic calculation logic based on pixel count, frame count, and modality.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~EstimateStudioHardwareFit"`

- [ ] **Step 5: Commit changes**

```bash
git add LocalLLMServerManager.Shared/Interfaces/ICanIRunItService.cs LocalLLMServerManager.Shared/Services/CanIRunItService.cs LocalLLMServerManager.Tests/CanIRunItServiceTests.cs
git commit -m "feat: implement pre-flight studio hardware fit estimation in CanIRunItService"
```

---

### Task 3: Reusable Avalonia UI Controls

**Files:**
- Create: `LocalLLMServerManager.Shared/Views/Controls/StudioPresetBarControl.axaml` + `.cs`
- Create: `LocalLLMServerManager.Shared/Views/Controls/GenerationStageTrackerControl.axaml` + `.cs`
- Create: `LocalLLMServerManager.Shared/Views/Controls/TestFlightModalControl.axaml` + `.cs`

**Interfaces:**
- Produces:
  - `<controls:StudioPresetBarControl />`: Preset selector, Quick Save dialog, starter prompt chips.
  - `<controls:GenerationStageTrackerControl />`: 4-stage pipeline stepper, elapsed timer, live engine logs drawer, cancel button.
  - `<controls:TestFlightModalControl />`: Full diagnostic modal for 1-click test runs.

- [ ] **Step 1: Create StudioPresetBarControl**
- [ ] **Step 2: Create GenerationStageTrackerControl**
- [ ] **Step 3: Create TestFlightModalControl**
- [ ] **Step 4: Compile and verify Avalonia XAML bindings**

Run: `dotnet build LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`

- [ ] **Step 5: Commit changes**

```bash
git add LocalLLMServerManager.Shared/Views/Controls/StudioPresetBarControl.axaml* LocalLLMServerManager.Shared/Views/Controls/GenerationStageTrackerControl.axaml* LocalLLMServerManager.Shared/Views/Controls/TestFlightModalControl.axaml*
git commit -m "feat: add reusable StudioPresetBarControl, GenerationStageTrackerControl, and TestFlightModalControl"
```

---

### Task 4: Integrate Studio Presets, Hardware Fit, & Stage Tracking into Studio ViewModels and UI

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/AudioStudioViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/EngineStudioTabControl.axaml`
- Create: `LocalLLMServerManager.Tests/StudioIntegrationTests.cs`

- [ ] **Step 1: Write integration tests for Studio ViewModel preset switching and stage tracking**
- [ ] **Step 2: Update MainViewModel & AudioStudioViewModel with StudioPresetService and Stage State Tracker**
- [ ] **Step 3: Wire up EngineStudioTabControl.axaml with PresetBar, Pre-Flight Hardware Badges, StageTracker, and Test Flight modal button**
- [ ] **Step 4: Run integration tests**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StudioIntegrationTests"`

- [ ] **Step 5: Commit changes**

```bash
git add LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs LocalLLMServerManager.Shared/ViewModels/AudioStudioViewModel.cs LocalLLMServerManager.Shared/Views/Controls/EngineStudioTabControl.axaml LocalLLMServerManager.Tests/StudioIntegrationTests.cs
git commit -m "feat: wire studio presets, hardware fit badges, and stage tracker into EngineStudioTabControl"
```

---

### Task 5: Centralized Presets Manager in Settings View

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`

- [ ] **Step 1: Add preset management commands & collections to SettingsViewModel**
  - `ObservableCollection<StudioPreset> AllPresets`
  - `CreatePresetCommand`, `EditPresetCommand`, `DeletePresetCommand`, `DuplicatePresetCommand`, `ExportPresetsCommand`, `ImportPresetsCommand`, `ResetPresetsToDefaultCommand`
- [ ] **Step 2: Add "Studio Presets Manager" Card in SettingsTabControl.axaml**
  - Modality filter tabs (`All`, `🎬 Video`, `🎨 Image`, `🎵 Audio/TTS`)
  - Preset data items control with edit/delete/duplicate action buttons
  - JSON import/export and reset defaults buttons
- [ ] **Step 3: Test and compile settings preset manager**

Run: `dotnet build LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`

- [ ] **Step 4: Commit changes**

```bash
git add LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml
git commit -m "feat: add centralized studio presets manager in Settings view"
```

---

### Task 6: Verification, Version Bump, and Branch/PR Preparation

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs` (app version bump)
- Modify: `.csproj` files if applicable

- [ ] **Step 1: Run complete test suite**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`

- [ ] **Step 2: Run linters & typecheck**

Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 3: Minor version bump**

Bump version from `3.12.1` to `3.13.0` across the application.

- [ ] **Step 4: Commit and verify git log**

```bash
git commit -am "chore: bump version to 3.13.0 and finalize studio presets & test flight release"
```
