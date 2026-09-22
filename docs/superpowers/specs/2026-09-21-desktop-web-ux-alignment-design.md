# Desktop & Web UX Alignment, Window Lifecycle, and Responsive Layout Design

## 1. Overview & Problem Statement

Recent UI iterations introduced features such as in-app slide-out drawers, companion pop-out windows, and an AI chat assistant. However, divergent requirements between the Desktop and Web (WASM) environments resulted in several UI/UX issues:

1. **Desktop vs. Web Side Panel Behavior**:
   - On Desktop, clicking the "Documentation" or "AI Assist" navigation buttons opened in-app slide-out drawers with modal backdrops that consumed significant workspace. On Desktop, these panels were designed to operate as attached/docked companion windows, not modal overlays.
   - On Web, slide-out drawers used an aggressive dark modal backdrop (`#66000000`) that blocked interaction with the underlying application, impeding reference during active workflows.

2. **Desktop Window Lifecycle & Docking**:
   - Attached/docked companion windows did not behave as a unified window unit with the `MainWindow`. Minimizing or maximizing the main window left companions unsynchronized, and detached companion windows remained floating orphaned on screen when the main window was minimized.

3. **Telemetry Header Real Estate on Compact / Tablet Displays**:
   - The top telemetry status header (3 fixed-width cards) occupied 80–100px of vertical space continuously. On horizontal tablets (e.g., 1024×768, 1280×800) and compact displays, this severely reduced the vertical viewport for primary content.

4. **Crowded Chat and Documentation Layouts**:
   - At default companion window widths (~440px) or drawer widths (~460–480px), `AiAssistantTabControl` suffered from horizontal collisions between composer controls (attachment button, model selector, send/stop buttons) and unwrapped header text.
   - `DocumentationTabControl` used a rigid `280px, *` two-column grid, compressing the content reading pane to ~160px when docked, causing text truncation and unusable horizontal crowding.
   - Automated layout inspection tests (`Avalonia.LayoutInspector`) audited general tab views but omitted dedicated audit fixtures for `AiAssistantTabControl` and `DocumentationTabControl`.

---

## 2. Core Architectural & UX Changes

### 2.1. Platform-Specific Side Panel Routing

- **Desktop (`MainWindow.axaml.cs`)**:
  - Clicking the `Documentation` or `AI Assist` buttons in the tab bar triggers the companion window toggle directly (`ToggleCompanionWindow`):
    - If closed: Instantiates and displays the window, docking it to its default flank (`Left` for Documentation, `Right` for AI Assist).
    - If open & attached: Toggles visibility or focuses the window.
  - The in-app slide-out drawer elements and dimmer backdrops are disabled/hidden on Desktop.
- **Web (`MainView.axaml`)**:
  - The in-app slide-out drawers remain the primary presentation mechanism because Web lacks multi-window OS primitives.
  - The dimmer backdrop (`IsVisible="{Binding IsAnyDrawerOpen}"`) is removed or set to non-modal (`IsHitTestVisible="False"` and transparent) so users can interact with models, workflows, and settings while referencing docs or chatting.

### 2.2. Unified Desktop Window Lifecycle (`WindowSnapManager`)

1. **Owner-Child Relationship**:
   - When instantiating companion windows (`DocumentationWindow`, `AiAssistWindow`), their `Owner` property is assigned to `MainWindow`. This ensures OS taskbar grouping and native z-order cohesion.
2. **Synchronized Minimizing**:
   - When `MainWindow.WindowState == WindowState.Minimized`:
     - Both **attached** and **detached** companion windows immediately minimize. They are subordinate to the main application session.
3. **Synchronized Restoring**:
   - When `MainWindow.WindowState` changes from `Minimized` to `Normal` or `Maximized`:
     - All active companion windows restore to `WindowState.Normal`.
     - Attached companions are repositioned to their docked flank coordinates via `CalculateSnappedPosition`.
4. **Docked Window Move & Maximize Handling**:
   - When `MainWindow` maximizes, attached companions maintain their docked edge alignment along the display boundary without occluding primary UI controls or being pushed into unreachable coordinates.
   - If an attached companion's minimize button is clicked, it minimizes alongside the main application rather than breaking docking state.

