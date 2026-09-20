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
