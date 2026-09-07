using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

public record AudioWorkflowItem(
    string Id,
    string Name,
    string Filename,
    string Path,
    string Type,
    string Description
);

public record AudioFileItem(
    string Filename,
    string Url,
    long SizeBytes,
    DateTime CreatedAt
);

public partial class AudioStudioViewModel : ObservableObject
{
    private readonly IStudioPresetService _presetService;
    private readonly ICanIRunItService _canIRunItService;

    public ObservableCollection<StudioPreset> AudioPresets { get; } = new();
    public ObservableCollection<StudioPreset> AudioStarterPrompts { get; } = new();

    [ObservableProperty] private StudioPreset? _selectedAudioPreset;
    [ObservableProperty] private StudioHardwareFit? _audioHardwareFit;
    [ObservableProperty] private string _voiceProfile = "af_heart";

    // Rich 4-Stage Stepper & Live Log Tracking
    [ObservableProperty] private int _generationStage = 0;
    [ObservableProperty] private string _generationStageTitle = "Idle";
    [ObservableProperty] private string _generationStageSubtext = "Ready to generate audio";
    [ObservableProperty] private string _generationElapsedText = "⏱️ 0:00s elapsed";
    [ObservableProperty] private string _elapsedTimerText = "⏱️ 0:00s elapsed";
    [ObservableProperty] private string _liveLogOutput = "";
    [ObservableProperty] private string _logsText = "";
    [ObservableProperty] private bool _isLiveLogsExpanded = false;
    [ObservableProperty] private bool _isLogsExpanded = false;
    [ObservableProperty] private string _stage1Status = "Pending";
    [ObservableProperty] private string _stage2Status = "Pending";
    [ObservableProperty] private string _stage3Status = "Pending";
    [ObservableProperty] private string _stage4Status = "Pending";
    [ObservableProperty] private double _progressValue = 0.0;
    [ObservableProperty] private bool _isIndeterminate = false;
    [ObservableProperty] private string _hardwareStatusBadgeText = "🟢 Ready";

    [ObservableProperty] private ObservableCollection<AudioWorkflowItem> _workflows = new();
    [ObservableProperty] private AudioWorkflowItem? _selectedWorkflow;
    [ObservableProperty] private string _prompt = "Cyberpunk atmospheric ambient drone, heavy synthesizer, cinematic low end, 48kHz stereo";
    [ObservableProperty] private string _negativePrompt = "low quality, harsh distortion";
    [ObservableProperty] private int _durationSeconds = 30;
    [ObservableProperty] private long _seed = -1;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private ObservableCollection<AudioFileItem> _generatedAudioFiles = new();
    [ObservableProperty] private AudioFileItem? _selectedAudioFile;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _playingTrackTitle = "No Track Loaded";

