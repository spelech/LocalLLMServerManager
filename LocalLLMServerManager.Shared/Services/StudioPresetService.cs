namespace LocalLLMServerManager.Shared.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;

/// <summary>
/// Service managing built-in and user-customized generation presets.
/// </summary>
public class StudioPresetService : IStudioPresetService
{
    private readonly object _lock = new();
    private readonly List<StudioPreset> _customPresets = new();

    private static readonly List<StudioPreset> BuiltInPresets = new()
    {
        // Video presets
        new StudioPreset
        {
            Id = "builtin-video-480p",
            Name = "Quick 480p Preview",
            Description = "Fast 480p preview optimized for rapid iteration and testing.",
            Modality = StudioModality.Video,
            WorkflowOrEngine = "wan2.2",
            Width = 832,
            Height = 480,
            FrameCount = 48,
            Fps = 16,
            DurationSeconds = 3,
            SamplePrompt = "A sleek sports car cruising down a neon-lit cyberpunk highway at night, cinematic lighting, 4k",
            NegativePrompt = "blurry, low quality, distorted, watermark",
            IsBuiltIn = true
        },
        new StudioPreset
        {
            Id = "builtin-video-720p",
            Name = "Cinematic HD 720p",
            Description = "High-definition 720p widescreen with fluid 24fps motion.",
            Modality = StudioModality.Video,
            WorkflowOrEngine = "wan2.2",
            Width = 1280,
            Height = 720,
            FrameCount = 80,
            Fps = 24,
            DurationSeconds = 4,
            SamplePrompt = "Cinematic drone shot of misty mountain peaks at sunrise, golden hour, volumetric rays, high detail",
            NegativePrompt = "blurry, low resolution, artifacts, jitter",
            IsBuiltIn = true
        },
        new StudioPreset
        {
            Id = "builtin-video-vertical-reel",
            Name = "Vertical Reel 9:16",
            Description = "Vertical portrait format ideal for mobile feeds and social reels.",
            Modality = StudioModality.Video,
            WorkflowOrEngine = "wan2.2",
            Width = 480,
            Height = 832,
            FrameCount = 48,
            Fps = 16,
            DurationSeconds = 3,
            SamplePrompt = "A stylish dancer in streetwear performing in an urban subway station, dynamic camera movement",
            NegativePrompt = "static, blurry, cropped, watermark",
            IsBuiltIn = true
        },
        new StudioPreset
        {
            Id = "builtin-video-master",
            Name = "High-Fidelity Master",
            Description = "Maximum quality setting with extended frames and smooth framerate.",
            Modality = StudioModality.Video,
            WorkflowOrEngine = "wan2.2",
            Width = 1280,
            Height = 720,
            FrameCount = 96,
            Fps = 24,
            DurationSeconds = 4,
            SamplePrompt = "Macro close-up of a blooming mechanical flower opening its bioluminescent petals in dark forest",
            NegativePrompt = "deformed, stutter, blurry, low quality",
            IsBuiltIn = true
        },

        // Image presets
        new StudioPreset
        {
            Id = "builtin-image-square",
            Name = "Standard Square 1024x1024",
            Description = "Classic 1:1 balanced square format for general imagery and icons.",
            Modality = StudioModality.Image,
            WorkflowOrEngine = "comfy",
            Width = 1024,
            Height = 1024,
            SamplePrompt = "An intricate clockwork dragon perched on an antique book, detailed brass gears, macro lens",
            NegativePrompt = "blurry, mutated, extra limbs, watermark",
            IsBuiltIn = true
        },
        new StudioPreset
        {
            Id = "builtin-image-landscape",
            Name = "Landscape Wallpaper 1344x768",
            Description = "16:9 widescreen format perfect for desktop wallpapers and landscape art.",
            Modality = StudioModality.Image,
            WorkflowOrEngine = "comfy",
            Width = 1344,
            Height = 768,
            SamplePrompt = "Breathtaking landscape of a fantasy floating island archipelago with waterfalls flowing into the clouds, sunset",
            NegativePrompt = "blurry, low resolution, ugly, artifacts",
            IsBuiltIn = true
        },
        new StudioPreset
        {
            Id = "builtin-image-portrait",
            Name = "Portrait Photo 768x1152",
            Description = "Tall 2:3 portrait aspect ratio tailored for character portraits and fashion photography.",
            Modality = StudioModality.Image,
            WorkflowOrEngine = "comfy",
            Width = 768,
            Height = 1152,
            SamplePrompt = "Studio portrait of an elven archer in ornamental silver armor, dramatic Rembrandt lighting, 85mm portrait photography",
            NegativePrompt = "blurry, deformed eyes, extra fingers, poor lighting",
            IsBuiltIn = true
        },

        // Audio presets
        new StudioPreset
        {
            Id = "builtin-audio-storyteller",
            Name = "Natural Storyteller",
            Description = "Warm, natural voice profile tuned for narration, audiobooks, and long-form storytelling.",
            Modality = StudioModality.Audio,
            WorkflowOrEngine = "kokoro",
            VoiceProfile = "af_heart",
            DurationSeconds = 10,
            SamplePrompt = "Welcome to the enchanted forest. Deep within these woods lies an ancient secret waiting to be discovered.",
            IsBuiltIn = true
        },
        new StudioPreset
        {
            Id = "builtin-audio-broadcaster",
            Name = "Energetic Broadcaster",
            Description = "Punchy, dynamic male voice ideal for announcements, podcasts, and energetic intros.",
            Modality = StudioModality.Audio,
            WorkflowOrEngine = "kokoro",
            VoiceProfile = "am_adam",
            DurationSeconds = 5,
            SamplePrompt = "Breaking news! The next-generation local AI engine is now live and running at peak performance.",
            IsBuiltIn = true
        },
        new StudioPreset
        {
            Id = "builtin-audio-ambient",
            Name = "Ambient Soundscape",
            Description = "Atmospheric ambient generation for background themes and immersive audio environments.",
            Modality = StudioModality.Audio,
            WorkflowOrEngine = "kokoro",
            DurationSeconds = 15,
            SamplePrompt = "Gentle ocean waves crashing against a rocky shore at twilight with distant seagulls calling.",
            IsBuiltIn = true
        },
        new StudioPreset
        {
            Id = "builtin-audio-song",
            Name = "Full Song Generator",
            Description = "Extended audio generation for musical motifs, melodic tracks, and synthesized songs.",
            Modality = StudioModality.Audio,
            WorkflowOrEngine = "kokoro",
            DurationSeconds = 30,
            SamplePrompt = "An uplifting synthwave track with driving 80s drum beat, arpeggiated bassline, and warm analog synthesizers.",
            IsBuiltIn = true
        }
    };

