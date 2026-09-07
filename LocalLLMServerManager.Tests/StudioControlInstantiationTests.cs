using System.Collections.Generic;
using Avalonia.Headless.XUnit;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Views.Controls;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StudioControlInstantiationTests
{
    [Fact]
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

    [Fact]
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

    [Fact]
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
}
