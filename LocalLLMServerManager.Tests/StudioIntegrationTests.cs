using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StudioIntegrationTests
{
    private class MockHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        public List<System.Net.Http.HttpRequestMessage> Requests { get; } = new();
        public bool SimulateFailure { get; set; }

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (SimulateFailure)
            {
                return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    Content = new System.Net.Http.StringContent("{\"error\":\"Simulated GPU OOM\"}")
                });
            }

            return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("{\"status\":\"ok\"}")
            });
        }
    }

    private MainViewModel CreateMainViewModel()
    {
        MainViewModel.EnableAutomaticPolling = false;
        var vm = new MainViewModel();
        return vm;
    }

    [Fact]
    public void VideoPresets_InitializedWithBuiltIns()
    {
        var vm = CreateMainViewModel();
        Assert.NotEmpty(vm.VideoPresets);
        Assert.Contains(vm.VideoPresets, p => p.Name.Contains("480p"));
        Assert.Contains(vm.VideoPresets, p => p.Name.Contains("720p"));
    }

    [Fact]
    public void SelectVideoPreset_UpdatesResolutionFrameCountAndCalculatesFit()
    {
        var vm = CreateMainViewModel();
        var preset720p = vm.VideoPresets.First(p => p.Name.Contains("720p"));

        vm.SelectVideoPresetCommand.Execute(preset720p);

        Assert.Equal("1280x720", vm.VideoResolution);
        Assert.Equal(preset720p.FrameCount, vm.VideoFrameCount);
        Assert.Equal(preset720p.SamplePrompt, vm.VideoPrompt);
        Assert.NotNull(vm.VideoHardwareFit);
        Assert.True(vm.VideoHardwareFit.EstimatedVramMb > 0);
    }

    [Fact]
    public void ImagePresets_InitializedWithBuiltIns_AndSelectionWorks()
    {
        var vm = CreateMainViewModel();
        Assert.NotEmpty(vm.ImagePresets);

        var landscapePreset = vm.ImagePresets.First(p => p.Name.Contains("Landscape"));
        vm.SelectImagePresetCommand.Execute(landscapePreset);

        Assert.Equal("1344x768", vm.ImageResolution);
        Assert.Equal(1344, vm.ImageWidth);
        Assert.Equal(768, vm.ImageHeight);
        Assert.NotNull(vm.ImageHardwareFit);
        Assert.True(vm.ImageHardwareFit.EstimatedVramMb > 0);
    }

    [Fact]
    public void ApplyStarterChip_SetsPromptAndPreset()
    {
        var vm = CreateMainViewModel();
        var preset = vm.VideoPresets.First();

        vm.ApplyStarterChipCommand.Execute(preset);

        Assert.Equal(preset.SamplePrompt, vm.VideoPrompt);
        Assert.Equal(preset, vm.SelectedVideoPreset);
    }

    [Fact]
    public void SaveAndDuplicateAndDeleteVideoPreset_ModifiesCollection()
    {
        var vm = CreateMainViewModel();
        int initialCount = vm.VideoPresets.Count;

        vm.VideoResolution = "1920x1080";
        vm.VideoFrameCount = 64;
        vm.SaveCurrentAsVideoPresetCommand.Execute("Epic Custom Cinematic");

        Assert.Equal(initialCount + 1, vm.VideoPresets.Count);
        var custom = vm.VideoPresets.First(p => p.Name == "Epic Custom Cinematic");
        Assert.Equal(1920, custom.Width);
        Assert.Equal(1080, custom.Height);
        Assert.Equal(64, custom.FrameCount);
        Assert.False(custom.IsBuiltIn);

        // Duplicate
        vm.DuplicateCurrentVideoPresetCommand.Execute(custom);
        Assert.Equal(initialCount + 2, vm.VideoPresets.Count);
        Assert.Contains(vm.VideoPresets, p => p.Name.Contains("Epic Custom Cinematic (Copy)"));

        // Delete
        vm.DeleteCurrentVideoPresetCommand.Execute(custom);
        Assert.Equal(initialCount + 1, vm.VideoPresets.Count);
        Assert.DoesNotContain(vm.VideoPresets, p => p.Name == "Epic Custom Cinematic");
    }

    [Fact]
    public void StageTracking_PropertiesAndCommands_Work()
    {
        var vm = CreateMainViewModel();

        Assert.Equal(0, vm.GenerationStage);
        Assert.False(vm.IsLiveLogsExpanded);

        vm.ToggleLiveLogsCommand.Execute(null);
        Assert.True(vm.IsLiveLogsExpanded);

        vm.ToggleLiveLogsCommand.Execute(null);
        Assert.False(vm.IsLiveLogsExpanded);

        vm.GenerationStage = 2;
        vm.GenerationStageTitle = "Denoising";
        vm.GenerationStageSubtext = "Step 15/30";
        vm.LiveLogOutput = "Sampling latent tensors...";

        Assert.Equal(2, vm.GenerationStage);
        Assert.Equal("Denoising", vm.GenerationStageTitle);
        Assert.Contains("Sampling", vm.LiveLogOutput);

        vm.CancelGenerationCommand.Execute(null);
        Assert.Equal(0, vm.GenerationStage);
        Assert.False(vm.IsGeneratingVideo);
    }

    [Fact]
    public async Task TestFlightModal_ExecutionFlow_Succeeds_AndSendsCorrectPayload()
    {
        var mockHandler = new MockHttpMessageHandler();
        var client = new System.Net.Http.HttpClient(mockHandler);
        var vm = new MainViewModel(client);
        vm.ApiBase = "http://test";

        vm.OpenTestFlightCommand.Execute(null);
        vm.SelectTestFlightModalityCommand.Execute(StudioModality.Video);

        await vm.LaunchTestFlightCommand.ExecuteAsync(null);

        Assert.True(vm.IsTestFlightSuccess);
        Assert.False(vm.TestFlightHasError);
        Assert.Contains("Succeeded", vm.TestFlightResultBannerText);

        Assert.Single(mockHandler.Requests);
        var req = mockHandler.Requests.First();
        Assert.Equal(System.Net.Http.HttpMethod.Post, req.Method);
        Assert.EndsWith("/prompt", req.RequestUri.ToString());
        
        var body = await req.Content.ReadAsStringAsync();
        Assert.Contains("prompt", body);
    }

    [Fact]
    public async Task TestFlightModal_ExecutionFlow_HandlesErrors()
    {
        var mockHandler = new MockHttpMessageHandler { SimulateFailure = true };
        var client = new System.Net.Http.HttpClient(mockHandler);
        var vm = new MainViewModel(client);

        vm.OpenTestFlightCommand.Execute(null);
        await vm.LaunchTestFlightCommand.ExecuteAsync(null);

        Assert.False(vm.IsTestFlightSuccess);
        Assert.True(vm.TestFlightHasError);
        Assert.Contains("500", vm.TestFlightErrorMessage);
        Assert.Contains("Simulated GPU OOM", vm.TestFlightErrorMessage);
    }

    [Fact]
    public void AudioStudioViewModel_PresetsAndHardwareFit_Work()
    {
        var audioVm = new AudioStudioViewModel();
        Assert.NotEmpty(audioVm.AudioPresets);

        var narratorPreset = audioVm.AudioPresets.First(p => p.Name.Contains("Storyteller") || p.VoiceProfile == "af_heart");
        audioVm.SelectAudioPresetCommand.Execute(narratorPreset);

        Assert.Equal(narratorPreset.SamplePrompt, audioVm.Prompt);
        Assert.NotNull(audioVm.AudioHardwareFit);
        Assert.True(audioVm.AudioHardwareFit.EstimatedVramMb > 0);

        audioVm.GenerationStage = 1;
        audioVm.Stage1Status = "Loading Kokoro weights";
        Assert.Equal(1, audioVm.GenerationStage);
        Assert.Equal("Loading Kokoro weights", audioVm.Stage1Status);

        audioVm.CancelGenerationCommand.Execute(null);
        Assert.Equal(0, audioVm.GenerationStage);
        Assert.False(audioVm.IsGenerating);
    }
}
