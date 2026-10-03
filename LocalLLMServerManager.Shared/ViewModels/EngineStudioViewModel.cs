using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

/// <summary>
/// ViewModel managing the fluid creative studio canvas, top modality switching,
/// creative prompt dock, fine-tuning parameters flyout, and dynamic model discovery.
/// </summary>
public partial class EngineStudioViewModel : ObservableObject
{
    private readonly ILocalModelScannerService _scanner;

    public ObservableCollection<LocalModelItem> ScannedModels { get; } = new();
    public ObservableCollection<string> AvailableCurrentModels { get; } = new();

    public EngineStudioViewModel() : this(new LocalModelScannerService())
    {
    }

    public EngineStudioViewModel(ILocalModelScannerService scanner)
    {
        _scanner = scanner;
        UpdateAvailableModelsForModality();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImageModalityActive))]
    [NotifyPropertyChangedFor(nameof(IsTextModalityActive))]
    [NotifyPropertyChangedFor(nameof(IsVideoModalityActive))]
    [NotifyPropertyChangedFor(nameof(Is3DModalityActive))]
    [NotifyPropertyChangedFor(nameof(IsAudioModalityActive))]
    [NotifyPropertyChangedFor(nameof(IsStickerModalityActive))]
    [NotifyPropertyChangedFor(nameof(ActiveModelBadge))]
    [NotifyPropertyChangedFor(nameof(ActivePromptPlaceholder))]
    [NotifyPropertyChangedFor(nameof(ShowForgeOfflineWarning))]
    private string _selectedModality = "Image";

    public bool IsImageModalityActive => SelectedModality == "Image" || SelectedModality == "Images";
    public bool IsTextModalityActive => SelectedModality == "Text";
    public bool IsVideoModalityActive => SelectedModality == "Video";
    public bool Is3DModalityActive => SelectedModality == "3D Mesh";
    public bool IsAudioModalityActive => SelectedModality == "Audio";
    public bool IsStickerModalityActive => SelectedModality == "Sticker" || SelectedModality == "Sticker Studio";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowForgeOfflineWarning))]
    private bool _isForgeOnline = false;

    public bool ShowForgeOfflineWarning => IsImageModalityActive && !IsForgeOnline && !IsGenerating;

    [ObservableProperty]
    private bool _isParametersFlyoutOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveModelBadge))]
    private string _imageModel = "SDXL Base 1.0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveModelBadge))]
    private string _textModel = "llama3.2:latest";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveModelBadge))]
    private string _videoModel = "Wan 2.2 / LTX-2.5";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveModelBadge))]
    private string _meshModel = "TRELLIS V2 (Gaussian Splat)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveModelBadge))]
    private string _audioModel = "Kokoro TTS (af_heart)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveModelBadge))]
    private string _stickerModel = "Sticker Studio (BirefNet + SDXL)";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAspectSquareActive))]
    [NotifyPropertyChangedFor(nameof(IsAspectLandscapeActive))]
    [NotifyPropertyChangedFor(nameof(IsAspectPortraitActive))]
    [NotifyPropertyChangedFor(nameof(IsAspectStandardActive))]
    private string _activeAspectPreset = "16:9";

    public bool IsAspectSquareActive => ActiveAspectPreset == "1:1";
    public bool IsAspectLandscapeActive => ActiveAspectPreset == "16:9";
    public bool IsAspectPortraitActive => ActiveAspectPreset == "9:16";
    public bool IsAspectStandardActive => ActiveAspectPreset == "4:3";

    [ObservableProperty]
    private int _steps = 30;

    [ObservableProperty]
    private double _cfgScale = 7.0;

    [ObservableProperty]
    private long _seed = -1;

    [ObservableProperty]
    private double _denoise = 0.75;

    [ObservableProperty]
    private string _promptText = "";

    [ObservableProperty]
    private string _negativePromptText = "";

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _attachedImagePath = "";

    [ObservableProperty]
    private bool _hasAttachedImage;

    public string ActiveModelBadge => SelectedModality switch
    {
        "Image" or "Images" => !string.IsNullOrWhiteSpace(ImageModel) ? ImageModel : "SDXL Base 1.0",
        "Text" => !string.IsNullOrWhiteSpace(TextModel) ? TextModel : "llama3.2:latest",
        "Video" => !string.IsNullOrWhiteSpace(VideoModel) ? VideoModel : "Wan 2.2 / LTX-2.5",
        "3D Mesh" => !string.IsNullOrWhiteSpace(MeshModel) ? MeshModel : "TRELLIS V2 (Gaussian Splat)",
        "Audio" => !string.IsNullOrWhiteSpace(AudioModel) ? AudioModel : "Kokoro TTS (af_heart)",
        "Sticker" or "Sticker Studio" => !string.IsNullOrWhiteSpace(StickerModel) ? StickerModel : "Sticker Studio (BirefNet + SDXL)",
        _ => "SDXL Base 1.0"
    };

    public string ActivePromptPlaceholder => SelectedModality switch
    {
        "Image" or "Images" => "Describe an image to generate with SDXL & Forge (e.g. 'cyberpunk street in rain')...",
        "Text" => "Enter a prompt or query for the local LLM...",
        "Video" => "Describe action and motion for video generation (e.g. 'cinematic drone sweep over mountains')...",
        "3D Mesh" => "Describe a 3D object to sculpt or synthesize (e.g. 'ancient stone relic with glowing runes')...",
        "Audio" => "Describe audio atmosphere or enter speech to synthesize with Kokoro TTS...",
        "Sticker" or "Sticker Studio" => "Describe a sticker design to generate with transparent background (e.g. 'cute astronaut cat sticker, die-cut vector')...",
        _ => "Describe what you want to create..."
    };

    public void UpdateAvailableModelsForModality()
    {
        AvailableCurrentModels.Clear();
        switch (SelectedModality)
        {
            case "Image" or "Images":
                var scannedImages = ScannedModels.Where(m => m.Category == LocalModelCategory.ImageCheckpoint || m.Category == LocalModelCategory.ImageLora).Select(m => m.Name).ToList();
                foreach (var m in scannedImages) AvailableCurrentModels.Add(m);
                var imageDefaults = new[] { "SDXL Base 1.0", "Flux.1 [dev]", "Flux.1 [schnell]", "SD 1.5", "Pony Diffusion V6", "Illustrious XL v0.1", "SDXL Turbo" };
                foreach (var d in imageDefaults)
                {
                    if (!AvailableCurrentModels.Contains(d)) AvailableCurrentModels.Add(d);
                }
                break;
            case "Text":
                var textDefaults = new[] { "llama3.2:latest", "mistral:latest", "deepseek-r1:latest", "qwen2.5:latest", "phi4:latest" };
                foreach (var d in textDefaults) AvailableCurrentModels.Add(d);
                break;
            case "Video":
                var scannedVideos = ScannedModels.Where(m => m.Category == LocalModelCategory.Video).Select(m => m.Name).ToList();
                foreach (var m in scannedVideos) AvailableCurrentModels.Add(m);
                var videoDefaults = new[] { "Wan 2.2 / LTX-2.5", "Wan 2.2 T2V (720p)", "Wan 2.2 I2V (480p)", "LTX-Video 2.5", "HunyuanVideo 1.5", "AnimateDiff SDXL" };
                foreach (var d in videoDefaults)
                {
                    if (!AvailableCurrentModels.Contains(d)) AvailableCurrentModels.Add(d);
                }
                break;
            case "3D Mesh":
                var scanned3D = ScannedModels.Where(m => m.Category == LocalModelCategory.ThreeD).Select(m => m.Name).ToList();
                foreach (var m in scanned3D) AvailableCurrentModels.Add(m);
                var threeDDefaults = new[] { "TRELLIS V2 (Gaussian Splat)", "Hunyuan3D V2", "InstantMesh" };
                foreach (var d in threeDDefaults)
                {
                    if (!AvailableCurrentModels.Contains(d)) AvailableCurrentModels.Add(d);
                }
                break;
            case "Audio":
                var scannedAudio = ScannedModels.Where(m => m.Category == LocalModelCategory.Audio).Select(m => m.Name).ToList();
                foreach (var m in scannedAudio) AvailableCurrentModels.Add(m);
                var audioDefaults = new[] { "Kokoro TTS (af_heart)", "Kokoro TTS (am_adam)", "Kokoro TTS (bf_emma)", "Stable Audio Open", "MusicGen Melody" };
                foreach (var d in audioDefaults)
                {
                    if (!AvailableCurrentModels.Contains(d)) AvailableCurrentModels.Add(d);
                }
                break;
            case "Sticker" or "Sticker Studio":
                AvailableCurrentModels.Add("Sticker Studio (BirefNet + SDXL)");
                break;
        }
    }

    [RelayCommand]
    public void SelectModel(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;

        switch (SelectedModality)
        {
            case "Image" or "Images":
                ImageModel = modelName;
                break;
            case "Text":
                TextModel = modelName;
                break;
            case "Video":
                VideoModel = modelName;
                break;
            case "3D Mesh":
                MeshModel = modelName;
                break;
            case "Audio":
                AudioModel = modelName;
                break;
            case "Sticker" or "Sticker Studio":
                StickerModel = modelName;
                break;
        }
        OnPropertyChanged(nameof(ActiveModelBadge));
    }

    public async Task LoadScannedModelsAsync(AppSettings? settings = null, string? baseDirectory = null)
    {
        var models = await _scanner.ScanAllModelsAsync(settings, baseDirectory);
        ScannedModels.Clear();
        foreach (var m in models)
        {
            ScannedModels.Add(m);
        }
        UpdateAvailableModelsForModality();
    }

    [RelayCommand]
    public void SelectModality(string modality)
    {
        if (string.IsNullOrWhiteSpace(modality)) return;
        SelectedModality = modality;
        UpdateAvailableModelsForModality();
        OnPropertyChanged(nameof(IsImageModalityActive));
        OnPropertyChanged(nameof(IsTextModalityActive));
        OnPropertyChanged(nameof(IsVideoModalityActive));
        OnPropertyChanged(nameof(Is3DModalityActive));
        OnPropertyChanged(nameof(IsAudioModalityActive));
        OnPropertyChanged(nameof(IsStickerModalityActive));
        OnPropertyChanged(nameof(ActiveModelBadge));
        OnPropertyChanged(nameof(ActivePromptPlaceholder));
    }

    [RelayCommand]
    public void ToggleParametersFlyout()
    {
        IsParametersFlyoutOpen = !IsParametersFlyoutOpen;
    }

    [RelayCommand]
    public void SelectAspectPreset(string preset)
    {
        if (!string.IsNullOrWhiteSpace(preset))
        {
            ActiveAspectPreset = preset;
        }
    }

    [RelayCommand]
    public void AttachImage()
    {
        HasAttachedImage = !HasAttachedImage;
    }

    [RelayCommand]
    public void ApplyPromptChip(string prompt)
    {
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            PromptText = prompt;
        }
    }

    [RelayCommand]
    public void GenerateFromDock()
    {
        Generate();
    }

    [RelayCommand]
    public void Generate()
    {
        IsGenerating = !IsGenerating;
    }
}
