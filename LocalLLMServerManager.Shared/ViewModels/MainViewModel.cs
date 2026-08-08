using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager;

namespace LocalLLMServerManager.Shared.ViewModels;

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

public record HfFileQuantItem(
    string Filename,
    string Quantization,
    string FormatSize,
    long SizeBytes
);

public record CivitaiModelItem(
    int Id,
    string Name,
    string Type,
    string ThumbnailUrl,
    string DownloadUrl,
    string FileName,
    double Rating,
    int DownloadCount
);

public partial class MainViewModel : ObservableObject
{
    public static HttpClient DefaultHttpClient { get; set; } = new();
    private HttpClient? _customHttp;

    public HttpClient Http
    {
        get => _customHttp ?? DefaultHttpClient;
        set => _customHttp = value;
    }

    public static bool EnableAutomaticPolling { get; set; } = false;

    private readonly ITelemetryService _telemetryService;
    private readonly IOllamaModelService _ollamaModelService;
    private readonly IHuggingFaceSearchService _hfSearchService;
    private readonly ICivitaiSearchService _civitaiSearchService;

    public MainViewModel(HttpClient? httpClient)
        : this(httpClient, new TelemetryService(), new OllamaModelService(), new HuggingFaceSearchService(), new CivitaiSearchService())
    {
    }

    public MainViewModel(
        HttpClient? httpClient,
        ITelemetryService telemetryService,
        IOllamaModelService ollamaModelService,
        IHuggingFaceSearchService hfSearchService,
        ICivitaiSearchService civitaiSearchService)
    {
        if (httpClient != null) _customHttp = httpClient;
        _telemetryService = telemetryService;
        _ollamaModelService = ollamaModelService;
        _hfSearchService = hfSearchService;
        _civitaiSearchService = civitaiSearchService;

        DetectLanIp();
        _ = RefreshStatusAsync();
        _ = LoadSettingsAsync();
        if (EnableAutomaticPolling)
        {
            _ = StartBackgroundPollingAsync();
        }
    }

    private async Task StartBackgroundPollingAsync()
    {
        while (EnableAutomaticPolling)
        {
            try
            {
                await Task.Delay(4000);
                if (!EnableAutomaticPolling) break;
                await RefreshStatusAsync();
            }
            catch { }
        }
    }

    public static string DefaultApiBase { get; set; } = "http://127.0.0.1:5246";
    [ObservableProperty] private string _apiBase = DefaultApiBase;

    [ObservableProperty] private string _gpuName = "Detecting GPU...";
    [ObservableProperty] private double _vramUsedGb = 0;
    [ObservableProperty] private double _vramTotalGb = 8.0;
    [ObservableProperty] private double _vramPercentage = 0;
    [ObservableProperty] private string _vramStatusText = "0 GB / 8 GB (0%)";

    [ObservableProperty] private string _ollamaStatus = "Checking...";
    [ObservableProperty] private string _forgeStatus = "Checking...";
    [ObservableProperty] private string _comfyStatus = "Checking...";
    [ObservableProperty] private string _serviceModeText = "Connected to Server (:5246)";
    [ObservableProperty] private bool _isServiceRunning = false;

    // Hugging Face Tab State
    [ObservableProperty] private string _hfSearchQuery = "llama 3.3";
    [ObservableProperty] private bool _isHfModalOpen = false;
    [ObservableProperty] private string _modalRepoId = string.Empty;
    [ObservableProperty] private string _modalAuthor = string.Empty;

    // CivitAI Tab State
    [ObservableProperty] private string _civitaiSearchQuery = "cyberpunk";
    [ObservableProperty] private string _selectedCivitaiType = "Checkpoint";

    // Pull Progress Drawer State
    [ObservableProperty] private bool _isPullDrawerOpen = false;
    [ObservableProperty] private string _pullModelName = string.Empty;
    [ObservableProperty] private double _pullProgressPercent = 0;
    [ObservableProperty] private string _pullProgressBytesText = "0 MB / 0 MB";
    [ObservableProperty] private string _pullStatusLog = string.Empty;

