# Fluid Studio & Docked Copilot UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform LocalLLMServerManager into a fluid, novice-friendly creative workspace with interactive engine status cards, a chat-inspired prompt dock with uncluttered preview canvas, and a unified right-docked Copilot & Knowledge sidebar.

**Architecture:** Refactor the top header into informative `EngineStatusCard` pills, replace rigid 5-step generation boxes with a modern center stage (top modality pills + generous viewport + bottom prompt dock with parameter flyouts), and merge the separate slide-out drawers into a single docked "Copilot & Knowledge" sidebar (AI Assistant + Documentation) with cross-tab Q&A bridging.

**Tech Stack:** C# 13 / .NET 10, Avalonia UI (Desktop & WebAssembly), CommunityToolkit.Mvvm, xUnit / Moq, TypeScript / Node.js tooling.

## Global Constraints

- Always run linting (`npm run lint`) and typechecking (`npx tsc --noEmit`) after code changes.
- All unit & integration tests (`dotnet test LocalLLMServerManager.Tests`) must pass.
- Preserve existing backend endpoints and engine process execution pipelines.
- Avalonia XAML styling must use `DesignTokens.axaml` and glassmorphic design tokens.

---

### Task 1: Interactive Engine Status Cards & Telemetry ViewModels

**Files:**
- Create: `LocalLLMServerManager.Shared/Models/EngineStatusCardModel.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/TelemetryViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/TelemetryRibbonControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml`
- Test: `LocalLLMServerManager.Tests/TelemetryViewModelTests.cs`

**Interfaces:**
- Produces: `EngineStatusCardModel` (`EngineKey`, `DisplayName`, `IsOnline`, `IsStarting`, `IsError`, `Port`, `ActiveModel`, `MemoryUsage`, `StatusTooltip`).
- Produces: `TelemetryViewModel.OllamaCard`, `ComfyUiCard`, `ForgeCard`, `KokoroCard`, `ToggleEngineCardCommand`.

- [x] **Step 1: Write failing unit tests for EngineStatusCardModel and TelemetryViewModel**

In `LocalLLMServerManager.Tests/TelemetryViewModelTests.cs`:
```csharp
[Fact]
public void TelemetryViewModel_EngineCards_ReflectEngineStatus()
{
    var vm = new TelemetryViewModel();
    Assert.NotNull(vm.OllamaCard);
    Assert.NotNull(vm.ComfyUiCard);
    Assert.NotNull(vm.ForgeCard);
    Assert.NotNull(vm.KokoroCard);

    vm.OllamaStatus = "Running";
    vm.OllamaModelName = "qwen2.5-coder:1.5b";
    Assert.True(vm.OllamaCard.IsOnline);
    Assert.Equal("qwen2.5-coder:1.5b", vm.OllamaCard.ActiveModel);
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~TelemetryViewModel_EngineCards"`
Expected: FAIL with missing properties/types.

- [x] **Step 3: Implement EngineStatusCardModel and update TelemetryViewModel**

Create `LocalLLMServerManager.Shared/Models/EngineStatusCardModel.cs`:
```csharp
namespace LocalLLMServerManager.Shared.Models;

public class EngineStatusCardModel
{
    public string EngineKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsOnline { get; set; }
    public bool IsStarting { get; set; }
    public bool IsError { get; set; }
    public int Port { get; set; }
    public string ActiveModel { get; set; } = "";
    public string MemoryUsage { get; set; } = "";
    public string StatusTooltip { get; set; } = "";
}
```

Update `TelemetryViewModel.cs`:
Add `OllamaCard`, `ComfyUiCard`, `ForgeCard`, `KokoroCard` properties and sync them in `UpdateFromTelemetry()` and `RefreshStatusAsync()`. Add `ToggleEngineCardCommand` taking `EngineKey` to start/stop the engine via `IAiEngineManager`.

- [x] **Step 4: Update TelemetryRibbonControl.axaml and TelemetryHeaderControl.axaml**

Replace the bare toggle switches (`ToggleSwitch`) with interactive pill cards styled with:
- Status dot (green `#10b981` when online, amber `#f59e0b` when starting, slate `#64748b` when stopped).
- Engine title and active model text (`qwen2.5-coder`, `SDXL / 3D Nodes`, etc.).
- Direct action button (`▷ Start` when stopped, `⏹ Stop` when running).
- ToolTip containing port, memory, and troubleshooting advice.

