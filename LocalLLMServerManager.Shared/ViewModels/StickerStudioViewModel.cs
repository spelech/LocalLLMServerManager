using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

/// <summary>
/// ViewModel driving the Sticker Studio workflow: reference input, style presets,
/// die-cut border configuration, multi-stage generation pipeline, and export actions.
/// </summary>
public partial class StickerStudioViewModel : ObservableObject
{
    private readonly IStickerGenerationService _generationService;
    private CancellationTokenSource? _generationCts;

    public ObservableCollection<StickerStylePreset> StylePresets { get; } = new();

    [ObservableProperty]
    private StickerStylePreset? _selectedStylePreset;

    [ObservableProperty]
    private string? _inputImagePath;

    [ObservableProperty]
    private byte[]? _inputImageBytes;

    [ObservableProperty]
    private string _customPrompt = string.Empty;

    [ObservableProperty]
    private string _negativePrompt = string.Empty;

    private int _borderWidth = 12;

    /// <summary>
    /// Die-cut white contour border thickness, clamped between 0 and 24.
    /// </summary>
    public int BorderWidth
    {
        get => _borderWidth;
        set => SetProperty(ref _borderWidth, Math.Clamp(value, 0, 24));
    }

    [ObservableProperty]
    private bool _isAutoCutoutEnabled = true;

    [ObservableProperty]
    private StickerPipelineStage _currentStage = StickerPipelineStage.Idle;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateStickerCommand))]
    private bool _isGenerating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGeneratedSticker))]
    [NotifyCanExecuteChangedFor(nameof(CopyStickerCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveStickerCommand))]
    private byte[]? _generatedPngBytes;

    [ObservableProperty]
    private string? _outputImagePath;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public bool HasInputImage => !string.IsNullOrEmpty(InputImagePath) || (InputImageBytes != null && InputImageBytes.Length > 0);
    public bool HasGeneratedSticker => GeneratedPngBytes != null && GeneratedPngBytes.Length > 0;

    partial void OnInputImagePathChanged(string? value) => OnPropertyChanged(nameof(HasInputImage));
    partial void OnInputImageBytesChanged(byte[]? value) => OnPropertyChanged(nameof(HasInputImage));

    partial void OnSelectedStylePresetChanged(StickerStylePreset? value)
    {
        if (value != null)
        {
            BorderWidth = value.DefaultBorderWidth;
        }
    }

    public event Func<byte[], Task>? CopyToClipboardRequested;
    public event Func<byte[], Task<string?>>? SaveFileRequested;

    public StickerStudioViewModel() : this(new StickerGenerationService())
    {
    }

    public StickerStudioViewModel(IStickerGenerationService generationService)
    {
        _generationService = generationService ?? throw new ArgumentNullException(nameof(generationService));

        // Populate curated presets
        var presets = _generationService.GetDefaultPresets();
        var presetList = (presets != null && presets.Count > 0) ? presets : StickerGenerationService.DefaultPresets;
        foreach (var preset in presetList)
        {
            StylePresets.Add(preset);
        }

        SelectedStylePreset = StylePresets.FirstOrDefault(p => p.Id == "die-cut-vinyl") ?? StylePresets.FirstOrDefault();
        if (SelectedStylePreset != null)
        {
            BorderWidth = SelectedStylePreset.DefaultBorderWidth;
        }
    }

    private bool CanGenerateSticker => !IsGenerating;
    private bool CanExportSticker => HasGeneratedSticker;

    [RelayCommand]
    public void SelectStylePreset(StickerStylePreset? preset)
    {
        if (preset != null)
        {
            SelectedStylePreset = preset;
            BorderWidth = preset.DefaultBorderWidth;
        }
    }

    [RelayCommand]
    public void ClearInput()
    {
        InputImagePath = null;
        InputImageBytes = null;
    }

    [RelayCommand]
    public void DropImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        InputImagePath = path;
        try
        {
            if (File.Exists(path))
            {
                InputImageBytes = File.ReadAllBytes(path);
            }
        }
        catch
        {
            // Fall back gracefully if direct file read fails
        }
    }

    public void SetInputImage(string? path, byte[]? bytes = null)
    {
        InputImagePath = path;
        InputImageBytes = bytes;
    }

    [RelayCommand(CanExecute = nameof(CanGenerateSticker))]
    public async Task GenerateStickerAsync()
    {
        if (IsGenerating)
        {
            return;
        }

        _generationCts?.Cancel();
        _generationCts = new CancellationTokenSource();
        var ct = _generationCts.Token;

        try
        {
            IsGenerating = true;
            ErrorMessage = null;
            StatusMessage = "Starting sticker generation pipeline...";

            CurrentStage = StickerPipelineStage.GeneratingDiffusion;
            await Task.Yield();

            CurrentStage = StickerPipelineStage.IsolatingSubject;
            await Task.Yield();

            CurrentStage = StickerPipelineStage.ApplyingContour;
            await Task.Yield();

            var request = new StickerGenerationRequest
            {
                ImagePath = InputImagePath,
                ImageBytes = InputImageBytes,
                StylePresetId = SelectedStylePreset?.Id ?? string.Empty,
                CustomPrompt = CustomPrompt,
                NegativePrompt = NegativePrompt,
                BorderWidth = BorderWidth,
                IsAutoCutoutEnabled = IsAutoCutoutEnabled
            };

            var result = await _generationService.GenerateStickerAsync(request, ct);

            if (result.IsSuccess)
            {
                GeneratedPngBytes = result.OutputPngBytes;
                OutputImagePath = result.OutputImagePath;
                CurrentStage = StickerPipelineStage.Ready;
                StatusMessage = "Sticker generated successfully.";
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Generation failed";
                CurrentStage = StickerPipelineStage.Failed;
                StatusMessage = $"Generation failed: {ErrorMessage}";
            }
        }
        catch (OperationCanceledException)
        {
            CurrentStage = StickerPipelineStage.Idle;
            StatusMessage = "Generation cancelled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CurrentStage = StickerPipelineStage.Failed;
            StatusMessage = $"Generation error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExportSticker))]
    public async Task CopyStickerAsync()
    {
        if (GeneratedPngBytes == null || GeneratedPngBytes.Length == 0)
        {
            return;
        }

        if (CopyToClipboardRequested != null)
        {
            await CopyToClipboardRequested.Invoke(GeneratedPngBytes);
        }
        StatusMessage = "Sticker copied to clipboard.";
    }

    [RelayCommand(CanExecute = nameof(CanExportSticker))]
    public async Task SaveStickerAsync()
    {
        if (GeneratedPngBytes == null || GeneratedPngBytes.Length == 0)
        {
            return;
        }

        if (SaveFileRequested != null)
        {
            var savedPath = await SaveFileRequested.Invoke(GeneratedPngBytes);
            if (!string.IsNullOrEmpty(savedPath))
            {
                OutputImagePath = savedPath;
            }
        }
        StatusMessage = "Sticker saved.";
    }

    public void CancelGeneration()
    {
        _generationCts?.Cancel();
    }
}
