using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    public AudioStudioViewModel()
    {
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

    [RelayCommand]
    public async Task GenerateAudioAsync(ParamContext? ctx)
    {
        if (IsGenerating) return;

        IsGenerating = true;
        StatusMessage = "Queuing audio workflow on ComfyUI...";

        try
        {
            var apiBase = ctx?.ApiBase ?? "http://127.0.0.1:5246";
            var http = ctx?.Http ?? MainViewModel.DefaultHttpClient;

            var payload = new
            {
                workflowId = SelectedWorkflow?.Id ?? "stable_audio_open_sfx",
                prompt = Prompt,
                negativePrompt = NegativePrompt,
                durationSeconds = DurationSeconds,
                seed = Seed
            };

            var response = await http.PostAsJsonAsync($"{apiBase}/api/audio/generate", payload);
            if (response.IsSuccessStatusCode)
            {
                StatusMessage = "🎵 Audio workflow queued successfully! Rendering track...";
                await LoadAudioFilesAsync(apiBase, http);
            }
            else
            {
                StatusMessage = "⚠️ Failed to queue audio generation.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"⚠️ Error: {ex.Message}";
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