- [x] **Step 5: Run tests and verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~TelemetryViewModel"`
Expected: PASS.

- [x] **Step 6: Run lint and typecheck**

Run: `npm run lint` and `npx tsc --noEmit`

- [x] **Step 7: Commit**

```bash
git add LocalLLMServerManager.Shared/Models/EngineStatusCardModel.cs LocalLLMServerManager.Shared/ViewModels/TelemetryViewModel.cs LocalLLMServerManager.Shared/Views/Controls/TelemetryRibbonControl.axaml LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml LocalLLMServerManager.Tests/TelemetryViewModelTests.cs
git commit -m "feat(ui): add interactive engine status cards to telemetry header"
```

---

### Task 2: Unified "Copilot & Knowledge" Sidebar Host

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/ActivityRailControl.axaml`
- Test: `LocalLLMServerManager.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `DocumentationViewModel`, `AiAssistantViewModel`.
- Produces: `MainViewModel.IsCopilotSidebarOpen`, `SelectedCopilotTab` ("Assistant" | "Docs"), `ToggleCopilotSidebarCommand`, `SelectCopilotTabCommand`.

- [x] **Step 1: Write failing unit test for Copilot Sidebar state in MainViewModel**

In `LocalLLMServerManager.Tests/MainViewModelTests.cs`:
```csharp
[Fact]
public void MainViewModel_ToggleCopilotSidebar_TogglesState()
{
    var vm = new MainViewModel();
    Assert.False(vm.IsCopilotSidebarOpen);

    vm.ToggleCopilotSidebarCommand.Execute("Assistant");
    Assert.True(vm.IsCopilotSidebarOpen);
    Assert.Equal("Assistant", vm.SelectedCopilotTab);

    vm.ToggleCopilotSidebarCommand.Execute("Assistant");
    Assert.False(vm.IsCopilotSidebarOpen);
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~MainViewModel_ToggleCopilotSidebar"`
Expected: FAIL.

- [x] **Step 3: Implement Copilot Sidebar properties and commands in MainViewModel**

In `MainViewModel.cs`:
- Add `[ObservableProperty] private bool _isCopilotSidebarOpen;`
- Add `[ObservableProperty] private string _selectedCopilotTab = "Assistant";`
- Add `[RelayCommand] private void ToggleCopilotSidebar(string? tab = null)`
- Add `[RelayCommand] private void SelectCopilotTab(string tab)`

- [x] **Step 4: Update MainView.axaml with unified right sidebar**

In `MainView.axaml`:
- Remove separate `DocumentationDrawer` (left drawer) and separate `AiAssistantDrawer` (right drawer).
- Add docked right sidebar column in the main grid:
  ```xml
  <Border x:Name="CopilotKnowledgeSidebar" Grid.Column="2" Width="440"
          Background="{StaticResource BgSurfaceBrush}" BorderBrush="{StaticResource GlassBorderBrush}"
          BorderThickness="1,0,0,0" IsVisible="{Binding IsCopilotSidebarOpen}">
      <Grid RowDefinitions="Auto, *">
          <!-- Sidebar Header & Tab Switcher -->
          <Border Grid.Row="0" Background="{StaticResource BgDarkBrush}" BorderBrush="{StaticResource GlassBorderBrush}" BorderThickness="0,0,0,1" Padding="12,8">
              <Grid ColumnDefinitions="Auto, *, Auto">
                  <StackPanel Orientation="Horizontal" Spacing="4">
                      <Button Content="🤖 Copilot" Command="{Binding SelectCopilotTabCommand}" CommandParameter="Assistant"
                              Classes.active="{Binding SelectedCopilotTab, Converter={x:Static ObjectConverters.Equal}, ConverterParameter=Assistant}" Classes="tab-chip"/>
                      <Button Content="📖 Knowledge" Command="{Binding SelectCopilotTabCommand}" CommandParameter="Docs"
                              Classes.active="{Binding SelectedCopilotTab, Converter={x:Static ObjectConverters.Equal}, ConverterParameter=Docs}" Classes="tab-chip"/>
                  </StackPanel>
                  <Button Grid.Column="2" Content="✕" Command="{Binding ToggleCopilotSidebarCommand}" Classes="matte-secondary"/>
              </Grid>
          </Border>
          <!-- Content Host -->
          <Panel Grid.Row="1">
              <controls:AiAssistantTabControl DataContext="{Binding Assistant}" IsVisible="{Binding $parent[UserControl].((vm:MainViewModel)DataContext).SelectedCopilotTab, Converter={x:Static ObjectConverters.Equal}, ConverterParameter=Assistant}"/>
              <controls:DocumentationTabControl DataContext="{Binding Documentation}" IsVisible="{Binding $parent[UserControl].((vm:MainViewModel)DataContext).SelectedCopilotTab, Converter={x:Static ObjectConverters.Equal}, ConverterParameter=Docs}"/>
          </Panel>
      </Grid>
  </Border>
  ```
