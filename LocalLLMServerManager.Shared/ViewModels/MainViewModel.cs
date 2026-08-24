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
    string Downloads,
    string PipelineTag = ""
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

public record VideoAssetItem(
    string Filename,
    string Url,
    string Duration,
    string Resolution,
    int Fps,
    long Seed,
    long SizeBytes,
    DateTime CreatedAt
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
    public AudioStudioViewModel Audio { get; }

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

        if (Http.BaseAddress != null)
        {
            ApiBase = Http.BaseAddress.ToString().TrimEnd('/');
        }
        else if (OperatingSystem.IsBrowser())
        {
            ApiBase = GetDefaultApiBase();
        }

        Telemetry = new TelemetryViewModel(telemetryService);
        Ollama = new OllamaLibraryViewModel(ollamaModelService);
        HuggingFace = new HuggingFaceSearchViewModel(hfSearchService);
        Civitai = new CivitaiSearchViewModel(civitaiSearchService);
        Settings = new SettingsViewModel();
        Audio = new AudioStudioViewModel();

        _ = RefreshStatusAsync();
        _ = Audio.LoadAudioWorkflowsAsync(ApiBase, Http);
        _ = Audio.LoadAudioFilesAsync(ApiBase, Http);
        _ = LoadSettingsAsync();
        if (EnableAutomaticPolling)
        {
            _ = StartBackgroundPollingAsync();
        }
    }

    private static string GetDefaultApiBase()
    {
        if (OperatingSystem.IsBrowser())
        {
            try
            {
                var origin = GetBrowserOrigin();
                if (!string.IsNullOrWhiteSpace(origin))
                {
                    return origin.TrimEnd('/');
                }
            }
            catch { }

            var envBase = Environment.GetEnvironmentVariable("APP_API_BASE");
            if (!string.IsNullOrWhiteSpace(envBase))
            {
                return envBase.TrimEnd('/');
            }
        }
        return "http://127.0.0.1:5246";
    }

    [System.Runtime.InteropServices.JavaScript.JSImport("globalThis.getOrigin")]
    internal static partial string GetBrowserOrigin();

    public string ApiBase { get; set; } = GetDefaultApiBase();

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
    public string SelectedTheme { get => Settings.SelectedTheme; set => Settings.SelectedTheme = value; }
    public System.Collections.Generic.IReadOnlyList<string> AvailableThemes => Settings.AvailableThemes;

    // Studio & Video Studio Observable Properties
    [ObservableProperty]
    private string _selectedStudioMode = "Video"; // "Images", "3D Mesh", "Video"

    [ObservableProperty]
    private string _selectedVideoWorkflow = "AnimateDiff SDXL";

    [ObservableProperty]
    private bool _isGeneratingVideo;

    [ObservableProperty]
    private double _videoGenerationProgress;

    [ObservableProperty]
    private string _renderedVideoUrl = "";

    [ObservableProperty]
    private string _videoPrompt = "a detailed high resolution video of a woman walking in Tokyo, dynamic motion";

    [ObservableProperty]
    private string _videoNegativePrompt = "deformed, blurry, low quality, static, artifacts";

    [ObservableProperty]
    private string _videoResolution = "832x480";

    [ObservableProperty]
    private int _videoFrameCount = 48;

    [ObservableProperty]
    private long _videoSeed = 42890;

    [ObservableProperty]
    private string _videoDurationText = "3.0s";

    [ObservableProperty]
    private string _videoResolutionBadge = "832x480";

    [ObservableProperty]
    private string _videoFpsBadge = "16 fps";

    [ObservableProperty]
    private string _videoSeedBadge = "42890";

    [ObservableProperty]
    private bool _isVideoLooping = true;

    [ObservableProperty]
    private bool _isVideoPlaying = true;

    public ObservableCollection<VideoAssetItem> GeneratedVideosList { get; } = new();

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

    [RelayCommand]
    public async Task GenerateAudioAsync()
    {
        await Audio.GenerateAudioAsync(new ParamContext(ApiBase, Http));
    }

    [RelayCommand]
    public async Task GenerateVideoAsync()
    {
        if (IsGeneratingVideo) return;

        IsGeneratingVideo = true;
        VideoGenerationProgress = 10;

        try
        {
            var req = new
            {
                Prompt = VideoPrompt,
                NegativePrompt = VideoNegativePrompt,
                Workflow = SelectedVideoWorkflow,
                Resolution = VideoResolution,
                FrameCount = VideoFrameCount,
                Seed = VideoSeed
            };

            var content = new StringContent(
                JsonSerializer.Serialize(req),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            VideoGenerationProgress = 40;
            var response = await Http.PostAsync($"{ApiBase}/api/video/generate", content);
            VideoGenerationProgress = 80;

            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                var url = root.GetProperty("url").GetString() ?? "";
                var duration = root.TryGetProperty("duration", out var durProp) ? durProp.GetString() ?? "3.0s" : "3.0s";
                var resolution = root.TryGetProperty("resolution", out var resProp) ? resProp.GetString() ?? "832x480" : "832x480";
                var fps = root.TryGetProperty("fps", out var fpsProp) ? fpsProp.GetInt32() : 16;
                var seed = root.TryGetProperty("seed", out var seedProp) ? seedProp.GetInt64() : VideoSeed;
                var filename = root.TryGetProperty("filename", out var fnProp) ? fnProp.GetString() ?? "video.mp4" : "video.mp4";

                RenderedVideoUrl = url.StartsWith("http") ? url : $"{ApiBase}{url}";
                VideoDurationText = duration;
                VideoResolutionBadge = resolution;
                VideoFpsBadge = $"{fps} fps";
                VideoSeedBadge = seed.ToString();

                var item = new VideoAssetItem(filename, RenderedVideoUrl, duration, resolution, fps, seed, 1024 * 1024, DateTime.UtcNow);
                GeneratedVideosList.Insert(0, item);
                ToastService.Instance.Show("Video generated successfully!", ToastType.Success);
            }
            else
            {
                ToastService.Instance.Show("Failed to generate video.", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"Video Generation Error: {ex.Message}", ToastType.Error);
        }
        finally
        {
            VideoGenerationProgress = 100;
            IsGeneratingVideo = false;
        }
    }

    [RelayCommand]
    public async Task LoadGeneratedVideosAsync()
    {
        try
        {
            var response = await Http.GetAsync($"{ApiBase}/api/video/files");
            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);
                GeneratedVideosList.Clear();

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var filename = el.GetProperty("filename").GetString() ?? "";
                    var url = el.GetProperty("url").GetString() ?? "";
                    var fullUrl = url.StartsWith("http") ? url : $"{ApiBase}{url}";
                    var duration = el.TryGetProperty("duration", out var dur) ? dur.GetString() ?? "3.0s" : "3.0s";
                    var resolution = el.TryGetProperty("resolution", out var res) ? res.GetString() ?? "832x480" : "832x480";
                    var fps = el.TryGetProperty("fps", out var fpsProp) ? fpsProp.GetInt32() : 16;
                    var seed = el.TryGetProperty("seed", out var seedProp) ? seedProp.GetInt64() : 42890L;
                    var sizeBytes = el.TryGetProperty("sizeBytes", out var size) ? size.GetInt64() : 0L;
                    var createdAt = el.TryGetProperty("createdAt", out var dt) ? dt.GetDateTime() : DateTime.UtcNow;

                    GeneratedVideosList.Add(new VideoAssetItem(filename, fullUrl, duration, resolution, fps, seed, sizeBytes, createdAt));
                }

                if (GeneratedVideosList.Count > 0 && string.IsNullOrEmpty(RenderedVideoUrl))
                {
                    SelectVideo(GeneratedVideosList[0]);
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    public void SelectVideo(VideoAssetItem item)
    {
        if (item == null) return;
        RenderedVideoUrl = item.Url;
        VideoDurationText = item.Duration;
        VideoResolutionBadge = item.Resolution;
        VideoFpsBadge = $"{item.Fps} fps";
        VideoSeedBadge = item.Seed.ToString();
    }

    [RelayCommand]
    public void DownloadVideo()
    {
        if (!string.IsNullOrWhiteSpace(RenderedVideoUrl))
        {
            BrowserLauncher.OpenUrl(RenderedVideoUrl);
        }
    }

    [RelayCommand]
    public void ToggleVideoPlay()
    {
        IsVideoPlaying = !IsVideoPlaying;
    }

    [RelayCommand]
    public void ToggleVideoLoop()
    {
        IsVideoLooping = !IsVideoLooping;
    }
}
