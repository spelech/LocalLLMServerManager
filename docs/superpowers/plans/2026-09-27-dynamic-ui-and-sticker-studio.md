# Dynamic UI Workspace & Sticker Studio Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the application shell from stock Avalonia horizontal tabs into a dynamic responsive workspace (Activity Rail + Dynamic Stage Container + Titlebar Telemetry Ribbon) and deliver the Sticker Studio (drag-and-drop image input + style preset chips + alpha cutout preview).

**Architecture:** A decoupled MVVM architecture where `MainViewModel` orchestrates a collapsible `NavigationRailViewModel`, a compact `TelemetryRibbonViewModel`, and a dedicated `StickerStudioViewModel`. The UI shell uses an `ActivityRailControl` flanking a `DynamicStageContainerControl` that provides a dual-column stage in Studio mode and full-bleed stage in management modes, validated against `Avalonia.LayoutInspector`.

**Tech Stack:** C# 10 / .NET 10, Avalonia 11, Avalonia.Headless.XUnit, Avalonia.LayoutInspector, CommunityToolkit.Mvvm / ReactiveUI.

## Global Constraints

- Always run linting and typechecking after making code changes (`npm run lint` and `npx tsc --noEmit`).
- For C# / Avalonia code changes, verify with `dotnet build LocalLLMServerManager.sln` and `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`.
- Preserve existing comments and docstrings.
- Web (WASM) and Desktop share `LocalLLMServerManager.Shared`. Desktop supports native companion popouts; Web uses non-modal in-canvas drawers.
- Widescreen (≥1200px) dual-stage layout (`380px, *`), medium viewports (900–1199px) condense or use segmented toggles, compact (<900px) collapses rail to 56px icon-only.

---

### Task 1: NavigationRailViewModel & Sticker Models

**Files:**
- Create: `LocalLLMServerManager.Shared/Models/StickerModels.cs`
- Create: `LocalLLMServerManager.Shared/ViewModels/NavigationRailViewModel.cs`
- Test: `LocalLLMServerManager.Tests/NavigationRailViewModelTests.cs`

**Interfaces:**
- Produces:
  - `enum NavDomain { Studio, Models, HardwareFit, Settings }`
  - `class StickerStylePreset` (`string Id`, `string DisplayName`, `string Icon`, `string PositiveTokens`, `string NegativeTokens`, `int DefaultBorderWidth`)
  - `enum StickerPipelineStage { Idle, GeneratingDiffusion, IsolatingSubject, ApplyingContour, Ready, Failed }`
  - `class NavigationRailViewModel`: `NavDomain SelectedDomain`, `bool IsExpanded`, `void ToggleRailExpanded()`, `void SelectDomain(NavDomain domain)`

- [ ] **Step 1: Write the failing tests for NavigationRailViewModel**

```csharp
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class NavigationRailViewModelTests
{
    [Fact]
    public void InitialState_IsCollapsed_AndStudioSelected()
    {
        var vm = new NavigationRailViewModel();
        Assert.False(vm.IsExpanded);
        Assert.Equal(NavDomain.Studio, vm.SelectedDomain);
    }

    [Fact]
    public void ToggleRailExpanded_FlipsIsExpanded()
    {
        var vm = new NavigationRailViewModel();
        vm.ToggleRailExpandedCommand.Execute(null);
        Assert.True(vm.IsExpanded);
        vm.ToggleRailExpandedCommand.Execute(null);
        Assert.False(vm.IsExpanded);
    }

    [Fact]
    public void SelectDomain_UpdatesSelectedDomain()
    {
        var vm = new NavigationRailViewModel();
        vm.SelectDomainCommand.Execute(NavDomain.Models);
        Assert.Equal(NavDomain.Models, vm.SelectedDomain);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~NavigationRailViewModelTests"`
Expected: FAIL (Compilation error: NavigationRailViewModel and NavDomain not found)

- [ ] **Step 3: Implement StickerModels and NavigationRailViewModel**

