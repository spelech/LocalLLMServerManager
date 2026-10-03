using System;
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
