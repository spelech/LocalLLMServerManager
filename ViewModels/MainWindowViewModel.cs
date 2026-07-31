using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LocalLLMServerManager.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly HttpClient _http = new();
    private const string ApiBase = "http://127.0.0.1:5246";

    [ObservableProperty]
    private string _gpuName = "Detecting GPU...";

    [ObservableProperty]
    private double _vramUsedGb = 0;

    [ObservableProperty]
    private double _vramTotalGb = 8.0;

    [ObservableProperty]
    private double _vramPercentage = 0;

    [ObservableProperty]
    private string _vramStatusText = "0 GB / 8 GB (0%)";

    [ObservableProperty]
    private string _ollamaStatus = "Checking...";

    [ObservableProperty]
    private string _forgeStatus = "Checking...";

    [ObservableProperty]
    private string _comfyStatus = "Checking...";

    [ObservableProperty]
    private string _serviceModeText = "Detecting Host Mode...";

    [ObservableProperty]
    private bool _isServiceRunning = false;

    public MainWindowViewModel()
    {
        _ = RefreshStatusAsync();
    }

    [RelayCommand]
    public async Task RefreshStatusAsync()
    {
        await CheckHealthAsync();
        await CheckGpuVramAsync();
    }

    [RelayCommand]
    public void OpenWebUiInBrowser()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ApiBase,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    public async Task ToggleEngineAsync(string engineName)
    {
        try
        {
            var endpoint = engineName.ToLowerInvariant() switch
            {
                "comfy" => $"{ApiBase}/api/comfy/start",
                "forge" => $"{ApiBase}/api/forge/start",
                _ => null
            };

            if (endpoint != null)
            {
                await _http.PostAsync(endpoint, null);
                await Task.Delay(1000);
                await RefreshStatusAsync();
            }
        }
        catch { }
    }

    private async Task CheckHealthAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{ApiBase}/health");
            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                var doc = JsonNode.Parse(jsonStr);

                OllamaStatus = doc?["ollama"]?.ToString() ?? doc?["Ollama"]?.ToString() ?? "Offline";
                ForgeStatus = doc?["stableDiffusion"]?.ToString() ?? doc?["StableDiffusion"]?.ToString() ?? "Offline";
                ComfyStatus = doc?["comfyUI"]?.ToString() ?? doc?["ComfyUI"]?.ToString() ?? "Offline";
                ServiceModeText = "Connected to Server (:5246)";
                IsServiceRunning = true;
            }
            else
            {
                OllamaStatus = "Offline";
                ForgeStatus = "Offline";
                ComfyStatus = "Offline";
                ServiceModeText = "Server Offline";
                IsServiceRunning = false;
            }
        }
        catch
        {
            OllamaStatus = "Offline";
            ForgeStatus = "Offline";
            ComfyStatus = "Offline";
            ServiceModeText = "Connecting...";
            IsServiceRunning = false;
        }
    }

    private async Task CheckGpuVramAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{ApiBase}/api/gpu/vram");
            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                var doc = JsonNode.Parse(jsonStr);

                GpuName = doc?["gpuName"]?.ToString() ?? doc?["GpuName"]?.ToString() ?? "GPU";
                long vramBytes = doc?["vramBytes"]?.GetValue<long>() ?? doc?["VramBytes"]?.GetValue<long>() ?? 8589934592L;

                VramTotalGb = Math.Round(vramBytes / (1024.0 * 1024.0 * 1024.0), 1);

                // Query Ollama VRAM usage if online
                long usedBytes = 0;
                try
                {
                    var psResp = await _http.GetAsync("http://127.0.0.1:11434/api/ps");
                    if (psResp.IsSuccessStatusCode)
                    {
                        var psDoc = JsonNode.Parse(await psResp.Content.ReadAsStringAsync());
                        var models = psDoc?["models"]?.AsArray();
                        if (models != null)
                        {
                            foreach (var model in models)
                            {
                                usedBytes += model?["size_vram"]?.GetValue<long>() ?? 0L;
                            }
                        }
                    }
                }
                catch { }

                VramUsedGb = Math.Round(usedBytes / (1024.0 * 1024.0 * 1024.0), 2);
                VramPercentage = VramTotalGb > 0 ? Math.Min(100, Math.Round((VramUsedGb / VramTotalGb) * 100, 1)) : 0;
                VramStatusText = $"{VramUsedGb} GB / {VramTotalGb} GB ({VramPercentage}%)";
            }
        }
        catch
        {
            GpuName = "System GPU";
        }
    }
}
