using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
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

    public bool IsOllamaOnline => string.Equals(OllamaStatus, "Online", StringComparison.OrdinalIgnoreCase);
    public bool IsForgeOnline => string.Equals(ForgeStatus, "Online", StringComparison.OrdinalIgnoreCase);
    public bool IsComfyOnline => string.Equals(ComfyStatus, "Online", StringComparison.OrdinalIgnoreCase);

    public string OllamaStatusColor => IsOllamaOnline ? "#22C55E" : "#64748B";
    public string ForgeStatusColor => IsForgeOnline ? "#22C55E" : "#64748B";
    public string ComfyStatusColor => IsComfyOnline ? "#22C55E" : "#64748B";

    [ObservableProperty] private string _serviceModeText = "Connecting...";
    [ObservableProperty] private bool _isServiceRunning = false;
    [ObservableProperty] private string _apiBase = OperatingSystem.IsBrowser() ? "" : $"http://{SettingsViewModel.GetLocalIPv4Address()}:5246";

    [ObservableProperty] private bool _isManageServiceModalOpen;
    [ObservableProperty] private string _manageServicePrompt = "";
    [ObservableProperty] private string _manageServiceTarget = "";
    [ObservableProperty] private bool _manageServiceIsStart;

    public TelemetryViewModel(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
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
        else if (lower.Contains("audio")) engineName = "audio";
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

        IsServiceRunning = ollama || forge || comfy;
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