    // KV Cache Calculator State
    [ObservableProperty] private double _targetContextTokens = 4096;
    [ObservableProperty] private string _estimatedKvCacheText = "~256 MB";
    [ObservableProperty] private string _lanAccessUrl = "http://localhost:5246";

    // Tool Paths & Settings State
    [ObservableProperty] private string _comfyUiExecutablePath = "%APPDATA%\\AI\\ComfyUI\\run_nvidia_gpu.bat";
    [ObservableProperty] private string _forgeExecutablePath = "%APPDATA%\\AI\\SD_Forge\\webui-user.bat";
    [ObservableProperty] private string _forgeModelsPath = "%APPDATA%\\AI\\SD_Forge\\models";
    [ObservableProperty] private string _threeDModelsPath = "%APPDATA%\\AI\\3d_outputs";
    [ObservableProperty] private string _workflowsPath = "%APPDATA%\\AI\\Workflows";
    [ObservableProperty] private string _comfyUiUrl = "http://127.0.0.1:8188";
    [ObservableProperty] private string _preferredImageEngine = "Forge";

    public ObservableCollection<OllamaModelItem> InstalledModels { get; } = new();
    public ObservableCollection<HuggingFaceRepoItem> HuggingFaceResults { get; } = new();
    public ObservableCollection<HfFileQuantItem> ModalHfFiles { get; } = new();
    public ObservableCollection<CivitaiModelItem> CivitaiResults { get; } = new();
    public ObservableCollection<ToastItem> Toasts => ToastService.Instance.ActiveToasts;

    public MainViewModel() : this(null)
    {
    }

