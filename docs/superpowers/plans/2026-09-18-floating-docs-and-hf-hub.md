# Floating Documentation & Hugging Face Hub Overhaul Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or direct execution to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide a floating, movable in-app documentation overlay/window with smart tab navigation, and overhaul the Hugging Face Hub with default model loading and clear active toggle styling.

**Architecture:** Extend `DocumentationViewModel` with overlay states and `TargetTab` step navigation. Create `DocumentationFloatingOverlay.axaml` on the top canvas layer of `MainView.axaml` and a native `DocumentationWindow.axaml` for desktop pop-out. Update `HuggingFaceSearchViewModel` to load starter models on startup, and update `HuggingFaceTabControl.axaml` with high-contrast active/inactive toggle styling.

**Tech Stack:** C# (.NET 10), Avalonia UI 12.x, CommunityToolkit.Mvvm, xUnit.

## Global Constraints

* Must maintain 100% test pass rate across `LocalLLMServerManager.Tests`.
* Must pass `npm run lint` and `npx tsc --noEmit` with zero errors.
* Must support both Desktop and WebAssembly without platform crashes.
* Avoid using "ASD-STE100" in user-facing labels.

---

### Task 1: Extend DocumentationViewModel with Floating States & Step Target Tabs

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/DocumentationViewModel.cs`
- Test: `LocalLLMServerManager.Tests/DocumentationViewModelTests.cs`

**Interfaces:**
- Produces: `DocStep.TargetTab`, `IsFloatingOverlayOpen`, `IsMinimizedToPill`, `OverlayX`, `OverlayY`, `CurrentStepIndex`, `JumpToStepTabCommand`.

- [ ] **Step 1: Update DocStep record to include TargetTab**
  ```csharp
  public record DocStep(
      int StepNumber,
      string Title,
      string Action,
      string ExpectedResult,
      int? TargetTab = null
  );
  ```

- [ ] **Step 2: Add floating state properties and commands to DocumentationViewModel**
  Add observable properties:
  - `bool IsFloatingOverlayOpen = false`
  - `bool IsMinimizedToPill = false`
  - `double OverlayX = 20`
  - `double OverlayY = 60`
  - `int CurrentStepIndex = 0`
  Add commands:
  - `ToggleFloatingOverlayCommand()`
  - `ToggleMinimizeToPillCommand()`
  - `NextStepCommand()`
  - `PreviousStepCommand()`
  - `JumpToStepTab(int? targetTab)` with an `Action<int>? OnNavigateToTabRequested` callback.

- [ ] **Step 3: Map TargetTab on procedural steps**
  Map `TargetTab: 1` (Workflows) on generation steps, `TargetTab: 0` (Models) on model steps, `TargetTab: 2` (Can I Run It) on compatibility steps, and `TargetTab: 5` (Settings) on setup steps.

- [ ] **Step 4: Update DocumentationViewModelTests and verify pass**
  Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~DocumentationViewModelTests"`

- [ ] **Step 5: Commit Task 1**
  ```bash
  git add LocalLLMServerManager.Shared/ViewModels/DocumentationViewModel.cs LocalLLMServerManager.Tests/DocumentationViewModelTests.cs
  git commit -m "feat(docs): add floating overlay states and step target tabs to DocumentationViewModel"
  ```

---

### Task 2: Create Floating Documentation Overlay & Pop-Out Controls

**Files:**
- Create: `LocalLLMServerManager.Shared/Views/Controls/DocumentationFloatingOverlay.axaml`
- Create: `LocalLLMServerManager.Shared/Views/Controls/DocumentationFloatingOverlay.axaml.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/DocumentationTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml.cs`

**Interfaces:**
- Consumes: `DocumentationViewModel`
- Produces: Movable floating guide overlay and pop-out trigger button.

- [ ] **Step 1: Create DocumentationFloatingOverlay.axaml**
  Implement floating card with:
  - Header drag region (`PointerPressed`, `PointerMoved`, `PointerReleased` handlers updating `OverlayX` and `OverlayY`).
  - Minimize/Expand button (`IsMinimizedToPill`).
  - Close button (`ToggleFloatingOverlayCommand`).
  - Step counter (`Step X of Y`) with `<` Previous and `>` Next buttons.
  - Active step description and action text.
  - `🚀 Jump to Tab` button invoking `JumpToStepTabCommand`.

