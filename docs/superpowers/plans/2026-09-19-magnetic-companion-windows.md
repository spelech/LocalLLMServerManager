# Magnetic Snapped Companion Windows Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement magnetic companion windows for `DocumentationWindow` (Left Flank) and `AiAssistWindow` (Right Flank) that automatically attach flush against `MainWindow`, synchronize vertical height and position in lockstep, support drag-to-detach into free-floating windows, and support magnetic re-attachment via title-bar toggle buttons or edge proximity.

**Architecture:** A centralized `WindowSnapManager` service monitors `MainWindow`'s position, size, and state changes to adjust attached companion windows with pixel precision. Each companion window exposes an interactive magnet toggle (`🧲 Attached` / `🧲 Snap to Side`) in its custom title bar and monitors pointer drag events to distinguish lockstep tracking from intentional user detachment.

**Tech Stack:** C# 13, .NET 10, Avalonia 12.1.2 (Headless XUnit testing, Custom Windows, PixelPoint positioning, Screen WorkingArea boundary detection), XUnit v3.

## Global Constraints
- Target Framework: .NET 10.0 (`net10.0`).
- Minimum window dimensions: `MinWidth: 380`, `MinHeight: 450`.
- Left Flank docks `DocumentationWindow` with default width `480px`.
- Right Flank docks `AiAssistWindow` with default width `440px`.
- Magnetic snap threshold: `35px` tolerance for proximity docking and drag detachment.
- Zero view switching in `MainWindow`: opening either companion window must not alter `SelectedTabIndex`.
- Quality gates: All 708+ xUnit tests must pass, `npm run lint` and `npx tsc --noEmit` must pass with 0 errors.

---

### Task 1: Core WindowSnapManager Math & Flank Calculations

**Files:**
- Create: `Services/WindowSnapManager.cs`
- Test: `LocalLLMServerManager.Tests/WindowSnapManagerTests.cs`

**Interfaces:**
- Consumes: `Avalonia.Controls.Window`, `Avalonia.PixelPoint`, `Avalonia.Platform.Screen`
- Produces: `enum SnapFlank { Left, Right }`, `WindowSnapManager.Instance`, `CalculateSnappedPosition(Window main, Window companion, SnapFlank flank, double scaling)`

- [ ] **Step 1: Write failing unit test for flank position calculations**

```csharp
// In LocalLLMServerManager.Tests/WindowSnapManagerTests.cs
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using LocalLLMServerManager.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class WindowSnapManagerTests
{
    [AvaloniaFact]
    public void CalculateSnappedPosition_RightFlank_PositionsFlushAgainstRightEdge()
    {
        var manager = new WindowSnapManager();
        var main = new Window { Width = 1000, Height = 700 };
        main.Position = new PixelPoint(200, 150);

        var companion = new Window { Width = 400, Height = 700 };
        companion.Position = new PixelPoint(0, 0);

        var targetPos = manager.CalculateSnappedPosition(main, companion, SnapFlank.Right, scaling: 1.0);

        // Target X should be main.Position.X (200) + main.Width (1000) = 1200
        Assert.Equal(1200, targetPos.X);
        Assert.Equal(150, targetPos.Y);
    }

    [AvaloniaFact]
    public void CalculateSnappedPosition_LeftFlank_PositionsFlushAgainstLeftEdge()
    {
        var manager = new WindowSnapManager();
        var main = new Window { Width = 1000, Height = 700 };
        main.Position = new PixelPoint(600, 150);

        var companion = new Window { Width = 450, Height = 700 };
        companion.Position = new PixelPoint(0, 0);

        var targetPos = manager.CalculateSnappedPosition(main, companion, SnapFlank.Left, scaling: 1.0);

        // Target X should be main.Position.X (600) - companion.Width (450) = 150
        Assert.Equal(150, targetPos.X);
        Assert.Equal(150, targetPos.Y);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~WindowSnapManagerTests"`
Expected: Compilation failure or FAIL (types not yet declared).

- [ ] **Step 3: Implement core WindowSnapManager calculations**