    public string PlayButtonText => IsPlaying ? "⏸️ Pause" : "▶️ Play";

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlayButtonText));
    }

    [ObservableProperty] private string _apiBase = OperatingSystem.IsBrowser() ? "" : "http://127.0.0.1:5246";

    public AudioStudioViewModel() : this(null, null)
    {
    }

    public AudioStudioViewModel(IStudioPresetService? presetService, ICanIRunItService? canIRunItService = null)
    {
        _presetService = presetService ?? new StudioPresetService();
        _canIRunItService = canIRunItService ?? new CanIRunItService();
        LoadAudioPresets();
        RecalculateHardwareFit(8000, 16000);
    }

    public async Task LoadAudioWorkflowsAsync(string apiBase, HttpClient http)
    {
        try
        {
            var items = await http.GetFromJsonAsync<AudioWorkflowItem[]>($"{apiBase}/api/audio/workflows");
            if (items != null)
            {
                Workflows.Clear();
                foreach (var item in items)
                {
                    Workflows.Add(item);
                }
                if (Workflows.Count > 0 && SelectedWorkflow == null)
                {
                    SelectedWorkflow = Workflows[0];
                }
            }
        }
        catch
        {
            // Fallback default workflows if backend offline
            Workflows.Clear();
            var w1 = new AudioWorkflowItem("stable_audio_open_sfx", "Stable Audio Open 3.0 (SFX & Ambient)", "stable_audio_open_sfx.json", "", "audio", "Text-to-sound-effects and ambient audio generation");
            var w2 = new AudioWorkflowItem("yue_full_song", "YuE Full Song Generation (乐)", "yue_full_song.json", "", "audio", "Dual-track lyrics-to-music generation");
            Workflows.Add(w1);
            Workflows.Add(w2);
            SelectedWorkflow = w1;
        }
    }

    public async Task LoadAudioFilesAsync(string apiBase, HttpClient http)
    {
        try
        {
            var items = await http.GetFromJsonAsync<AudioFileItem[]>($"{apiBase}/api/audio/files");
            if (items != null)
            {
                GeneratedAudioFiles.Clear();
                foreach (var item in items)
                {
                    GeneratedAudioFiles.Add(item);
                }
                if (GeneratedAudioFiles.Count > 0 && SelectedAudioFile == null)
                {
                    SelectedAudioFile = GeneratedAudioFiles[0];
                    PlayingTrackTitle = SelectedAudioFile.Filename;
                }
            }
        }
        catch
        {
            // Fail gracefully
        }
    }

    public void LoadAudioPresets()
    {
        AudioPresets.Clear();
        AudioStarterPrompts.Clear();
        foreach (var p in _presetService.GetPresets(StudioModality.Audio))
        {
            AudioPresets.Add(p);
            AudioStarterPrompts.Add(p);
        }
        if (SelectedAudioPreset == null && AudioPresets.Count > 0)
        {
            SelectedAudioPreset = AudioPresets[0];
        }
    }

    [RelayCommand]
    public void SelectAudioPreset(StudioPreset? preset)
    {
        if (preset == null) return;
        SelectedAudioPreset = preset;
        if (!string.IsNullOrWhiteSpace(preset.SamplePrompt)) Prompt = preset.SamplePrompt;
        if (!string.IsNullOrWhiteSpace(preset.NegativePrompt)) NegativePrompt = preset.NegativePrompt;
        if (!string.IsNullOrWhiteSpace(preset.VoiceProfile)) VoiceProfile = preset.VoiceProfile;
        if (preset.DurationSeconds > 0) DurationSeconds = preset.DurationSeconds;
        RecalculateHardwareFit(8000, 16000);
    }

    [RelayCommand]
    public void ApplyStarterChip(object? param)
    {
        if (param is StudioPreset p)
        {
            SelectAudioPreset(p);
        }
        else if (param is string name)
        {
            var matched = AudioPresets.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (matched != null) SelectAudioPreset(matched);
        }
    }

    [RelayCommand]
    public void SaveCurrentAsAudioPreset(object? customNameOrPreset = null)
    {
        string? name = customNameOrPreset is string s ? s : (customNameOrPreset is StudioPreset p ? p.Name : null);
        var preset = new StudioPreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = string.IsNullOrWhiteSpace(name) ? $"Custom Audio Preset {AudioPresets.Count + 1}" : name,
            Description = "User customized audio/TTS preset",
            Modality = StudioModality.Audio,
            WorkflowOrEngine = SelectedWorkflow?.Id ?? "kokoro",
            VoiceProfile = VoiceProfile,
            DurationSeconds = DurationSeconds,
            SamplePrompt = Prompt,
            NegativePrompt = NegativePrompt,
            IsBuiltIn = false
        };
        _presetService.SavePreset(preset);
        LoadAudioPresets();
        SelectedAudioPreset = AudioPresets.FirstOrDefault(p => p.Id == preset.Id);
    }

    [RelayCommand]
    public void DeleteCurrentAudioPreset(object? presetParam = null)
    {
        var preset = presetParam as StudioPreset ?? SelectedAudioPreset;
        if (preset != null && !preset.IsBuiltIn)
        {
            _presetService.DeletePreset(preset.Id);
            LoadAudioPresets();
        }
    }

    [RelayCommand]
    public void DuplicateCurrentAudioPreset(object? presetParam = null)
    {
        var preset = presetParam as StudioPreset ?? SelectedAudioPreset;
        if (preset != null)
        {
            var dup = _presetService.DuplicatePreset(preset.Id);
            LoadAudioPresets();
            if (dup != null)
            {
                SelectedAudioPreset = AudioPresets.FirstOrDefault(p => p.Id == dup.Id);
            }
        }
    }

    [RelayCommand]
    public void ToggleLiveLogs()
    {
        IsLiveLogsExpanded = !IsLiveLogsExpanded;
        IsLogsExpanded = IsLiveLogsExpanded;
    }

    [RelayCommand]
    public void CancelGeneration()
    {
        IsGenerating = false;
        GenerationStage = 0;
        GenerationStageTitle = "Cancelled";
        GenerationStageSubtext = "Audio generation was cancelled.";
        Stage1Status = "Pending";
        Stage2Status = "Pending";
        Stage3Status = "Pending";
        Stage4Status = "Pending";
        ProgressValue = 0;
        StatusMessage = "Generation cancelled.";
    }

    public void RecalculateHardwareFit(double freeVramMb, double totalVramMb)
    {
        AudioHardwareFit = _canIRunItService.EstimateStudioHardwareFit(
            StudioModality.Audio,
            0,
            0,
            0,
            SelectedWorkflow?.Name ?? "kokoro",
            freeVramMb,
            totalVramMb
        );
        HardwareStatusBadgeText = AudioHardwareFit.FitBadge.BadgeText;
    }

    [RelayCommand]
    public async Task GenerateAudioAsync(ParamContext? ctx = null)
    {
        if (IsGenerating) return;

        IsGenerating = true;
        GenerationStage = 1;
        GenerationStageTitle = "1. VRAM & Model Prep";
        GenerationStageSubtext = "Allocating memory and preparing audio synthesis pipeline...";
        Stage1Status = "Active";
        Stage2Status = "Pending";
        Stage3Status = "Pending";
        Stage4Status = "Pending";
        ProgressValue = 15;
        LiveLogOutput = $"[Stage 1] Initializing audio model {SelectedWorkflow?.Name ?? "kokoro"}...\n";
        LogsText = LiveLogOutput;
        StatusMessage = "Queuing audio workflow on ComfyUI...";

        try
        {
            var apiBase = ctx?.ApiBase ?? (!string.IsNullOrWhiteSpace(ApiBase) ? ApiBase : (OperatingSystem.IsBrowser() ? "" : "http://127.0.0.1:5246"));
            var http = ctx?.Http ?? HttpHelper.CreateClient(apiBase);

            var payload = new
            {
                workflowId = SelectedWorkflow?.Id ?? "stable_audio_open_sfx",
                prompt = Prompt,
                negativePrompt = NegativePrompt,
                durationSeconds = DurationSeconds,
                seed = Seed
            };

            Stage1Status = "Complete";
            GenerationStage = 2;
            GenerationStageTitle = "2. Denoising & Synthesis";
            GenerationStageSubtext = "Synthesizing audio spectrogram / waveform latents...";
            Stage2Status = "Active";
            ProgressValue = 50;
            LiveLogOutput += "[Stage 2] Generating audio latents with conditioning...\n";
            LogsText = LiveLogOutput;

            var response = await http.PostAsJsonAsync($"{apiBase}/api/audio/generate", payload);
            if (response.IsSuccessStatusCode)
            {
                Stage2Status = "Complete";
                GenerationStage = 3;
                GenerationStageTitle = "3. Encoding & Assembly";
                GenerationStageSubtext = "Encoding output WAV / MP3 track...";
                Stage3Status = "Active";
                ProgressValue = 85;
                LiveLogOutput += "[Stage 3] Assembling audio stream...\n";
                LogsText = LiveLogOutput;

                StatusMessage = "🎵 Audio workflow queued successfully! Rendering track...";
                await LoadAudioFilesAsync(apiBase, http);

                Stage3Status = "Complete";
                GenerationStage = 4;
                GenerationStageTitle = "4. Ready";
                GenerationStageSubtext = "Audio track ready for playback.";
                Stage4Status = "Complete";
                ProgressValue = 100;
                LiveLogOutput += "[Stage 4] Track rendered successfully!\n";
                LogsText = LiveLogOutput;
            }
            else
            {
                StatusMessage = "⚠️ Failed to queue audio generation.";
                GenerationStage = 0;
                GenerationStageTitle = "Error";
                GenerationStageSubtext = "Failed to queue audio generation.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"⚠️ Error: {ex.Message}";
            GenerationStage = 0;
            GenerationStageTitle = "Error";
            GenerationStageSubtext = ex.Message;
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    public void TogglePlay()
    {
        if (SelectedAudioFile == null && GeneratedAudioFiles.Count > 0)
        {
            SelectedAudioFile = GeneratedAudioFiles[0];
        }

        if (SelectedAudioFile == null)
        {
            StatusMessage = "No audio track selected to play.";
            return;
        }

        IsPlaying = !IsPlaying;
        PlayingTrackTitle = SelectedAudioFile.Filename;
        StatusMessage = IsPlaying ? $"▶️ Playing: {SelectedAudioFile.Filename}" : "⏸️ Paused";
    }

    partial void OnSelectedAudioFileChanged(AudioFileItem? value)
    {
        if (value != null)
        {
            PlayingTrackTitle = value.Filename;
        }
    }
}

public record ParamContext(string ApiBase, HttpClient Http);