    public StudioPresetService(IEnumerable<StudioPreset>? customPresets = null)
    {
        if (customPresets != null)
        {
            foreach (var preset in customPresets)
            {
                if (!preset.IsBuiltIn)
                {
                    _customPresets.Add(preset);
                }
            }
        }
    }

    public IReadOnlyList<StudioPreset> GetPresets(StudioModality modality)
    {
        lock (_lock)
        {
            var builtIns = BuiltInPresets.Where(p => p.Modality == modality);
            var customs = _customPresets.Where(p => p.Modality == modality);
            return builtIns.Concat(customs).ToList();
        }
    }

    public IReadOnlyList<StudioPreset> GetAllPresets()
    {
        lock (_lock)
        {
            return BuiltInPresets.Concat(_customPresets).ToList();
        }
    }

    public StudioPreset? GetPresetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        lock (_lock)
        {
            return _customPresets.FirstOrDefault(p => p.Id == id)
                ?? BuiltInPresets.FirstOrDefault(p => p.Id == id);
        }
    }

    public void SavePreset(StudioPreset preset)
    {
        if (preset == null) throw new ArgumentNullException(nameof(preset));

        lock (_lock)
        {
            var targetPreset = preset with { IsBuiltIn = false };

            // If an existing custom preset has the same Id, replace it
            var index = _customPresets.FindIndex(p => p.Id == targetPreset.Id);
            if (index >= 0)
            {
                _customPresets[index] = targetPreset;
            }
            else
            {
                // If it matched a built-in ID, assign a new unique ID
                if (BuiltInPresets.Any(p => p.Id == targetPreset.Id))
                {
                    targetPreset = targetPreset with { Id = Guid.NewGuid().ToString() };
                }
                _customPresets.Add(targetPreset);
            }
        }
    }

    public bool DeletePreset(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        lock (_lock)
        {
            // Built-ins cannot be deleted
            if (BuiltInPresets.Any(p => p.Id == id))
            {
                return false;
            }

            var removedCount = _customPresets.RemoveAll(p => p.Id == id);
            return removedCount > 0;
        }
    }

    public StudioPreset? DuplicatePreset(string id)
    {
        var existing = GetPresetById(id);
        if (existing == null) return null;

        var duplicate = existing with
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"{existing.Name} (Copy)",
            IsBuiltIn = false
        };

        lock (_lock)
        {
            _customPresets.Add(duplicate);
        }

        return duplicate;
    }

    public string ExportJson()
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(_customPresets, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });
        }
    }

    public bool ImportJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        try
        {
            var imported = JsonSerializer.Deserialize<List<StudioPreset>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (imported == null) return false;

            lock (_lock)
            {
                foreach (var item in imported)
                {
                    var customItem = item with { IsBuiltIn = false };
                    var index = _customPresets.FindIndex(p => p.Id == customItem.Id);
                    if (index >= 0)
                    {
                        _customPresets[index] = customItem;
                    }
                    else
                    {
                        _customPresets.Add(customItem);
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ResetToDefaults()
    {
        lock (_lock)
        {
            _customPresets.Clear();
        }
    }
}
