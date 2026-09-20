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

    [AvaloniaFact]
    public void CalculateSnappedPosition_WithFractionalScaling_CalculatesCorrectly()
    {
        var manager = new WindowSnapManager();
        var main = new Window { Width = 1000, Height = 700 };
        main.Position = new PixelPoint(100, 100);

        var companion = new Window { Width = 400, Height = 700 };
        var target = manager.CalculateSnappedPosition(main, companion, SnapFlank.Right, scaling: 1.25);

        // 1000 * 1.25 = 1250, target X = 100 + 1250 = 1350
        Assert.Equal(1350, target.X);
        Assert.Equal(100, target.Y);
    }

    [AvaloniaFact]
    public void SynchronizeCompanion_ClampsHeightToMinimum450()
    {
        var manager = new WindowSnapManager();
        var main = new Window { Width = 1000, Height = 300 };
        var companion = new Window { Width = 400, Height = 300 };

        manager.RegisterCompanion(main, companion, SnapFlank.Right, autoAttach: true);
        manager.SynchronizeCompanion(companion);

        Assert.Equal(450, companion.Height);
    }

    [AvaloniaFact]
    public void ArgumentValidation_ThrowsArgumentNullException_WhenNullPassed()
    {
        var manager = new WindowSnapManager();
        var window = new Window();

        Assert.Throws<ArgumentNullException>(() => manager.RegisterCompanion(null!, window, SnapFlank.Right));
        Assert.Throws<ArgumentNullException>(() => manager.RegisterCompanion(window, null!, SnapFlank.Right));
        Assert.Throws<ArgumentNullException>(() => manager.IsSnapped(null!));
        Assert.Throws<ArgumentNullException>(() => manager.Attach(null!));
        Assert.Throws<ArgumentNullException>(() => manager.Detach(null!));
        Assert.Throws<ArgumentNullException>(() => manager.ToggleSnap(null!));
        Assert.Throws<ArgumentNullException>(() => manager.SynchronizeCompanion(null!));
        Assert.Throws<ArgumentNullException>(() => manager.SynchronizeAllForMain(null!));
        Assert.Throws<ArgumentNullException>(() => manager.IsWithinSnapThreshold(null!, window, SnapFlank.Right));
        Assert.Throws<ArgumentNullException>(() => manager.IsWithinSnapThreshold(window, null!, SnapFlank.Right));
    }

    [AvaloniaFact]
    public void RegisterCompanion_MultipleCompanions_SharesMainWindowHookSafely()
    {
        var manager = new WindowSnapManager();
        var main = new Window { Width = 1000, Height = 700 };
        main.Position = new PixelPoint(100, 100);

        var comp1 = new Window { Width = 300, Height = 700 };
        var comp2 = new Window { Width = 400, Height = 700 };

        manager.RegisterCompanion(main, comp1, SnapFlank.Left, autoAttach: true);
        manager.RegisterCompanion(main, comp2, SnapFlank.Right, autoAttach: true);

        // Move MainWindow and trigger sync for all
        main.Position = new PixelPoint(200, 200);
        manager.SynchronizeAllForMain(main);

        Assert.Equal(200 - 300, comp1.Position.X);
        Assert.Equal(200 + 1000, comp2.Position.X);
    }
}


