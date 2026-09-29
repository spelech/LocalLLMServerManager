using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StickerStudioViewModelTests
{
    private class FakeStickerService : IStickerGenerationService
    {
        public bool ShouldSucceed { get; set; } = true;
        public StickerGenerationRequest? LastRequest { get; private set; }

        public Task<StickerResult> GenerateStickerAsync(StickerGenerationRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            if (!ShouldSucceed)
            {
                return Task.FromResult(new StickerResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Engine offline"
                });
            }

            return Task.FromResult(new StickerResult
            {
                IsSuccess = true,
                OutputPngBytes = new byte[] { 1, 2, 3, 4 },
                Width = 1024,
                Height = 1024
            });
        }
    }

    [Fact]
    public void Presets_InitializedWithSixCoreStyles()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        Assert.Equal(6, vm.StylePresets.Count);
        Assert.NotNull(vm.SelectedStylePreset);
        Assert.Equal("die-cut-vinyl", vm.SelectedStylePreset.Id);
    }

    [Fact]
    public void BorderWidth_ClampedBetweenZeroAndTwentyFour()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        vm.BorderWidth = 50;
        Assert.Equal(24, vm.BorderWidth);
        vm.BorderWidth = -5;
        Assert.Equal(0, vm.BorderWidth);
    }

    [Fact]
    public async Task GenerateSticker_TransitionsThroughStagesToReady()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        vm.InputImagePath = "test.png";
        await vm.GenerateStickerCommand.ExecuteAsync(null);
        Assert.Equal(StickerPipelineStage.Ready, vm.CurrentStage);
        Assert.False(vm.IsGenerating);
        Assert.NotNull(vm.GeneratedPngBytes);
    }

    [Fact]
    public async Task GenerateSticker_TransitionsThroughAllStagesInOrder()
    {
        var stages = new List<StickerPipelineStage>();
        var vm = new StickerStudioViewModel(new FakeStickerService());
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(StickerStudioViewModel.CurrentStage))
            {
                stages.Add(vm.CurrentStage);
            }
        };

        vm.InputImagePath = "test.png";
        await vm.GenerateStickerCommand.ExecuteAsync(null);

        Assert.Equal(StickerPipelineStage.Ready, vm.CurrentStage);
        Assert.Contains(StickerPipelineStage.GeneratingDiffusion, stages);
        Assert.Contains(StickerPipelineStage.IsolatingSubject, stages);
        Assert.Contains(StickerPipelineStage.ApplyingContour, stages);
        Assert.Contains(StickerPipelineStage.Ready, stages);

        // Verify order
        int idxDiffusion = stages.IndexOf(StickerPipelineStage.GeneratingDiffusion);
        int idxIsolation = stages.IndexOf(StickerPipelineStage.IsolatingSubject);
        int idxContour = stages.IndexOf(StickerPipelineStage.ApplyingContour);
        int idxReady = stages.IndexOf(StickerPipelineStage.Ready);

        Assert.True(idxDiffusion < idxIsolation);
        Assert.True(idxIsolation < idxContour);
        Assert.True(idxContour < idxReady);
    }

    [Fact]
    public async Task GenerateSticker_WhenServiceFails_TransitionsToFailedAndSetsErrorMessage()
    {
        var fakeService = new FakeStickerService { ShouldSucceed = false };
        var vm = new StickerStudioViewModel(fakeService);
        vm.InputImagePath = "test.png";

        await vm.GenerateStickerCommand.ExecuteAsync(null);

        Assert.Equal(StickerPipelineStage.Failed, vm.CurrentStage);
        Assert.False(vm.IsGenerating);
        Assert.Equal("Engine offline", vm.ErrorMessage);
    }

    [Fact]
    public void SelectStylePresetCommand_UpdatesSelectedPresetAndBorderWidth()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        var holographic = vm.StylePresets.FirstOrDefault(p => p.Id == "holographic");
        Assert.NotNull(holographic);

        vm.SelectStylePresetCommand.Execute(holographic);

        Assert.Same(holographic, vm.SelectedStylePreset);
        Assert.Equal(holographic.DefaultBorderWidth, vm.BorderWidth);
    }

    [Fact]
    public void ClearInputCommand_ResetsInputImagePathAndBytes()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        vm.InputImagePath = "some/path.png";
        vm.InputImageBytes = new byte[] { 10, 20 };
        Assert.True(vm.HasInputImage);

        vm.ClearInputCommand.Execute(null);

        Assert.Null(vm.InputImagePath);
        Assert.Null(vm.InputImageBytes);
        Assert.False(vm.HasInputImage);
    }

    [Fact]
    public void DefaultPresets_ContainsAllSixExpectedStyles()
    {
        var presets = StickerGenerationService.DefaultPresets;
        Assert.Equal(6, presets.Count);

        var ids = presets.Select(p => p.Id).ToList();
        Assert.Contains("die-cut-vinyl", ids);
        Assert.Contains("holographic", ids);
        Assert.Contains("chibi-anime", ids);
        Assert.Contains("retro-80s", ids);
        Assert.Contains("pop-art", ids);
        Assert.Contains("watercolor", ids);

        foreach (var preset in presets)
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(preset.Icon));
            Assert.False(string.IsNullOrWhiteSpace(preset.PositiveTokens));
            Assert.InRange(preset.DefaultBorderWidth, 1, 24);
        }
    }

    [Fact]
    public async Task DefaultService_CanGenerateSimulatedSticker()
    {
        var service = new StickerGenerationService();
        var request = new StickerGenerationRequest
        {
            StylePresetId = "die-cut-vinyl",
            CustomPrompt = "cute cosmic astronaut cat",
            BorderWidth = 12,
            IsAutoCutoutEnabled = true
        };

        var result = await service.GenerateStickerAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputPngBytes);
        Assert.True(result.OutputPngBytes.Length > 0);
        Assert.Equal(1024, result.Width);
        Assert.Equal(1024, result.Height);
    }
}
