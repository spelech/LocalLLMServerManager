using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

/// <summary>
/// ViewModel managing hardware compatibility evaluation, memory distributions,
/// modality selection, preset profiles, and responsive calculation triggers.
/// </summary>
public partial class CanIRunItViewModel : ObservableObject
{
    private readonly ICanIRunItService _canIRunItService;
    private readonly ITelemetryService _telemetryService;
    private readonly HttpClient? _httpClient;
    private bool _isUpdatingPresetInternally;

    // Available Preset Catalogs
    public IReadOnlyList<string> AvailableLlmPresets { get; } = new[]
    {
        "Llama 3.3 70B",
        "Llama 3.1 8B",
        "Qwen 2.5 32B",
        "Qwen 2.5 72B",
        "DeepSeek R1 32B",
        "DeepSeek R1 70B",
        "DeepSeek R1 671B",
        "Mistral Small 24B",
        "Gemma 2 9B",
        "Gemma 2 27B",
        "Phi-4 14B",
        "Custom"
    };

    public IReadOnlyList<string> AvailableQuantizations { get; } = new[]
    {
        "Q2_K",
        "Q3_K_M",
        "Q4_K_M",
        "Q5_K_M",
        "Q6_K",
        "Q8_0",
        "FP16"
    };

    public IReadOnlyList<int> AvailableContextLengths { get; } = new[]
    {
        2048,
        4096,
        8192,
        16384,
        32768,
        65536,
        131072
    };

    public IReadOnlyList<string> AvailableKvPrecisions { get; } = new[]
    {
        "FP16",
        "Q8_0",
        "Q4_0"
    };

    public IReadOnlyList<string> AvailableImagePresets { get; } = new[]
    {
        "Flux.1 Dev",
        "Flux.1 Schnell",
        "SDXL",
        "SD 3.5 Large",
        "SD 1.5",
        "Custom"
    };

    public IReadOnlyList<int> AvailableImageResolutions { get; } = new[]
    {
        512,
        768,
        1024,
        1536
    };

    public IReadOnlyList<string> AvailableImageQuantizations { get; } = new[]
    {
        "FP8",
        "FP16",
        "Q4"
    };

    public IReadOnlyList<string> AvailableVideoPresets { get; } = new[]
    {
        "Wan 2.2 14B",
        "Wan 2.2 1.3B",
        "LTX-Video",
        "HunyuanVideo"
    };

    public IReadOnlyList<int> AvailableVideoFrameCounts { get; } = new[]
    {
        49,
        81,
        97,
        129
    };

    public IReadOnlyList<string> AvailableVideoResolutions { get; } = new[]
    {
        "480p",
        "720p"
    };

    public IReadOnlyList<string> AvailableVideoQuantizations { get; } = new[]
    {
        "FP8",
        "FP16",
        "Q4"
    };

    public IReadOnlyList<string> AvailableAudioPresets { get; } = new[]
    {
        "Kokoro TTS",
        "Faster-Whisper Large-v3-Turbo",
        "AllTalk XTTS-v2",
        "MusicGen Melody"
    };

    public IReadOnlyList<string> AvailableThreeDPresets { get; } = new[]
    {
        "TRELLIS V2",
        "Hunyuan3D-2"
    };

    // Hardware State Properties
    [ObservableProperty] private string _gpuName = "NVIDIA GeForce RTX 4070 Ti SUPER";
    [ObservableProperty] private double _totalVramMb = 16384.0;
    [ObservableProperty] private double _freeVramMb = 16384.0;
    [ObservableProperty] private double _usedVramMb = 0.0;
    [ObservableProperty] private double _totalRamMb = 32768.0;
    [ObservableProperty] private double _availableRamMb = 32768.0;
    [ObservableProperty] private string _vramText = "16.0 GB VRAM";
    [ObservableProperty] private string _ramText = "32.0 GB RAM";

    // Modality State
    [ObservableProperty] private string _selectedModality = "LLM";

    // LLM Parameters
    [ObservableProperty] private string _selectedPreset = "Llama 3.1 8B";
    [ObservableProperty] private double _parametersBillions = 8.0;
    [ObservableProperty] private string _selectedQuantization = "Q4_K_M";
    [ObservableProperty] private int _contextLength = 4096;
    [ObservableProperty] private string _kvCachePrecision = "FP16";

    // Image Parameters
    [ObservableProperty] private string _selectedImagePreset = "Flux.1 Dev";
    [ObservableProperty] private int _selectedImageResolution = 1024;
    [ObservableProperty] private int _imageBatchSize = 1;
    [ObservableProperty] private string _selectedImageQuantization = "FP8";

