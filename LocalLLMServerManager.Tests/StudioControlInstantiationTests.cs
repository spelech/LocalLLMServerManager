using System.Collections.Generic;
using Avalonia.Headless.XUnit;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views.Controls;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StudioControlInstantiationTests
{
    [AvaloniaFact]
    public void StudioPresetBarControl_InstantiatesAndSetsProperties()
    {
        var control = new StudioPresetBarControl();
        Assert.NotNull(control);

        var presets = new List<StudioPreset>
        {
            new StudioPreset { Name = "Test 1", Modality = StudioModality.Video }
        };

        control.Presets = presets;
        control.SelectedPreset = presets[0];
        control.StarterPrompts = presets;
        control.IsCustomPreset = true;

        Assert.Equal(presets, control.Presets);
        Assert.Equal("Test 1", control.SelectedPreset?.Name);
        Assert.True(control.IsCustomPreset);
    }

    [AvaloniaFact]
    public void GenerationStageTrackerControl_InstantiatesAndToggles()
    {
        var control = new GenerationStageTrackerControl();
        Assert.NotNull(control);

        Assert.Equal(0, control.CurrentStage);
        Assert.False(control.IsLogsExpanded);
        Assert.Equal("📜 Show Live Logs", control.LogsButtonText);

        control.CurrentStage = 2;
        control.Stage2Status = "Step 10/20";
        control.ProgressValue = 50.0;
        control.IsLogsExpanded = true;
        control.LogsText = "Allocating tensors...";

        Assert.Equal(2, control.CurrentStage);
        Assert.Equal("Step 10/20", control.Stage2Status);
        Assert.Equal(50.0, control.ProgressValue);
        Assert.True(control.IsLogsExpanded);
        Assert.Equal("📜 Hide Live Logs", control.LogsButtonText);
        Assert.Equal("Allocating tensors...", control.LogsText);
    }

    [AvaloniaFact]
    public void TestFlightModalControl_InstantiatesAndSetsDefaults()
    {
        var control = new TestFlightModalControl();
        Assert.NotNull(control);

        Assert.Equal(StudioModality.Video, control.SelectedModality);
        Assert.True(control.IsEngineOnline);
        Assert.True(control.IsVramClear);
        Assert.False(control.IsRunning);
        Assert.False(control.IsSuccess);
        Assert.False(control.HasError);

        control.SelectedModality = StudioModality.Image;
        control.StatusMessage = "Running image pass...";
        control.ProgressValue = 75.0;
        control.ErrorMessage = "CUDA OOM simulated";

        Assert.Equal(StudioModality.Image, control.SelectedModality);
        Assert.Equal("Running image pass...", control.StatusMessage);
        Assert.Equal(75.0, control.ProgressValue);
        Assert.True(control.HasError);
    }

    [AvaloniaFact]
    public void ImageDropZoneControl_InstantiatesAndBindsToViewModel()
    {
        var vm = new StickerStudioViewModel(new StickerGenerationService());
        var control = new ImageDropZoneControl { DataContext = vm };

        Assert.False(control.HasImage);
        Assert.Null(control.DisplayFileName);

        // Simulate dropping/setting an image
        var dummyBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        control.SetImage("sample_sticker.png", dummyBytes);

        Assert.True(control.HasImage);
        Assert.Equal("sample_sticker.png", control.DisplayFileName);
        Assert.Equal("sample_sticker.png", vm.InputImagePath);
        Assert.Equal(dummyBytes, vm.InputImageBytes);
        Assert.True(vm.HasInputImage);

        // Clear image
        control.ClearImage();
        Assert.False(control.HasImage);
        Assert.Null(control.DisplayFileName);
        Assert.Null(vm.InputImagePath);
        Assert.Null(vm.InputImageBytes);
        Assert.False(vm.HasInputImage);
    }

    [AvaloniaFact]
    public void StickerStudioControl_InstantiatesAndVerifiesConverters()
    {
        var vm = new StickerStudioViewModel(new StickerGenerationService());
        var control = new StickerStudioControl { DataContext = vm };

        Assert.NotNull(control);
        Assert.Equal(6, vm.StylePresets.Count);

        // Verify stage icon converter
        var iconConv = StageIconConverter.Instance;
        Assert.Equal("●", iconConv.Convert(StickerPipelineStage.GeneratingDiffusion, typeof(string), "1", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("○", iconConv.Convert(StickerPipelineStage.GeneratingDiffusion, typeof(string), "2", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("✓", iconConv.Convert(StickerPipelineStage.Ready, typeof(string), "1", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("✓", iconConv.Convert(StickerPipelineStage.Ready, typeof(string), "4", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("✕", iconConv.Convert(StickerPipelineStage.Failed, typeof(string), "5", System.Globalization.CultureInfo.InvariantCulture));

        // Verify stage status brush converter
        var brushConv = StageStatusBrushConverter.Instance;
        var activeBrush = brushConv.Convert(StickerPipelineStage.IsolatingSubject, typeof(Avalonia.Media.IBrush), "2", System.Globalization.CultureInfo.InvariantCulture) as Avalonia.Media.SolidColorBrush;
        Assert.NotNull(activeBrush);
        Assert.Equal(Avalonia.Media.Color.Parse("#388bfd"), activeBrush.Color);

        // Verify preset match converter
        var matchConv = PresetMatchConverter.Instance;
        Assert.True((bool)matchConv.Convert(new object?[] { "die-cut-vinyl", "die-cut-vinyl" }, typeof(bool), null, System.Globalization.CultureInfo.InvariantCulture)!);
        Assert.False((bool)matchConv.Convert(new object?[] { "die-cut-vinyl", "holographic" }, typeof(bool), null, System.Globalization.CultureInfo.InvariantCulture)!);
    }
}
