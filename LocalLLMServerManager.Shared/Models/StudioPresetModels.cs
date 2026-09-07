namespace LocalLLMServerManager.Shared.Models;

using System;

/// <summary>
/// Studio modality types supported for generation presets.
/// </summary>
public enum StudioModality
{
    Image,
    Video,
    Audio
}

/// <summary>
/// A preset configuration for Image, Video, or Audio/TTS generation.
/// </summary>
public record StudioPreset
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public StudioModality Modality { get; init; } = StudioModality.Image;
    public string WorkflowOrEngine { get; init; } = "";
    public int Width { get; init; } = 832;
    public int Height { get; init; } = 480;
    public int FrameCount { get; init; } = 48;
    public int Fps { get; init; } = 16;
    public int DurationSeconds { get; init; } = 3;
    public string VoiceProfile { get; init; } = "";
    public string SamplePrompt { get; init; } = "";
    public string NegativePrompt { get; init; } = "";
    public bool IsBuiltIn { get; init; } = false;

    public bool IsCustom => !IsBuiltIn;

    public string SummaryText => Modality switch
    {
        StudioModality.Video => $"{Width}x{Height} • {FrameCount} frames • {Fps} fps",
        StudioModality.Image => $"{Width}x{Height}",
        StudioModality.Audio => !string.IsNullOrWhiteSpace(VoiceProfile) ? $"{VoiceProfile} • {DurationSeconds}s" : $"{DurationSeconds}s audio",
        _ => $"{Width}x{Height}"
    };

    public string BadgeText => IsBuiltIn ? "🔒 Built-in" : "✨ Custom";

    public string ModalityText => Modality switch
    {
        StudioModality.Video => "🎬 Video",
        StudioModality.Image => "🎨 Image",
        StudioModality.Audio => "🎵 Audio",
        _ => Modality.ToString()
    };
}