    // Video Parameters
    [ObservableProperty] private string _selectedVideoPreset = "Wan 2.2 14B";
    [ObservableProperty] private int _videoFrameCount = 81;
    [ObservableProperty] private string _selectedVideoResolution = "720p";
    [ObservableProperty] private string _selectedVideoQuantization = "FP8";

    // Audio & 3D Parameters
    [ObservableProperty] private string _selectedAudioPreset = "Kokoro TTS";
    [ObservableProperty] private string _selectedThreeDPreset = "TRELLIS V2";

    // Calculated Results
    [ObservableProperty] private LlmFitResult? _llmResult;
    [ObservableProperty] private DiffusionFitResult? _diffusionResult;
    [ObservableProperty] private VideoFitResult? _videoResult;
    [ObservableProperty] private AudioFitResult? _audioResult;
    [ObservableProperty] private ThreeDFitResult? _threeDResult;
    [ObservableProperty] private QuickFitBadge? _fitVerdictBadge;

    // Visual Percentage Bar
    [ObservableProperty] private double _modelWeightsVramPercent = 0.0;
    [ObservableProperty] private double _kvCacheVramPercent = 0.0;
    [ObservableProperty] private double _overheadVramPercent = 0.0;
    [ObservableProperty] private double _freeVramPercent = 100.0;

    // Text Summaries & Recommendations
    [ObservableProperty] private string _verdictSummaryText = "";
    [ObservableProperty] private string _offloadSummaryText = "";
    [ObservableProperty] private string _recommendationText = "";
    [ObservableProperty] private string _speedEstimationText = "";

    public CanIRunItViewModel()
        : this(null, null, null)
    {
    }

    public CanIRunItViewModel(ITelemetryService telemetryService)
        : this(null, telemetryService, null)
    {
    }

    public CanIRunItViewModel(
        ICanIRunItService? canIRunItService,
        ITelemetryService? telemetryService = null,
        HttpClient? httpClient = null)
    {
        _canIRunItService = canIRunItService ?? new CanIRunItService();
        _telemetryService = telemetryService ?? new TelemetryService();
        _httpClient = httpClient;

        UpdateHardwareFormattedTexts();
        Recalculate();
    }


    private void UpdateHardwareFormattedTexts()
    {
        VramText = $"{TotalVramMb / 1024.0:F1} GB VRAM";
        RamText = $"{TotalRamMb / 1024.0:F1} GB RAM";
    }

    partial void OnTotalVramMbChanged(double value)
    {
        UpdateHardwareFormattedTexts();
        Recalculate();
    }

    partial void OnFreeVramMbChanged(double value) => Recalculate();
    partial void OnTotalRamMbChanged(double value)
    {
        UpdateHardwareFormattedTexts();
        Recalculate();
    }
    partial void OnAvailableRamMbChanged(double value) => Recalculate();

    partial void OnSelectedModalityChanged(string value) => Recalculate();

    partial void OnSelectedPresetChanged(string value)
    {
        if (_isUpdatingPresetInternally) return;

        _isUpdatingPresetInternally = true;
        try
        {
            switch (value)
            {
                case "Llama 3.3 70B":
                    ParametersBillions = 70.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
                case "Llama 3.1 8B":
                    ParametersBillions = 8.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
                case "Qwen 2.5 32B":
                    ParametersBillions = 32.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
                case "Qwen 2.5 72B":
                    ParametersBillions = 72.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
                case "DeepSeek R1 32B":
                    ParametersBillions = 32.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
                case "DeepSeek R1 70B":
                    ParametersBillions = 70.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
                case "DeepSeek R1 671B":
                    ParametersBillions = 671.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
                case "Mistral Small 24B":
                    ParametersBillions = 24.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
                case "Gemma 2 9B":
                    ParametersBillions = 9.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
                case "Gemma 2 27B":
                    ParametersBillions = 27.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
                case "Phi-4 14B":
                    ParametersBillions = 14.0;
                    SelectedQuantization = "Q4_K_M";
                    ContextLength = 4096;
                    KvCachePrecision = "FP16";
                    break;
            }
        }
        finally
        {
            _isUpdatingPresetInternally = false;
        }

        Recalculate();
    }

    partial void OnParametersBillionsChanged(double value)
    {
        if (!_isUpdatingPresetInternally)
        {
            CheckAndSyncLlmPreset();
        }
        Recalculate();
    }

    partial void OnSelectedQuantizationChanged(string value)
    {
        if (!_isUpdatingPresetInternally)
        {
            CheckAndSyncLlmPreset();
        }
        Recalculate();
    }

    partial void OnContextLengthChanged(int value) => Recalculate();
    partial void OnKvCachePrecisionChanged(string value) => Recalculate();

