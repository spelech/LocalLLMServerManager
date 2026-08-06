using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LocalLLMServerManager.Views;

namespace LocalLLMServerManager;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new MainWindow();
            try { desktop.MainWindow.Show(); } catch { }
        }

        var icons = TrayIcon.GetIcons(this);
        if (icons != null)
        {
            foreach (var icon in icons)
            {
                icon.IsVisible = true;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void OnOpenDashboardClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow == null)
            {
                desktop.MainWindow = new MainWindow();
            }
            try
            {
                desktop.MainWindow.Show();
                desktop.MainWindow.Activate();
            }
            catch { }
        }
    }

    public void OnOpenWebUiClick(object? sender, EventArgs e)
    {
        LocalLLMServerManager.Shared.Services.BrowserLauncher.OpenUrl("http://127.0.0.1:5246");
    }

    public void OnExitClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
