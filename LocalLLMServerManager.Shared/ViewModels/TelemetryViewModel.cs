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
    public void ManageService(string serviceName)
    {
        ManageServiceTarget = serviceName;
        bool isOnline = false;
        if (serviceName == "Ollama") isOnline = IsOllamaOnline;
        else if (serviceName == "Forge SD") isOnline = IsForgeOnline;
        else if (serviceName == "ComfyUI") isOnline = IsComfyOnline;

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
        try
        {
            var actionEndpoint = ManageServiceIsStart ? "/api/comfy/start" : "/api/comfy/stop";
            var engineName = ManageServiceTarget.ToLower().Replace(" sd", "");
            if (ManageServiceTarget == "ComfyUI") engineName = "comfy";

            var req = new { engine = engineName };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(req),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            using var http = HttpHelper.CreateClient(ApiBase);
            await http.PostAsync($"{ApiBase}{actionEndpoint}", content);
        }
        catch { }
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
