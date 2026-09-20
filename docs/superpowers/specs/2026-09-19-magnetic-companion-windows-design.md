# Magnetic Snapped Companion Windows Design Specification

## Overview
This specification details the architecture, UI controls, positioning synchronization, and lifecycle management for **Magnetic Snapped Companion Windows** in `LocalLLMServerManager`. 

`DocumentationWindow` docks flush to the **left flank** of `MainWindow`, while `AiAssistWindow` docks flush to the **right flank**. Both windows match `MainWindow`'s vertical height and position in lockstep when attached, preserve independent width adjustments, support drag-to-detach into free-floating windows, and support magnetic re-attachment via a title-bar toggle or proximity snapping.

---

## 1. Window Architecture & States

### 1.1 Snap Flank Placement
- **MainWindow**: The central application host containing the 4 primary workspaces: Models, Workflows, Can I Run It, and Settings.
- **DocumentationWindow (Left Flank)**:
  - Default width: `480px`.
  - Target Position when snapped:
    $$\text{Target X} = \text{MainWindow.Position.X} - (\text{DocWindow.Bounds.Width} \times \text{Scaling})$$
    $$\text{Target Y} = \text{MainWindow.Position.Y}$$
    $$\text{Target Height} = \text{MainWindow.Bounds.Height}$$
- **AiAssistWindow (Right Flank)**:
  - Default width: `440px`.
  - Target Position when snapped:
    $$\text{Target X} = \text{MainWindow.Position.X} + (\text{MainWindow.Bounds.Width} \times \text{Scaling})$$
    $$\text{Target Y} = \text{MainWindow.Position.Y}$$
    $$\text{Target Height} = \text{MainWindow.Bounds.Height}$$

### 1.2 State Model (`WindowSnapManager`)
Each companion window registered with `WindowSnapManager` tracks:
- `CompanionWindow`: The managed `Window` instance.
- `Flank`: Enum (`Left`, `Right`).
- `IsSnapped`: Boolean (`true` = magnetically docked and following `MainWindow`; `false` = free-floating).
- `UserWidth`: Double (stores the user's preferred width, preserved across snap and detach).
- `Minimum Constraints`:
  - `MinWidth`: `380px`
  - `MinHeight`: `450px`

---

## 2. Synchronization & Movement Mechanics

### 2.1 MainWindow Tracking
- When `MainWindow` fires `PositionChanged`:
  - If `DocumentationWindow.IsSnapped`, update its `Position` so its right edge remains flush against `MainWindow`'s left edge.
  - If `AiAssistWindow.IsSnapped`, update its `Position` so its left edge remains flush against `MainWindow`'s right edge.
- When `MainWindow` fires `SizeChanged` / `Bounds` change:
  - Snapped companion windows synchronize their `Height` to match `MainWindow.Bounds.Height`.
  - Snapped companion windows recompute horizontal position if `MainWindow`'s width changed.
- When `MainWindow` fires `WindowState` changes:
  - `WindowState.Minimized`: Snapped companion windows hide or minimize in lockstep.
  - `WindowState.Normal` or `WindowState.Maximized`: Snapped companion windows restore and realign to their respective flanks.

### 2.2 Detachment & Free-Floating Mode
- **Drag-to-Detach**:
  - When the user drags a companion window by its title bar, if the delta between its actual position and the expected snapped position exceeds `SnapTolerance` (35 physical pixels), `IsSnapped` transitions to `false`.
  - Once `IsSnapped == false`, moving `MainWindow` no longer displaces the companion window.
  - In detached mode, the companion window can be resized in all directions (subject to `MinWidth: 380`, `MinHeight: 450`).

### 2.3 Re-Snapping Mechanics
- **Title Bar Magnet Toggle**:
  - A button in the custom title bar of both `AiAssistWindow` and `DocumentationWindow`.
  - Visual state:
    - Snapped: `🧲 Attached` with accent border and active highlight. Clicking it sets `IsSnapped = false` (detaches in place).
    - Detached: `🧲 Snap to Side` with subtle border. Clicking it recomputes flank position, snaps flush, synchronizes height, and sets `IsSnapped = true`.
- **Proximity Snapping**:
  - When dragging a detached window, if its dock edge comes within `35px` of `MainWindow`'s corresponding flank (and vertical overlap is $> 50\%$), it magnetically snaps flush and re-engages `IsSnapped = true`.

### 2.4 Multi-Monitor & Screen Boundary Safety
- Clamping is applied using `Screens.ScreenFromVisual`:
  - If single-monitor and `MainWindow` is at `X = 0`, `DocumentationWindow` clamps to the working area left boundary without disappearing off-screen.
  - If multi-monitor, negative coordinates across displays are fully supported.

---

## 3. UI Controls & Styling

### 3.1 Title Bar Controls
Both `AiAssistWindow.axaml` and `DocumentationWindow.axaml` will feature:
```xml
<!-- Magnet Snap Toggle -->
<Button Classes="magnet-snap-btn"
        Command="{Binding ToggleSnapCommand}"
        ToolTip.Tip="Toggle Magnetic Snapping to Main Window">
    <StackPanel Orientation="Horizontal" Spacing="6">
        <TextBlock Text="🧲" FontSize="12" />
        <TextBlock Text="{Binding SnapButtonText}" FontSize="11" FontWeight="Medium" />
    </StackPanel>
</Button>
```
Styling in `MatteTheme.axaml`:
- Default (snapped): Accent border (`#4f46e5` / `#06b6d4`), subtle glow background.
- Detached: `#27272a` background, `#52525b` border, subtle hover highlight.

### 3.2 Main Navigation Bar Integration
- Top segmented navigation bar buttons:
  - `NavAiAssistBtn`: Opens `AiAssistWindow`, ensures it is snapped to the Right Flank, and brings to front.
  - `NavDocumentationBtn`: Opens `DocumentationWindow`, ensures it is snapped to the Left Flank, and brings to front.
  - Workspace tabs (`Models`, `Workflows`, `Can I Run It`, `Settings`) remain stable and unselected when opening either companion.

---

## 4. Testing & Verification

### 4.1 Automated Headless Tests (`LocalLLMServerManager.Tests`)
1. `WindowSnapManager_CalculatesFlanksAccurately`:
   - Checks Left and Right position math against DPI scaling factors.
2. `WindowSnapManager_FollowsMainWindowMovement`:
   - Verifies companion positions change by exact $\Delta X, \Delta Y$ when `MainWindow.Position` shifts.
3. `WindowSnapManager_SyncsHeight`:
   - Verifies companion height changes when `MainWindow.Bounds.Height` shifts.
4. `WindowSnapManager_DetachOnDrag`:
   - Simulates companion position delta $> 35px$ and asserts `IsSnapped == false`.
5. `WindowSnapManager_ToggleSnap`:
   - Invokes toggle command and asserts transition between snapped and free-floating.
6. `WindowSnapManager_ProximitySnap`:
   - Positions companion within $30px$ of target flank and asserts `IsSnapped == true`.

### 4.2 Quality Gates
- `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj` (zero failures).
- `npm run lint` and `npx tsc --noEmit` (clean).
- Desktop GUI verification with live window management.
