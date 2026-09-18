# Window Taskbar, Hub Loading, Tab Reorganization & ASD-STE100 Documentation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the desktop application layout into Models, Workflows, Can I Run It, Documentation, and Settings; provide a draggable custom window taskbar/titlebar; add loading feedback to Hugging Face Hub; add starter models and suggestions to CivitAI; and implement interactive in-app ASD-STE100 documentation.

**Architecture:** Avalonia XAML 11.x MVVM with CommunityToolkit.Mvvm, custom glassmorphic styling, .NET 10 desktop and WASM compatibility, and xUnit headless UI tests.

**Tech Stack:** C# 13, .NET 10, Avalonia UI 11.x, CommunityToolkit.Mvvm, TypeScript/Node tooling for linting/typechecking, xUnit v3, GitHub CLI (`gh`).

## Global Constraints
- Always run linting (`npm run lint`) and typechecking (`npx tsc --noEmit`) after code changes.
- Ensure all .NET unit and headless UI tests pass with `dotnet test LocalLLMServerManager.sln`.
- Preserve existing styling conventions from `DesignTokens.axaml` and `GlassmorphicTheme.axaml`.

---

### Task 1: Draggable Window Taskbar & Window Controls
**Files:**
- Modify: `Views/MainWindow.axaml`
- Modify: `Views/MainWindow.axaml.cs`
- Test: `LocalLLMServerManager.Tests/MainWindowUiTests.cs`

- [ ] **Step 1: Add unit tests for window properties and state handling in MainWindowUiTests.cs**
- [ ] **Step 2: Update MainWindow.axaml with the custom titlebar/taskbar header, draggable grid, app title/badge, and minimize/maximize/close buttons**
- [ ] **Step 3: Implement PointerPressed dragging, maximize toggle, minimize, and close handlers in MainWindow.axaml.cs**
- [ ] **Step 4: Run tests to verify window behavior**

---

### Task 2: Hugging Face Search Loading State & Visual Feedback
**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/HuggingFaceSearchViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/HuggingFaceSearchViewModelTests.cs`

- [ ] **Step 1: Write failing test in HuggingFaceSearchViewModelTests.cs checking IsLoading toggling during search**
- [ ] **Step 2: Add `[ObservableProperty] private bool _isLoading;` and try/finally handling in HuggingFaceSearchViewModel.cs**
- [ ] **Step 3: Update HuggingFaceTabControl.axaml to display a progress bar, busy button state, and empty result message**
- [ ] **Step 4: Run tests to verify Hugging Face loading indicator behavior**

---

### Task 3: CivitAI Starter Content, Suggestions & Loading Feedback
**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/CivitaiSearchViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/CivitaiTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/CivitaiSearchViewModelTests.cs`

- [ ] **Step 1: Write test for initial starter suggestions and loading state in CivitaiSearchViewModelTests.cs**
- [ ] **Step 2: Update CivitaiSearchViewModel.cs with starter suggestions, default popular models, and loading indicators**
- [ ] **Step 3: Update CivitaiTabControl.axaml with starter keyword chip buttons and progress indicator**
- [ ] **Step 4: Run tests to verify CivitAI starter suggestions and search behavior**

---

### Task 4: In-App ASD-STE100 Documentation View & ViewModel
**Files:**
- Create: `LocalLLMServerManager.Shared/ViewModels/DocumentationViewModel.cs`
- Create: `LocalLLMServerManager.Shared/Views/Controls/DocumentationTabControl.axaml`
- Create: `LocalLLMServerManager.Shared/Views/Controls/DocumentationTabControl.axaml.cs`
- Test: `LocalLLMServerManager.Tests/DocumentationViewModelTests.cs`

- [ ] **Step 1: Create DocumentationViewModel with structured ASD-STE100 technical documentation topics and procedural steps**
- [ ] **Step 2: Create DocumentationTabControl.axaml with category selector and step-by-step procedure viewer**
- [ ] **Step 3: Add unit tests in DocumentationViewModelTests.cs verifying topic coverage and content integrity**
- [ ] **Step 4: Run tests to verify documentation functionality**

---

### Task 5: Overcrowded Studio Tab Reorganization into Guided Multi-Step Modalities
**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/Controls/EngineStudioTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Test: `LocalLLMServerManager.Tests/StudioIntegrationTests.cs`

- [ ] **Step 1: Reorganize EngineStudioTabControl.axaml into distinct modality views (Image, Text, Video, 3D, Audio) driven by SelectedStudioMode**
- [ ] **Step 2: Add step-by-step pathway headers ("Step 1: Select Model/Engine", "Step 2: Configure Prompts", "Step 3: Generate", "Step 4: Preview Output")**
- [ ] **Step 3: Run studio integration tests to verify all commands and modalities bind properly**

---

### Task 6: Top-Level Tab Reorganization in MainView
**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml`
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Test: `LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs`

- [ ] **Step 1: Add ModelsTabControl or sub-tab host for Models (Downloaded/Manage, Hugging Face, CivitAI)**
- [ ] **Step 2: Reconfigure MainView.axaml top TabControl to: Models, Workflows, Can I Run It, Documentation, Settings**
- [ ] **Step 3: Update MainViewModel.cs with Documentation ViewModel property and sub-tab selection commands**
- [ ] **Step 4: Update and expand AvaloniaHeadlessInteractionTests.cs to test the 5-tab hierarchy and sub-tabs**

---

### Task 7: Full Verification Suite
**Files:**
- Test: Solution build & full test run
- Lint: npm run lint
- Typecheck: npx tsc --noEmit

- [ ] **Step 1: Run `npm run lint` and `npx tsc --noEmit`**
- [ ] **Step 2: Run `dotnet test LocalLLMServerManager.sln` and verify all tests pass**

---

### Task 8: Git Commit, Push & Pull Request Creation
- [ ] **Step 1: Commit all changes with descriptive commit messages**
- [ ] **Step 2: Push branch `feat/ui-reorg-taskbar-docs-and-hubs` to remote origin**
- [ ] **Step 3: Create GitHub Pull Request via `gh pr create`**
