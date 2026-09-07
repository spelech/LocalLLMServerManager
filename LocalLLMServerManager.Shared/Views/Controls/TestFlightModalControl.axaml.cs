using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class TestFlightModalControl : UserControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<TestFlightModalControl, bool>(nameof(IsOpen), true);

    public static readonly StyledProperty<StudioModality> SelectedModalityProperty =
        AvaloniaProperty.Register<TestFlightModalControl, StudioModality>(nameof(SelectedModality), StudioModality.Video, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<ICommand?> SelectModalityCommandProperty =
        AvaloniaProperty.Register<TestFlightModalControl, ICommand?>(nameof(SelectModalityCommand));

    public static readonly StyledProperty<IEnumerable?> StarterPromptsProperty =
        AvaloniaProperty.Register<TestFlightModalControl, IEnumerable?>(nameof(StarterPrompts));

    public static readonly StyledProperty<StudioPreset?> SelectedStarterPromptProperty =
        AvaloniaProperty.Register<TestFlightModalControl, StudioPreset?>(nameof(SelectedStarterPrompt), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> EngineStatusTextProperty =
        AvaloniaProperty.Register<TestFlightModalControl, string>(nameof(EngineStatusText), "✓ ComfyUI Online");

    public static readonly StyledProperty<bool> IsEngineOnlineProperty =
        AvaloniaProperty.Register<TestFlightModalControl, bool>(nameof(IsEngineOnline), true);

    public static readonly StyledProperty<string> VramStatusTextProperty =
        AvaloniaProperty.Register<TestFlightModalControl, string>(nameof(VramStatusText), "✓ VRAM Clearance: 8.5 GB free");

    public static readonly StyledProperty<bool> IsVramClearProperty =
        AvaloniaProperty.Register<TestFlightModalControl, bool>(nameof(IsVramClear), true);

    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<TestFlightModalControl, bool>(nameof(IsRunning), false);

    public static readonly StyledProperty<double> ProgressValueProperty =
        AvaloniaProperty.Register<TestFlightModalControl, double>(nameof(ProgressValue), 0.0);

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<TestFlightModalControl, bool>(nameof(IsIndeterminate), false);

    public static readonly StyledProperty<string> StatusMessageProperty =
        AvaloniaProperty.Register<TestFlightModalControl, string>(nameof(StatusMessage), "Ready to launch");

    public static readonly StyledProperty<bool> IsSuccessProperty =
        AvaloniaProperty.Register<TestFlightModalControl, bool>(nameof(IsSuccess), false);

    public static readonly StyledProperty<string> ErrorMessageProperty =
        AvaloniaProperty.Register<TestFlightModalControl, string>(nameof(ErrorMessage), string.Empty);

    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<TestFlightModalControl, bool>(nameof(HasError), false);

    public static readonly StyledProperty<string> ResultBannerTextProperty =
        AvaloniaProperty.Register<TestFlightModalControl, string>(nameof(ResultBannerText), "🎉 Test Flight Succeeded! Your local engine and GPU are verified and ready for generation.");

    public static readonly StyledProperty<ICommand?> LaunchTestFlightCommandProperty =
        AvaloniaProperty.Register<TestFlightModalControl, ICommand?>(nameof(LaunchTestFlightCommand));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<TestFlightModalControl, ICommand?>(nameof(CloseCommand));

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public StudioModality SelectedModality
    {
        get => GetValue(SelectedModalityProperty);
        set => SetValue(SelectedModalityProperty, value);
    }

    public ICommand? SelectModalityCommand
    {
        get => GetValue(SelectModalityCommandProperty);
        set => SetValue(SelectModalityCommandProperty, value);
    }

    public IEnumerable? StarterPrompts
    {
        get => GetValue(StarterPromptsProperty);
        set => SetValue(StarterPromptsProperty, value);
    }

    public StudioPreset? SelectedStarterPrompt
    {
        get => GetValue(SelectedStarterPromptProperty);
        set => SetValue(SelectedStarterPromptProperty, value);
    }

    public string EngineStatusText
    {
        get => GetValue(EngineStatusTextProperty);
        set => SetValue(EngineStatusTextProperty, value);
    }

    public bool IsEngineOnline
    {
        get => GetValue(IsEngineOnlineProperty);
        set => SetValue(IsEngineOnlineProperty, value);
    }

    public string VramStatusText
    {
        get => GetValue(VramStatusTextProperty);
        set => SetValue(VramStatusTextProperty, value);
    }

    public bool IsVramClear
    {
        get => GetValue(IsVramClearProperty);
        set => SetValue(IsVramClearProperty, value);
    }

    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    public double ProgressValue
    {
        get => GetValue(ProgressValueProperty);
        set => SetValue(ProgressValueProperty, value);
    }

    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    public string StatusMessage
    {
        get => GetValue(StatusMessageProperty);
        set => SetValue(StatusMessageProperty, value);
    }

    public bool IsSuccess
    {
        get => GetValue(IsSuccessProperty);
        set => SetValue(IsSuccessProperty, value);
    }

    public string ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set
        {
            SetValue(ErrorMessageProperty, value);
            HasError = !string.IsNullOrWhiteSpace(value);
        }
    }

    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public string ResultBannerText
    {
        get => GetValue(ResultBannerTextProperty);
        set => SetValue(ResultBannerTextProperty, value);
    }

    public ICommand? LaunchTestFlightCommand
    {
        get => GetValue(LaunchTestFlightCommandProperty);
        set => SetValue(LaunchTestFlightCommandProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public TestFlightModalControl()
    {
        InitializeComponent();
    }
}