### 2.3. Responsive Telemetry Header

- **Compact Ribbon Mode**:
  - Add an `IsCollapsed` / `IsCompact` state to `TelemetryViewModel` and `TelemetryHeaderControl`.
  - On viewports with height < 800px or width < 900px (such as tablets in landscape mode or compact desktop windows), the header automatically collapses into a slim 32px status ribbon showing:
    - Service status indicator dots (Ollama, Forge, ComfyUI).
    - Compact VRAM badge (% and GPU name).
    - Chevron toggle button (▼ / ▲) to manually expand or collapse the full 3-card telemetry dashboard on demand.
  - Manual toggle preference persists across sessions or is remembered in-session.

### 2.4. Adaptive Chat UI (`AiAssistantTabControl.axaml`)

- **Header Density**:
  - Replace rigid `Auto, *, Auto` 3-column layout with a responsive container. Subtitle text wraps or condenses gracefully at narrow widths (<600px).
  - Model indicator and generation status pills wrap into an inline status row when width is insufficient.
- **Adaptive Setup Wizard**:
  - Replace `Grid ColumnDefinitions="*, *, *"` with an adaptive layout that stacks the Endpoint URL, API Key, and Model Selector vertically on viewports under 600px, giving each field sufficient width and clear labeling.
- **Stacked Composer Toolbar**:
  - Below the multiline prompt `TextBox`, restructure the controls:
    - Tier 1: `AttachFileButton` and `ComboBox` (stretched to fill horizontal space).
    - Tier 2: `SendButton` / `StopButton` aligned right with clear action styling, ensuring zero horizontal collision at 440px.

### 2.5. Adaptive Documentation Master-Detail (`DocumentationTabControl.axaml`)

- **Adaptive Master-Detail Pattern**:
  - Replace rigid `280, *` column grid with an adaptive view:
    - **Wide Mode (≥ 600px)**: Two-column layout (sidebar topics list + main reading pane).
    - **Narrow / Companion Mode (< 600px)**:
      - **Topic List State**: Displays full-width guide topic cards.
      - **Topic Detail State**: When a topic is clicked, transitions to the full-width topic content reader. The header displays a prominent **"← Back to Topics"** button to return to the selection list.
      - Eliminates horizontal text crushing and ensures readability at 440px companion width.

---

## 3. Testing & Verification Plan

### 3.1. Avalonia.LayoutInspector Audits (`AvaloniaLayoutAuditTests.cs`)

1. **`AiAssistantTabControl_LayoutAudit`**:
   - Breakpoints tested:
     - Companion Window: 440 × 700.
     - Web Drawer: 480 × 700.
     - Tablet: 768 × 1024.
     - Standard Desktop: 1280 × 800.
   - Assertions:
     - `CheckBoundaryOverflow = true`
     - `CheckSiblingCollisions = true`
     - `CheckTextClipping = true`
     - Asserts 0 visual overlaps or collisions.
2. **`DocumentationTabControl_LayoutAudit`**:
   - Breakpoints tested:
     - Companion Window (Master View): 440 × 700.
     - Companion Window (Detail View): 440 × 700.
     - Standard Desktop (Wide 2-Column View): 1280 × 800.
   - Assertions:
     - Zero boundary overflows and zero collisions between sidebar cards and reading pane.

### 3.2. Window Lifecycle Unit Tests (`WindowSnapManagerTests.cs`)

- Test `MainWindow_Minimize_SynchronizesAllCompanions`:
  - MainWindow minimized $\rightarrow$ verify snapped and detached companions both minimize.
- Test `MainWindow_Restore_SynchronizesAllCompanions`:
  - MainWindow restored $\rightarrow$ verify companions restore and snapped companions align positions.
- Test `AttachedCompanion_FollowsMainWindowState`:
  - Snapped companion follows resize and bounds changes cleanly.

### 3.3. Regression Verification
- Run `dotnet test` (all unit and Avalonia headless layout tests pass).
- Run `npm run lint` and `npx tsc --noEmit` to verify TypeScript and lint conformance.
