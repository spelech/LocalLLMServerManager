# Deterministic Testing Guidelines & Quality Standards

> **Mandate:** All new and refactored code in LocalLLMServerManager must adhere to these deterministic testing rules. Test theatre, tautological assertions, and zombie backwards-compatibility for superseded UI components are strictly prohibited.

---

## 1. Clean Architectural Break (No Zombie Backwards-Compatibility)

When a UI paradigm, component, or workflow is superseded:
- **Do not retain legacy shims or dual-state synchronization** just to keep obsolete unit tests green.
- **Retire obsolete tests immediately** and replace them with tests targeting the actual, active architecture.
- If a slide-out drawer is replaced by a docked sidebar, remove the obsolete drawer properties (`IsDocumentationDrawerOpen`, `IsAiAssistDrawerOpen`) instead of artificially mirroring them to the new sidebar state.
- **Truth over green:** A failing test on obsolete code is a signal to update or delete the test, not to add boilerplate backwards-compatibility shims to production code.

---

## 2. Zero Test Theatre (No Hollow Tests)

Tests must assert real contracts, state mutations, and system behavior:
- **No Exception Swallowing:** Absolute ban on `try { ... } catch { }` blocks in test methods. Tests must never swallow errors to artificially pass.
- **No Tautological Assertions:**
  - Do NOT test default auto-property values without exercising behavior (e.g. `Assert.Equal("Image", vm.SelectedModality)` alone is insufficient without testing the selection transition).
  - Do NOT assert mock return values without verifying that business logic consumed them correctly.
  - Do NOT write assertions that are tautologically true by construction (`Assert.True(x == x)`).
- **Verify Real Side Effects:** Tests must verify state changes, collection mutations, command dispatch, or external contract invocation.

---

## 3. Mandatory Avalonia Visual Tree Headless Testing for UI

Testing ViewModels in isolation is necessary but **not sufficient** for UI quality:
- **The ViewModel Blind Spot:** A ViewModel unit test will pass even if the XAML binding is completely broken (e.g. misspelled property name, missing prefix, or unattached event).
- **Every interactive UI component must have headless visual tree tests (`[AvaloniaFact]`)**:
  1. Mount the control in a headless `Window` and pump the UI thread (`Avalonia.Threading.Dispatcher.UIThread.RunJobs()`).
  2. Locate target controls (buttons, inputs, flyouts, status indicators) using visual tree traversal (`GetVisualDescendants()`).
  3. Simulate user interactions (command execution, input entry).
  4. Verify that the visual tree reflects the change (`IsVisible`, text content, classes, layout).

---

## 4. Multi-Breakpoint Responsive Layout Audits

Every major view (`MainView`, `EngineStudioTabControl`, `TelemetryRibbonControl`, `DocumentationTabControl`) must be audited with `Avalonia.LayoutInspector`:
- Must pass zero `LAYOUT001_OVERFLOW` (elements extending beyond parent boundaries without clipping/scrolling).
- Must pass zero `LAYOUT002_COLLISION` (unintended sibling overlap).
- Must verify across all supported breakpoints:
  - **Tablet Landscape:** 1024 × 768
  - **Standard Desktop:** 1280 × 800
  - **Widescreen:** 1440 × 900

---

## 5. Verification Gate (Evidence Before Assertion)

Before claiming any task is complete or merging any branch:
1. `dotnet test LocalLLMServerManager.Tests` must pass 100% of non-skipped tests.
2. `npm run lint` must exit with 0 errors.
3. `npx tsc --noEmit` must exit with 0 errors.
4. `pwsh scripts/fast_update.ps1 -NoLaunch` must build clean WASM and Desktop binaries.
