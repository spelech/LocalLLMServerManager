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

    public DocumentationWindow? DocWindow => _docWindow;
    public AiAssistWindow? AiAssistWindow => _aiAssistWindow;

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
        mainVm.IsDesktopHost = true;
        DataContext = mainVm;

        mainVm.Documentation.OnPopOutNativeWindowRequested = () =>
        {
            mainVm.IsCopilotSidebarOpen = false;
            if (_docWindow == null)
            {
                _docWindow = new DocumentationWindow(mainVm.Documentation);
                _docWindow.Closed += (s, e) => _docWindow = null;
                WindowSnapManager.SetOwner(_docWindow, this);
                WindowSnapManager.Instance.RegisterCompanion(this, _docWindow, SnapFlank.Left, autoAttach: true);
                _docWindow.Show();
                _docWindow.UpdateSnapVisuals();
                _docWindow.Activate();
            }
            else if (!_docWindow.IsVisible)
            {
                WindowSnapManager.SetOwner(_docWindow, this);
                _docWindow.Show();
                WindowSnapManager.Instance.Attach(_docWindow);
                _docWindow.UpdateSnapVisuals();
                _docWindow.Activate();
            }
            else
            {
                _docWindow.Activate();
            }
        };

        mainVm.Assistant.OnPopOutNativeWindowRequested = () =>
        {
            mainVm.IsCopilotSidebarOpen = false;
            if (_aiAssistWindow == null)
            {
                _aiAssistWindow = new AiAssistWindow(mainVm.Assistant);
                _aiAssistWindow.Closed += (s, e) => _aiAssistWindow = null;
                WindowSnapManager.SetOwner(_aiAssistWindow, this);
                WindowSnapManager.Instance.RegisterCompanion(this, _aiAssistWindow, SnapFlank.Right, autoAttach: true);
                _aiAssistWindow.Show();
                _aiAssistWindow.UpdateSnapVisuals();
                _aiAssistWindow.Activate();
            }
            else if (!_aiAssistWindow.IsVisible)
            {
                WindowSnapManager.SetOwner(_aiAssistWindow, this);
                _aiAssistWindow.Show();
                WindowSnapManager.Instance.Attach(_aiAssistWindow);
                _aiAssistWindow.UpdateSnapVisuals();
                _aiAssistWindow.Activate();
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
