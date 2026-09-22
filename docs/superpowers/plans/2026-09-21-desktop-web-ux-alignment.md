# Desktop & Web UX Alignment, Window Lifecycle, and Responsive Layouts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve Desktop vs. Web UX discrepancies, unify desktop window minimize/restore lifecycle, make the telemetry header responsive/collapsible, eliminate chat and documentation layout crowding at 440px width, and add automated `Avalonia.LayoutInspector` audit tests.

**Architecture:** 
- `WindowSnapManager` coordinates companion window states with `MainWindow`, setting ownership and syncing minimize/restore across both attached and detached windows.
- Desktop UI bypasses in-app slide-out drawers, toggling native companion windows directly. Web uses non-modal slide-out drawers.
- `TelemetryHeaderControl` gains a compact 32px ribbon mode for horizontal tablets and compact heights.
- `AiAssistantTabControl` and `DocumentationTabControl` gain responsive/adaptive layouts (stacked composer, wrapped header, and master-detail docs navigation).
- `AvaloniaLayoutAuditTests` validates zero boundary overflows, zero sibling collisions, and zero text clipping.

**Tech Stack:** C# .NET 8 / 9, Avalonia UI 11.2, Avalonia.Headless.XUnit, Avalonia.LayoutInspector, XUnit, TypeScript.

## Global Constraints

- Always run linting and typechecking after making code changes (`npm run lint` and `npx tsc --noEmit`).
- All headless UI tests must execute via `dotnet test` with 0 failures.
- Zero sibling collisions and zero boundary overflows across companion breakpoints (440px width).

---

### Task 1: Desktop Window Lifecycle & Synchronization in `WindowSnapManager`

**Files:**
- Modify: `Services/WindowSnapManager.cs`
- Modify: `Views/MainWindow.axaml.cs`
- Test: `LocalLLMServerManager.Tests/WindowSnapManagerTests.cs`

**Interfaces:**
- Consumes: `MainWindow`, `DocumentationWindow`, `AiAssistWindow`, `SnapFlank`
- Produces: `WindowSnapManager.RegisterCompanion(Window, Window, SnapFlank, bool)`, `WindowSnapManager.SynchronizeAllForMain(Window)`, `WindowSnapManager.GetAllCompanionsForMain(Window)`

- [ ] **Step 1: Write failing tests in `WindowSnapManagerTests.cs` for minimize synchronization of both attached and detached companions**

```csharp
[AvaloniaFact]
public void SynchronizeAllForMain_WhenMainMinimized_MinimizesAttachedAndDetachedCompanions()
{
    var main = new Window { Width = 1024, Height = 768 };
    var attached = new Window { Width = 440, Height = 768 };
    var detached = new Window { Width = 440, Height = 768 };

    WindowSnapManager.Instance.RegisterCompanion(main, attached, SnapFlank.Right, autoAttach: true);
    WindowSnapManager.Instance.RegisterCompanion(main, detached, SnapFlank.Left, autoAttach: false);

    main.WindowState = WindowState.Minimized;
    WindowSnapManager.Instance.SynchronizeAllForMain(main);

    Assert.Equal(WindowState.Minimized, attached.WindowState);
    Assert.Equal(WindowState.Minimized, detached.WindowState);
}

[AvaloniaFact]
public void SynchronizeAllForMain_WhenMainRestored_RestoresCompanions()
{
    var main = new Window { Width = 1024, Height = 768, WindowState = WindowState.Minimized };
    var attached = new Window { Width = 440, Height = 768, WindowState = WindowState.Minimized };
    var detached = new Window { Width = 440, Height = 768, WindowState = WindowState.Minimized };

    WindowSnapManager.Instance.RegisterCompanion(main, attached, SnapFlank.Right, autoAttach: true);
    WindowSnapManager.Instance.RegisterCompanion(main, detached, SnapFlank.Left, autoAttach: false);

    main.WindowState = WindowState.Normal;
    WindowSnapManager.Instance.SynchronizeAllForMain(main);

    Assert.Equal(WindowState.Normal, attached.WindowState);
    Assert.Equal(WindowState.Normal, detached.WindowState);
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test --filter "FullyQualifiedName~WindowSnapManagerTests"`
Expected: FAIL due to detached windows not being minimized or synchronized.

