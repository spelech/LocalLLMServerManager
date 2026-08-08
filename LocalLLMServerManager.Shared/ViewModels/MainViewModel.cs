using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Services;

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

    public ObservableCollection<ToastItem> Toasts => ToastService.Instance.ActiveToasts;

    // Sub-ViewModels for modular feature breakdown
    public TelemetryViewModel Telemetry { get; }
    public OllamaLibraryViewModel Ollama { get; }
    public HuggingFaceSearchViewModel HuggingFace { get; }
    public CivitaiSearchViewModel Civitai { get; }
    public SettingsViewModel Settings { get; }

    public MainViewModel() : this(null)
    {
    }

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

        Telemetry = new TelemetryViewModel(telemetryService);
        Ollama = new OllamaLibraryViewModel(ollamaModelService);
        HuggingFace = new HuggingFaceSearchViewModel(hfSearchService);
        Civitai = new CivitaiSearchViewModel(civitaiSearchService);
        Settings = new SettingsViewModel();

        _ = RefreshStatusAsync();
        _ = LoadSettingsAsync();
        if (EnableAutomaticPolling)
        {
            _ = StartBackgroundPollingAsync();
        }
    }

    public string ApiBase { get; set; } = "http://127.0.0.1:5246";

    // Backward-compatible properties forwarding to sub-ViewModels
    public string GpuName { get => Telemetry.GpuName; set => Telemetry.GpuName = value; }
    public double VramUsedGb { get => Telemetry.VramUsedGb; set => Telemetry.VramUsedGb = value; }
    public double VramTotalGb { get => Telemetry.VramTotalGb; set => Telemetry.VramTotalGb = value; }
    public double VramPercentage { get => Telemetry.VramPercentage; set => Telemetry.VramPercentage = value; }
    public string VramStatusText { get => Telemetry.VramStatusText; set => Telemetry.VramStatusText = value; }
    public string OllamaStatus { get => Telemetry.OllamaStatus; set => Telemetry.OllamaStatus = value; }
    public string ForgeStatus { get => Telemetry.ForgeStatus; set => Telemetry.ForgeStatus = value; }
    public string ComfyStatus { get => Telemetry.ComfyStatus; set => Telemetry.ComfyStatus = value; }
    public string ServiceModeText { get => Telemetry.ServiceModeText; set => Telemetry.ServiceModeText = value; }
    public bool IsServiceRunning { get => Telemetry.IsServiceRunning; set => Telemetry.IsServiceRunning = value; }

    public ObservableCollection<OllamaModelItem> InstalledModels => Ollama.InstalledModels;
    public double TargetContextTokens { get => Ollama.TargetContextTokens; set => Ollama.TargetContextTokens = value; }
    public string EstimatedKvCacheText { get => Ollama.EstimatedKvCacheText; set => Ollama.EstimatedKvCacheText = value; }
    public string PullModelName { get => Ollama.PullModelName; set => Ollama.PullModelName = value; }
    public double PullProgressPercent { get => Ollama.PullProgressPercent; set => Ollama.PullProgressPercent = value; }
    public string PullProgressBytesText { get => Ollama.PullProgressBytesText; set => Ollama.PullProgressBytesText = value; }
    public string PullStatusLog { get => Ollama.PullStatusLog; set => Ollama.PullStatusLog = value; }
    public bool IsPullDrawerOpen { get => Ollama.IsPullDrawerOpen; set => Ollama.IsPullDrawerOpen = value; }

    public string HfSearchQuery { get => HuggingFace.HfSearchQuery; set => HuggingFace.HfSearchQuery = value; }
    public ObservableCollection<HuggingFaceRepoItem> HuggingFaceResults => HuggingFace.HuggingFaceResults;
    public bool IsHfModalOpen { get => HuggingFace.IsHfModalOpen; set => HuggingFace.IsHfModalOpen = value; }
    public string ModalRepoId { get => HuggingFace.ModalRepoId; set => HuggingFace.ModalRepoId = value; }
    public string ModalAuthor { get => HuggingFace.ModalAuthor; set => HuggingFace.ModalAuthor = value; }
    public ObservableCollection<HfFileQuantItem> ModalHfFiles => HuggingFace.ModalHfFiles;

    public string CivitaiSearchQuery { get => Civitai.CivitaiSearchQuery; set => Civitai.CivitaiSearchQuery = value; }
    public string SelectedCivitaiType { get => Civitai.SelectedCivitaiType; set => Civitai.SelectedCivitaiType = value; }
    public ObservableCollection<CivitaiModelItem> CivitaiResults => Civitai.CivitaiResults;

    public string ForgeModelsPath { get => Settings.ForgeModelsPath; set => Settings.ForgeModelsPath = value; }
    public string ComfyUiUrl { get => Settings.ComfyUiUrl; set => Settings.ComfyUiUrl = value; }
    public string ThreeDModelsPath { get => Settings.ThreeDModelsPath; set => Settings.ThreeDModelsPath = value; }
    public string WorkflowsPath { get => Settings.WorkflowsPath; set => Settings.WorkflowsPath = value; }
    public string PreferredImageEngine { get => Settings.PreferredImageEngine; set => Settings.PreferredImageEngine = value; }
    public string ComfyUiExecutablePath { get => Settings.ComfyUiExecutablePath; set => Settings.ComfyUiExecutablePath = value; }
    public string ForgeExecutablePath { get => Settings.ForgeExecutablePath; set => Settings.ForgeExecutablePath = value; }
    public string LanAccessUrl { get => Settings.LanAccessUrl; set => Settings.LanAccessUrl = value; }

    private async Task StartBackgroundPollingAsync()
    {
        while (EnableAutomaticPolling)
        {
            try
            {
                await Task.Delay(5000);
                if (!EnableAutomaticPolling) break;
                await RefreshStatusAsync();
            }
            catch { }
        }
    }

    [RelayCommand]
    public async Task RefreshStatusAsync()
    {
        await Telemetry.RefreshStatusAsync(ApiBase, ComfyUiUrl, Http);
        await Ollama.LoadInstalledModelsAsync(ApiBase, Http);
    }

    [RelayCommand]
    public async Task LoadInstalledModelsAsync() => await Ollama.LoadInstalledModelsAsync(ApiBase, Http);

    [RelayCommand]
    public async Task UnloadAllVramAsync() => await Ollama.UnloadAllVramAsync(ApiBase, Http);

    [RelayCommand]
    public async Task SearchHuggingFaceAsync() => await HuggingFace.SearchHuggingFaceAsync(ApiBase, Http);

    [RelayCommand]
    public async Task OpenHfModalAsync(string repoId) => await HuggingFace.OpenHfModalAsync(repoId, ApiBase, Http);

    [RelayCommand]
    public void CloseHfModal() => HuggingFace.CloseHfModal();

    [RelayCommand]
    public async Task PullModelAsync(string fullPullString) => await Ollama.PullModelAsync(fullPullString, Http);

    [RelayCommand]
    public void ClosePullDrawer() => Ollama.ClosePullDrawer();

    [RelayCommand]
    public async Task SearchCivitaiAsync() => await Civitai.SearchCivitaiAsync(ApiBase, Http);

    [RelayCommand]
    public async Task DownloadCivitaiModelAsync(CivitaiModelItem item) => await Civitai.DownloadCivitaiModelAsync(item, ApiBase, Http);

    [RelayCommand]
    public async Task ToggleEngineAsync(string engineName)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { engine = engineName }),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            await Http.PostAsync($"{ApiBase}/api/comfy/start", content);
            await RefreshStatusAsync();
        }
        catch { }
    }

    [RelayCommand]
    public void OpenWebUiInBrowser()
    {
        BrowserLauncher.OpenUrl("http://localhost:3000");
    }

    [RelayCommand]
    public async Task LoadSettingsAsync() => await Settings.LoadSettingsAsync(ApiBase, Http);

    [RelayCommand]
    public async Task SaveSettingsAsync() => await Settings.SaveSettingsAsync(ApiBase, Http);
}