Create `LocalLLMServerManager.Shared/Models/StickerModels.cs`:
```csharp
namespace LocalLLMServerManager.Shared.Models;

public enum NavDomain
{
    Studio,
    Models,
    HardwareFit,
    Settings
}

public enum StickerPipelineStage
{
    Idle,
    GeneratingDiffusion,
    IsolatingSubject,
    ApplyingContour,
    Ready,
    Failed
}

public class StickerStylePreset
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PositiveTokens { get; set; } = string.Empty;
    public string NegativeTokens { get; set; } = string.Empty;
    public int DefaultBorderWidth { get; set; } = 12;
}

public class StickerGenerationRequest
{
    public string? ImagePath { get; set; }
    public byte[]? ImageBytes { get; set; }
    public string StylePresetId { get; set; } = string.Empty;
    public string CustomPrompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public int BorderWidth { get; set; } = 12;
    public bool IsAutoCutoutEnabled { get; set; } = true;
}

public class StickerResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OutputImagePath { get; set; }
    public byte[]? OutputPngBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
```

Create `LocalLLMServerManager.Shared/ViewModels/NavigationRailViewModel.cs`:
```csharp
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class NavigationRailViewModel : ObservableObject
{
    [ObservableProperty]
    private NavDomain _selectedDomain = NavDomain.Studio;

    [ObservableProperty]
    private bool _isExpanded = false;

    public IRelayCommand ToggleRailExpandedCommand { get; }
    public IRelayCommand<NavDomain> SelectDomainCommand { get; }

    public NavigationRailViewModel()
    {
        ToggleRailExpandedCommand = new RelayCommand(ToggleRailExpanded);
        SelectDomainCommand = new RelayCommand<NavDomain>(SelectDomain);
    }

    public void ToggleRailExpanded() => IsExpanded = !IsExpanded;

    public void SelectDomain(NavDomain domain) => SelectedDomain = domain;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~NavigationRailViewModelTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Models/StickerModels.cs LocalLLMServerManager.Shared/ViewModels/NavigationRailViewModel.cs LocalLLMServerManager.Tests/NavigationRailViewModelTests.cs
git commit -m "feat(nav): add NavigationRailViewModel and StickerModels"
```

---

### Task 2: StickerStudioViewModel & IStickerGenerationService

**Files:**
- Create: `LocalLLMServerManager.Shared/Services/IStickerGenerationService.cs`
- Create: `LocalLLMServerManager.Shared/Services/StickerGenerationService.cs`
- Create: `LocalLLMServerManager.Shared/ViewModels/StickerStudioViewModel.cs`
- Test: `LocalLLMServerManager.Tests/StickerStudioViewModelTests.cs`

**Interfaces:**
- Consumes: `StickerStylePreset`, `StickerGenerationRequest`, `StickerResult`, `StickerPipelineStage`
- Produces:
  - `IStickerGenerationService`: `Task<StickerResult> GenerateStickerAsync(StickerGenerationRequest request, CancellationToken ct)`
  - `StickerStudioViewModel`:
    - `ObservableCollection<StickerStylePreset> StylePresets`
    - `StickerStylePreset? SelectedStylePreset`
    - `string? InputImagePath`, `string CustomPrompt`, `int BorderWidth` (clamped 0–24), `bool IsAutoCutoutEnabled`
    - `StickerPipelineStage CurrentStage`, `bool IsGenerating`
    - `IRelayCommand SelectStylePresetCommand`, `IAsyncRelayCommand GenerateStickerCommand`, `IRelayCommand ClearInputCommand`

- [ ] **Step 1: Write failing tests for StickerStudioViewModel**