- [ ] **Step 3: Update `WindowSnapManager.cs` and `MainWindow.axaml.cs`**

In `WindowSnapManager.cs`:
- Set companion's `Owner = mainWindow` during registration when running in native desktop mode.
- In `SynchronizeAllForMain(Window mainWindow)`:
  - If `mainWindow.WindowState == WindowState.Minimized`: iterate all registered companions (whether `IsSnapped` is true or false) and set `companion.WindowState = WindowState.Minimized`.
  - If `mainWindow.WindowState == WindowState.Normal` or `WindowState.Maximized`: restore companions that were minimized with the main window to `WindowState.Normal`, and call `SynchronizeCompanion` on snapped ones.
- When companion window's minimize button is clicked while attached, minimize `mainWindow` (or minimize together).

In `MainWindow.axaml.cs`:
- Wire navigation buttons on Desktop to toggle companion native windows directly (`mainVm.Documentation.OnPopOutNativeWindowRequested` / `mainVm.Assistant.OnPopOutNativeWindowRequested`).
- Ensure companion window owners are set to `this`.

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~WindowSnapManagerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/WindowSnapManager.cs Views/MainWindow.axaml.cs LocalLLMServerManager.Tests/WindowSnapManagerTests.cs
git commit -m "feat(desktop): synchronize companion window minimize/restore lifecycle with MainWindow"
```

---

### Task 2: Platform Separation & Non-Modal Web Drawers

**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml`
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Test: `LocalLLMServerManager.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `MainViewModel.IsAnyDrawerOpen`, `Documentation.IsDrawerOpen`, `Assistant.IsDrawerOpen`
- Produces: `MainViewModel.IsDesktopHost`, non-modal drawer behavior on Web

- [ ] **Step 1: Write test in `MainViewModelTests.cs` verifying drawer commands and host flag**

```csharp
[Fact]
public void ToggleDocumentationDrawerCommand_TogglesDrawerState()
{
    var vm = new MainViewModel();
    Assert.False(vm.Documentation.IsDrawerOpen);

    vm.ToggleDocumentationDrawerCommand.Execute(null);
    Assert.True(vm.Documentation.IsDrawerOpen);

    vm.CloseDrawersCommand.Execute(null);
    Assert.False(vm.Documentation.IsDrawerOpen);
}
```

- [ ] **Step 2: Run test to verify current state**

Run: `dotnet test --filter "FullyQualifiedName~MainViewModelTests"`
Expected: PASS or verify drawer state transitions.

- [ ] **Step 3: Modify `MainView.axaml` and `MainViewModel.cs`**

In `MainView.axaml`:
- Update drawer backdrop: Remove dark blocking backdrop `#66000000` or change to non-modal overlay:
  - Set `IsHitTestVisible="False"` or eliminate the blocking backdrop so Web users can interact with the app while referencing drawers.
- On Desktop host (checked via `IsDesktopHost` on `MainViewModel` or view-level check), hide drawer containers so Desktop uses companion windows exclusively.

- [ ] **Step 4: Verify with tests**

Run: `dotnet test --filter "FullyQualifiedName~MainViewModelTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Views/MainView.axaml LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs LocalLLMServerManager.Tests/MainViewModelTests.cs
git commit -m "feat(ui): make web drawers non-modal and align desktop side-panel routing"
```

---