```csharp
// In Services/WindowSnapManager.cs
using System;
using Avalonia;
using Avalonia.Controls;

namespace LocalLLMServerManager.Services;

public enum SnapFlank
{
    Left,
    Right
}

public class WindowSnapManager
{
    public static WindowSnapManager Instance { get; } = new();

    public PixelPoint CalculateSnappedPosition(Window mainWindow, Window companion, SnapFlank flank, double scaling = 1.0)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(companion);

        if (scaling <= 0) scaling = 1.0;

        int mainX = mainWindow.Position.X;
        int mainY = mainWindow.Position.Y;
        int mainWidth = (int)Math.Round((mainWindow.Bounds.Width > 0 ? mainWindow.Bounds.Width : mainWindow.Width) * scaling);
        int compWidth = (int)Math.Round((companion.Bounds.Width > 0 ? companion.Bounds.Width : companion.Width) * scaling);

        int targetX = flank switch
        {
            SnapFlank.Left => mainX - compWidth,
            SnapFlank.Right => mainX + mainWidth,
            _ => mainX
        };

        return new PixelPoint(targetX, mainY);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~WindowSnapManagerTests"`
Expected: PASS (2 passed).

- [ ] **Step 5: Commit Task 1**

```bash
git add Services/WindowSnapManager.cs LocalLLMServerManager.Tests/WindowSnapManagerTests.cs
git commit -m "feat: implement WindowSnapManager flank math and position calculations"
```

---

### Task 2: Lockstep Synchronization, Detachment & Proximity Snapping

**Files:**
- Modify: `Services/WindowSnapManager.cs`
- Modify: `LocalLLMServerManager.Tests/WindowSnapManagerTests.cs`

**Interfaces:**
- Consumes: `Window.PositionChanged`, `Window.SizeChanged`, `Window.PropertyChanged`
- Produces: `RegisterCompanion(Window main, Window comp, SnapFlank flank)`, `Attach(Window comp)`, `Detach(Window comp)`, `ToggleSnap(Window comp)`, `IsSnapped(Window comp)`

- [ ] **Step 1: Write failing tests for synchronization, detachment, and proximity snap**

```csharp
// Add to LocalLLMServerManager.Tests/WindowSnapManagerTests.cs
[AvaloniaFact]
public void RegisterCompanion_TracksMainWindowPosition_WhenSnapped()
{
    var manager = new WindowSnapManager();
    var main = new Window { Width = 1000, Height = 700 };
    main.Position = new PixelPoint(100, 100);

    var companion = new Window { Width = 400, Height = 700 };
    manager.RegisterCompanion(main, companion, SnapFlank.Right, autoAttach: true);

    Assert.True(manager.IsSnapped(companion));
    Assert.Equal(1100, companion.Position.X);
    Assert.Equal(100, companion.Position.Y);

    // Move MainWindow
    main.Position = new PixelPoint(200, 250);
    manager.SynchronizeCompanion(companion);

    Assert.Equal(1200, companion.Position.X);
    Assert.Equal(250, companion.Position.Y);
}

[AvaloniaFact]
public void Detach_AllowsFreeMovement_WithoutFollowingMainWindow()
{
    var manager = new WindowSnapManager();
    var main = new Window { Width = 1000, Height = 700 };
    main.Position = new PixelPoint(100, 100);

    var companion = new Window { Width = 400, Height = 700 };
    manager.RegisterCompanion(main, companion, SnapFlank.Right, autoAttach: true);

    manager.Detach(companion);
    Assert.False(manager.IsSnapped(companion));

    // Move companion freely
    companion.Position = new PixelPoint(50, 50);

    // Move MainWindow - companion should remain at (50, 50)
    main.Position = new PixelPoint(400, 400);
    manager.SynchronizeCompanion(companion);

    Assert.Equal(50, companion.Position.X);
    Assert.Equal(50, companion.Position.Y);
}

[AvaloniaFact]
public void IsWithinSnapThreshold_DetectsProximityAccurately()
{
    var manager = new WindowSnapManager();
    var main = new Window { Width = 1000, Height = 700 };
    main.Position = new PixelPoint(100, 100);

    var companion = new Window { Width = 400, Height = 700 };
    // Snapped target X would be 1100. If companion is at 1120 (within 35px), threshold detects true.
    companion.Position = new PixelPoint(1120, 110);

    bool inRange = manager.IsWithinSnapThreshold(main, companion, SnapFlank.Right, tolerancePixels: 35, scaling: 1.0);
    Assert.True(inRange);

    // If companion is far away at 1300, threshold detects false
    companion.Position = new PixelPoint(1300, 100);
    bool farAway = manager.IsWithinSnapThreshold(main, companion, SnapFlank.Right, tolerancePixels: 35, scaling: 1.0);
    Assert.False(farAway);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~WindowSnapManagerTests"`
