using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LocalLLMServerManager.Shared.Views.Controls;

public partial class GenerationStageTrackerControl : UserControl
{
    public static readonly StyledProperty<int> CurrentStageProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, int>(nameof(CurrentStage), 0);

    public static readonly StyledProperty<string> Stage1StatusProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, string>(nameof(Stage1Status), "Pending");

    public static readonly StyledProperty<string> Stage2StatusProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, string>(nameof(Stage2Status), "Pending");

    public static readonly StyledProperty<string> Stage3StatusProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, string>(nameof(Stage3Status), "Pending");

    public static readonly StyledProperty<string> Stage4StatusProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, string>(nameof(Stage4Status), "Pending");

    public static readonly StyledProperty<double> ProgressValueProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, double>(nameof(ProgressValue), 0.0);

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, bool>(nameof(IsIndeterminate), false);

    public static readonly StyledProperty<string> ElapsedTimerTextProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, string>(nameof(ElapsedTimerText), "⏱️ 0:00s elapsed");

    public static readonly StyledProperty<string> HardwareStatusBadgeTextProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, string>(nameof(HardwareStatusBadgeText), "🟢 Ready");

    public static readonly StyledProperty<string> LogsTextProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, string>(nameof(LogsText), string.Empty);

    public static readonly StyledProperty<bool> IsLogsExpandedProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, bool>(nameof(IsLogsExpanded), false);

    public static readonly StyledProperty<string> LogsButtonTextProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, string>(nameof(LogsButtonText), "📜 Show Live Logs");

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, bool>(nameof(IsActive), false);

    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<GenerationStageTrackerControl, ICommand?>(nameof(CancelCommand));

    public int CurrentStage
    {
        get => GetValue(CurrentStageProperty);
        set => SetValue(CurrentStageProperty, value);
    }

    public string Stage1Status
    {
        get => GetValue(Stage1StatusProperty);
        set => SetValue(Stage1StatusProperty, value);
    }

    public string Stage2Status
    {
        get => GetValue(Stage2StatusProperty);
        set => SetValue(Stage2StatusProperty, value);
    }

    public string Stage3Status
    {
        get => GetValue(Stage3StatusProperty);
        set => SetValue(Stage3StatusProperty, value);
    }

    public string Stage4Status
    {
        get => GetValue(Stage4StatusProperty);
        set => SetValue(Stage4StatusProperty, value);
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

    public string ElapsedTimerText
    {
        get => GetValue(ElapsedTimerTextProperty);
        set => SetValue(ElapsedTimerTextProperty, value);
    }

    public string HardwareStatusBadgeText
    {
        get => GetValue(HardwareStatusBadgeTextProperty);
        set => SetValue(HardwareStatusBadgeTextProperty, value);
    }

    public string LogsText
    {
        get => GetValue(LogsTextProperty);
        set => SetValue(LogsTextProperty, value);
    }

    public bool IsLogsExpanded
    {
        get => GetValue(IsLogsExpandedProperty);
        set
        {
            SetValue(IsLogsExpandedProperty, value);
            LogsButtonText = value ? "📜 Hide Live Logs" : "📜 Show Live Logs";
        }
    }

    public string LogsButtonText
    {
        get => GetValue(LogsButtonTextProperty);
        set => SetValue(LogsButtonTextProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public GenerationStageTrackerControl()
    {
        InitializeComponent();
    }

    private void OnToggleLogsClick(object? sender, RoutedEventArgs e)
    {
        IsLogsExpanded = !IsLogsExpanded;
    }
}