    private void CheckAndSyncLlmPreset()
    {
        // If parameters don't match standard preset, set to Custom
        bool matches = (SelectedPreset == "Llama 3.3 70B" && Math.Abs(ParametersBillions - 70.0) < 0.01 && SelectedQuantization == "Q4_K_M") ||
                       (SelectedPreset == "Llama 3.1 8B" && Math.Abs(ParametersBillions - 8.0) < 0.01 && SelectedQuantization == "Q4_K_M") ||
                       (SelectedPreset == "Qwen 2.5 32B" && Math.Abs(ParametersBillions - 32.0) < 0.01 && SelectedQuantization == "Q4_K_M") ||
                       (SelectedPreset == "Qwen 2.5 72B" && Math.Abs(ParametersBillions - 72.0) < 0.01 && SelectedQuantization == "Q4_K_M") ||
                       (SelectedPreset == "DeepSeek R1 32B" && Math.Abs(ParametersBillions - 32.0) < 0.01 && SelectedQuantization == "Q4_K_M") ||
                       (SelectedPreset == "DeepSeek R1 70B" && Math.Abs(ParametersBillions - 70.0) < 0.01 && SelectedQuantization == "Q4_K_M") ||
                       (SelectedPreset == "DeepSeek R1 671B" && Math.Abs(ParametersBillions - 671.0) < 0.01 && SelectedQuantization == "Q4_K_M") ||
                       (SelectedPreset == "Mistral Small 24B" && Math.Abs(ParametersBillions - 24.0) < 0.01 && SelectedQuantization == "Q4_K_M") ||
                       (SelectedPreset == "Gemma 2 9B" && Math.Abs(ParametersBillions - 9.0) < 0.01 && SelectedQuantization == "Q4_K_M") ||
                       (SelectedPreset == "Gemma 2 27B" && Math.Abs(ParametersBillions - 27.0) < 0.01 && SelectedQuantization == "Q4_K_M") ||
                       (SelectedPreset == "Phi-4 14B" && Math.Abs(ParametersBillions - 14.0) < 0.01 && SelectedQuantization == "Q4_K_M");

        if (!matches && SelectedPreset != "Custom")
        {
            _isUpdatingPresetInternally = true;
            try
            {
                SelectedPreset = "Custom";
            }
            finally
            {
                _isUpdatingPresetInternally = false;
            }
        }
    }

    partial void OnSelectedImagePresetChanged(string value)
    {
        switch (value)
        {
            case "Flux.1 Dev":
            case "Flux.1 Schnell":
                SelectedImageResolution = 1024;
                SelectedImageQuantization = "FP8";
                break;
            case "SDXL":
                SelectedImageResolution = 1024;
                SelectedImageQuantization = "FP16";
                break;
            case "SD 3.5 Large":
                SelectedImageResolution = 1024;
                SelectedImageQuantization = "FP8";
                break;
            case "SD 1.5":
                SelectedImageResolution = 512;
                SelectedImageQuantization = "FP16";
                break;
        }
        Recalculate();
    }

    partial void OnSelectedImageResolutionChanged(int value) => Recalculate();
    partial void OnImageBatchSizeChanged(int value) => Recalculate();
    partial void OnSelectedImageQuantizationChanged(string value) => Recalculate();

    partial void OnSelectedVideoPresetChanged(string value) => Recalculate();
    partial void OnVideoFrameCountChanged(int value) => Recalculate();
    partial void OnSelectedVideoResolutionChanged(string value) => Recalculate();
    partial void OnSelectedVideoQuantizationChanged(string value) => Recalculate();

    partial void OnSelectedAudioPresetChanged(string value) => Recalculate();
    partial void OnSelectedThreeDPresetChanged(string value) => Recalculate();

    // Modality Relay Commands
    [RelayCommand] public void SelectLlmModality() => SelectedModality = "LLM";
    [RelayCommand] public void SelectImageModality() => SelectedModality = "Image";
    [RelayCommand] public void SelectVideoModality() => SelectedModality = "Video";
    [RelayCommand] public void SelectAudioModality() => SelectedModality = "Audio";
    [RelayCommand] public void SelectThreeDModality() => SelectedModality = "ThreeD";
    [RelayCommand] public void Select3DModality() => SelectedModality = "ThreeD";

    [RelayCommand]
    public void SelectModality(string modality)
    {
        if (string.IsNullOrWhiteSpace(modality)) return;
        string mod = modality.Trim().ToUpperInvariant();
        SelectedModality = mod switch
        {
            "IMAGE" or "DIFFUSION" => "Image",
            "VIDEO" => "Video",
            "AUDIO" or "SPEECH" or "TTS" => "Audio",
            "THREED" or "3D" => "ThreeD",
            _ => "LLM"
        };
    }