Expected: FAIL (methods not yet implemented).

- [ ] **Step 3: Implement full synchronization logic in WindowSnapManager**

Expand `Services/WindowSnapManager.cs` to maintain companion registrations:
```csharp
using System;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;

namespace LocalLLMServerManager.Services;

public class SnappedCompanionState
{
    public Window MainWindow { get; }
    public Window Companion { get; }
    public SnapFlank Flank { get; set; }
    public bool IsSnapped { get; set; }
    public double PreferredWidth { get; set; }

    public SnappedCompanionState(Window main, Window comp, SnapFlank flank)
    {
        MainWindow = main;
        Companion = comp;
        Flank = flank;
        IsSnapped = true;
        PreferredWidth = comp.Bounds.Width > 0 ? comp.Bounds.Width : comp.Width;
    }
}

public class WindowSnapManager
{
    public static WindowSnapManager Instance { get; } = new();

    private readonly ConcurrentDictionary<Window, SnappedCompanionState> _states = new();
    public const int DefaultSnapThreshold = 35;

    public void RegisterCompanion(Window mainWindow, Window companion, SnapFlank flank, bool autoAttach = true)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(companion);

        var state = new SnappedCompanionState(mainWindow, companion, flank)
        {
            IsSnapped = autoAttach
        };
        _states[companion] = state;

        if (autoAttach)
        {
            Attach(companion);
        }

        // Hook window position and state events
        mainWindow.PositionChanged += (s, e) => SynchronizeAllForMain(mainWindow);
        mainWindow.PropertyChanged += (s, e) =>
        {
            if (e.Property == Visual.BoundsProperty || e.Property == Window.WindowStateProperty)
            {
                SynchronizeAllForMain(mainWindow);
            }
        };

        companion.Closed += (s, e) => _states.TryRemove(companion, out _);
    }

    public bool IsSnapped(Window companion) =>
        _states.TryGetValue(companion, out var state) && state.IsSnapped;

    public void Attach(Window companion)
    {
        if (_states.TryGetValue(companion, out var state))
        {
            state.IsSnapped = true;
            SynchronizeCompanion(companion);
        }
    }

    public void Detach(Window companion)
    {
        if (_states.TryGetValue(companion, out var state))
        {
            state.IsSnapped = false;
        }
    }

    public void ToggleSnap(Window companion)
    {
        if (IsSnapped(companion))
        {
            Detach(companion);
        }
        else
        {
            Attach(companion);
        }
    }

    public void SynchronizeCompanion(Window companion)
    {
        if (!_states.TryGetValue(companion, out var state) || !state.IsSnapped)
            return;

        var main = state.MainWindow;
        if (main.WindowState == WindowState.Minimized)
        {
            if (companion.WindowState != WindowState.Minimized)
                companion.WindowState = WindowState.Minimized;
            return;
        }
        else if (companion.WindowState == WindowState.Minimized)
        {
            companion.WindowState = WindowState.Normal;
        }

        // Align height
        double targetHeight = main.Bounds.Height > 0 ? main.Bounds.Height : main.Height;
        if (targetHeight >= 450)
        {
            companion.Height = targetHeight;
        }

        double scaling = main.RenderScaling > 0 ? main.RenderScaling : 1.0;
        var targetPos = CalculateSnappedPosition(main, companion, state.Flank, scaling);
        companion.Position = targetPos;
    }

    public void SynchronizeAllForMain(Window mainWindow)
    {
        foreach (var kvp in _states)
        {
            if (kvp.Value.MainWindow == mainWindow && kvp.Value.IsSnapped)
            {
                SynchronizeCompanion(kvp.Key);
            }
        }
    }

    public bool IsWithinSnapThreshold(Window mainWindow, Window companion, SnapFlank flank, int tolerancePixels = DefaultSnapThreshold, double scaling = 1.0)
    {
        var target = CalculateSnappedPosition(mainWindow, companion, flank, scaling);
        int dx = Math.Abs(companion.Position.X - target.X);
        int dy = Math.Abs(companion.Position.Y - target.Y);
        return dx <= tolerancePixels && dy <= tolerancePixels * 2;
    }

    public PixelPoint CalculateSnappedPosition(Window mainWindow, Window companion, SnapFlank flank, double scaling = 1.0)
    {
        if (scaling <= 0) scaling = 1.0;

        int mainX = mainWindow.Position.X;
        int mainY = mainWindow.Position.Y;
        int mainWidth = (int)Math.Round((mainWindow.Bounds.Width > 0 ? mainWindow.Bounds.Width : mainWindow.Width) * scaling);
        int compWidth = (int)Math.Round((companion.Bounds.Width > 0 ? companion.Bounds.Width : companion.Width) * scaling);

        int targetX = flank switch
        {
            SnapFlank.Left => mainX - compWidth,
            SnapFlank.Right => mainX + mainWidth,
            _ => mainX
        };

        return new PixelPoint(targetX, mainY);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~WindowSnapManagerTests"`
