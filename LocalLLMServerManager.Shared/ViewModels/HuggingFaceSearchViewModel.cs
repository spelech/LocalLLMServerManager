using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    public ObservableCollection<HuggingFaceRepoItem> FilteredHuggingFaceResults { get; } = new();

    // Hardware Compatibility Filter Flags
    [ObservableProperty] private bool _isFullVramActive = true;
    [ObservableProperty] private bool _isPartialOffloadActive = true;
    [ObservableProperty] private bool _isCpuOnlyActive = true;
    [ObservableProperty] private bool _isOomActive = true;

    // Multimodal Input / Output Modality States
    public ObservableCollection<string> SelectedInputModalities { get; } = new() { "Text" };
    public ObservableCollection<string> SelectedOutputModalities { get; } = new() { "Text" };

    [ObservableProperty] private bool _isInputTextActive = true;
    [ObservableProperty] private bool _isInputImageActive = false;
    [ObservableProperty] private bool _isInputAudioActive = false;
    [ObservableProperty] private bool _isInputVideoActive = false;

    [ObservableProperty] private bool _isOutputTextActive = true;
    [ObservableProperty] private bool _isOutputImageActive = false;
    [ObservableProperty] private bool _isOutputAudioActive = false;
    [ObservableProperty] private bool _isOutputVideoActive = false;
    [ObservableProperty] private bool _isOutputThreeDActive = false;

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
        HuggingFaceResults.CollectionChanged += (s, e) => ApplyFilter();
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

        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredHuggingFaceResults.Clear();
        foreach (var r in HuggingFaceResults)
        {
            if (r.FitBadge == null)
            {
                FilteredHuggingFaceResults.Add(r);
                continue;
            }

            bool matches = r.FitBadge.FitVerdict switch
            {
                FitVerdict.FullVram => IsFullVramActive,
                FitVerdict.PartialOffload => IsPartialOffloadActive,
                FitVerdict.CpuOnly => IsCpuOnlyActive,
                FitVerdict.OutOfMemory => IsOomActive,
                _ => true
            };

            if (matches)
            {
                FilteredHuggingFaceResults.Add(r);
            }
        }
    }

    [RelayCommand]
    public void ToggleFitVerdict(string verdict)
    {
        var v = (verdict ?? "").Trim().ToLowerInvariant();
        if (v.Contains("full") || v.Contains("vram"))
        {
            IsFullVramActive = !IsFullVramActive;
        }
        else if (v.Contains("partial") || v.Contains("offload"))
        {
            IsPartialOffloadActive = !IsPartialOffloadActive;
        }
        else if (v.Contains("cpu"))
        {
            IsCpuOnlyActive = !IsCpuOnlyActive;
        }
        else if (v.Contains("oom") || v.Contains("won") || v.Contains("memory"))
        {
            IsOomActive = !IsOomActive;
        }
        ApplyFilter();
    }

    partial void OnIsFullVramActiveChanged(bool value) => ApplyFilter();
    partial void OnIsPartialOffloadActiveChanged(bool value) => ApplyFilter();
    partial void OnIsCpuOnlyActiveChanged(bool value) => ApplyFilter();
    partial void OnIsOomActiveChanged(bool value) => ApplyFilter();

    [RelayCommand]
    public void ToggleInputModality(string modality)
    {
        var m = (modality ?? "").Trim();
        if (m.Equals("Text", StringComparison.OrdinalIgnoreCase))
            IsInputTextActive = !IsInputTextActive;
        else if (m.Equals("Image", StringComparison.OrdinalIgnoreCase))
            IsInputImageActive = !IsInputImageActive;
        else if (m.Equals("Audio", StringComparison.OrdinalIgnoreCase))
            IsInputAudioActive = !IsInputAudioActive;
        else if (m.Equals("Video", StringComparison.OrdinalIgnoreCase))
            IsInputVideoActive = !IsInputVideoActive;

        SyncInputModalitiesList();
        _ = SearchHuggingFaceAsync();
    }

    [RelayCommand]
    public void ToggleOutputModality(string modality)
    {
        var m = (modality ?? "").Trim();
        if (m.Equals("Text", StringComparison.OrdinalIgnoreCase))
            IsOutputTextActive = !IsOutputTextActive;
        else if (m.Equals("Image", StringComparison.OrdinalIgnoreCase))
            IsOutputImageActive = !IsOutputImageActive;
        else if (m.Equals("Audio", StringComparison.OrdinalIgnoreCase))
            IsOutputAudioActive = !IsOutputAudioActive;
        else if (m.Equals("Video", StringComparison.OrdinalIgnoreCase))
            IsOutputVideoActive = !IsOutputVideoActive;
        else if (m.Equals("3D", StringComparison.OrdinalIgnoreCase) || m.Equals("ThreeD", StringComparison.OrdinalIgnoreCase))
            IsOutputThreeDActive = !IsOutputThreeDActive;

        SyncOutputModalitiesList();
        _ = SearchHuggingFaceAsync();
    }

    public void SyncInputModalitiesList()
    {
        SelectedInputModalities.Clear();
        if (IsInputTextActive) SelectedInputModalities.Add("Text");
        if (IsInputImageActive) SelectedInputModalities.Add("Image");
        if (IsInputAudioActive) SelectedInputModalities.Add("Audio");
        if (IsInputVideoActive) SelectedInputModalities.Add("Video");
    }

    public void SyncOutputModalitiesList()
    {
        SelectedOutputModalities.Clear();
        if (IsOutputTextActive) SelectedOutputModalities.Add("Text");
        if (IsOutputImageActive) SelectedOutputModalities.Add("Image");
        if (IsOutputAudioActive) SelectedOutputModalities.Add("Audio");
        if (IsOutputVideoActive) SelectedOutputModalities.Add("Video");
        if (IsOutputThreeDActive) SelectedOutputModalities.Add("3D");
    }

    [RelayCommand]
    public void ApplyPreset(string preset)
    {
        var p = (preset ?? "").Trim().ToLowerInvariant();
        if (p.Contains("multimodal") || p.Contains("vlm") || p.Contains("vision"))
        {
            IsInputTextActive = true;
            IsInputImageActive = true;
            IsInputAudioActive = false;
            IsInputVideoActive = false;

            IsOutputTextActive = true;
            IsOutputImageActive = false;
            IsOutputAudioActive = false;
            IsOutputVideoActive = false;
            IsOutputThreeDActive = false;
        }
        else if (p.Contains("llm") || p.Contains("text"))
        {
            IsInputTextActive = true;
            IsInputImageActive = false;
            IsInputAudioActive = false;
            IsInputVideoActive = false;

            IsOutputTextActive = true;
            IsOutputImageActive = false;
            IsOutputAudioActive = false;
            IsOutputVideoActive = false;
            IsOutputThreeDActive = false;
        }
        else if (p.Contains("image") || p.Contains("diffusion"))
        {
            IsInputTextActive = true;
            IsInputImageActive = false;
            IsInputAudioActive = false;
            IsInputVideoActive = false;

            IsOutputTextActive = false;
            IsOutputImageActive = true;
            IsOutputAudioActive = false;
            IsOutputVideoActive = false;
            IsOutputThreeDActive = false;
        }
        else if (p.Contains("video"))
        {
            IsInputTextActive = true;
            IsInputImageActive = true;
            IsInputAudioActive = false;
            IsInputVideoActive = false;

            IsOutputTextActive = false;
            IsOutputImageActive = false;
            IsOutputAudioActive = false;
            IsOutputVideoActive = true;
            IsOutputThreeDActive = false;
        }
        else if (p.Contains("audio") || p.Contains("speech") || p.Contains("tts"))
        {
            IsInputTextActive = true;
            IsInputImageActive = false;
            IsInputAudioActive = true;
            IsInputVideoActive = false;

            IsOutputTextActive = true;
            IsOutputImageActive = false;
            IsOutputAudioActive = true;
            IsOutputVideoActive = false;
            IsOutputThreeDActive = false;
        }
        else if (p.Contains("3d"))
        {
            IsInputTextActive = true;
            IsInputImageActive = true;
            IsInputAudioActive = false;
            IsInputVideoActive = false;

            IsOutputTextActive = false;
            IsOutputImageActive = false;
            IsOutputAudioActive = false;
            IsOutputVideoActive = false;
            IsOutputThreeDActive = true;
        }

        SyncInputModalitiesList();
        SyncOutputModalitiesList();
        _ = SearchHuggingFaceAsync();
    }

    public static List<string> ResolvePipelineTags(IEnumerable<string> inputs, IEnumerable<string> outputs)
    {
        var tags = new List<string>();
        var inSet = new HashSet<string>(inputs ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var outSet = new HashSet<string>(outputs ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        // VLM / Multimodal Vision-Language
        if (inSet.Contains("Text") && inSet.Contains("Image") && outSet.Contains("Text"))
        {
            tags.Add("image-text-to-text");
            tags.Add("image-to-text");
            tags.Add("visual-question-answering");
        }
        else if (inSet.Contains("Image") && outSet.Contains("Text"))
        {
            tags.Add("image-to-text");
        }
        else if (inSet.Contains("Text") && outSet.Contains("Text"))
        {
            tags.Add("text-generation");
        }

        if (inSet.Contains("Text") && outSet.Contains("Image"))
        {
            tags.Add("text-to-image");
        }

        if (inSet.Contains("Image") && outSet.Contains("Image"))
        {
            tags.Add("image-to-image");
        }

        if (outSet.Contains("Video"))
        {
            if (inSet.Contains("Image")) tags.Add("image-to-video");
            tags.Add("text-to-video");
        }

        if (outSet.Contains("Audio"))
        {
            tags.Add("text-to-speech");
            tags.Add("text-to-audio");
        }

        if (inSet.Contains("Audio") && outSet.Contains("Text"))
        {
            tags.Add("automatic-speech-recognition");
        }

        if (outSet.Contains("3D") || outSet.Contains("ThreeD"))
        {
            tags.Add("text-to-3d");
            tags.Add("image-to-3d");
        }

        return tags.Distinct().ToList();
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
            List<HuggingFaceRepoItem> results;
            if (!string.IsNullOrWhiteSpace(SelectedPipelineTag))
            {
                results = await _hfSearchService.SearchRepositoriesAsync(apiBase, HfSearchQuery, SelectedPipelineTag, http);
            }
            else
            {
                var resolvedTags = ResolvePipelineTags(SelectedInputModalities, SelectedOutputModalities);
                if (resolvedTags.Count > 0)
                {
                    results = await _hfSearchService.SearchRepositoriesAsync(apiBase, HfSearchQuery, resolvedTags, http);
                }
                else
                {
                    results = await _hfSearchService.SearchRepositoriesAsync(apiBase, HfSearchQuery, null as string, http);
                }
            }

            HuggingFaceResults.Clear();
            foreach (var r in results)
            {
                string modality = DetermineModality(r.Id, r.PipelineTag);
                var badge = _canIRunItService.EvaluateQuickFit(r.Id, null, modality, (long)TotalVramMb, (long)TotalRamMb);
                HuggingFaceResults.Add(r with { FitBadge = badge });
            }
            ApplyFilter();
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