    /// <summary>
    /// Updates hardware telemetry state and triggers recalculation.
    /// </summary>
    public void UpdateHardwareTelemetry(TelemetryInfo info)
    {
        if (info == null) return;
        GpuName = info.GpuName;
        TotalVramMb = info.TotalVramMb;
        FreeVramMb = info.FreeVramMb;
        UsedVramMb = info.UsedVramMb;
        TotalRamMb = info.TotalRamMb;
        AvailableRamMb = info.AvailableRamMb;
        UpdateHardwareFormattedTexts();
        Recalculate();
    }

    /// <summary>
    /// Updates hardware telemetry from a GpuTelemetryInfo record.
    /// </summary>
    public void UpdateHardwareTelemetry(GpuTelemetryInfo info)
    {
        if (info == null) return;
        if (!string.IsNullOrWhiteSpace(info.GpuName) && info.GpuName != "GPU Telemetry Active")
        {
            GpuName = info.GpuName;
        }
        TotalVramMb = info.TotalVramGb * 1024.0;
        UsedVramMb = info.UsedVramGb * 1024.0;
        FreeVramMb = info.FreeVramGb * 1024.0;
        UpdateHardwareFormattedTexts();
        Recalculate();
    }

    /// <summary>
    /// Updates hardware telemetry with explicit values.
    /// </summary>
    public void UpdateHardwareTelemetry(double totalVramMb, double freeVramMb, double totalRamMb, double availableRamMb, string? gpuName = null)
    {
        if (!string.IsNullOrWhiteSpace(gpuName))
        {
            GpuName = gpuName;
        }
        TotalVramMb = totalVramMb;
        FreeVramMb = freeVramMb;
        UsedVramMb = Math.Max(0, totalVramMb - freeVramMb);
        TotalRamMb = totalRamMb;
        AvailableRamMb = availableRamMb;
        UpdateHardwareFormattedTexts();
        Recalculate();
    }

    /// <summary>
    /// Polls live telemetry from backend server.
    /// </summary>
    public async Task RefreshTelemetryAsync(string? apiBase = null, HttpClient? http = null)
    {
        var client = http ?? _httpClient ?? HttpHelper.CreateClient(apiBase ?? "");
        string endpoint = apiBase ?? (OperatingSystem.IsBrowser() ? "" : "http://127.0.0.1:5246");
        var gpuInfo = await _telemetryService.QueryGpuVramAsync(endpoint, client);
        UpdateHardwareTelemetry(gpuInfo);
    }

