using CommunityToolkit.Mvvm.ComponentModel;

namespace LocalLLMServerManager.Shared.Models;

public partial class EngineStatusCardModel : ObservableObject
{
    [ObservableProperty] private string _engineKey = "";
    [ObservableProperty] private string _displayName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(DisplaySubtitle))]
    private bool _isOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(DisplaySubtitle))]
    private bool _isStarting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(DisplaySubtitle))]
    private bool _isError;

    [ObservableProperty] private int _port;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveModel))]
    [NotifyPropertyChangedFor(nameof(DisplaySubtitle))]
    private string _activeModel = "";

    [ObservableProperty] private string _memoryUsage = "";
    [ObservableProperty] private string _statusTooltip = "";

    public string StatusColor => IsError ? "#ef4444" : IsOnline ? "#10b981" : IsStarting ? "#f59e0b" : "#64748b";
    public string ActionButtonText => IsOnline ? "⏹ Stop" : "▷ Start";
    public string StatusText => IsError ? "Error" : IsOnline ? "Online" : IsStarting ? "Starting..." : "Offline";
    public bool HasActiveModel => !string.IsNullOrWhiteSpace(ActiveModel);
    public string DisplaySubtitle => HasActiveModel ? ActiveModel : StatusText;

    public EngineStatusCardModel()
    {
    }

    public EngineStatusCardModel(string engineKey, string displayName, int port)
    {
        EngineKey = engineKey;
        DisplayName = displayName;
        Port = port;
        StatusTooltip = $"{displayName}: Offline (Port {port})";
    }
}

public class TelemetryData
{
    public bool OllamaOnline { get; set; }
    public bool ComfyOnline { get; set; }
    public bool ForgeOnline { get; set; }
    public bool KokoroOnline { get; set; }

    public bool OllamaStarting { get; set; }
    public bool ComfyStarting { get; set; }
    public bool ForgeStarting { get; set; }
    public bool KokoroStarting { get; set; }

    public string OllamaModel { get; set; } = "";
    public string ComfyModel { get; set; } = "";
    public string ForgeModel { get; set; } = "";
    public string KokoroModel { get; set; } = "";

    public string OllamaMemory { get; set; } = "";
    public string ComfyMemory { get; set; } = "";
    public string ForgeMemory { get; set; } = "";
    public string KokoroMemory { get; set; } = "";

    public string GpuName { get; set; } = "";
    public double UsedVramGb { get; set; }
    public double TotalVramGb { get; set; } = 16.0;
    public int VramPercent { get; set; }

    public TelemetryData()
    {
    }

    public TelemetryData(
        bool ollamaOnline = false,
        bool comfyOnline = false,
        bool forgeOnline = false,
        bool kokoroOnline = false,
        string? ollamaModel = null,
        string? comfyModel = null,
        string? forgeModel = null,
        string? kokoroModel = null)
    {
        OllamaOnline = ollamaOnline;
        ComfyOnline = comfyOnline;
        ForgeOnline = forgeOnline;
        KokoroOnline = kokoroOnline;
        OllamaModel = ollamaModel ?? "";
        ComfyModel = comfyModel ?? "";
        ForgeModel = forgeModel ?? "";
        KokoroModel = kokoroModel ?? "";
    }
}
