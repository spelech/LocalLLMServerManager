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
    private string _selectedModality = "Image";

    public bool IsImageModalityActive => SelectedModality == "Image" || SelectedModality == "Images";
    public bool IsTextModalityActive => SelectedModality == "Text";
    public bool IsVideoModalityActive => SelectedModality == "Video";
    public bool Is3DModalityActive => SelectedModality == "3D Mesh";
    public bool IsAudioModalityActive => SelectedModality == "Audio";
    public bool IsStickerModalityActive => SelectedModality == "Sticker" || SelectedModality == "Sticker Studio";

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
    private string _activeAspectPreset = "16:9";

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

/// <summary>
/// Partial class extensions to MainViewModel to support the Fluid Studio Canvas & Creative Prompt Dock
/// when EngineStudioTabControl is hosted with DataContext="{Binding}" (MainViewModel).
/// </summary>
public partial class MainViewModel
{
    [ObservableProperty]
    private EngineStudioViewModel _studio = new();

    public ObservableCollection<LocalModelItem> ScannedModels => Studio.ScannedModels;
    public ObservableCollection<string> AvailableCurrentModels => Studio.AvailableCurrentModels;

    public ObservableCollection<LocalModelItem> ScannedImageModels { get; } = new();
    public ObservableCollection<LocalModelItem> ScannedVideoModels { get; } = new();
    public ObservableCollection<LocalModelItem> ScannedAudioModels { get; } = new();
    public ObservableCollection<LocalModelItem> ScannedThreeDModels { get; } = new();

    [ObservableProperty]
    private LocalModelCategory _selectedManageModelCategory = LocalModelCategory.Ollama;

    [RelayCommand]
    public void SelectManageCategory(string categoryStr)
    {
        if (Enum.TryParse<LocalModelCategory>(categoryStr, out var cat))
        {
            SelectedManageModelCategory = cat;
            OnPropertyChanged(nameof(IsManageOllamaSelected));
            OnPropertyChanged(nameof(IsManageImageSelected));
            OnPropertyChanged(nameof(IsManageVideoSelected));
            OnPropertyChanged(nameof(IsManageAudioSelected));
            OnPropertyChanged(nameof(IsManageThreeDSelected));
        }
    }

    public bool IsManageOllamaSelected => SelectedManageModelCategory == LocalModelCategory.Ollama;
    public bool IsManageImageSelected => SelectedManageModelCategory == LocalModelCategory.ImageCheckpoint || SelectedManageModelCategory == LocalModelCategory.ImageLora;
    public bool IsManageVideoSelected => SelectedManageModelCategory == LocalModelCategory.Video;
    public bool IsManageAudioSelected => SelectedManageModelCategory == LocalModelCategory.Audio;
    public bool IsManageThreeDSelected => SelectedManageModelCategory == LocalModelCategory.ThreeD;

