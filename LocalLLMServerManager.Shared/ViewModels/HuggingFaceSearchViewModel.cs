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
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isModalLoading = false;
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

    [ObservableProperty] private string? _activePreset = "LLM";

    public bool IsPresetMultimodal => string.Equals(ActivePreset, "Multimodal", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetLlm => string.Equals(ActivePreset, "LLM", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetImage => string.Equals(ActivePreset, "Image", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetVideo => string.Equals(ActivePreset, "Video", StringComparison.OrdinalIgnoreCase);
    public bool IsPresetAudio => string.Equals(ActivePreset, "Audio", StringComparison.OrdinalIgnoreCase);
    public bool IsPreset3D => string.Equals(ActivePreset, "3D", StringComparison.OrdinalIgnoreCase);

    partial void OnActivePresetChanged(string? value)
    {
        OnPropertyChanged(nameof(IsPresetMultimodal));
        OnPropertyChanged(nameof(IsPresetLlm));
        OnPropertyChanged(nameof(IsPresetImage));
        OnPropertyChanged(nameof(IsPresetVideo));
        OnPropertyChanged(nameof(IsPresetAudio));
        OnPropertyChanged(nameof(IsPreset3D));
    }

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
    public Action<string>? OnPullModelRequested { get; set; }

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
        LoadCuratedStarterModels();
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

        ActivePreset = null;
        SelectedPipelineTag = null;
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

        ActivePreset = null;
        SelectedPipelineTag = null;
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
        SelectedPipelineTag = null;
        var p = (preset ?? "").Trim().ToLowerInvariant();
        if (p.Contains("multimodal") || p.Contains("vlm") || p.Contains("vision"))
        {
            ActivePreset = "Multimodal";
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
            ActivePreset = "LLM";
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
            ActivePreset = "Image";
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
            ActivePreset = "Video";
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
            ActivePreset = "Audio";
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
            ActivePreset = "3D";
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
        else if (inSet.Contains("Video") && outSet.Contains("Text"))
        {
            tags.Add("video-text-to-text");
        }
        else if (inSet.Contains("Text") && outSet.Contains("Text") && !inSet.Contains("Audio") && !outSet.Contains("Audio") && !outSet.Contains("Image") && !outSet.Contains("Video") && !outSet.Contains("3D"))
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
            if (inSet.Contains("Video")) tags.Add("video-to-video");
            tags.Add("text-to-video");
        }

        if (outSet.Contains("Audio"))
        {
            tags.Add("text-to-speech");
            tags.Add("text-to-audio");
            if (inSet.Contains("Audio")) tags.Add("audio-to-audio");
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

        // Check multimodal VLM first so image-to-text / image-text-to-text isn't misclassified as image diffusion
        if (tag.Contains("image-text-to-text") || tag.Contains("image-to-text") || tag.Contains("visual-question-answering") || tag.Contains("vlm") || name.Contains("vl-") || name.Contains("-vl"))
            return "LLM";

        if (tag.Contains("video") || name.Contains("wan") || name.Contains("ltx") || name.Contains("hunyuanvideo") || name.Contains("cogvideox"))
            return "Video";
        if (tag.Contains("audio") || tag.Contains("speech") || tag.Contains("tts") || name.Contains("kokoro") || name.Contains("whisper") || name.Contains("xtts"))
            return "Audio";
        if (tag.Contains("3d") || name.Contains("trellis") || name.Contains("hunyuan3d"))
            return "ThreeD";
        if (tag.Contains("image") || tag.Contains("diffusion") || name.Contains("flux") || name.Contains("sdxl") || name.Contains("stable-diffusion") || name.Contains("sd-") || name.Contains("sd3"))
            return "Image";

        return "LLM";
    }

    public static List<HuggingFaceRepoItem> GetCuratedStarterModels()
    {
        return new List<HuggingFaceRepoItem>
        {
            new HuggingFaceRepoItem("Qwen/Qwen2.5-Coder-7B-Instruct-GGUF", "Qwen", 4820, "1,250,000 downloads", "text-generation"),
            new HuggingFaceRepoItem("meta-llama/Llama-3.2-3B-Instruct-GGUF", "meta-llama", 3100, "980,000 downloads", "text-generation"),
            new HuggingFaceRepoItem("deepseek-ai/DeepSeek-R1-Distill-Qwen-7B-GGUF", "deepseek-ai", 5200, "1,450,000 downloads", "text-generation"),
            new HuggingFaceRepoItem("Wan-AI/Wan2.1-T2V-14B", "Wan-AI", 2800, "420,000 downloads", "text-to-video"),
            new HuggingFaceRepoItem("hexgrad/Kokoro-82M", "hexgrad", 3900, "680,000 downloads", "text-to-audio"),
            new HuggingFaceRepoItem("black-forest-labs/FLUX.1-schnell", "black-forest-labs", 6200, "1,850,000 downloads", "text-to-image")
        };
    }

    public void LoadCuratedStarterModels()
    {
        if (HuggingFaceResults.Count > 0) return;

        foreach (var r in GetCuratedStarterModels())
        {
            string modality = DetermineModality(r.Id, r.PipelineTag);
            var badge = _canIRunItService.EvaluateQuickFit(r.Id, null, modality, (long)TotalVramMb, (long)TotalRamMb);
            HuggingFaceResults.Add(r with { FitBadge = badge });
        }
        ApplyFilter();
    }

    public async Task LoadDefaultModelsAsync(string apiBase, HttpClient http)
    {
        try
        {
            var results = await _hfSearchService.SearchRepositoriesAsync(apiBase, "", "text-generation", http);
            if (results != null && results.Count > 0)
            {
                HuggingFaceResults.Clear();
                foreach (var r in results)
                {
                    string modality = DetermineModality(r.Id, r.PipelineTag);
                    var badge = _canIRunItService.EvaluateQuickFit(r.Id, null, modality, (long)TotalVramMb, (long)TotalRamMb);
                    HuggingFaceResults.Add(r with { FitBadge = badge });
                }
                ApplyFilter();
            }
        }
        catch
        {
            // Curated starter models are already loaded, retain them
        }
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
        IsLoading = true;
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

            if (results.Count == 0 && string.IsNullOrWhiteSpace(HfSearchQuery))
            {
                LoadCuratedStarterModels();
            }

            ApplyFilter();
        }
        catch
        {
            if (HuggingFaceResults.Count == 0 && string.IsNullOrWhiteSpace(HfSearchQuery))
            {
                LoadCuratedStarterModels();
            }
            ToastService.Instance.Show("Failed to query Hugging Face Hub.", ToastType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task OpenHfModalAsync(string repoId, string apiBase, HttpClient http)
    {
        if (string.IsNullOrWhiteSpace(repoId)) return;

        ModalRepoId = repoId;
        ModalAuthor = repoId.Contains("/") ? repoId.Split('/')[0] : "Community";
        ModalHfFiles.Clear();
        IsHfModalOpen = true;
        IsModalLoading = true;

        try
        {
            var quants = await _hfSearchService.FetchQuantizationsAsync(apiBase, repoId, http);
            foreach (var q in quants)
            {
                var badge = _canIRunItService.EvaluateQuickFit(q.Filename, q.SizeBytes > 0 ? q.SizeBytes : null, "LLM", (long)TotalVramMb, (long)TotalRamMb);
                ModalHfFiles.Add(q with { FitBadge = badge });
            }
        }
        finally
        {
            IsModalLoading = false;
        }
    }

    [RelayCommand]
    public void CloseHfModal()
    {
        IsHfModalOpen = false;
    }

    [RelayCommand]
    public void OpenInBrowser(string? repoId)
    {
        if (string.IsNullOrWhiteSpace(repoId)) return;
        var safeId = repoId.Trim();
        BrowserLauncher.OpenUrl($"https://huggingface.co/{safeId}");
    }

    [RelayCommand]
    public async Task OpenHfModalAsync(HuggingFaceRepoItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;
        await OpenHfModalAsync(item.Id, ApiBase, HttpHelper.CreateClient(ApiBase));
    }

    [RelayCommand]
    public async Task DownloadHfFileAsync(HfFileQuantItem? file)
    {
        if (file == null || string.IsNullOrWhiteSpace(file.Filename) || string.IsNullOrWhiteSpace(ModalRepoId)) return;
        await DownloadHfFileAsync(file, ApiBase, HttpHelper.CreateClient(ApiBase));
    }

    public async Task DownloadHfFileAsync(HfFileQuantItem file, string apiBase, HttpClient http)
    {
        if (file == null || string.IsNullOrWhiteSpace(file.Filename) || string.IsNullOrWhiteSpace(ModalRepoId)) return;

        var fileUrl = $"https://huggingface.co/{ModalRepoId}/resolve/main/{file.Filename}";
        var pipelineTag = SelectedPipelineTag ?? DetermineModality(ModalRepoId, null);

        ToastService.Instance.Show($"Queued download for '{file.Filename}'", ToastType.Info);

        try
        {
            var url = $"{apiBase}/api/hf/download?fileUrl={Uri.EscapeDataString(fileUrl)}&fileName={Uri.EscapeDataString(file.Filename)}&pipelineTag={Uri.EscapeDataString(pipelineTag)}";
            var resp = await http.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                ToastService.Instance.Show($"Download started for '{file.Filename}'", ToastType.Success);
            }
            else
            {
                ToastService.Instance.Show($"Download failed ({(int)resp.StatusCode}) for '{file.Filename}'", ToastType.Error);
            }
        }
        catch
        {
            ToastService.Instance.Show($"Failed to queue download for '{file.Filename}'", ToastType.Error);
        }
    }

    [RelayCommand]
    public void PullHfGgufInOllama(HfFileQuantItem? file)
    {
        if (file == null || string.IsNullOrWhiteSpace(ModalRepoId)) return;
        string quant = (file.Quantization ?? "").Trim().ToLowerInvariant();
        string pullTag = string.IsNullOrEmpty(quant) ? $"hf.co/{ModalRepoId}" : $"hf.co/{ModalRepoId}:{quant}";
        OnPullModelRequested?.Invoke(pullTag);
    }
}