    /// <summary>
    /// Pre-selects matching preset or configures custom parameters to immediately evaluate the inspected model.
    /// </summary>
    public void InspectModel(string modelName, string modality = "LLM", long? fileSizeBytes = null)
    {
        string mod = (modality ?? "LLM").Trim().ToUpperInvariant();
        string name = (modelName ?? "").Trim();

        if (mod.Contains("IMAGE") || mod.Contains("DIFFUSION"))
        {
            SelectedModality = "Image";
            string lowerName = name.ToLowerInvariant();
            if (lowerName.Contains("schnell")) SelectedImagePreset = "Flux.1 Schnell";
            else if (lowerName.Contains("flux")) SelectedImagePreset = "Flux.1 Dev";
            else if (lowerName.Contains("sdxl") || lowerName.Contains("pony")) SelectedImagePreset = "SDXL";
            else if (lowerName.Contains("sd 3") || lowerName.Contains("sd3")) SelectedImagePreset = "SD 3.5 Large";
            else if (lowerName.Contains("1.5") || lowerName.Contains("sd-1-5")) SelectedImagePreset = "SD 1.5";
            else SelectedImagePreset = "Flux.1 Dev";
        }
        else if (mod.Contains("VIDEO"))
        {
            SelectedModality = "Video";
            string lowerName = name.ToLowerInvariant();
            if (lowerName.Contains("1.3b") || (lowerName.Contains("wan") && lowerName.Contains("1.3"))) SelectedVideoPreset = "Wan 2.2 1.3B";
            else if (lowerName.Contains("wan")) SelectedVideoPreset = "Wan 2.2 14B";
            else if (lowerName.Contains("ltx")) SelectedVideoPreset = "LTX-Video";
            else if (lowerName.Contains("hunyuan")) SelectedVideoPreset = "HunyuanVideo";
            else SelectedVideoPreset = "Wan 2.2 14B";
        }
        else if (mod.Contains("AUDIO") || mod.Contains("SPEECH") || mod.Contains("TTS"))
        {
            SelectedModality = "Audio";
            string lowerName = name.ToLowerInvariant();
            if (lowerName.Contains("kokoro")) SelectedAudioPreset = "Kokoro TTS";
            else if (lowerName.Contains("whisper")) SelectedAudioPreset = "Faster-Whisper Large-v3-Turbo";
            else if (lowerName.Contains("xtts") || lowerName.Contains("alltalk")) SelectedAudioPreset = "AllTalk XTTS-v2";
            else if (lowerName.Contains("musicgen") || lowerName.Contains("audiocraft")) SelectedAudioPreset = "MusicGen Melody";
            else SelectedAudioPreset = "Kokoro TTS";
        }
        else if (mod.Contains("3D") || mod.Contains("THREED"))
        {
            SelectedModality = "ThreeD";
            string lowerName = name.ToLowerInvariant();
            if (lowerName.Contains("trellis")) SelectedThreeDPreset = "TRELLIS V2";
            else if (lowerName.Contains("hunyuan")) SelectedThreeDPreset = "Hunyuan3D-2";
            else SelectedThreeDPreset = "TRELLIS V2";
        }
        else
        {
            SelectedModality = "LLM";
            string lowerName = name.ToLowerInvariant();

            if (lowerName.Contains("llama-3.3-70b") || lowerName.Contains("llama 3.3 70b")) SelectedPreset = "Llama 3.3 70B";
            else if (lowerName.Contains("llama-3.1-8b") || lowerName.Contains("llama 3.1 8b") || lowerName.Contains("llama-3-8b") || lowerName.Contains("llama 3 8b")) SelectedPreset = "Llama 3.1 8B";
            else if (lowerName.Contains("qwen2.5-32b") || lowerName.Contains("qwen 2.5 32b")) SelectedPreset = "Qwen 2.5 32B";
            else if (lowerName.Contains("qwen2.5-72b") || lowerName.Contains("qwen 2.5 72b")) SelectedPreset = "Qwen 2.5 72B";
            else if (lowerName.Contains("deepseek-r1") && lowerName.Contains("671")) SelectedPreset = "DeepSeek R1 671B";
            else if (lowerName.Contains("deepseek-r1") && lowerName.Contains("70")) SelectedPreset = "DeepSeek R1 70B";
            else if (lowerName.Contains("deepseek-r1") && lowerName.Contains("32")) SelectedPreset = "DeepSeek R1 32B";
            else if (lowerName.Contains("mistral-small") || lowerName.Contains("mistral small 24b")) SelectedPreset = "Mistral Small 24B";
            else if (lowerName.Contains("gemma-2-9b") || lowerName.Contains("gemma 2 9b")) SelectedPreset = "Gemma 2 9B";
            else if (lowerName.Contains("gemma-2-27b") || lowerName.Contains("gemma 2 27b")) SelectedPreset = "Gemma 2 27B";
            else if (lowerName.Contains("phi-4") || lowerName.Contains("phi 4")) SelectedPreset = "Phi-4 14B";
            else
            {
                // Custom LLM model
                _isUpdatingPresetInternally = true;
                try
                {
                    SelectedPreset = "Custom";
                    ParametersBillions = ExtractParamBillions(name);
                    SelectedQuantization = ExtractQuantization(name);
                }
                finally
                {
                    _isUpdatingPresetInternally = false;
                }
            }
        }

        Recalculate();
    }