- [ ] **Step 2: Add Pop-Out button to DocumentationTabControl.axaml**
  Add `[🗗 Pop Out Floating Guide]` button in the top right of the documentation header that executes `ToggleFloatingOverlayCommand`.

- [ ] **Step 3: Integrate floating overlay layer in MainView.axaml**
  Wrap `MainView.axaml` content in a `Panel` or top-level `Grid` with `DocumentationFloatingOverlay` positioned via `Margin` or `Canvas` bound to `Documentation`. Wire up `OnNavigateToTabRequested` in `MainViewModel` to set `SelectedTabIndex`.

- [ ] **Step 4: Verify build and test**
  Run `dotnet build LocalLLMServerManager.slnx` and verify zero compilation errors.

- [ ] **Step 5: Commit Task 2**
  ```bash
  git add LocalLLMServerManager.Shared/Views/
  git commit -m "feat(ui): implement floating documentation overlay with draggable controls and step navigation"
  ```

---

### Task 3: Overhaul Hugging Face Hub (Default Loading, Active Toggle Styling, Filter Logic)

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/HuggingFaceSearchViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml`
- Modify: `LocalLLMServerManager.Tests/HuggingFaceSearchViewModelTests.cs`

**Interfaces:**
- Produces: Initial model loading on launch, distinct visual active/inactive states for modality buttons and hardware filters.

- [ ] **Step 1: Implement default starter model loading in HuggingFaceSearchViewModel**
  Add an `InitializeDefaultModelsAsync()` method or auto-trigger in constructor/navigation that queries curated high-utility models (`Qwen/Qwen2.5-Coder-7B-Instruct-GGUF`, `meta-llama/Llama-3.2-3B-Instruct-GGUF`, `deepseek-ai/DeepSeek-R1-Distill-Qwen-7B-GGUF`, `Wan-AI/Wan2.1-T2V-14B`, `hexgrad/Kokoro-82M`) so the hub is populated immediately.

- [ ] **Step 2: Update HuggingFaceTabControl.axaml with high-contrast active toggle styles**
  - For input buttons (`Text`, `Image`, `Audio`, `Video`): Bind background and foreground to active states (`IsInputTextActive`, etc.). Active buttons show vibrant blue `#2563EB` with `✓` checkmark; inactive show muted `#1E293B`.
  - For output buttons (`Text`, `Image`, `Audio`, `Video`, `3D`): Active buttons show vibrant purple `#8B5CF6` with `✓` checkmark; inactive show muted `#1E293B`.
  - For hardware filters: Active chips show full color (`#10B981`, `#F59E0B`, `#F97316`, `#EF4444`) with solid border; inactive chips show dim `Opacity="0.35"`.

- [ ] **Step 3: Verify and update HuggingFaceSearchViewModelTests**
  Ensure tests verify initial loading and filter toggle states. Run `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~HuggingFaceSearchViewModelTests"`.

- [ ] **Step 4: Commit Task 3**
  ```bash
  git add LocalLLMServerManager.Shared/ViewModels/HuggingFaceSearchViewModel.cs LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml LocalLLMServerManager.Tests/HuggingFaceSearchViewModelTests.cs
  git commit -m "feat(huggingface): add default model loading and clear active toggle styling"
  ```

---

### Task 4: Verification, Quality Gates & Pull Request

**Files:**
- All modified files.

- [ ] **Step 1: Run complete test suite**
  Run `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj -c Release --nologo`
- [ ] **Step 2: Run linter and TypeScript checks**
  Run `npm run lint` and `npx tsc --noEmit`
- [ ] **Step 3: Run VitePress docs build**
  Run `npm run docs:build`
- [ ] **Step 4: Commit and push feature branch**
  Push `feature/fixes-and-adjustments` to origin and open a PR.
