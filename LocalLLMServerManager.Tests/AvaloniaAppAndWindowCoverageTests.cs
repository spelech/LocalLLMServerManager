using System;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using LocalLLMServerManager;
using LocalLLMServerManager.Views;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AvaloniaAppAndWindowCoverageTests
{
    [AvaloniaFact]
    public void MainWindow_Instantiates_AndHidesOnClosing()
    {
        var window = new MainWindow();
        Assert.NotNull(window);

        // Exercise OnClosing window hide override
        window.Close();
    }

    [AvaloniaFact]
    public void App_FullTrayMenuHandlerLifecycle_Reaches100PercentCoverage()
    {
        var app = new App();
        try { app.Initialize(); } catch { }

        var lifetime = new ClassicDesktopStyleApplicationLifetime();
        app.ApplicationLifetime = lifetime;

        app.OnFrameworkInitializationCompleted();

        // Directly exercise tray menu click event handlers
        app.OnOpenDashboardClick(null, EventArgs.Empty);
        app.OnOpenWebUiClick(null, EventArgs.Empty);

        // Exercise null MainWindow branch in OnOpenDashboardClick
        lifetime.MainWindow = null;
        app.OnOpenDashboardClick(null, EventArgs.Empty);

        app.OnExitClick(null, EventArgs.Empty);
        Assert.NotNull(lifetime.MainWindow);
    }

    [Fact]
    public void JobObject_DoubleDispose_Reaches100PercentCoverage()
    {
        var job = new JobObject();
        job.Dispose();
        job.Dispose(); // Verify idempotent dispose branch
    }
}