```csharp
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StickerStudioViewModelTests
{
    private class FakeStickerService : IStickerGenerationService
    {
        public bool ShouldSucceed { get; set; } = true;
        public Task<StickerResult> GenerateStickerAsync(StickerGenerationRequest request, CancellationToken ct = default)
        {
            if (!ShouldSucceed)
                return Task.FromResult(new StickerResult { IsSuccess = false, ErrorMessage = "Engine offline" });
            return Task.FromResult(new StickerResult
            {
                IsSuccess = true,
                OutputPngBytes = new byte[] { 1, 2, 3 },
                Width = 1024,
                Height = 1024
            });
        }
    }

    [Fact]
    public void Presets_InitializedWithSixCoreStyles()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        Assert.Equal(6, vm.StylePresets.Count);
        Assert.NotNull(vm.SelectedStylePreset);
        Assert.Equal("die-cut-vinyl", vm.SelectedStylePreset.Id);
    }

    [Fact]
    public void BorderWidth_ClampedBetweenZeroAndTwentyFour()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        vm.BorderWidth = 50;
        Assert.Equal(24, vm.BorderWidth);
        vm.BorderWidth = -5;
        Assert.Equal(0, vm.BorderWidth);
    }

    [Fact]
    public async Task GenerateSticker_TransitionsThroughStagesToReady()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        vm.InputImagePath = "test.png";
        await vm.GenerateStickerCommand.ExecuteAsync(null);
        Assert.Equal(StickerPipelineStage.Ready, vm.CurrentStage);
        Assert.False(vm.IsGenerating);
        Assert.NotNull(vm.GeneratedPngBytes);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StickerStudioViewModelTests"`
Expected: FAIL (Compilation error: IStickerGenerationService and StickerStudioViewModel not found)

- [ ] **Step 3: Implement IStickerGenerationService, StickerGenerationService, and StickerStudioViewModel**

Create `LocalLLMServerManager.Shared/Services/IStickerGenerationService.cs`:
```csharp
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Services;

public interface IStickerGenerationService
{
    Task<StickerResult> GenerateStickerAsync(StickerGenerationRequest request, CancellationToken ct = default);
}
```

Create `LocalLLMServerManager.Shared/Services/StickerGenerationService.cs` with the default 6 curated sticker styles (Die-Cut Vinyl, Holographic, Chibi Anime, 80s Retro, Pop Art, Watercolor) and fallback simulation for testing/offline states.

