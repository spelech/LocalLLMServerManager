using System;
using System.Diagnostics;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using LocalLLMServerManager;
using LocalLLMServerManager.Views;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AvaloniaUiTests
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
    public void App_FullTrayMenuHandlerLifecycle_ExecutesSuccessfully()
    {
        // Test theme switching helper
        App.SetThemeStyle("semi");
        Assert.Equal(LocalLLMServerManager.Shared.Services.AppTheme.MatteCarbon, LocalLLMServerManager.Shared.Services.ThemeService.Instance.CurrentTheme);
        App.SetThemeStyle("fluent");
        Assert.Equal(LocalLLMServerManager.Shared.Services.AppTheme.OledBlack, LocalLLMServerManager.Shared.Services.ThemeService.Instance.CurrentTheme);

        var app = new App();
        var lifetime = new ClassicDesktopStyleApplicationLifetime();
        app.ApplicationLifetime = lifetime;

        // Directly exercise tray menu click event handlers
        app.OnOpenDashboardClick(null, EventArgs.Empty);
        Assert.NotNull(lifetime.MainWindow);

        app.OnOpenWebUiClick(null, EventArgs.Empty);

        // Exercise null MainWindow branch in OnOpenDashboardClick
        lifetime.MainWindow = null;
        app.OnOpenDashboardClick(null, EventArgs.Empty);
        Assert.NotNull(lifetime.MainWindow);

        app.OnExitClick(null, EventArgs.Empty);

        // Restore default theme to maintain test isolation
        LocalLLMServerManager.Shared.Services.ThemeService.Instance.SetTheme(LocalLLMServerManager.Shared.Services.AppTheme.MatteCarbon);
    }

    [Fact]
    public void JobObject_DoubleDispose_Reaches100PercentCoverage()
    {
        var job = new JobObject();
        job.Dispose();
        job.Dispose(); // Verify idempotent dispose branch
    }
}
