using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class TelemetryViewModel : ObservableObject
{
    private readonly ITelemetryService _telemetryService;

    [ObservableProperty] private bool _isCollapsed = false;
    [ObservableProperty] private string _gpuName = "GPU Telemetry Active";
    [ObservableProperty] private double _vramUsedGb = 0.0;
    [ObservableProperty] private double _vramTotalGb = 16.0;
    [ObservableProperty] private double _vramPercentage = 0.0;
    [ObservableProperty] private string _vramStatusText = "0.0 GB / 16.0 GB (0%)";

    [ObservableProperty] private EngineStatusCardModel _ollamaCard = new("ollama", "Ollama", 11434);
    [ObservableProperty] private EngineStatusCardModel _comfyUiCard = new("comfyui", "ComfyUI", 8188);
    [ObservableProperty] private EngineStatusCardModel _forgeCard = new("forge", "SD Forge", 7860);
    [ObservableProperty] private EngineStatusCardModel _kokoroCard = new("kokoro", "Kokoro TTS", 8880);

    [ObservableProperty] private string _ollamaModelName = "";
    [ObservableProperty] private string _comfyModelName = "";
    [ObservableProperty] private string _forgeModelName = "";
    [ObservableProperty] private string _kokoroModelName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOllamaOnline))]
    [NotifyPropertyChangedFor(nameof(OllamaStatusColor))]
    private string _ollamaStatus = "Offline";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsForgeOnline))]
    [NotifyPropertyChangedFor(nameof(ForgeStatusColor))]
    private string _forgeStatus = "Offline";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComfyOnline))]
    [NotifyPropertyChangedFor(nameof(ComfyStatusColor))]
    private string _comfyStatus = "Offline";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsKokoroOnline))]
    [NotifyPropertyChangedFor(nameof(KokoroStatusColor))]
    private string _kokoroStatus = "Offline";

    public bool IsOllamaOnline => string.Equals(OllamaStatus, "Online", StringComparison.OrdinalIgnoreCase) || string.Equals(OllamaStatus, "Running", StringComparison.OrdinalIgnoreCase);
    public bool IsForgeOnline => string.Equals(ForgeStatus, "Online", StringComparison.OrdinalIgnoreCase) || string.Equals(ForgeStatus, "Running", StringComparison.OrdinalIgnoreCase);
    public bool IsComfyOnline => string.Equals(ComfyStatus, "Online", StringComparison.OrdinalIgnoreCase) || string.Equals(ComfyStatus, "Running", StringComparison.OrdinalIgnoreCase);
    public bool IsKokoroOnline => string.Equals(KokoroStatus, "Online", StringComparison.OrdinalIgnoreCase) || string.Equals(KokoroStatus, "Running", StringComparison.OrdinalIgnoreCase);

    public string OllamaStatusColor => IsOllamaOnline ? "#22C55E" : "#64748B";
    public string ForgeStatusColor => IsForgeOnline ? "#22C55E" : "#64748B";
    public string ComfyStatusColor => IsComfyOnline ? "#22C55E" : "#64748B";
    public string KokoroStatusColor => IsKokoroOnline ? "#22C55E" : "#64748B";

    partial void OnOllamaStatusChanged(string value) => UpdateCardFromStatus(OllamaCard, value);
    partial void OnForgeStatusChanged(string value) => UpdateCardFromStatus(ForgeCard, value);
    partial void OnComfyStatusChanged(string value) => UpdateCardFromStatus(ComfyUiCard, value);
    partial void OnKokoroStatusChanged(string value) => UpdateCardFromStatus(KokoroCard, value);

    partial void OnOllamaModelNameChanged(string value)
    {
        if (OllamaCard != null)
        {
            OllamaCard.ActiveModel = value;
            OllamaCard.StatusTooltip = $"{OllamaCard.DisplayName}: {(OllamaCard.IsOnline ? "Online" : "Offline")} (Port {OllamaCard.Port})" +
                (!string.IsNullOrEmpty(value) ? $" | Model: {value}" : "");
        }
    }

    partial void OnForgeModelNameChanged(string value)
    {
        if (ForgeCard != null)
        {
            ForgeCard.ActiveModel = value;
            ForgeCard.StatusTooltip = $"{ForgeCard.DisplayName}: {(ForgeCard.IsOnline ? "Online" : "Offline")} (Port {ForgeCard.Port})" +
                (!string.IsNullOrEmpty(value) ? $" | Model: {value}" : "");
        }
    }

    partial void OnComfyModelNameChanged(string value)
    {
        if (ComfyUiCard != null)
        {
            ComfyUiCard.ActiveModel = value;
            ComfyUiCard.StatusTooltip = $"{ComfyUiCard.DisplayName}: {(ComfyUiCard.IsOnline ? "Online" : "Offline")} (Port {ComfyUiCard.Port})" +
                (!string.IsNullOrEmpty(value) ? $" | Model: {value}" : "");
        }
    }

    partial void OnKokoroModelNameChanged(string value)
    {
        if (KokoroCard != null)
        {
            KokoroCard.ActiveModel = value;
            KokoroCard.StatusTooltip = $"{KokoroCard.DisplayName}: {(KokoroCard.IsOnline ? "Online" : "Offline")} (Port {KokoroCard.Port})" +
                (!string.IsNullOrEmpty(value) ? $" | Model: {value}" : "");
        }
    }

    private void UpdateCardFromStatus(EngineStatusCardModel? card, string status)
    {
        if (card == null) return;
        var isOnline = string.Equals(status, "Online", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase);
        var isStarting = string.Equals(status, "Starting", StringComparison.OrdinalIgnoreCase);
        var isError = string.Equals(status, "Error", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase);

        card.IsOnline = isOnline;
        card.IsStarting = isStarting;
        card.IsError = isError;
        card.StatusTooltip = $"{card.DisplayName}: {(isOnline ? "Online" : isStarting ? "Starting..." : isError ? "Error" : "Offline")} (Port {card.Port})" +
            (!string.IsNullOrEmpty(card.ActiveModel) ? $" | Model: {card.ActiveModel}" : "");
    }

    [ObservableProperty] private string _serviceModeText = "Connecting...";
    [ObservableProperty] private bool _isServiceRunning = false;
    [ObservableProperty] private string _apiBase = OperatingSystem.IsBrowser() ? "" : $"http://{SettingsViewModel.GetLocalIPv4Address()}:5246";

    [ObservableProperty] private bool _isManageServiceModalOpen;
    [ObservableProperty] private string _manageServicePrompt = "";
    [ObservableProperty] private string _manageServiceTarget = "";
    [ObservableProperty] private bool _manageServiceIsStart;

    public TelemetryViewModel() : this(new TelemetryService())
    {
    }

    public TelemetryViewModel(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
        UpdateCardFromStatus(OllamaCard, OllamaStatus);
        UpdateCardFromStatus(ComfyUiCard, ComfyStatus);
        UpdateCardFromStatus(ForgeCard, ForgeStatus);
        UpdateCardFromStatus(KokoroCard, KokoroStatus);
    }

    [RelayCommand]
    public void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    public Action<string>? OnManageServiceRequested { get; set; }

    [RelayCommand]
    public void ManageService(string serviceName)
    {
        if (OnManageServiceRequested != null)
        {
            OnManageServiceRequested(serviceName);
            return;
        }

        ManageServiceTarget = serviceName;
        bool isOnline = false;
        var lower = (serviceName ?? "").ToLowerInvariant();
        if (lower.Contains("ollama")) isOnline = IsOllamaOnline;
        else if (lower.Contains("forge")) isOnline = IsForgeOnline;
        else if (lower.Contains("comfy")) isOnline = IsComfyOnline;
        else if (lower.Contains("kokoro") || lower.Contains("audio")) isOnline = IsKokoroOnline;

        ManageServiceIsStart = !isOnline;
        string action = ManageServiceIsStart ? "start" : "stop";
        ManageServicePrompt = $"Are you sure you want to {action} the {serviceName} service?";
        IsManageServiceModalOpen = true;
    }

    [RelayCommand]
    public void CancelManageService()
    {
        IsManageServiceModalOpen = false;
    }

    [RelayCommand]
    public async Task ConfirmManageServiceAsync()
    {
        IsManageServiceModalOpen = false;
        var serviceName = ManageServiceTarget ?? "Service";
        var isStart = ManageServiceIsStart;
        var action = isStart ? "start" : "stop";

        var lower = serviceName.Trim().ToLowerInvariant();
        string engineName = "ollama";
        if (lower.Contains("comfy")) engineName = "comfy";
        else if (lower.Contains("forge")) engineName = "forge";
        else if (lower.Contains("audio") || lower.Contains("kokoro")) engineName = "audio";
        else if (lower.Contains("ollama")) engineName = "ollama";

        ToastService.Instance.Show($"{(isStart ? "Starting" : "Stopping")} {serviceName}...", ToastType.Info, 2500);

        try
        {
            var actionEndpoint = isStart ? $"/api/{engineName}/start" : $"/api/{engineName}/stop";
            var req = new { engine = engineName };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(req),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            using var http = HttpHelper.CreateClient(ApiBase);
            var response = await http.PostAsync($"{ApiBase}{actionEndpoint}", content);
            if (response.IsSuccessStatusCode)
            {
                ToastService.Instance.Show($"{serviceName} {(isStart ? "started" : "stopped")} successfully.", ToastType.Success);
            }
            else
            {
                var errorText = await response.Content.ReadAsStringAsync();
                var msg = string.IsNullOrWhiteSpace(errorText) ? $"Server returned {response.StatusCode}" : errorText;
                ToastService.Instance.Show($"Failed to {action} {serviceName}: {msg}", ToastType.Error, 6000);
            }
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"Error while attempting to {action} {serviceName}: {ex.Message}", ToastType.Error, 6000);
        }
        finally
        {
            await RefreshStatusAsync();
        }
    }

    [RelayCommand]
    public async Task ToggleEngineCardAsync(string? engineKey)
    {
        if (string.IsNullOrWhiteSpace(engineKey)) return;

        var key = engineKey.Trim().ToLowerInvariant();
        EngineStatusCardModel? card = key switch
        {
            "ollama" => OllamaCard,
            "comfy" or "comfyui" => ComfyUiCard,
            "forge" or "sdforge" => ForgeCard,
            "kokoro" or "audio" => KokoroCard,
            _ => null
        };

        if (card == null) return;

        bool isOnline = card.IsOnline;
        bool isStarting = !isOnline;
        card.IsStarting = isStarting;
        card.StatusTooltip = $"{card.DisplayName}: {(isStarting ? "Starting..." : "Stopping...")} (Port {card.Port})";

        string apiEngine = key switch
        {
            "kokoro" => "audio",
            "comfyui" => "comfy",
            _ => key
        };

        var action = isStarting ? "start" : "stop";
        ToastService.Instance.Show($"{(isStarting ? "Starting" : "Stopping")} {card.DisplayName}...", ToastType.Info, 2500);

        try
        {
            var actionEndpoint = $"/api/{apiEngine}/{action}";
            var req = new { engine = apiEngine };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(req),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            using var http = HttpHelper.CreateClient(ApiBase);
            var response = await http.PostAsync($"{ApiBase}{actionEndpoint}", content);
            if (response.IsSuccessStatusCode)
            {
                ToastService.Instance.Show($"{card.DisplayName} {(isStarting ? "started" : "stopped")} successfully.", ToastType.Success);
            }
            else
            {
                var errorText = await response.Content.ReadAsStringAsync();
                var msg = string.IsNullOrWhiteSpace(errorText) ? $"Server returned {response.StatusCode}" : errorText;
                ToastService.Instance.Show($"Failed to {action} {card.DisplayName}: {msg}", ToastType.Error, 6000);
            }
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"Error while attempting to {action} {card.DisplayName}: {ex.Message}", ToastType.Error, 6000);
        }
        finally
        {
            await RefreshStatusAsync();
        }
    }

    public void UpdateFromTelemetry(TelemetryData data)
    {
        if (data == null) return;

        if (OllamaCard != null)
        {
            OllamaCard.IsOnline = data.OllamaOnline;
            OllamaCard.IsStarting = data.OllamaStarting;
            if (!string.IsNullOrEmpty(data.OllamaModel))
            {
                OllamaCard.ActiveModel = data.OllamaModel;
                OllamaModelName = data.OllamaModel;
            }
            if (!string.IsNullOrEmpty(data.OllamaMemory))
            {
                OllamaCard.MemoryUsage = data.OllamaMemory;
            }
            OllamaStatus = data.OllamaOnline ? "Online" : (data.OllamaStarting ? "Starting" : "Offline");
            OllamaCard.StatusTooltip = $"{OllamaCard.DisplayName}: {(OllamaCard.IsOnline ? "Online" : OllamaCard.IsStarting ? "Starting..." : "Offline")} (Port {OllamaCard.Port})" +
                (!string.IsNullOrEmpty(OllamaCard.ActiveModel) ? $" | Model: {OllamaCard.ActiveModel}" : "");
        }

        if (ComfyUiCard != null)
        {
            ComfyUiCard.IsOnline = data.ComfyOnline;
            ComfyUiCard.IsStarting = data.ComfyStarting;
            if (!string.IsNullOrEmpty(data.ComfyModel))
            {
                ComfyUiCard.ActiveModel = data.ComfyModel;
                ComfyModelName = data.ComfyModel;
            }
            if (!string.IsNullOrEmpty(data.ComfyMemory))
            {
                ComfyUiCard.MemoryUsage = data.ComfyMemory;
            }
            ComfyStatus = data.ComfyOnline ? "Online" : (data.ComfyStarting ? "Starting" : "Offline");
            ComfyUiCard.StatusTooltip = $"{ComfyUiCard.DisplayName}: {(ComfyUiCard.IsOnline ? "Online" : ComfyUiCard.IsStarting ? "Starting..." : "Offline")} (Port {ComfyUiCard.Port})" +
                (!string.IsNullOrEmpty(ComfyUiCard.ActiveModel) ? $" | Model: {ComfyUiCard.ActiveModel}" : "");
        }

        if (ForgeCard != null)
        {
            ForgeCard.IsOnline = data.ForgeOnline;
            ForgeCard.IsStarting = data.ForgeStarting;
            if (!string.IsNullOrEmpty(data.ForgeModel))
            {
                ForgeCard.ActiveModel = data.ForgeModel;
                ForgeModelName = data.ForgeModel;
            }
            if (!string.IsNullOrEmpty(data.ForgeMemory))
            {
                ForgeCard.MemoryUsage = data.ForgeMemory;
            }
            ForgeStatus = data.ForgeOnline ? "Online" : (data.ForgeStarting ? "Starting" : "Offline");
            ForgeCard.StatusTooltip = $"{ForgeCard.DisplayName}: {(ForgeCard.IsOnline ? "Online" : ForgeCard.IsStarting ? "Starting..." : "Offline")} (Port {ForgeCard.Port})" +
                (!string.IsNullOrEmpty(ForgeCard.ActiveModel) ? $" | Model: {ForgeCard.ActiveModel}" : "");
        }

        if (KokoroCard != null)
        {
            KokoroCard.IsOnline = data.KokoroOnline;
            KokoroCard.IsStarting = data.KokoroStarting;
            if (!string.IsNullOrEmpty(data.KokoroModel))
            {
                KokoroCard.ActiveModel = data.KokoroModel;
                KokoroModelName = data.KokoroModel;
            }
            if (!string.IsNullOrEmpty(data.KokoroMemory))
            {
                KokoroCard.MemoryUsage = data.KokoroMemory;
            }
            KokoroStatus = data.KokoroOnline ? "Online" : (data.KokoroStarting ? "Starting" : "Offline");
            KokoroCard.StatusTooltip = $"{KokoroCard.DisplayName}: {(KokoroCard.IsOnline ? "Online" : KokoroCard.IsStarting ? "Starting..." : "Offline")} (Port {KokoroCard.Port})" +
                (!string.IsNullOrEmpty(KokoroCard.ActiveModel) ? $" | Model: {KokoroCard.ActiveModel}" : "");
        }

        if (!string.IsNullOrEmpty(data.GpuName))
        {
            GpuName = data.GpuName;
        }
        if (data.TotalVramGb > 0)
        {
            VramTotalGb = data.TotalVramGb;
            VramUsedGb = data.UsedVramGb;
            VramPercentage = data.VramPercent > 0 ? data.VramPercent : Math.Round((VramUsedGb / VramTotalGb) * 100.0, 1);
            VramStatusText = $"{VramUsedGb} GB / {VramTotalGb} GB ({VramPercentage}%)";
        }

        IsServiceRunning = (OllamaCard?.IsOnline ?? false) || (ComfyUiCard?.IsOnline ?? false) || (ForgeCard?.IsOnline ?? false) || (KokoroCard?.IsOnline ?? false);
        ServiceModeText = IsServiceRunning ? "Service Connected 🟢" : "Connecting...";
    }

    [RelayCommand]
    public async Task RefreshStatusAsync()
    {
        await RefreshStatusAsync(ApiBase, "http://127.0.0.1:8188", HttpHelper.CreateClient(ApiBase));
    }

    public async Task RefreshStatusAsync(string apiBase, string comfyUrl, HttpClient http)
    {
        await CheckHealthAsync(apiBase, comfyUrl, http);
        await CheckGpuVramAsync(apiBase, http);
    }

    public async Task CheckHealthAsync(string apiBase, string comfyUrl, HttpClient http)
    {
        var (ollama, forge, comfy) = await _telemetryService.CheckServiceHealthAsync(apiBase, comfyUrl, http);

        OllamaStatus = ollama ? "Online" : "Offline";
        ForgeStatus = forge ? "Online" : "Offline";
        ComfyStatus = comfy ? "Online" : "Offline";

        if (OllamaCard != null)
        {
            OllamaCard.IsOnline = ollama;
            OllamaCard.StatusTooltip = $"{OllamaCard.DisplayName}: {(ollama ? "Online" : "Offline")} (Port {OllamaCard.Port})" +
                (!string.IsNullOrEmpty(OllamaCard.ActiveModel) ? $" | Model: {OllamaCard.ActiveModel}" : "");
        }
        if (ForgeCard != null)
        {
            ForgeCard.IsOnline = forge;
            ForgeCard.StatusTooltip = $"{ForgeCard.DisplayName}: {(forge ? "Online" : "Offline")} (Port {ForgeCard.Port})" +
                (!string.IsNullOrEmpty(ForgeCard.ActiveModel) ? $" | Model: {ForgeCard.ActiveModel}" : "");
        }
        if (ComfyUiCard != null)
        {
            ComfyUiCard.IsOnline = comfy;
            ComfyUiCard.StatusTooltip = $"{ComfyUiCard.DisplayName}: {(comfy ? "Online" : "Offline")} (Port {ComfyUiCard.Port})" +
                (!string.IsNullOrEmpty(ComfyUiCard.ActiveModel) ? $" | Model: {ComfyUiCard.ActiveModel}" : "");
        }

        IsServiceRunning = ollama || forge || comfy || (KokoroCard?.IsOnline ?? false);
        ServiceModeText = IsServiceRunning ? "Service Connected 🟢" : "Connecting...";
    }

    public async Task CheckGpuVramAsync(string apiBase, HttpClient http)
    {
        var info = await _telemetryService.QueryGpuVramAsync(apiBase, http);
        if (info.GpuName != "GPU Telemetry Active" || GpuName == "GPU Telemetry Active")
        {
            GpuName = info.GpuName;
        }
        VramTotalGb = info.TotalVramGb;
        VramUsedGb = info.UsedVramGb;
        VramPercentage = info.Percent;
        VramStatusText = $"{VramUsedGb} GB / {VramTotalGb} GB ({VramPercentage}%)";
    }
}
