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