    /// <summary>
    /// Recalculates compatibility results, memory breakdown percentages, badges, and textual summaries.
    /// </summary>
    public void Recalculate()
    {
        double vram = TotalVramMb > 0 ? TotalVramMb : 16384.0;
        double ram = AvailableRamMb > 0 ? AvailableRamMb : (TotalRamMb > 0 ? TotalRamMb : 32768.0);
        long vramLong = (long)vram;
        long ramLong = (long)ram;

        string mod = (SelectedModality ?? "LLM").Trim().ToUpperInvariant();

        if (mod == "IMAGE" || mod == "DIFFUSION")
        {
            var req = new DiffusionFitRequest(
                ModelName: SelectedImagePreset,
                Quantization: SelectedImageQuantization,
                Resolution: SelectedImageResolution,
                AvailableVramMb: vramLong,
                AvailableRamMb: ramLong
            );

            DiffusionResult = _canIRunItService.EvaluateDiffusionFit(req);
            var res = DiffusionResult;

            if (res.FitVerdict == FitVerdict.OutOfMemory)
            {
                ModelWeightsVramPercent = 0.0;
                KvCacheVramPercent = 0.0;
                OverheadVramPercent = 0.0;
                FreeVramPercent = 100.0;
            }
            else
            {
                ModelWeightsVramPercent = Math.Clamp((double)res.BaseModelMb / vram * 100.0, 0.0, 100.0);
                KvCacheVramPercent = Math.Clamp((double)(res.EncodersMb + res.LatentBufferMb) / vram * 100.0, 0.0, 100.0);
                OverheadVramPercent = Math.Clamp((double)res.VaeMb / vram * 100.0, 0.0, 100.0);
                FreeVramPercent = Math.Clamp(100.0 - ModelWeightsVramPercent - KvCacheVramPercent - OverheadVramPercent, 0.0, 100.0);
            }

            FitVerdictBadge = CreateBadge(res.FitVerdict, res.RecommendationMessage);
            SpeedEstimationText = $"~{res.EstimatedSecondsPerImage:F1}s / image";
            RecommendationText = res.RecommendationMessage;

            VerdictSummaryText = res.FitVerdict switch
            {
                FitVerdict.FullVram => $"🟢 Fits 100% in VRAM — Blazing Fast GPU Acceleration (~{res.EstimatedSecondsPerImage:F1}s / image)",
                FitVerdict.PartialOffload => $"🟡 Partial Offload — Sequential CPU Encoder Offloading (~{res.EstimatedSecondsPerImage:F1}s / image)",
                FitVerdict.CpuOnly => $"🟠 CPU Only — Slow System RAM Generation",
                _ => $"🔴 Out of Memory — Pipeline Exceeds System Memory"
            };

            OffloadSummaryText = res.FitVerdict == FitVerdict.FullVram
                ? "100% GPU Accelerated Pipeline"
                : $"Sequential Offload ({res.EncodersMb:N0} MB in RAM)";
        }
        else if (mod == "VIDEO")
        {
            int resPx = SelectedVideoResolution.Contains("480") ? 480 : 720;
            var req = new VideoFitRequest(
                ModelName: SelectedVideoPreset,
                Quantization: SelectedVideoQuantization,
                FrameCount: VideoFrameCount,
                Resolution: resPx,
                AvailableVramMb: vramLong,
                AvailableRamMb: ramLong
            );

            VideoResult = _canIRunItService.EvaluateVideoFit(req);
            var res = VideoResult;

            if (res.FitVerdict == FitVerdict.OutOfMemory)
            {
                ModelWeightsVramPercent = 0.0;
                KvCacheVramPercent = 0.0;
                OverheadVramPercent = 0.0;
                FreeVramPercent = 100.0;
            }
            else
            {
                ModelWeightsVramPercent = Math.Clamp((double)res.DiTModelMb / vram * 100.0, 0.0, 100.0);
                KvCacheVramPercent = Math.Clamp((double)res.FrameContextMb / vram * 100.0, 0.0, 100.0);
                OverheadVramPercent = Math.Clamp((double)res.VaeDecodeMb / vram * 100.0, 0.0, 100.0);
                FreeVramPercent = Math.Clamp(100.0 - ModelWeightsVramPercent - KvCacheVramPercent - OverheadVramPercent, 0.0, 100.0);
            }

            FitVerdictBadge = CreateBadge(res.FitVerdict, res.RecommendationMessage);
            SpeedEstimationText = $"~{res.EstimatedSecondsPerFrame:F2}s / frame";
            RecommendationText = res.RecommendationMessage;

            VerdictSummaryText = res.FitVerdict switch
            {
                FitVerdict.FullVram => $"🟢 Fits 100% in VRAM — Full Speed Video Generation (~{res.EstimatedSecondsPerFrame:F2}s / frame)",
                FitVerdict.PartialOffload => $"🟡 Partial Offload — Mixed VRAM/RAM Video Pipeline (~{res.EstimatedSecondsPerFrame:F2}s / frame)",
                FitVerdict.CpuOnly => $"🟠 CPU Only — Very Slow System RAM Generation",
                _ => $"🔴 Out of Memory — Pipeline Exceeds Available Memory"
            };

            OffloadSummaryText = res.FitVerdict == FitVerdict.FullVram
                ? "Full VRAM DiT + Context Pipeline"
                : $"Offloading DiT/VAE ({res.TotalRamMb:N0} MB in RAM)";
        }
        else if (mod == "AUDIO")
        {
            AudioResult = _canIRunItService.EvaluateAudioFit(SelectedAudioPreset, vramLong, ramLong);
            var res = AudioResult;

            if (res.FitVerdict == FitVerdict.OutOfMemory)
            {
                ModelWeightsVramPercent = 0.0;
                KvCacheVramPercent = 0.0;
                OverheadVramPercent = 0.0;
                FreeVramPercent = 100.0;
            }
            else
            {
                ModelWeightsVramPercent = Math.Clamp((double)res.VramRequiredMb / vram * 100.0, 0.0, 100.0);
                KvCacheVramPercent = 0.0;
                OverheadVramPercent = 0.0;
                FreeVramPercent = Math.Clamp(100.0 - ModelWeightsVramPercent, 0.0, 100.0);
            }

            FitVerdictBadge = CreateBadge(res.FitVerdict, res.RecommendationMessage);
            SpeedEstimationText = $"~{res.EstimatedRealtimeFactor:F1}x Realtime";
            RecommendationText = res.RecommendationMessage;

            VerdictSummaryText = res.FitVerdict switch
            {
                FitVerdict.FullVram => $"🟢 Fits 100% in VRAM — Realtime Audio Synthesis (~{res.EstimatedRealtimeFactor:F1}x RT)",
                FitVerdict.CpuOnly => $"🟠 CPU Only — Running on CPU Audio Engine (~{res.EstimatedRealtimeFactor:F1}x RT)",
                _ => $"🔴 Out of Memory — Audio Model Exceeds Memory"
            };

            OffloadSummaryText = res.FitVerdict == FitVerdict.FullVram
                ? "100% GPU Audio Pipeline"
                : "Running on CPU Engine";
        }
        else if (mod == "THREED" || mod == "3D")
        {
            ThreeDResult = _canIRunItService.Evaluate3DFit(SelectedThreeDPreset, vramLong, ramLong);
            var res = ThreeDResult;

            if (res.FitVerdict == FitVerdict.OutOfMemory)
            {
                ModelWeightsVramPercent = 0.0;
                KvCacheVramPercent = 0.0;
                OverheadVramPercent = 0.0;
                FreeVramPercent = 100.0;
            }
            else
            {
                ModelWeightsVramPercent = Math.Clamp((double)res.VramRequiredMb / vram * 100.0, 0.0, 100.0);
                KvCacheVramPercent = 0.0;
                OverheadVramPercent = 0.0;
                FreeVramPercent = Math.Clamp(100.0 - ModelWeightsVramPercent, 0.0, 100.0);
            }

            FitVerdictBadge = CreateBadge(res.FitVerdict, res.RecommendationMessage);
            SpeedEstimationText = $"~{res.EstimatedSecondsPerMesh:F1}s / mesh";
            RecommendationText = res.RecommendationMessage;

            VerdictSummaryText = res.FitVerdict switch
            {
                FitVerdict.FullVram => $"🟢 Fits 100% in VRAM — Fast 3D Latent Diffusion (~{res.EstimatedSecondsPerMesh:F1}s / mesh)",
                FitVerdict.PartialOffload => $"🟡 Partial Offload — Mixed System RAM Execution (~{res.EstimatedSecondsPerMesh:F1}s / mesh)",
                _ => $"🔴 Out of Memory — 3D Model Exceeds Memory"
            };

            OffloadSummaryText = res.FitVerdict == FitVerdict.FullVram
                ? "100% GPU 3D Latent Diffusion"
                : "Partial System RAM Offload";
        }
        else
        {
            // Default: LLM
            var req = new LlmFitRequest(
                ParametersBillions: ParametersBillions,
                Quantization: SelectedQuantization,
                ContextLength: ContextLength,
                KvPrecision: KvCachePrecision,
                AvailableVramMb: vramLong,
                AvailableRamMb: ramLong
            );

            LlmResult = _canIRunItService.EvaluateLlmFit(req);
            var res = LlmResult;

            if (res.FitVerdict == FitVerdict.FullVram)
            {
                ModelWeightsVramPercent = Math.Clamp((double)res.ModelWeightMb / vram * 100.0, 0.0, 100.0);
                KvCacheVramPercent = Math.Clamp((double)res.KvCacheMb / vram * 100.0, 0.0, 100.0);
                OverheadVramPercent = Math.Clamp((double)res.OverheadMb / vram * 100.0, 0.0, 100.0);
                FreeVramPercent = Math.Clamp(100.0 - ModelWeightsVramPercent - KvCacheVramPercent - OverheadVramPercent, 0.0, 100.0);
            }
            else if (res.FitVerdict == FitVerdict.PartialOffload)
            {
                double gpuRatio = res.TotalLayers > 0 ? (double)res.GpuLayers / res.TotalLayers : 0.0;
                double gpuWeightMb = gpuRatio * res.ModelWeightMb;

                ModelWeightsVramPercent = Math.Clamp(gpuWeightMb / vram * 100.0, 0.0, 100.0);
                KvCacheVramPercent = Math.Clamp((double)res.KvCacheMb / vram * 100.0, 0.0, 100.0);
                OverheadVramPercent = Math.Clamp((double)res.OverheadMb / vram * 100.0, 0.0, 100.0);
                FreeVramPercent = Math.Clamp(100.0 - ModelWeightsVramPercent - KvCacheVramPercent - OverheadVramPercent, 0.0, 100.0);
            }
            else
            {
                ModelWeightsVramPercent = 0.0;
                KvCacheVramPercent = 0.0;
                OverheadVramPercent = 0.0;
                FreeVramPercent = 100.0;
            }

            FitVerdictBadge = CreateBadge(res.FitVerdict, res.RecommendationMessage);
            SpeedEstimationText = $"~{res.EstimatedTokPerSec:F1} tok/s";
            RecommendationText = res.RecommendationMessage;

            VerdictSummaryText = res.FitVerdict switch
            {
                FitVerdict.FullVram => $"🟢 Fits 100% in VRAM — Blazing Fast GPU Acceleration (~{res.EstimatedTokPerSec:F1} tok/s)",
                FitVerdict.PartialOffload => $"🟡 Partial Offload — Mixed GPU & CPU Execution (~{res.EstimatedTokPerSec:F1} tok/s)",
                FitVerdict.CpuOnly => $"🟠 CPU Only — Execution Entirely in System RAM (~{res.EstimatedTokPerSec:F1} tok/s)",
                _ => $"🔴 Out of Memory — Model Exceeds Combined VRAM and RAM"
            };

            OffloadSummaryText = res.FitVerdict switch
            {
                FitVerdict.FullVram => $"{res.TotalLayers} / {res.TotalLayers} Layers in VRAM (100% GPU)",
                FitVerdict.PartialOffload => $"{res.GpuLayers} / {res.TotalLayers} Layers in VRAM ({(res.TotalLayers > 0 ? (double)res.GpuLayers / res.TotalLayers * 100.0 : 0):F0}% GPU)",
                FitVerdict.CpuOnly => $"0 / {res.TotalLayers} Layers in VRAM (0% GPU, 100% CPU)",
                _ => "0 Layers in VRAM (Out of Memory)"
            };
        }
    }

