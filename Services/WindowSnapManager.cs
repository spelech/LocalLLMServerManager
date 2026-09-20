using System;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;

namespace LocalLLMServerManager.Services;

public enum SnapFlank
{
    Left,
    Right
}

public class SnappedCompanionState
{
    public Window MainWindow { get; }
    public Window Companion { get; }
    public SnapFlank Flank { get; set; }
    public bool IsSnapped { get; set; }
    public double PreferredWidth { get; set; }

    public SnappedCompanionState(Window main, Window comp, SnapFlank flank)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(comp);

        MainWindow = main;
        Companion = comp;
        Flank = flank;
        IsSnapped = true;
        double w = comp.Bounds.Width > 0 ? comp.Bounds.Width : comp.Width;
        PreferredWidth = double.IsFinite(w) && w > 0 ? w : 440;
    }
}

public class WindowSnapManager
{
    public static WindowSnapManager Instance { get; } = new();

    private readonly ConcurrentDictionary<Window, SnappedCompanionState> _states = new();
    private readonly ConcurrentDictionary<Window, byte> _hookedMainWindows = new();
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

        // Deduplicate event subscriptions on MainWindow
        if (_hookedMainWindows.TryAdd(mainWindow, 0))
        {
            mainWindow.PositionChanged += (s, e) => SynchronizeAllForMain(mainWindow);
            mainWindow.PropertyChanged += (s, e) =>
            {
                if (e.Property == Visual.BoundsProperty || e.Property == Window.WindowStateProperty)
                {
                    SynchronizeAllForMain(mainWindow);
                }
            };
            mainWindow.Closed += (s, e) => _hookedMainWindows.TryRemove(mainWindow, out _);
        }

        companion.Closed += (s, e) => _states.TryRemove(companion, out _);
    }

    public bool IsSnapped(Window companion)
    {
        ArgumentNullException.ThrowIfNull(companion);
        return _states.TryGetValue(companion, out var state) && state.IsSnapped;
    }

    public void Attach(Window companion)
    {
        ArgumentNullException.ThrowIfNull(companion);
        if (_states.TryGetValue(companion, out var state))
        {
            state.IsSnapped = true;
            SynchronizeCompanion(companion);
        }
    }

    public void Detach(Window companion)
    {
        ArgumentNullException.ThrowIfNull(companion);
        if (_states.TryGetValue(companion, out var state))
        {
            state.IsSnapped = false;
        }
    }

    public void ToggleSnap(Window companion)
    {
        ArgumentNullException.ThrowIfNull(companion);
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
        ArgumentNullException.ThrowIfNull(companion);
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

        // Align height (clamp minimum 450)
        double targetHeight = main.Bounds.Height > 0 ? main.Bounds.Height : main.Height;
        if (double.IsFinite(targetHeight))
        {
            companion.Height = Math.Max(450, targetHeight);
        }

        double scaling = main.RenderScaling > 0 ? main.RenderScaling : 1.0;
        var targetPos = CalculateSnappedPosition(main, companion, state.Flank, scaling);
        companion.Position = targetPos;
    }

    public void SynchronizeAllForMain(Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

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
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(companion);

        var target = CalculateSnappedPosition(mainWindow, companion, flank, scaling);
        int dx = Math.Abs(companion.Position.X - target.X);
        int dy = Math.Abs(companion.Position.Y - target.Y);
        return dx <= tolerancePixels && dy <= tolerancePixels * 2;
    }

    public PixelPoint CalculateSnappedPosition(Window mainWindow, Window companion, SnapFlank flank, double scaling = 1.0)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(companion);

        if (scaling <= 0) scaling = 1.0;

        int mainX = mainWindow.Position.X;
        int mainY = mainWindow.Position.Y;
        
        double rawMainW = mainWindow.Bounds.Width > 0 ? mainWindow.Bounds.Width : mainWindow.Width;
        double rawCompW = companion.Bounds.Width > 0 ? companion.Bounds.Width : companion.Width;
        if (!double.IsFinite(rawMainW) || rawMainW <= 0) rawMainW = 1024;
        if (!double.IsFinite(rawCompW) || rawCompW <= 0) rawCompW = 440;

        int mainWidth = (int)Math.Round(rawMainW * scaling);
        int compWidth = (int)Math.Round(rawCompW * scaling);

        int targetX = flank switch
        {
            SnapFlank.Left => mainX - compWidth,
            SnapFlank.Right => mainX + mainWidth,
            _ => mainX
        };

        return new PixelPoint(targetX, mainY);
    }
}
