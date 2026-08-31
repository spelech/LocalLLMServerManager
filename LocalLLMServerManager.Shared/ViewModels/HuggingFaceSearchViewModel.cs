using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class HuggingFaceSearchViewModel : ObservableObject
{
    private readonly IHuggingFaceSearchService _hfSearchService;
    private readonly ICanIRunItService _canIRunItService;
    private readonly ITelemetryService? _telemetryService;

    [ObservableProperty] private string _hfSearchQuery = "";
    [ObservableProperty] private string? _selectedPipelineTag = null;
    public ObservableCollection<HuggingFaceRepoItem> HuggingFaceResults { get; } = new();

    [ObservableProperty] private bool _isHfModalOpen = false;
    [ObservableProperty] private string _modalRepoId = "";
    [ObservableProperty] private string _modalAuthor = "";
    public ObservableCollection<HfFileQuantItem> ModalHfFiles { get; } = new();
    [ObservableProperty] private string _apiBase = OperatingSystem.IsBrowser() ? "" : "http://127.0.0.1:5246";

    [ObservableProperty] private double _totalVramMb = 16384.0;
    [ObservableProperty] private double _totalRamMb = 32768.0;

    public Action<string, string>? OnInspectModelRequested { get; set; }

    public HuggingFaceSearchViewModel(IHuggingFaceSearchService hfSearchService)
        : this(hfSearchService, new CanIRunItService(), null)
    {
    }

    public HuggingFaceSearchViewModel(
        IHuggingFaceSearchService hfSearchService,
        ICanIRunItService? canIRunItService,
        ITelemetryService? telemetryService = null)
    {
        _hfSearchService = hfSearchService;
        _canIRunItService = canIRunItService ?? new CanIRunItService();
        _telemetryService = telemetryService;
    }

    public void UpdateHardwareTelemetry(double totalVramMb, double totalRamMb)
    {
        if (totalVramMb > 0) TotalVramMb = totalVramMb;
        if (totalRamMb > 0) TotalRamMb = totalRamMb;

        RecomputeBadges();
    }

    public void RecomputeBadges()
    {
        for (int i = 0; i < HuggingFaceResults.Count; i++)
        {
            var r = HuggingFaceResults[i];
            string modality = DetermineModality(r.Id, r.PipelineTag);
            var badge = _canIRunItService.EvaluateQuickFit(r.Id, null, modality, (long)TotalVramMb, (long)TotalRamMb);
            HuggingFaceResults[i] = r with { FitBadge = badge };
        }

        for (int i = 0; i < ModalHfFiles.Count; i++)
        {
            var q = ModalHfFiles[i];
            var badge = _canIRunItService.EvaluateQuickFit(q.Filename, q.SizeBytes > 0 ? q.SizeBytes : null, "LLM", (long)TotalVramMb, (long)TotalRamMb);
            ModalHfFiles[i] = q with { FitBadge = badge };
        }
    }

    public static string DetermineModality(string modelName, string? pipelineTag)
    {
        string tag = (pipelineTag ?? "").ToLowerInvariant();
        string name = (modelName ?? "").ToLowerInvariant();

        if (tag.Contains("video") || name.Contains("wan") || name.Contains("ltx") || name.Contains("hunyuanvideo"))
            return "Video";
        if (tag.Contains("audio") || tag.Contains("speech") || tag.Contains("tts") || name.Contains("kokoro") || name.Contains("whisper") || name.Contains("xtts"))
            return "Audio";
        if (tag.Contains("3d") || name.Contains("trellis") || name.Contains("hunyuan3d"))
            return "ThreeD";
        if (tag.Contains("image") || tag.Contains("diffusion") || name.Contains("flux") || name.Contains("sdxl") || name.Contains("stable-diffusion") || name.Contains("sd-") || name.Contains("sd3"))
            return "Image";

        return "LLM";
    }

    [RelayCommand]
    public void NavigateToCanIRunIt(string? modelName)
    {
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            string modality = DetermineModality(modelName, SelectedPipelineTag);
            OnInspectModelRequested?.Invoke(modelName, modality);
        }
    }

    [RelayCommand]
    public void InspectModel(HuggingFaceRepoItem? item)
    {
        if (item != null)
        {
            string modality = DetermineModality(item.Id, item.PipelineTag);
            OnInspectModelRequested?.Invoke(item.Id, modality);
        }
    }

    [RelayCommand]
    public void InspectQuantFile(HfFileQuantItem? file)
    {
        if (file != null)
        {
            OnInspectModelRequested?.Invoke(file.Filename, "LLM");
        }
    }

    [RelayCommand]
    public async Task SearchHuggingFaceAsync()
    {
        await SearchHuggingFaceAsync(ApiBase, HttpHelper.CreateClient(ApiBase));
    }

    [RelayCommand]
    public async Task SelectCategoryAsync(string? tag)
    {
        SelectedPipelineTag = string.IsNullOrWhiteSpace(tag) ? null : tag;
        await SearchHuggingFaceAsync();
    }

    public async Task SearchHuggingFaceAsync(string apiBase, HttpClient http)
    {
        try
        {
            var results = await _hfSearchService.SearchRepositoriesAsync(apiBase, HfSearchQuery, SelectedPipelineTag, http);
            HuggingFaceResults.Clear();
            foreach (var r in results)
            {
                string modality = DetermineModality(r.Id, r.PipelineTag);
                var badge = _canIRunItService.EvaluateQuickFit(r.Id, null, modality, (long)TotalVramMb, (long)TotalRamMb);
                HuggingFaceResults.Add(r with { FitBadge = badge });
            }
        }
        catch
        {
            ToastService.Instance.Show("Failed to query Hugging Face Hub.", ToastType.Error);
        }
    }

    public async Task OpenHfModalAsync(string repoId, string apiBase, HttpClient http)
    {
        if (string.IsNullOrWhiteSpace(repoId)) return;

        ModalRepoId = repoId;
        ModalAuthor = repoId.Contains("/") ? repoId.Split('/')[0] : "Community";
        ModalHfFiles.Clear();
        IsHfModalOpen = true;

        var quants = await _hfSearchService.FetchQuantizationsAsync(apiBase, repoId, http);
        foreach (var q in quants)
        {
            var badge = _canIRunItService.EvaluateQuickFit(q.Filename, q.SizeBytes > 0 ? q.SizeBytes : null, "LLM", (long)TotalVramMb, (long)TotalRamMb);
            ModalHfFiles.Add(q with { FitBadge = badge });
        }
    }

    [RelayCommand]
    public void CloseHfModal()
    {
        IsHfModalOpen = false;
    }
}