    private static QuickFitBadge CreateBadge(FitVerdict verdict, string recommendation)
    {
        var (badgeText, badgeColorHex) = verdict switch
        {
            FitVerdict.FullVram => ("🟢 Full VRAM", "#10B981"),
            FitVerdict.PartialOffload => ("🟡 Partial Offload", "#F59E0B"),
            FitVerdict.CpuOnly => ("🟠 CPU Only", "#F97316"),
            _ => ("🔴 Won't Fit (OOM)", "#EF4444")
        };

        return new QuickFitBadge(badgeText, badgeColorHex, recommendation, verdict);
    }

    private static double ExtractParamBillions(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return 8.0;

        var match = Regex.Match(modelName, @"(?i)(\d+(?:\.\d+)?)\s*b\b");
        if (match.Success && double.TryParse(match.Groups[1].Value, out double parsed))
        {
            return parsed;
        }

        string lower = modelName.ToLowerInvariant();
        if (lower.Contains("deepseek-r1") || lower.Contains("deepseek_r1") || lower.Contains("deepseek-v3") || lower.Contains("deepseek_v3") || lower.Contains("671"))
            return 671.0;
        if (lower.Contains("70") || lower.Contains("70b"))
            return 70.0;
        if (lower.Contains("72") || lower.Contains("72b"))
            return 72.0;
        if (lower.Contains("32") || lower.Contains("32b"))
            return 32.0;
        if (lower.Contains("27") || lower.Contains("27b"))
            return 27.0;
        if (lower.Contains("24") || lower.Contains("24b"))
            return 24.0;
        if (lower.Contains("14") || lower.Contains("14b"))
            return 14.0;
        if (lower.Contains("13") || lower.Contains("13b"))
            return 13.0;
        if (lower.Contains("9") || lower.Contains("9b"))
            return 9.0;
        if (lower.Contains("8") || lower.Contains("8b"))
            return 8.0;
        if (lower.Contains("7") || lower.Contains("7b"))
            return 7.0;
        if (lower.Contains("3") || lower.Contains("3b"))
            return 3.0;
        if (lower.Contains("1.5") || lower.Contains("1.5b"))
            return 1.5;

        return 8.0;
    }

    private static string ExtractQuantization(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return "Q4_K_M";

        var match = Regex.Match(modelName, @"(?i)(Q\d+_[K01A-Z_]+|FP16|FP8|BF16|Q\d+_0|Q\d+_1)");
        if (match.Success)
        {
            return match.Value.ToUpperInvariant();
        }

        return "Q4_K_M";
    }
}
