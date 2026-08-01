using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LocalLLMServerManager.ViewModels;

public record OllamaModelItem(
    string Name,
    string FormatSize,
    string CapabilityTag,
    string CapabilityColor,
    bool IsLoaded
);

public record HuggingFaceRepoItem(
    string Id,
    string Author,
    int Likes,
    string Downloads
);

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
    private string _serviceModeText = "Connected to Server (:5246)";

    [ObservableProperty]
    private bool _isServiceRunning = false;

    [ObservableProperty]
    private string _searchQuery = "llama 3.3";

    [ObservableProperty]
    private double _targetContextTokens = 4096;

    [ObservableProperty]
    private string _estimatedKvCacheText = "~256 MB";

    [ObservableProperty]
    private string _lanAccessUrl = "http://localhost:5246";

    public ObservableCollection<OllamaModelItem> InstalledModels { get; } = new();
    public ObservableCollection<HuggingFaceRepoItem> HuggingFaceResults { get; } = new();

    public MainWindowViewModel()
    {
        DetectLanIp();
        _ = RefreshStatusAsync();
    }

    private void DetectLanIp()
    {
        try
        {
            foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    LanAccessUrl = $"http://{ip}:5246";
                    break;
                }
            }
        }
        catch
        {
            LanAccessUrl = "http://127.0.0.1:5246";
        }
    }

    partial void OnTargetContextTokensChanged(double value)
    {
        // Compute estimated KV cache footprint (roughly 64 KB per token for typical 8B-70B models)
        double estimatedBytes = value * 65536.0;
        double mb = estimatedBytes / (1024.0 * 1024.0);
        EstimatedKvCacheText = mb >= 1024 ? $"~{(mb / 1024.0):F1} GB" : $"~{mb:F0} MB";
    }

    [RelayCommand]
    public async Task RefreshStatusAsync()
    {
        await CheckHealthAsync();
        await CheckGpuVramAsync();
        await LoadInstalledModelsAsync();
    }

    [RelayCommand]
    public async Task LoadInstalledModelsAsync()
    {
        try
        {
            var response = await _http.GetAsync("http://127.0.0.1:11434/api/tags");
            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                var doc = JsonNode.Parse(jsonStr);
                var models = doc?["models"]?.AsArray();

                InstalledModels.Clear();
                if (models != null)
                {
                    foreach (var m in models)
                    {
                        string name = m?["name"]?.ToString() ?? "Unknown";
                        long size = m?["size"]?.GetValue<long>() ?? 0L;
                        double sizeGb = Math.Round(size / (1024.0 * 1024.0 * 1024.0), 2);
                        string formatSize = sizeGb > 0 ? $"{sizeGb} GB" : "N/A";

                        string cap = "💻 Coding & General";
                        string color = "#38BDF8";
                        if (name.Contains("math", StringComparison.OrdinalIgnoreCase)) { cap = "🧮 Mathematics"; color = "#C084FC"; }
                        else if (name.Contains("r1", StringComparison.OrdinalIgnoreCase) || name.Contains("deepseek", StringComparison.OrdinalIgnoreCase)) { cap = "🧠 Reasoning"; color = "#A855F7"; }
                        else if (name.Contains("gemma", StringComparison.OrdinalIgnoreCase)) { cap = "💎 General Chat"; color = "#FB923C"; }

                        InstalledModels.Add(new OllamaModelItem(name, formatSize, cap, color, false));
                    }
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task UnloadAllVramAsync()
    {
        try
        {
            var psResp = await _http.GetAsync("http://127.0.0.1:11434/api/ps");
            if (psResp.IsSuccessStatusCode)
            {
                var doc = JsonNode.Parse(await psResp.Content.ReadAsStringAsync());
                var models = doc?["models"]?.AsArray();
                if (models != null)
                {
                    foreach (var m in models)
                    {
                        string name = m?["name"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(name))
                        {
                            var content = new StringContent(
                                JsonSerializer.Serialize(new { model = name, keep_alive = 0 }),
                                System.Text.Encoding.UTF8,
                                "application/json"
                            );
                            await _http.PostAsync("http://127.0.0.1:11434/api/generate", content);
                        }
                    }
                }
            }
            await Task.Delay(1000);
            await RefreshStatusAsync();
        }
        catch { }
    }

    [RelayCommand]
    public async Task SearchHuggingFaceAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        try
        {
            string url = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(SearchQuery)}&filter=gguf&limit=8&sort=downloads";
            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                var arr = JsonNode.Parse(jsonStr)?.AsArray();

                HuggingFaceResults.Clear();
                if (arr != null)
                {
                    foreach (var item in arr)
                    {
                        string id = item?["id"]?.ToString() ?? "";
                        string author = item?["author"]?.ToString() ?? "Community";
                        int likes = item?["likes"]?.GetValue<int>() ?? 0;
                        int downloads = item?["downloads"]?.GetValue<int>() ?? 0;

                        if (!string.IsNullOrEmpty(id))
                        {
                            HuggingFaceResults.Add(new HuggingFaceRepoItem(id, author, likes, $"{downloads:N0} downloads"));
                        }
                    }
                }
            }
        }
        catch { }
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
