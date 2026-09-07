using System;
using System.Collections.Generic;
using System.Linq;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StudioPresetServiceTests
{
    [Fact]
    public void GetPresets_ReturnsBuiltInDefaults_ForVideoImageAudio()
    {
        var service = new StudioPresetService();
        var videoPresets = service.GetPresets(StudioModality.Video);
        var imagePresets = service.GetPresets(StudioModality.Image);
        var audioPresets = service.GetPresets(StudioModality.Audio);

        Assert.NotEmpty(videoPresets);
        Assert.Contains(videoPresets, p => p.Name.Contains("480p", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(videoPresets, p => p.Name.Contains("720p", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(videoPresets, p => p.Name.Contains("Vertical Reel", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(videoPresets, p => p.Name.Contains("High-Fidelity", StringComparison.OrdinalIgnoreCase));

        Assert.NotEmpty(imagePresets);
        Assert.Contains(imagePresets, p => p.Name.Contains("Square", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(imagePresets, p => p.Name.Contains("Landscape", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(imagePresets, p => p.Name.Contains("Portrait", StringComparison.OrdinalIgnoreCase));

        Assert.NotEmpty(audioPresets);
        Assert.Contains(audioPresets, p => p.Name.Contains("Storyteller", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(audioPresets, p => p.Name.Contains("Broadcaster", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(audioPresets, p => p.Name.Contains("Ambient", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(audioPresets, p => p.Name.Contains("Song", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetAllPresets_ReturnsCombinedList()
    {
        var service = new StudioPresetService();
        var all = service.GetAllPresets();
        Assert.True(all.Count >= 11);
        Assert.All(all, p => Assert.True(p.IsBuiltIn));
    }

    [Fact]
    public void GetPresetById_FindsBuiltInAndCustomPresets()
    {
        var service = new StudioPresetService();
        var builtIn = service.GetPresetById("builtin-video-480p");
        Assert.NotNull(builtIn);
        Assert.Equal("Quick 480p Preview", builtIn.Name);

        var custom = new StudioPreset
        {
            Id = "custom-test-id",
            Name = "My Custom Preset",
            Modality = StudioModality.Image
        };
        service.SavePreset(custom);

        var foundCustom = service.GetPresetById("custom-test-id");
        Assert.NotNull(foundCustom);
        Assert.Equal("My Custom Preset", foundCustom.Name);

        var notFound = service.GetPresetById("non-existent-id");
        Assert.Null(notFound);
    }

    [Fact]
    public void SaveAndGetCustomPreset_WorksCorrectly()
    {
        var service = new StudioPresetService();
        var custom = new StudioPreset
        {
            Name = "Custom 4K Video",
            Modality = StudioModality.Video,
            Width = 3840,
            Height = 2160,
            FrameCount = 60,
            Fps = 30
        };

        service.SavePreset(custom);
        var videoPresets = service.GetPresets(StudioModality.Video);

        Assert.Contains(videoPresets, p => p.Name == "Custom 4K Video" && p.Width == 3840);
    }

    [Fact]
    public void SavePreset_UpdatesExistingCustomPreset_WhenIdMatches()
    {
        var service = new StudioPresetService();
        var custom = new StudioPreset
        {
            Id = "preset-to-update",
            Name = "Initial Version",
            Modality = StudioModality.Image,
            Width = 512,
            Height = 512
        };
        service.SavePreset(custom);

        var updated = custom with { Name = "Updated Version", Width = 1024 };
        service.SavePreset(updated);

        var retrieved = service.GetPresetById("preset-to-update");
        Assert.NotNull(retrieved);
        Assert.Equal("Updated Version", retrieved.Name);
        Assert.Equal(1024, retrieved.Width);
    }

    [Fact]
    public void DeletePreset_RemovesCustom_DoesNotRemoveBuiltIn()
    {
        var service = new StudioPresetService();
        var custom = new StudioPreset
        {
            Name = "Temporary Preset",
            Modality = StudioModality.Image
        };
        service.SavePreset(custom);
        Assert.Contains(service.GetPresets(StudioModality.Image), p => p.Name == "Temporary Preset");

        var deleted = service.DeletePreset(custom.Id);
        Assert.True(deleted);
        Assert.DoesNotContain(service.GetPresets(StudioModality.Image), p => p.Name == "Temporary Preset");

        var builtIn = service.GetPresets(StudioModality.Video).First(p => p.IsBuiltIn);
        var deletedBuiltIn = service.DeletePreset(builtIn.Id);
        Assert.False(deletedBuiltIn);

        var nonExistentDeleted = service.DeletePreset("does-not-exist");
        Assert.False(nonExistentDeleted);
    }

    [Fact]
    public void DuplicatePreset_CreatesCopy_WithAppendedName()
    {
        var service = new StudioPresetService();
        var builtIn = service.GetPresets(StudioModality.Video).First(p => p.IsBuiltIn);
        var copy = service.DuplicatePreset(builtIn.Id);

        Assert.NotNull(copy);
        Assert.NotEqual(builtIn.Id, copy.Id);
        Assert.Equal($"{builtIn.Name} (Copy)", copy.Name);
        Assert.False(copy.IsBuiltIn);

        var foundInPresets = service.GetPresets(StudioModality.Video);
        Assert.Contains(foundInPresets, p => p.Id == copy.Id);
    }

    [Fact]
    public void DuplicatePreset_ReturnsNull_WhenIdNotFound()
    {
        var service = new StudioPresetService();
        var copy = service.DuplicatePreset("non-existent-preset-id");
        Assert.Null(copy);
    }

    [Fact]
    public void ExportAndImportJson_PreservesCustomPresets()
    {
        var service = new StudioPresetService();
        service.SavePreset(new StudioPreset { Name = "ExportTest", Modality = StudioModality.Audio });
        var json = service.ExportJson();

        var newService = new StudioPresetService();
        var success = newService.ImportJson(json);

        Assert.True(success);
        Assert.Contains(newService.GetPresets(StudioModality.Audio), p => p.Name == "ExportTest");
    }

    [Fact]
    public void ImportJson_HandlesInvalidJsonGracefully()
    {
        var service = new StudioPresetService();
        Assert.False(service.ImportJson(""));
        Assert.False(service.ImportJson("{ not valid json }"));
    }

    [Fact]
    public void ResetToDefaults_ClearsAllCustomPresets()
    {
        var service = new StudioPresetService();
        service.SavePreset(new StudioPreset { Name = "Custom 1", Modality = StudioModality.Video });
        service.SavePreset(new StudioPreset { Name = "Custom 2", Modality = StudioModality.Image });

        Assert.Contains(service.GetPresets(StudioModality.Video), p => p.Name == "Custom 1");
        Assert.Contains(service.GetPresets(StudioModality.Image), p => p.Name == "Custom 2");

        service.ResetToDefaults();

        Assert.DoesNotContain(service.GetPresets(StudioModality.Video), p => p.Name == "Custom 1");
        Assert.DoesNotContain(service.GetPresets(StudioModality.Image), p => p.Name == "Custom 2");
    }

    [Fact]
    public void Constructor_LoadsInitialCustomPresets()
    {
        var initialList = new List<StudioPreset>
        {
            new StudioPreset { Id = "init-1", Name = "Preloaded Custom", Modality = StudioModality.Audio, IsBuiltIn = false }
        };

        var service = new StudioPresetService(initialList);
        var preset = service.GetPresetById("init-1");

        Assert.NotNull(preset);
        Assert.Equal("Preloaded Custom", preset.Name);
    }
}
