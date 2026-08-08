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

    [ObservableProperty] private string _ollamaStatus = "Offline";
    [ObservableProperty] private string _forgeStatus = "Offline";
    [ObservableProperty] private string _comfyStatus = "Offline";
    [ObservableProperty] private string _serviceModeText = "Connecting...";
    [ObservableProperty] private bool _isServiceRunning = false;

    public TelemetryViewModel(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
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
        GpuName = info.GpuName;
        VramTotalGb = info.TotalVramGb;
        VramUsedGb = info.UsedVramGb;
        VramPercentage = info.Percent;
        VramStatusText = $"{VramUsedGb} GB / {VramTotalGb} GB ({VramPercentage}%)";
    }
}