- Update `ActivityRailControl.axaml` to have a dedicated `Copilot & Docs` button that toggles `ToggleCopilotSidebarCommand`.

- [x] **Step 5: Run tests and verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~MainViewModel"`
Expected: PASS.

- [x] **Step 6: Run lint and typecheck**

Run: `npm run lint` and `npx tsc --noEmit`

- [x] **Step 7: Commit**

```bash
git add LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs LocalLLMServerManager.Shared/Views/MainView.axaml LocalLLMServerManager.Shared/Views/Controls/ActivityRailControl.axaml LocalLLMServerManager.Tests/MainViewModelTests.cs
git commit -m "feat(ui): unify documentation and assistant into docked copilot sidebar"
```

---

### Task 3: Contextual Documentation-to-Copilot Bridge

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/DocumentationViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/AiAssistantViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/DocumentationTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/DocumentationAndAssistantBridgeTests.cs`

**Interfaces:**
- Produces: `DocumentationViewModel.AskCopilotAboutGuideCommand`
- Consumes: `AiAssistantViewModel.AppendPromptAndSendAsync(string prompt)`

- [x] **Step 1: Write failing unit test for guide citation in assistant**

Create `LocalLLMServerManager.Tests/DocumentationAndAssistantBridgeTests.cs`:
```csharp
[Fact]
public void DocumentationViewModel_AskCopilot_ForwardsTopicToAssistant()
{
    var mainVm = new MainViewModel();
    mainVm.Documentation.SelectedDocument = "Getting Started with 3D Generation";
    mainVm.Documentation.AskCopilotAboutGuideCommand.Execute(null);

    Assert.True(mainVm.IsCopilotSidebarOpen);
    Assert.Equal("Assistant", mainVm.SelectedCopilotTab);
    Assert.Contains("Getting Started with 3D Generation", mainVm.Assistant.PromptText);
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~DocumentationAndAssistantBridgeTests"`
Expected: FAIL.

- [x] **Step 3: Implement bridge logic in DocumentationViewModel and AiAssistantViewModel**

In `DocumentationViewModel.cs`:
- Add `[RelayCommand] private void AskCopilotAboutGuide()`
- Forward event/callback or reference to `MainViewModel` to switch `SelectedCopilotTab = "Assistant"` and set `Assistant.PromptText = $"Can you explain '{SelectedDocument}' and how I use it in the Studio?"`.

In `DocumentationTabControl.axaml`:
- Add prominent header action button:
  `<Button Content="💬 Ask Copilot About This Guide" Command="{Binding AskCopilotAboutGuideCommand}" Classes="copilot-action-btn"/>`
- Add search box fuzzy filtering at top of documentation list.

- [x] **Step 4: Run tests and verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~DocumentationAndAssistantBridgeTests"`
Expected: PASS.

- [x] **Step 5: Run lint and typecheck**

Run: `npm run lint` and `npx tsc --noEmit`

- [x] **Step 6: Commit**

```bash
git add LocalLLMServerManager.Shared/ViewModels/DocumentationViewModel.cs LocalLLMServerManager.Shared/Views/Controls/DocumentationTabControl.axaml LocalLLMServerManager.Tests/DocumentationAndAssistantBridgeTests.cs
git commit -m "feat(ui): add ask-copilot action to documentation guides"
```

---

### Task 4: Fluid Studio Canvas & Creative Prompt Dock

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/EngineStudioViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/EngineStudioTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/EngineStudioViewModelTests.cs`

**Interfaces:**
- Produces: `EngineStudioViewModel.SelectedModality` ("Image", "Text", "Video", "3D Mesh", "Audio").
- Produces: `EngineStudioViewModel.IsParametersFlyoutOpen`, `ToggleParametersFlyoutCommand`.
- Produces: `EngineStudioViewModel.ActiveModelBadge`, `ActiveAspectPreset`.

- [x] **Step 1: Write failing unit test for Studio modality and dock parameters**

In `LocalLLMServerManager.Tests/EngineStudioViewModelTests.cs`:
```csharp
[Fact]
public void EngineStudioViewModel_ModalitySwitch_UpdatesDockState()
{
    var vm = new EngineStudioViewModel();
    vm.SelectModalityCommand.Execute("3D Mesh");

    Assert.Equal("3D Mesh", vm.SelectedModality);
    Assert.True(vm.Is3DModalityActive);

    vm.ToggleParametersFlyoutCommand.Execute(null);
    Assert.True(vm.IsParametersFlyoutOpen);
}
```

- [x] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~EngineStudioViewModel_ModalitySwitch"`
Expected: FAIL.

- [x] **Step 3: Update EngineStudioViewModel.cs**

In `EngineStudioViewModel.cs`:
- Add `[ObservableProperty] private string _selectedModality = "Image";`
- Add `[ObservableProperty] private bool _isParametersFlyoutOpen;`
- Add `[RelayCommand] private void SelectModality(string modality)`
- Add `[RelayCommand] private void ToggleParametersFlyout()`
- Add active model resolution helper syncing with `SettingsService` and active engines.

- [x] **Step 4: Redesign EngineStudioTabControl.axaml**

Replace rigid 5-step numbered boxes:
1. **Top Modality Bar:**
   - Pills for `🎨 Image`, `💬 Text`, `🎬 Video`, `🧊 3D Mesh`, `🎙️ Audio`.
2. **Generous Center Viewport:**
   - Image canvas with pan/zoom.
   - 3D interactive viewport container.
   - Video player with frame scrubber.
   - Audio waveform player with voice selector.
   - Zero-state prompt starter suggestions when canvas is idle.
3. **Bottom Creative Prompt Dock:**
   - Multi-line autosizing prompt textarea (`AcceptsReturn="True"`).
   - Context Pill Bar:
     - Model pill showing active model name with dropdown trigger.
     - Attachment button (`📎 Attach Image`).
     - Parameters trigger button (`⚙️ Settings (30 steps, 16:9)`).
     - Prominent `Generate ↵` action button with loading spinner state.
   - Collapsible Parameters Popover for steps, CFG, seed, denoise, aspect ratio.

- [x] **Step 5: Run tests and verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~EngineStudioViewModel"`
Expected: PASS.

- [x] **Step 6: Run lint and typecheck**

Run: `npm run lint` and `npx tsc --noEmit`

- [x] **Step 7: Commit**

```bash
git add LocalLLMServerManager.Shared/ViewModels/EngineStudioViewModel.cs LocalLLMServerManager.Shared/Views/Controls/EngineStudioTabControl.axaml LocalLLMServerManager.Tests/EngineStudioViewModelTests.cs
git commit -m "feat(ui): redesign engine studio with fluid canvas and creative prompt dock"
```

---

### Task 5: End-to-End Build, Verification & Production Deployment

**Files:**
- Modify: `package.json` (if any scripts needed)
- Test: Full unit and integration test suite

- [x] **Step 1: Execute fast update build script**

Run: `pwsh scripts/fast_update.ps1 -NoLaunch`
Verify: Compiles WASM and Windows binaries cleanly and deploys to `C:\Program Files\LocalLLMServerManager`.

- [x] **Step 2: Run full unit & integration tests**

Run: `dotnet test LocalLLMServerManager.Tests`
Expected: All 800+ tests pass with 0 failures.

- [x] **Step 3: Run frontend lint & typecheck**

Run: `npm run lint` and `npx tsc --noEmit`
Expected: 0 errors.

- [x] **Step 4: Verify live service health**

Run: `Invoke-RestMethod http://127.0.0.1:5246/health`
Expected: Healthy status with all engines reachable.

- [x] **Step 5: Commit and tag release**

```bash
git add -A
git commit -m "chore: complete fluid studio and docked copilot UI redesign"
```
