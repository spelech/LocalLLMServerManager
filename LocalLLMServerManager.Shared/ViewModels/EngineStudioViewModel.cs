using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LocalLLMServerManager.Shared.ViewModels;

/// <summary>
/// ViewModel managing the fluid creative studio canvas, top modality switching,
/// creative prompt dock, and fine-tuning parameters flyout.
/// </summary>
public partial class EngineStudioViewModel : ObservableObject
{
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

    [RelayCommand]
    public void SelectModality(string modality)
    {
        if (string.IsNullOrWhiteSpace(modality)) return;
        SelectedModality = modality;
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

    [RelayCommand]
    public void AttachImage()
    {
        Studio.AttachImage();
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
    public void GenerateFromDock()
    {
        if (IsImageModalityActive)
        {
            if (!string.IsNullOrWhiteSpace(PromptText)) ImagePrompt = PromptText;
            OpenForgeWebUiCommand.Execute(null);
        }
        else if (IsTextModalityActive)
        {
            if (!string.IsNullOrWhiteSpace(PromptText)) OllamaPrompt = PromptText;
            GenerateOllamaTextCommand.Execute(null);
        }
        else if (IsVideoModalityActive)
        {
            if (!string.IsNullOrWhiteSpace(PromptText)) VideoPrompt = PromptText;
            GenerateVideoCommand.Execute(null);
        }
        else if (Is3DModalityActive)
        {
            if (!string.IsNullOrWhiteSpace(PromptText)) Prompt3D = PromptText;
            Generate3DCommand.Execute(null);
        }
        else if (IsAudioModalityActive && Audio != null)
        {
            if (!string.IsNullOrWhiteSpace(PromptText)) Audio.Prompt = PromptText;
            Audio.GenerateAudioCommand.Execute(null);
        }
    }
}