### Task 3: Responsive Collapsible Telemetry Header

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/TelemetryViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml`
- Test: `LocalLLMServerManager.Tests/TelemetryViewModelTests.cs`

**Interfaces:**
- Consumes: `TelemetryViewModel.GpuName`, `TelemetryViewModel.VramStatusText`, `TelemetryViewModel.OllamaStatus`
- Produces: `TelemetryViewModel.IsCollapsed`, `TelemetryViewModel.ToggleCollapseCommand`

- [ ] **Step 1: Write unit tests in `TelemetryViewModelTests.cs` for collapse toggle**

```csharp
[Fact]
public void ToggleCollapseCommand_TogglesIsCollapsed()
{
    var vm = new TelemetryViewModel(new MockTelemetryService());
    Assert.False(vm.IsCollapsed);

    vm.ToggleCollapseCommand.Execute(null);
    Assert.True(vm.IsCollapsed);

    vm.ToggleCollapseCommand.Execute(null);
    Assert.False(vm.IsCollapsed);
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test --filter "FullyQualifiedName~TelemetryViewModelTests"`
Expected: FAIL due to missing `IsCollapsed` property.

- [ ] **Step 3: Implement `IsCollapsed` & Toggle in `TelemetryViewModel.cs` and responsive template in `TelemetryHeaderControl.axaml`**

In `TelemetryViewModel.cs`:
- Add `[ObservableProperty] private bool _isCollapsed;`
- Add `[RelayCommand] private void ToggleCollapse() => IsCollapsed = !IsCollapsed;`

In `TelemetryHeaderControl.axaml`:
- When `IsCollapsed == false`: Display the full 3-card grid with a compact collapse button `▲`.
- When `IsCollapsed == true`: Display a slim 32px horizontal ribbon:
  - Indicators: Ollama, Forge, ComfyUI status dots with tooltips.
  - VRAM % badge.
  - Expand button `▼` to restore full 3-card view.

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~TelemetryViewModelTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/ViewModels/TelemetryViewModel.cs LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml LocalLLMServerManager.Tests/TelemetryViewModelTests.cs
git commit -m "feat(ui): add responsive collapsible 32px ribbon mode to TelemetryHeaderControl"
```

---

### Task 4: Responsive Chat Layout (`AiAssistantTabControl.axaml`) & Layout Audit Tests

**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/Controls/AiAssistantTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs`

**Interfaces:**
- Consumes: `AiAssistantViewModel`, `AiModelCapabilityInfo`
- Produces: Overlap-free responsive layout across 440px to 1440px widths.

- [ ] **Step 1: Write failing layout audit test `AiAssistantTabControl_LayoutAudit` in `AvaloniaLayoutAuditTests.cs`**

```csharp
[AvaloniaFact]
public void AiAssistantTabControl_LayoutAudit()
{
    var vm = new MainViewModel();
    var control = new AiAssistantTabControl { DataContext = vm.Assistant };
    var window = new Window { Content = control, Width = 440, Height = 700 };
    try
    {
        window.Show();

        var auditor = new LayoutAuditor();
        var options = CreateConfiguredAuditOptions(checkTouchErgonomics: false, checkTextClipping: false);
        var report = auditor.Audit(control, options);

        _output.WriteLine($"AiAssistantTabControl Audit (440x700) - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
        if (report.Violations.Count > 0)
        {
            _output.WriteLine(report.ToDetailedReport());
        }

        var appViolations = FilterAppViolations(report);
        Assert.Empty(appViolations);
    }
    finally
    {
        window.Close();
    }
}
```

- [ ] **Step 2: Run test to verify current state**

Run: `dotnet test --filter "FullyQualifiedName~AiAssistantTabControl_LayoutAudit"`
Expected: May report violations or collisions due to 3-column setup and horizontal composer row at 440px.

- [ ] **Step 3: Update `AiAssistantTabControl.axaml` for narrow/companion responsiveness**

- Header:
  - Set text trimming and wrapping on subtitle.
  - Allow model indicator and status pills to wrap or fit gracefully without colliding with action buttons.
- Setup wizard card:
  - Wrap columns or use responsive stack so Endpoint, API Key, and Model selector have comfortable width (>300px) instead of 3 squished columns.
- Composer row:
  - Restructure controls row:
    - Stack into two tiers on narrow widths: Tier 1 contains `AttachFileButton` and `ComboBox` (stretched to fill); Tier 2 contains `Send` and `Stop` buttons right-aligned.
    - Zero horizontal collision between model combo and send button at 440px.

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~AiAssistantTabControl_LayoutAudit"`
Expected: PASS with 0 violations.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Views/Controls/AiAssistantTabControl.axaml LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs
git commit -m "feat(ui): responsive chat layout and automated Avalonia layout inspector audit test"
```

---

### Task 5: Adaptive Master-Detail Documentation (`DocumentationTabControl.axaml`) & Layout Audit Tests

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/DocumentationViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/DocumentationTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs`

**Interfaces:**
- Consumes: `DocumentationViewModel.Sections`, `DocumentationViewModel.SelectedSection`
- Produces: `DocumentationViewModel.IsDetailActive`, `DocumentationViewModel.BackToTopicsCommand`, Adaptive master-detail layout

- [ ] **Step 1: Write failing layout audit test `DocumentationTabControl_LayoutAudit` in `AvaloniaLayoutAuditTests.cs`**

```csharp
[AvaloniaFact]
public void DocumentationTabControl_LayoutAudit()
{
    var vm = new MainViewModel();
    var control = new DocumentationTabControl { DataContext = vm.Documentation };
    var window = new Window { Content = control, Width = 440, Height = 700 };
    try
    {
        window.Show();

        var auditor = new LayoutAuditor();
        var options = CreateConfiguredAuditOptions(checkTouchErgonomics: false, checkTextClipping: false);
        var report = auditor.Audit(control, options);

        _output.WriteLine($"DocumentationTabControl Audit (440x700) - Health={report.HealthScore}/100, Violations={report.Violations.Count}");
        if (report.Violations.Count > 0)
        {
            _output.WriteLine(report.ToDetailedReport());
        }

        var appViolations = FilterAppViolations(report);
        Assert.Empty(appViolations);
    }
    finally
    {
        window.Close();
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test --filter "FullyQualifiedName~DocumentationTabControl_LayoutAudit"`
Expected: Reports boundary overflows or severe crowding from the fixed 280px sidebar at 440px width.

- [ ] **Step 3: Implement adaptive Master-Detail layout in `DocumentationViewModel.cs` and `DocumentationTabControl.axaml`**

In `DocumentationViewModel.cs`:
- Add `[ObservableProperty] private bool _isDetailActive;`
- In `SelectSection(string sectionId)`: set `IsDetailActive = true;`
- Add `[RelayCommand] private void BackToTopics() => IsDetailActive = false;`

In `DocumentationTabControl.axaml`:
- Use responsive layout:
  - When container width < 600px:
    - If `!IsDetailActive`: show full-width Topics list cards.
    - If `IsDetailActive`: show full-width Topic reading pane with header button "← Back to Topics" calling `BackToTopicsCommand`.
  - When container width >= 600px:
    - Show standard two-column side-by-side layout (`Grid ColumnDefinitions="280, *"`).

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test --filter "FullyQualifiedName~DocumentationTabControl_LayoutAudit"`
Expected: PASS with 0 violations.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/ViewModels/DocumentationViewModel.cs LocalLLMServerManager.Shared/Views/Controls/DocumentationTabControl.axaml LocalLLMServerManager.Tests/AvaloniaLayoutAuditTests.cs
git commit -m "feat(ui): adaptive master-detail documentation view with automated layout audit test"
```

---

### Task 6: Comprehensive Verification & Test Suite Execution

**Files:**
- Verify: Entire solution and repository

- [ ] **Step 1: Run all .NET unit and layout audit tests**

Run: `dotnet test`
Expected: 100% tests pass with 0 errors.

- [ ] **Step 2: Run frontend lint and TypeScript check**

Run: `npm run lint`
Run: `npx tsc --noEmit`
Expected: 0 lint errors, 0 type errors.

- [ ] **Step 3: Final Commit & Tagging (if applicable)**

```bash
git add -A
git commit -m "chore(ui): complete desktop/web UX alignment, responsive chat & docs, and layout audits"
```
