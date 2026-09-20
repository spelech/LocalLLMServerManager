using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LocalLLMServerManager.Shared.ViewModels;

using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.Views;

namespace LocalLLMServerManager.Views;

public partial class MainWindow : Window
{
    private DocumentationWindow? _docWindow;
    private AiAssistWindow? _aiAssistWindow;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        var mainVm = new MainViewModel(
            httpClient: null,
            new TelemetryService(),
            new OllamaModelService(),
            new HuggingFaceSearchService(),
            new CivitaiSearchService(),
            new StudioPresetService(),
            new CanIRunItService(),
            assistantService: null,
            settingsService: new SettingsService(),
            promptService: new PromptManagementService());
        DataContext = mainVm;

        mainVm.Documentation.OnPopOutNativeWindowRequested = () =>
        {
            if (_docWindow == null || !_docWindow.IsVisible)
            {
                _docWindow = new DocumentationWindow(mainVm.Documentation);
                _docWindow.Closed += (s, e) => _docWindow = null;
                _docWindow.Show();
            }
            else
            {
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
            }
            else
            {
                _aiAssistWindow.Activate();
            }
        };

        PropertyChanged += (sender, e) =>
        {
            if (e.Property == WindowStateProperty)
            {
                var maxIcon = this.FindControl<TextBlock>("MaximizeIcon");
                if (maxIcon != null)
                {
                    maxIcon.Text = WindowState == WindowState.Maximized ? "❐" : "🗖";
                }
            }
        };
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClicked(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Hide window to system tray when user clicks X close button
        e.Cancel = true;
        Hide();
    }
}