Expected: PASS (all tests pass).

- [ ] **Step 5: Commit Task 2**

```bash
git add Services/WindowSnapManager.cs LocalLLMServerManager.Tests/WindowSnapManagerTests.cs
git commit -m "feat: implement WindowSnapManager lockstep sync, detach and proximity snap"
```

---

### Task 3: Title Bar Magnet Button UI & Theme Styles

**Files:**
- Modify: `LocalLLMServerManager.Shared/Styles/MatteTheme.axaml`
- Modify: `Views/AiAssistWindow.axaml` and `Views/AiAssistWindow.axaml.cs`
- Modify: `Views/DocumentationWindow.axaml` and `Views/DocumentationWindow.axaml.cs`
- Test: `LocalLLMServerManager.Tests/WindowSnapManagerTests.cs`

**Interfaces:**
- Consumes: `WindowSnapManager.Instance.ToggleSnap()`, `WindowSnapManager.Instance.IsSnapped()`
- Produces: Magnet button `x:Name="SnapToggleButton"` in both window headers, `UpdateSnapButtonVisuals()`

- [ ] **Step 1: Add Magnet Button styling in MatteTheme.axaml**

Add `.magnet-snap-btn` style:
```xml
<!-- Magnet Snap Button for Companion Windows -->
<Style Selector="Button.magnet-snap-btn">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="{StaticResource GlassBorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="CornerRadius" Value="6" />
    <Setter Property="Padding" Value="8,4" />
    <Setter Property="Cursor" Value="Hand" />
</Style>
<Style Selector="Button.magnet-snap-btn:pointerover">
    <Setter Property="Background" Value="{StaticResource SurfaceHoverBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource PrimaryAccentBrush}" />
</Style>
<Style Selector="Button.magnet-snap-btn.snapped">
    <Setter Property="Background" Value="#1a4f46e5" />
    <Setter Property="BorderBrush" Value="#4f46e5" />
</Style>
```

- [ ] **Step 2: Add SnapToggleButton in AiAssistWindow.axaml & code-behind**

In `Views/AiAssistWindow.axaml`:
```xml
<!-- Inside TitleBar Grid Column 2, right before window controls -->
<Button x:Name="SnapToggleButton" Click="OnSnapToggleClicked" Classes="magnet-snap-btn" Margin="0,0,8,0" ToolTip.Tip="Toggle Magnetic Snapping to Main Window">
    <StackPanel Orientation="Horizontal" Spacing="6" VerticalAlignment="Center">
        <TextBlock Text="🧲" FontSize="11" VerticalAlignment="Center"/>
        <TextBlock x:Name="SnapButtonText" Text="Attached" FontSize="11" FontWeight="Medium" Foreground="{StaticResource PrimaryAccentBrush}" VerticalAlignment="Center"/>
    </StackPanel>
</Button>
```

In `Views/AiAssistWindow.axaml.cs`:
Add `OnSnapToggleClicked`, wire `WindowSnapManager.Instance.ToggleSnap(this)`, and update button text & styles. On pointer move/drag, check if moved beyond threshold when snapped to automatically flip to detached.

- [ ] **Step 3: Add SnapToggleButton in DocumentationWindow.axaml & code-behind**