Create `LocalLLMServerManager.Shared/ViewModels/StickerStudioViewModel.cs`:
- Implements properties: `StylePresets`, `SelectedStylePreset`, `CustomPrompt`, `BorderWidth`, `IsAutoCutoutEnabled`, `CurrentStage`, `GeneratedPngBytes`, `ErrorMessage`.
- Implements commands: `SelectStylePresetCommand`, `GenerateStickerCommand`, `ClearInputCommand`, `CopyStickerCommand`, `SaveStickerCommand`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StickerStudioViewModelTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Services/IStickerGenerationService.cs LocalLLMServerManager.Shared/Services/StickerGenerationService.cs LocalLLMServerManager.Shared/ViewModels/StickerStudioViewModel.cs LocalLLMServerManager.Tests/StickerStudioViewModelTests.cs
git commit -m "feat(studio): add StickerStudioViewModel and IStickerGenerationService"
```

---

### Task 3: ActivityRailControl & TelemetryRibbonControl XAML

**Files:**
- Create: `LocalLLMServerManager.Shared/Views/Controls/ActivityRailControl.axaml`
- Create: `LocalLLMServerManager.Shared/Views/Controls/ActivityRailControl.axaml.cs`
- Create: `LocalLLMServerManager.Shared/Views/Controls/TelemetryRibbonControl.axaml`
- Create: `LocalLLMServerManager.Shared/Views/Controls/TelemetryRibbonControl.axaml.cs`
- Test: `LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs` (add unit checks for ActivityRailControl & TelemetryRibbonControl)

**Interfaces:**
- Consumes: `NavigationRailViewModel`, `TelemetryViewModel`
- Produces:
  - `ActivityRailControl`: UserControl rendering 56px/200px rail with domain buttons, indicators, and companion triggers.
  - `TelemetryRibbonControl`: UserControl rendering 34px compact header with status dots (Ollama, Forge, ComfyUI) and VRAM pill.

- [ ] **Step 1: Write the failing layout audit test in AvaloniaLayoutAuditTests.cs**

```csharp
[AvaloniaFact]
public void ActivityRailControl_LayoutAudit()
{
    var rail = new ActivityRailControl
    {
        DataContext = new NavigationRailViewModel()
    };
    var window = new Window { Content = rail, Width = 200, Height = 800 };
    window.Show();

    var options = CreateConfiguredAuditOptions(checkTouchErgonomics: false, checkTextClipping: true);
    var inspector = new LayoutInspectorEngine(options);
    var report = inspector.Audit(rail);
    var violations = FilterAppViolations(report);

    Assert.Empty(violations);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~ActivityRailControl_LayoutAudit"`
Expected: FAIL (ActivityRailControl not found)

- [ ] **Step 3: Implement ActivityRailControl and TelemetryRibbonControl**

Create `LocalLLMServerManager.Shared/Views/Controls/ActivityRailControl.axaml` and `.cs`:
- Smooth transition between 56px (icon-only with tooltips) and 200px (icon + text).
- Top domain buttons: ⚡ Studio, 📦 Models, 💻 Fit, ⚙️ Settings.
- Bottom utility buttons: 📖 Docs, 🤖 AI Assist, ◀ Expand/Collapse toggle.

Create `LocalLLMServerManager.Shared/Views/Controls/TelemetryRibbonControl.axaml` and `.cs`:
- Height 34px.
- Status dots with green/yellow/red indicators for Ollama, Forge, ComfyUI.
- VRAM badge with % and GPU name.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~ActivityRailControl_LayoutAudit"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Views/Controls/ActivityRailControl.axaml* LocalLLMServerManager.Shared/Views/Controls/TelemetryRibbonControl.axaml* LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs
git commit -m "feat(ui): create ActivityRailControl and TelemetryRibbonControl"
```

---

### Task 4: ImageDropZoneControl & StickerStudioControl XAML

**Files:**
- Create: `LocalLLMServerManager.Shared/Views/Controls/ImageDropZoneControl.axaml`
- Create: `LocalLLMServerManager.Shared/Views/Controls/ImageDropZoneControl.axaml.cs`
- Create: `LocalLLMServerManager.Shared/Views/Controls/StickerStudioControl.axaml`
- Create: `LocalLLMServerManager.Shared/Views/Controls/StickerStudioControl.axaml.cs`
- Test: `LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs` (add StickerStudioControl_LayoutAudit)

**Interfaces:**
- Consumes: `StickerStudioViewModel`
- Produces:
  - `ImageDropZoneControl`: UserControl supporting drag & drop, file selection, thumbnail preview, and removal.
  - `StickerStudioControl`: UserControl featuring 2-column stage (380px Input Deck + Flexible Output Canvas with alpha checkerboard).

- [ ] **Step 1: Write the failing layout audit test in AvaloniaLayoutAuditTests.cs**

```csharp
[AvaloniaFact]
public void StickerStudioControl_LayoutAudit()
{
    var control = new StickerStudioControl
    {
        DataContext = new StickerStudioViewModel(new StickerGenerationService())
    };
    var window = new Window { Content = control, Width = 1280, Height = 800 };
    window.Show();

    var options = CreateConfiguredAuditOptions(checkTouchErgonomics: false, checkTextClipping: true);
    var inspector = new LayoutInspectorEngine(options);
    var report = inspector.Audit(control);
    var violations = FilterAppViolations(report);

    Assert.Empty(violations);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StickerStudioControl_LayoutAudit"`
Expected: FAIL (StickerStudioControl not found)

- [ ] **Step 3: Implement ImageDropZoneControl and StickerStudioControl**

Create `LocalLLMServerManager.Shared/Views/Controls/ImageDropZoneControl.axaml` and `.cs`:
- Avalonia `DragDrop.SetAllowDrop(this, true)` for desktop file drop.
- Fallback button for file picker dialog.
- Preview thumbnail with clear button.

Create `LocalLLMServerManager.Shared/Views/Controls/StickerStudioControl.axaml` and `.cs`:
- Left column (380px Input Deck): ImageDropZone, Style Preset Chips (ItemsControl with WrapPanel), Border Width Slider (0–24px), Auto-cutout Switch, Generate Button.
- Right column (Flexible Output Canvas): Checkerboard background grid, sticker preview bitmap, GenerationStageTracker, floating export pills (Copy PNG / Save).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StickerStudioControl_LayoutAudit"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Views/Controls/ImageDropZoneControl.axaml* LocalLLMServerManager.Shared/Views/Controls/StickerStudioControl.axaml* LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs
git commit -m "feat(studio): create ImageDropZoneControl and StickerStudioControl"
```

---

### Task 5: DynamicStageContainerControl & MainView/MainWindow Integration

**Files:**
- Create: `LocalLLMServerManager.Shared/Views/Controls/DynamicStageContainerControl.axaml`
- Create: `LocalLLMServerManager.Shared/Views/Controls/DynamicStageContainerControl.axaml.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml`
- Modify: `Views/MainWindow.axaml`
- Test: `LocalLLMServerManager.Tests/MainViewModelTests.cs` & `LocalLLMServerManager.Tests/MainWindowUiTests.cs`

**Interfaces:**
- Consumes: `MainViewModel`, `NavigationRailViewModel`, `StickerStudioViewModel`, `TelemetryViewModel`
- Produces: Integrated dynamic shell replacing stock TabControl.

- [ ] **Step 1: Write unit test in MainViewModelTests for dynamic domain orchestration**

```csharp
[Fact]
public void MainViewModel_OrchestratesNavigationRailAndStickerStudio()
{
    var vm = new MainViewModel();
    Assert.NotNull(vm.NavigationRail);
    Assert.NotNull(vm.StickerStudio);
    Assert.Equal(NavDomain.Studio, vm.NavigationRail.SelectedDomain);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~MainViewModel_OrchestratesNavigationRailAndStickerStudio"`
Expected: FAIL (NavigationRail / StickerStudio properties not on MainViewModel)

- [ ] **Step 3: Implement DynamicStageContainerControl and update MainView and MainWindow**

1. In `MainViewModel.cs`:
   - Add `NavigationRailViewModel NavigationRail { get; } = new();`
   - Add `StickerStudioViewModel StickerStudio { get; } = new(new StickerGenerationService());`
2. Create `DynamicStageContainerControl.axaml`:
   - Switches view based on `NavigationRail.SelectedDomain`:
     - `Studio`: Displays `StickerStudioControl` (with option to switch to other workflows).
     - `Models`: Displays `ModelsTabControl`.
     - `HardwareFit`: Displays `CanIRunItView`.
     - `Settings`: Displays `SettingsTabControl`.
3. In `MainView.axaml`:
   - Replace the horizontal `<TabControl>` with `<Grid ColumnDefinitions="Auto, *">` hosting `<controls:ActivityRailControl>` in Column 0 and `<controls:DynamicStageContainerControl>` in Column 1.
   - Retain in-canvas non-modal slide-out drawers for Docs and AI Assistant.
4. In `MainWindow.axaml`:
   - Integrate `TelemetryRibbonControl` into the titlebar on Desktop (`Border x:Name="TitleBar"`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~MainViewModel_OrchestratesNavigationRailAndStickerStudio"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Views/Controls/DynamicStageContainerControl.axaml* LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs LocalLLMServerManager.Shared/Views/MainView.axaml Views/MainWindow.axaml LocalLLMServerManager.Tests/MainViewModelTests.cs
git commit -m "feat(shell): integrate ActivityRail, DynamicStageContainer and titlebar ribbon"
```

---

### Task 6: Full Responsive Audits, TypeScript Linting & Verification

**Files:**
- Modify: `LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs`
- Run: `npm run lint` and `npx tsc --noEmit`
- Run: `dotnet test LocalLLMServerManager.sln`

- [ ] **Step 1: Update MainWindow responsive layout audit test in AvaloniaLayoutAuditTests.cs**

Verify `MainWindow_ResponsiveLayoutAudit` passes at:
- 1440 × 900 (Widescreen Desktop)
- 1024 × 768 (Tablet Landscape)
- 1280 × 800 with 320px Web Drawer open

- [ ] **Step 2: Run all layout audit tests**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~AvaloniaLayoutAuditTests"`
Expected: ALL PASS with 0 errors.

- [ ] **Step 3: Run full solution test suite**

Run: `dotnet test LocalLLMServerManager.sln`
Expected: ALL PASS.

- [ ] **Step 4: Run web TypeScript & lint checks**

Run: `npm run lint` and `npx tsc --noEmit`
Expected: 0 lint errors, 0 type errors.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs
git commit -m "test(layout): verify responsive layout audits for dynamic UI and sticker studio"
```