    private void DetectLanIp()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            if (endPoint != null)
            {
                LanAccessUrl = $"http://{endPoint.Address}:5246";
            }
        }
        catch
        {
            LanAccessUrl = "http://127.0.0.1:5246";
        }
    }

    partial void OnTargetContextTokensChanged(double value)
    {
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
        var models = await _ollamaModelService.LoadInstalledModelsAsync(ApiBase, Http);
        InstalledModels.Clear();
        foreach (var m in models)
        {
            InstalledModels.Add(m);
        }
    }

    [RelayCommand]
    public async Task UnloadAllVramAsync()
    {
        try
        {
            ToastService.Instance.Show("Unloading all models from VRAM...", ToastType.Info);
            await _ollamaModelService.UnloadAllVramAsync(ApiBase, Http);
            await Task.Delay(1000);
            await RefreshStatusAsync();
            ToastService.Instance.Show("All models unloaded from VRAM successfully.", ToastType.Success);
        }
        catch
        {
            ToastService.Instance.Show("Failed to unload VRAM.", ToastType.Error);
        }
    }

    [RelayCommand]
    public async Task SearchHuggingFaceAsync()
    {
        if (string.IsNullOrWhiteSpace(HfSearchQuery)) return;

        try
        {
            string query = HfSearchQuery ?? "";
            string url = $"{ApiBase}/api/hf/search?q={Uri.EscapeDataString(query)}";
            var response = await Http.GetAsync(url);
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
        catch
        {
            ToastService.Instance.Show("Failed to query Hugging Face Hub.", ToastType.Error);
        }
    }

    [RelayCommand]
    public async Task OpenHfModalAsync(string repoId)
    {
        if (string.IsNullOrWhiteSpace(repoId)) return;

        ModalRepoId = repoId;
        ModalAuthor = repoId.Split('/')[0];
        ModalHfFiles.Clear();
        IsHfModalOpen = true;

        try
        {
            string url = $"{ApiBase}/api/hf/model?repoId={Uri.EscapeDataString(repoId)}";
            var response = await Http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
                var siblings = doc?["siblings"]?.AsArray();

                if (siblings != null)
                {
                    foreach (var sib in siblings)
                    {
                        string rfilename = sib?["rfilename"]?.ToString() ?? "";
                        if (rfilename.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                        {
                            string quant = "Q4_K_M";
                            if (rfilename.Contains("Q8_0", StringComparison.OrdinalIgnoreCase)) quant = "Q8_0";
                            else if (rfilename.Contains("Q5_K_M", StringComparison.OrdinalIgnoreCase)) quant = "Q5_K_M";
                            else if (rfilename.Contains("FP16", StringComparison.OrdinalIgnoreCase)) quant = "FP16";

                            ModalHfFiles.Add(new HfFileQuantItem(rfilename, quant, "~4.5 GB", 4831838208L));
                        }
                    }
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    public void CloseHfModal()
    {
        IsHfModalOpen = false;
    }

    [RelayCommand]
    public async Task PullModelAsync(string fullPullString)
    {
        if (string.IsNullOrWhiteSpace(fullPullString)) return;

        IsHfModalOpen = false;
        PullModelName = fullPullString;
        PullProgressPercent = 0;
        PullStatusLog = $"Connecting to Ollama to pull '{fullPullString}'...\n";
        IsPullDrawerOpen = true;

        ToastService.Instance.Show($"Started pulling model '{fullPullString}'", ToastType.Info);

        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { name = fullPullString, stream = true }),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            using var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11434/api/pull") { Content = content };
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            if (resp.IsSuccessStatusCode)
            {
                using var stream = await resp.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    string? line = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        try
                        {
                            var doc = JsonNode.Parse(line);
                            string status = doc?["status"]?.ToString() ?? "";
                            long total = doc?["total"]?.GetValue<long>() ?? 0L;
                            long completed = doc?["completed"]?.GetValue<long>() ?? 0L;

                            if (total > 0)
                            {
                                PullProgressPercent = Math.Round(((double)completed / total) * 100, 1);
                                double compMb = completed / (1024.0 * 1024.0);
                                double totMb = total / (1024.0 * 1024.0);
                                PullProgressBytesText = $"{compMb:F1} MB / {totMb:F1} MB ({PullProgressPercent}%)";
                            }

                            PullStatusLog += $"{status}\n";
                        }
                        catch { }
                    }
                }

                ToastService.Instance.Show($"Model '{fullPullString}' pulled successfully!", ToastType.Success);
                await LoadInstalledModelsAsync();
            }
        }
        catch (Exception ex)
        {
            PullStatusLog += $"\nError: {ex.Message}\n";
            ToastService.Instance.Show($"Failed to pull model '{fullPullString}'.", ToastType.Error);
        }
    }

    [RelayCommand]
    public void ClosePullDrawer()
    {
        IsPullDrawerOpen = false;
    }

    [RelayCommand]
    public async Task SearchCivitaiAsync()
    {
        try
        {
            string query = CivitaiSearchQuery ?? "";
            string typeStr = SelectedCivitaiType ?? "";
            string url = $"{ApiBase}/api/civitai/search?q={Uri.EscapeDataString(query)}&types={Uri.EscapeDataString(typeStr)}";
            var response = await Http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
                var items = doc?["items"]?.AsArray();

                CivitaiResults.Clear();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        int id = item?["id"]?.GetValue<int>() ?? 0;
                        string name = item?["name"]?.ToString() ?? "Model";
                        string type = item?["type"]?.ToString() ?? "Checkpoint";
                        var modelVersions = item?["modelVersions"]?.AsArray();
                        string thumbUrl = "";
                        string downloadUrl = "";
                        string fileName = $"{name}.safetensors";

                        if (modelVersions != null && modelVersions.Count > 0)
                        {
                            var latest = modelVersions[0];
                            downloadUrl = latest?["downloadUrl"]?.ToString() ?? "";
                            var images = latest?["images"]?.AsArray();
                            if (images != null && images.Count > 0)
                            {
                                thumbUrl = images[0]?["url"]?.ToString() ?? "";
                            }
                            var files = latest?["files"]?.AsArray();
                            if (files != null && files.Count > 0)
                            {
                                fileName = files[0]?["name"]?.ToString() ?? fileName;
                            }
                        }

                        if (!string.IsNullOrEmpty(downloadUrl))
                        {
                            CivitaiResults.Add(new CivitaiModelItem(id, name, type, thumbUrl, downloadUrl, fileName, 4.9, 12400));
                        }
                    }
                }
            }
        }
        catch
        {
            ToastService.Instance.Show("Failed to query CivitAI.", ToastType.Error);
        }
    }

    [RelayCommand]
    public async Task DownloadCivitaiModelAsync(CivitaiModelItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.DownloadUrl)) return;

        ToastService.Instance.Show($"Downloading '{item.Name}' from CivitAI...", ToastType.Info);
        try
        {
            string url = $"{ApiBase}/api/civitai/download?fileUrl={Uri.EscapeDataString(item.DownloadUrl)}&modelType={Uri.EscapeDataString(item.Type)}&fileName={Uri.EscapeDataString(item.FileName)}";
            var response = await Http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                ToastService.Instance.Show($"Saved '{item.FileName}' directly to models disk!", ToastType.Success);
            }
        }
        catch
        {
            ToastService.Instance.Show($"Failed to download '{item.Name}'.", ToastType.Error);
        }
    }

    [RelayCommand]
    public void OpenWebUiInBrowser()
    {
        BrowserLauncher.OpenUrl(ApiBase);
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
                await Http.PostAsync(endpoint, null);
                ToastService.Instance.Show($"Triggered {engineName.ToUpper()} engine status update", ToastType.Info);
                await Task.Delay(1000);
                await RefreshStatusAsync();
            }
        }
        catch { }
    }

    private async Task CheckHealthAsync()
    {
        var (ollama, forge, comfy) = await _telemetryService.CheckServiceHealthAsync(ApiBase, ComfyUiUrl, Http);

        OllamaStatus = ollama ? "Online" : "Offline";
        ForgeStatus = forge ? "Online" : "Offline";
        ComfyStatus = comfy ? "Online" : "Offline";

        IsServiceRunning = ollama || forge || comfy;
        ServiceModeText = IsServiceRunning ? "Service Connected 🟢" : "Connecting...";
    }

    private async Task CheckGpuVramAsync()
    {
        var info = await _telemetryService.QueryGpuVramAsync(ApiBase, Http);
        GpuName = info.GpuName;
        VramTotalGb = info.TotalVramGb;
        VramUsedGb = info.UsedVramGb;
        VramPercentage = info.Percent;
        VramStatusText = $"{VramUsedGb} GB / {VramTotalGb} GB ({VramPercentage}%)";
    }

    [RelayCommand]
    public async Task LoadSettingsAsync()
    {
        try
        {
            var response = await Http.GetAsync($"{ApiBase}/api/settings");
            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                var settings = JsonSerializer.Deserialize<AppSettings>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (settings != null)
                {
                    ForgeModelsPath = settings.ForgeModelsPath;
                    ComfyUiUrl = settings.ComfyUiUrl;
                    ThreeDModelsPath = settings.ThreeDModelsPath;
                    WorkflowsPath = settings.WorkflowsPath;
                    PreferredImageEngine = settings.PreferredImageEngine;
                    ComfyUiExecutablePath = settings.ComfyUiExecutablePath;
                    ForgeExecutablePath = settings.ForgeExecutablePath;
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new AppSettings(
                ForgeModelsPath: this.ForgeModelsPath,
                ComfyUiUrl: this.ComfyUiUrl,
                ThreeDModelsPath: this.ThreeDModelsPath,
                WorkflowsPath: this.WorkflowsPath,
                PreferredImageEngine: this.PreferredImageEngine,
                ComfyUiExecutablePath: this.ComfyUiExecutablePath,
                ForgeExecutablePath: this.ForgeExecutablePath
            );

            var content = new StringContent(
                JsonSerializer.Serialize(settings),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await Http.PostAsync($"{ApiBase}/api/settings", content);
            if (response.IsSuccessStatusCode)
            {
                ToastService.Instance.Show("Settings saved successfully.", ToastType.Success);
            }
        }
        catch
        {
            ToastService.Instance.Show("Failed to save settings.", ToastType.Error);
        }
    }
}