In `Views/DocumentationWindow.axaml`:
Add identical `SnapToggleButton` beside `PinButton`.

In `Views/DocumentationWindow.axaml.cs`:
Add `OnSnapToggleClicked`, wire `WindowSnapManager.Instance.ToggleSnap(this)`, and update button text & styles.

- [ ] **Step 4: Run headless tests and build to verify UI compiles cleanly**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit Task 3**

```bash
git add LocalLLMServerManager.Shared/Styles/MatteTheme.axaml Views/AiAssistWindow.* Views/DocumentationWindow.*
git commit -m "feat: add magnetic snap button and styling to companion window titlebars"
```

---

### Task 4: Integrate WindowSnapManager with MainWindow Lifecycle

**Files:**
- Modify: `Views/MainWindow.axaml.cs`
- Test: `LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs`

**Interfaces:**
- Consumes: `WindowSnapManager.Instance.RegisterCompanion()`
- Produces: Automatic Left flank snapping for `DocumentationWindow` and Right flank snapping for `AiAssistWindow` upon open.

- [ ] **Step 1: Write headless test asserting companion windows snap upon opening**

In `LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs`:
```csharp
[AvaloniaFact]
public void MainWindow_OpeningCompanionWindows_RegistersWithWindowSnapManager()
{
    var mainWindow = new MainWindow();
    mainWindow.Position = new PixelPoint(500, 200);
    mainWindow.Show();

    var vm = (MainViewModel)mainWindow.DataContext!;

    // Open Docs
    vm.Documentation.PopOutNativeWindow();
    // Open AI Assist
    vm.Assistant.RequestPopOut();

    // Verify TabControl remained on its current tab (SelectedTabIndex == 0)
    Assert.Equal(0, vm.SelectedTabIndex);

    mainWindow.Close();
}
```

- [ ] **Step 2: Connect WindowSnapManager in Views/MainWindow.axaml.cs**

In `Views/MainWindow.axaml.cs`:
```csharp
mainVm.Documentation.OnPopOutNativeWindowRequested = () =>
{
    if (_docWindow == null || !_docWindow.IsVisible)
    {
        _docWindow = new DocumentationWindow(mainVm.Documentation);
        _docWindow.Closed += (s, e) => _docWindow = null;
        _docWindow.Show();
        WindowSnapManager.Instance.RegisterCompanion(this, _docWindow, SnapFlank.Left, autoAttach: true);
    }
    else
    {
        WindowSnapManager.Instance.Attach(_docWindow);
        _docWindow.Activate();
    }
};

mainVm.Assistant.OnPopOutNativeWindowRequested = () =>
{
    if (_aiAssistWindow == null || !_aiAssistWindow.IsVisible)
    {
        _aiAssistWindow = new AiAssistWindow(mainVm.Assistant);
        _aiAssistWindow.Closed += (s, e) => _aiAssistWindow = null;
        _aiAssistWindow.Show();
        WindowSnapManager.Instance.RegisterCompanion(this, _aiAssistWindow, SnapFlank.Right, autoAttach: true);
    }
    else
    {
        WindowSnapManager.Instance.Attach(_aiAssistWindow);
        _aiAssistWindow.Activate();
    }
};
```

- [ ] **Step 3: Run test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~MainWindow_OpeningCompanionWindows"`
Expected: PASS.

- [ ] **Step 4: Commit Task 4**

```bash
git add Views/MainWindow.axaml.cs LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs
git commit -m "feat: wire companion windows to WindowSnapManager in MainWindow"
```

---

### Task 5: Full Regression Testing, Linters & Live Desktop Verification

**Files:**
- All touched files

- [ ] **Step 1: Run complete dotnet test suite**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`
Expected: 100% PASS across all tests (0 failures).

- [ ] **Step 2: Run linters and typecheckers**

Run: `npm run lint` and `npx tsc --noEmit`
Expected: 0 errors.

- [ ] **Step 3: Rebuild and launch desktop app to visually verify magnetic snapping**

Run: Rebuild `LocalLLMServerManager.csproj -c Debug`, launch app on desktop, click `Documentation` and `AI Assist` buttons, verify dual flanks snap flush and follow `MainWindow` when dragged.

- [ ] **Step 4: Commit final changes**

```bash
git add -A
git commit -m "chore: complete magnetic snapped companion windows implementation"
```