    public string SelectedStudioModelText
    {
        get => ActiveModelBadge;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && value != ActiveModelBadge)
            {
                SelectStudioModel(value);
            }
        }
    }

    [RelayCommand]
    public void SelectStudioModel(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return;
        Studio.SelectModel(modelName);
        if (IsImageModalityActive) SelectedImageWorkflow = modelName;
        else if (IsVideoModalityActive) SelectedVideoWorkflow = modelName;
        else if (IsAudioModalityActive && Audio != null) Audio.VoiceProfile = modelName;
        OnPropertyChanged(nameof(ActiveModelBadge));
        OnPropertyChanged(nameof(SelectedStudioModelText));
        ToastService.Instance.Show($"Active model set to '{modelName}'", ToastType.Success);
    }

    [RelayCommand]
    public void UseModelInStudio(LocalModelItem item)
    {
        if (item == null) return;
        var targetModality = item.Category switch
        {
            LocalModelCategory.ImageCheckpoint or LocalModelCategory.ImageLora => "Image",
            LocalModelCategory.Video => "Video",
            LocalModelCategory.Audio => "Audio",
            LocalModelCategory.ThreeD => "3D Mesh",
            _ => "Image"
        };
        SelectModality(targetModality);
        SelectStudioModel(item.Name);
        SelectedTabIndex = 1;
    }

    [RelayCommand]
    public void RevealModelInExplorer(LocalModelItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.FullPath)) return;
        try
        {
            if (System.IO.File.Exists(item.FullPath))
            {
                var argument = $"/select,\"{item.FullPath}\"";
                System.Diagnostics.Process.Start("explorer.exe", argument);
            }
            else if (System.IO.Directory.Exists(item.FullPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{item.FullPath}\"");
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task DeleteLocalModelAsync(LocalModelItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.FullPath)) return;
        try
        {
            if (System.IO.File.Exists(item.FullPath))
            {
                System.IO.File.Delete(item.FullPath);
                ToastService.Instance.Show($"Deleted '{item.FileName}'", ToastType.Info);
                await RefreshScannedModelsAsync();
            }
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"Failed to delete: {ex.Message}", ToastType.Error);
        }
    }

    [RelayCommand]
    public async Task RefreshScannedModelsAsync()
    {
        var appSettings = Settings != null ? new AppSettings(
            ForgeModelsPath: Settings.ForgeModelsPath,
            ComfyModelsPath: Settings.ComfyModelsPath,
            ThreeDModelsPath: Settings.ThreeDModelsPath,
            WorkflowsPath: Settings.WorkflowsPath
        ) : null;

        await Studio.LoadScannedModelsAsync(appSettings, AppContext.BaseDirectory);

        ScannedImageModels.Clear();
        ScannedVideoModels.Clear();
        ScannedAudioModels.Clear();
        ScannedThreeDModels.Clear();

        foreach (var m in Studio.ScannedModels)
        {
            if (m.Category == LocalModelCategory.ImageCheckpoint || m.Category == LocalModelCategory.ImageLora)
                ScannedImageModels.Add(m);
            else if (m.Category == LocalModelCategory.Video)
                ScannedVideoModels.Add(m);
            else if (m.Category == LocalModelCategory.Audio)
                ScannedAudioModels.Add(m);
            else if (m.Category == LocalModelCategory.ThreeD)
                ScannedThreeDModels.Add(m);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImageModalityActive))]
    [NotifyPropertyChangedFor(nameof(IsTextModalityActive))]
    [NotifyPropertyChangedFor(nameof(IsVideoModalityActive))]
    [NotifyPropertyChangedFor(nameof(Is3DModalityActive))]
    [NotifyPropertyChangedFor(nameof(IsAudioModalityActive))]
    [NotifyPropertyChangedFor(nameof(ActiveModelBadge))]
    [NotifyPropertyChangedFor(nameof(ActivePromptPlaceholder))]
    private string _selectedModality = "Image";

    public bool IsImageModalityActive => SelectedModality == "Image" || SelectedModality == "Images" || SelectedStudioMode == "Images";
    public bool IsTextModalityActive => SelectedModality == "Text" || SelectedStudioMode == "Text";
    public bool IsVideoModalityActive => SelectedModality == "Video" || SelectedStudioMode == "Video";
    public bool Is3DModalityActive => SelectedModality == "3D Mesh" || SelectedStudioMode == "3D Mesh";
    public bool IsAudioModalityActive => SelectedModality == "Audio" || SelectedStudioMode == "Audio";

    [ObservableProperty]
    private bool _isParametersFlyoutOpen;

    [ObservableProperty]
    private string _activeAspectPreset = "16:9";

    [ObservableProperty]
    private int _studioSteps = 30;

    [ObservableProperty]
    private double _studioCfgScale = 7.0;

    [ObservableProperty]
    private long _studioSeed = -1;

    [ObservableProperty]
    private double _studioDenoise = 0.75;

    [ObservableProperty]
    private string _promptText = "";

    [ObservableProperty]
    private string _negativePromptText = "";

    public string ActiveModelBadge => SelectedModality switch
    {
        "Image" or "Images" => !string.IsNullOrWhiteSpace(SelectedImageWorkflow) ? SelectedImageWorkflow : "SDXL Base 1.0",
        "Text" => Ollama?.SelectedInstalledModel?.Name ?? (!string.IsNullOrWhiteSpace(Telemetry?.OllamaModelName) ? Telemetry.OllamaModelName : "llama3.2:latest"),
        "Video" => !string.IsNullOrWhiteSpace(SelectedVideoWorkflow) ? SelectedVideoWorkflow : "Wan 2.2 / LTX-2.5",
        "3D Mesh" => !string.IsNullOrWhiteSpace(Selected3DFormat) ? $"TRELLIS V2 ({Selected3DFormat})" : "TRELLIS V2 (Gaussian Splat)",
        "Audio" => Audio != null ? $"Kokoro TTS ({Audio.VoiceProfile})" : "Kokoro TTS (af_heart)",
        _ => "SDXL Base 1.0"
    };

    public string ActivePromptPlaceholder => SelectedModality switch
    {
        "Image" or "Images" => "Describe an image to generate with SDXL & Forge (e.g. 'cyberpunk street in rain')...",
        "Text" => "Enter a prompt or query for the local LLM...",
        "Video" => "Describe action and motion for video generation (e.g. 'cinematic drone sweep over mountains')...",
        "3D Mesh" => "Describe a 3D object to sculpt or synthesize (e.g. 'ancient stone relic with glowing runes')...",
        "Audio" => "Describe audio atmosphere or enter speech to synthesize with Kokoro TTS...",
        _ => "Describe what you want to create..."
    };

    [RelayCommand]
    public void SelectModality(string modality)
    {
        if (string.IsNullOrWhiteSpace(modality)) return;
        SelectedModality = modality;
        if (modality == "Sticker" || modality == "Sticker Studio")
        {
            IsStickerStudioActive = true;
            SelectedStudioMode = "Sticker Studio";
        }
        else
        {
            IsStickerStudioActive = false;
            SelectedStudioMode = modality switch
            {
                "Image" => "Images",
                _ => modality
            };
        }
        Studio.SelectModality(modality);
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
        Studio.IsParametersFlyoutOpen = IsParametersFlyoutOpen;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStudioGeneratedImage))]
    [NotifyPropertyChangedFor(nameof(ShowImageCanvasZeroState))]
    private byte[]? _studioGeneratedImageBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneratingAnyStudio))]
    [NotifyPropertyChangedFor(nameof(StudioGenerateButtonText))]
    [NotifyPropertyChangedFor(nameof(ShowImageCanvasZeroState))]
    private bool _isGeneratingStudioImage;

    [ObservableProperty]
    private string _studioImageGenerationStatus = "Ready";

    [ObservableProperty]
    private byte[]? _attachedImageBytes;

    [ObservableProperty]
    private bool _hasAttachedImage;

    public bool HasStudioGeneratedImage => StudioGeneratedImageBytes != null && StudioGeneratedImageBytes.Length > 0;
    public bool ShowImageCanvasZeroState => !HasStudioGeneratedImage && !IsGeneratingStudioImage;

    public bool IsGeneratingAnyStudio =>
        IsGeneratingStudioImage ||
        IsGeneratingOllamaText ||
        IsGeneratingVideo ||
        IsGenerating3D ||
        (Audio != null && Audio.IsGenerating) ||
        (StickerStudio != null && StickerStudio.IsGenerating);

    public string StudioGenerateButtonText => IsGeneratingAnyStudio ? "Generating..." : "Generate ↵";

    public event Func<byte[], Task>? CopyStudioImageRequested;
    public event Func<byte[], Task<string?>>? SaveStudioImageRequested;
    public event Func<Task<byte[]?>>? PickImageRequested;

    [RelayCommand]
    public async Task CopyStudioImageAsync()
    {
        if (StudioGeneratedImageBytes != null && CopyStudioImageRequested != null)
        {
            await CopyStudioImageRequested(StudioGeneratedImageBytes);
            ToastService.Instance.Show("Image copied to clipboard!", ToastType.Success);
        }
    }

    [RelayCommand]
    public async Task SaveStudioImageAsync()
    {
        if (StudioGeneratedImageBytes != null && SaveStudioImageRequested != null)
        {
            var path = await SaveStudioImageRequested(StudioGeneratedImageBytes);
            if (!string.IsNullOrEmpty(path))
            {
                ToastService.Instance.Show($"Saved image to {Path.GetFileName(path)}", ToastType.Success);
            }
        }
    }

    [RelayCommand]
    public void ClearStudioImage()
    {
        StudioGeneratedImageBytes = null;
        StudioImageGenerationStatus = "Ready";
    }

    [RelayCommand]
    public async Task AttachImageAsync()
    {
        if (PickImageRequested != null)
        {
            var bytes = await PickImageRequested();
            if (bytes != null && bytes.Length > 0)
            {
                AttachedImageBytes = bytes;
                HasAttachedImage = true;
                Studio.HasAttachedImage = true;
                ToastService.Instance.Show("Reference image attached.", ToastType.Info);
            }
        }
        else
        {
            Studio.AttachImage();
            HasAttachedImage = Studio.HasAttachedImage;
        }
    }

    [RelayCommand]
    public void RemoveAttachedImage()
    {
        AttachedImageBytes = null;
        HasAttachedImage = false;
        Studio.HasAttachedImage = false;
        ToastService.Instance.Show("Reference image removed.", ToastType.Info);
    }

    [RelayCommand]
    public void SelectAspectPreset(string preset)
    {
        if (!string.IsNullOrWhiteSpace(preset))
        {
            ActiveAspectPreset = preset;
            Studio.ActiveAspectPreset = preset;
        }
    }

    [RelayCommand]
    public void ApplyPromptChip(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;
        PromptText = prompt;
        Studio.PromptText = prompt;
        if (IsImageModalityActive) ImagePrompt = prompt;
        else if (IsTextModalityActive) OllamaPrompt = prompt;
        else if (IsVideoModalityActive) VideoPrompt = prompt;
        else if (Is3DModalityActive) Prompt3D = prompt;
        else if (IsAudioModalityActive && Audio != null) Audio.Prompt = prompt;
    }

    [RelayCommand]
    public async Task GenerateStudioImageAsync()
    {
        if (IsGeneratingStudioImage) return;

        var prompt = !string.IsNullOrWhiteSpace(PromptText) ? PromptText : ImagePrompt;
        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = "A majestic dragon perched on a mountain peak at sunrise, detailed fantasy concept art, octane render";
            PromptText = prompt;
            ImagePrompt = prompt;
        }

        IsGeneratingStudioImage = true;
        StudioImageGenerationStatus = "Preparing inference parameters...";

        var (width, height) = ActiveAspectPreset switch
        {
            "1:1" => (1024, 1024),
            "16:9" => (1344, 768),
            "9:16" => (768, 1344),
            "4:3" => (1152, 864),
            _ => (1024, 1024)
        };

        var forgeBase = "http://127.0.0.1:7860";
        bool isImg2Img = HasAttachedImage && AttachedImageBytes != null && AttachedImageBytes.Length > 0;
        var endpoint = isImg2Img ? $"{forgeBase}/sdapi/v1/img2img" : $"{forgeBase}/sdapi/v1/txt2img";

        var steps = StudioSteps > 0 ? StudioSteps : 25;
        var cfg = StudioCfgScale > 0 ? StudioCfgScale : 7.0;
        var seed = StudioSeed;

        var selectedModel = ActiveModelBadge;
        string? checkpointOverride = null;
        if (!string.IsNullOrWhiteSpace(selectedModel) && !selectedModel.StartsWith("SDXL Base") && selectedModel.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase))
        {
            checkpointOverride = selectedModel;
        }

        object payload;
        if (isImg2Img)
        {
            payload = new
            {
                prompt = prompt,
                negative_prompt = NegativePromptText ?? "",
                init_images = new[] { Convert.ToBase64String(AttachedImageBytes!) },
                denoising_strength = StudioDenoise > 0 ? StudioDenoise : 0.7,
                steps = steps,
                cfg_scale = cfg,
                seed = seed,
                width = width,
                height = height,
                override_settings = checkpointOverride != null ? new { sd_model_checkpoint = checkpointOverride } : null
            };
        }
        else
        {
            payload = new
            {
                prompt = prompt,
                negative_prompt = NegativePromptText ?? "",
                steps = steps,
                cfg_scale = cfg,
                seed = seed,
                width = width,
                height = height,
                override_settings = checkpointOverride != null ? new { sd_model_checkpoint = checkpointOverride } : null
            };
        }

        StudioImageGenerationStatus = $"Dispatching {width}x{height} request to Forge ({forgeBase})...";

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await Http.PostAsync(endpoint, content, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                StudioImageGenerationStatus = "Decoding generated image...";
                var respStr = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(respStr);
                if (doc.RootElement.TryGetProperty("images", out var imagesElem) &&
                    imagesElem.ValueKind == JsonValueKind.Array &&
                    imagesElem.GetArrayLength() > 0)
                {
                    var base64 = imagesElem[0].GetString();
                    if (!string.IsNullOrEmpty(base64))
                    {
                        var bytes = Convert.FromBase64String(base64);
                        StudioGeneratedImageBytes = bytes;
                        StudioImageGenerationStatus = "Generation complete!";
                        ToastService.Instance.Show("Image generated successfully!", ToastType.Success);
                        return;
                    }
                }
                throw new Exception("No image data found in Forge API response.");
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync(cts.Token);
                throw new Exception($"HTTP {(int)response.StatusCode}: {err}");
            }
        }
        catch (TaskCanceledException)
        {
            StudioImageGenerationStatus = "Generation timed out after 120s.";
            ToastService.Instance.Show("Generation timed out. Forge engine may be overloaded or hung.", ToastType.Error);
        }
        catch (HttpRequestException)
        {
            StudioImageGenerationStatus = $"Forge engine offline or unreachable at {forgeBase}.";
            ToastService.Instance.Show($"Cannot reach Forge at {forgeBase}. Is Forge running?", ToastType.Warning);
        }
        catch (Exception ex)
        {
            StudioImageGenerationStatus = $"Generation failed: {ex.Message}";
            ToastService.Instance.Show($"Generation failed: {ex.Message}", ToastType.Error);
        }
        finally
        {
            IsGeneratingStudioImage = false;
        }
    }

    [RelayCommand]
    public async Task GenerateFromDockAsync()
    {
        if (IsImageModalityActive)
        {
            await GenerateStudioImageAsync();
        }
        else if (IsTextModalityActive)
        {
            if (!string.IsNullOrWhiteSpace(PromptText)) OllamaPrompt = PromptText;
            await GenerateOllamaTextAsync();
        }
        else if (IsVideoModalityActive)
        {
            if (!string.IsNullOrWhiteSpace(PromptText)) VideoPrompt = PromptText;
            await GenerateVideoAsync();
        }
        else if (IsAudioModalityActive && Audio != null)
        {
            if (!string.IsNullOrWhiteSpace(PromptText)) Audio.Prompt = PromptText;
            await GenerateAudioAsync();
        }
        else if (IsStickerModalityActive && StickerStudio != null)
        {
            if (!string.IsNullOrWhiteSpace(PromptText)) StickerStudio.CustomPrompt = PromptText;
            await StickerStudio.GenerateStickerCommand.ExecuteAsync(null);
        }
        else if (Is3DModalityActive)
        {
            if (!string.IsNullOrWhiteSpace(PromptText)) Prompt3D = PromptText;
            await Generate3DAsync();
        }
    }
}
