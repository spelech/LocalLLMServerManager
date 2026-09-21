using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class HuggingFaceSearchViewModelTests
{
    [Fact]
    public void InitialState_DefaultValues_ConfiguredCorrectly()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object);

        Assert.Equal("", vm.HfSearchQuery);
        Assert.Null(vm.SelectedPipelineTag);
        Assert.True(vm.IsFullVramActive);
        Assert.True(vm.IsPartialOffloadActive);
        Assert.True(vm.IsCpuOnlyActive);
        Assert.True(vm.IsOomActive);
        Assert.True(vm.IsInputTextActive);
        Assert.False(vm.IsInputImageActive);
        Assert.True(vm.IsOutputTextActive);
        Assert.False(vm.IsHfModalOpen);
        Assert.Equal(16384.0, vm.TotalVramMb);
        Assert.Equal(32768.0, vm.TotalRamMb);
    }

    [Fact]
    public void ToggleInputModality_UpdatesFlagsAndList()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object);

        vm.ToggleInputModality("Text");
        Assert.False(vm.IsInputTextActive);
        Assert.DoesNotContain("Text", vm.SelectedInputModalities);

        vm.ToggleInputModality("Image");
        Assert.True(vm.IsInputImageActive);
        Assert.Contains("Image", vm.SelectedInputModalities);

        vm.ToggleInputModality("Audio");
        Assert.True(vm.IsInputAudioActive);
        Assert.Contains("Audio", vm.SelectedInputModalities);

        vm.ToggleInputModality("Video");
        Assert.True(vm.IsInputVideoActive);
        Assert.Contains("Video", vm.SelectedInputModalities);
    }

    [Fact]
    public void ToggleOutputModality_UpdatesFlagsAndList()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object);

        vm.ToggleOutputModality("Text");
        Assert.False(vm.IsOutputTextActive);
        Assert.DoesNotContain("Text", vm.SelectedOutputModalities);

        vm.ToggleOutputModality("Image");
        Assert.True(vm.IsOutputImageActive);
        Assert.Contains("Image", vm.SelectedOutputModalities);

        vm.ToggleOutputModality("Audio");
        Assert.True(vm.IsOutputAudioActive);
        Assert.Contains("Audio", vm.SelectedOutputModalities);

        vm.ToggleOutputModality("Video");
        Assert.True(vm.IsOutputVideoActive);
        Assert.Contains("Video", vm.SelectedOutputModalities);

        vm.ToggleOutputModality("3D");
        Assert.True(vm.IsOutputThreeDActive);
        Assert.Contains("3D", vm.SelectedOutputModalities);
    }

    [Theory]
    [InlineData("llm", true, false, false, false, true, false, false, false, false)]
    [InlineData("multimodal", true, true, false, false, true, false, false, false, false)]
    [InlineData("image", true, false, false, false, false, true, false, false, false)]
    [InlineData("video", true, true, false, false, false, false, false, true, false)]
    [InlineData("audio", true, false, true, false, true, false, true, false, false)]
    [InlineData("3d", true, true, false, false, false, false, false, false, true)]
    public void ApplyPreset_ConfiguresModalitiesProperly(
        string preset,
        bool expInTxt, bool expInImg, bool expInAud, bool expInVid,
        bool expOutTxt, bool expOutImg, bool expOutAud, bool expOutVid, bool expOut3D)
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object);

        vm.ApplyPreset(preset);

        Assert.Equal(expInTxt, vm.IsInputTextActive);
        Assert.Equal(expInImg, vm.IsInputImageActive);
        Assert.Equal(expInAud, vm.IsInputAudioActive);
        Assert.Equal(expInVid, vm.IsInputVideoActive);

        Assert.Equal(expOutTxt, vm.IsOutputTextActive);
        Assert.Equal(expOutImg, vm.IsOutputImageActive);
        Assert.Equal(expOutAud, vm.IsOutputAudioActive);
        Assert.Equal(expOutVid, vm.IsOutputVideoActive);
        Assert.Equal(expOut3D, vm.IsOutputThreeDActive);
    }

    [Theory]
    [InlineData("Wan-2.1-T2V", "text-to-video", "Video")]
    [InlineData("whisper-large-v3", "automatic-speech-recognition", "Audio")]
    [InlineData("TRELLIS-image-large", "text-to-3d", "ThreeD")]
    [InlineData("FLUX.1-schnell", "text-to-image", "Image")]
    [InlineData("Meta-Llama-3.1-8B", "text-generation", "LLM")]
    public void DetermineModality_ClassifiesCorrectly(string name, string tag, string expected)
    {
        var result = HuggingFaceSearchViewModel.DetermineModality(name, tag);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToggleFitVerdict_TogglesEachFilter()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object);

        vm.ToggleFitVerdict("full");
        Assert.False(vm.IsFullVramActive);

        vm.ToggleFitVerdict("partial");
        Assert.False(vm.IsPartialOffloadActive);

        vm.ToggleFitVerdict("cpu");
        Assert.False(vm.IsCpuOnlyActive);

        vm.ToggleFitVerdict("oom");
        Assert.False(vm.IsOomActive);
    }

    [Fact]
    public void UpdateHardwareTelemetry_UpdatesVramRamAndRecalculatesBadges()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object);

        vm.HuggingFaceResults.Add(new HuggingFaceRepoItem("meta-llama/Llama-3.1-8B", "meta", 50, "10k", "text-generation", null));
        vm.UpdateHardwareTelemetry(8192.0, 16384.0);

        Assert.Equal(8192.0, vm.TotalVramMb);
        Assert.Equal(16384.0, vm.TotalRamMb);
        Assert.NotNull(vm.HuggingFaceResults[0].FitBadge);
    }

    [Fact]
    public void NavigateToCanIRunIt_And_InspectModel_InvokeCallback()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object);

        string? inspectedModel = null;
        string? inspectedModality = null;

        vm.OnInspectModelRequested = (m, mod) =>
        {
            inspectedModel = m;
            inspectedModality = mod;
        };

        vm.NavigateToCanIRunIt("black-forest-labs/FLUX.1-dev");
        Assert.Equal("black-forest-labs/FLUX.1-dev", inspectedModel);
        Assert.Equal("Image", inspectedModality);

        var repoItem = new HuggingFaceRepoItem("Wan-AI/Wan2.1-T2V", "Wan", 100, "5k", "text-to-video", null);
        vm.InspectModel(repoItem);
        Assert.Equal("Wan-AI/Wan2.1-T2V", inspectedModel);
        Assert.Equal("Video", inspectedModality);

        var fileItem = new HfFileQuantItem("llama-3.1-8b-q4.gguf", "Q4_K_M", "4.0 GB", 4000000000L, null);
        vm.InspectQuantFile(fileItem);
        Assert.Equal("llama-3.1-8b-q4.gguf", inspectedModel);
        Assert.Equal("LLM", inspectedModality);
    }

    [Fact]
    public async Task SearchHuggingFaceAsync_PopulatesResultsAndAppliesFilter()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        mockHf.Setup(s => s.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<HttpClient>()))
            .ReturnsAsync(new List<HuggingFaceRepoItem>
            {
                new HuggingFaceRepoItem("test-repo/model1", "test-repo", 10, "1k", "text-generation", null)
            });

        var vm = new HuggingFaceSearchViewModel(mockHf.Object);
        vm.SelectedPipelineTag = "text-generation";

        using var client = new HttpClient();
        await vm.SearchHuggingFaceAsync("http://localhost", client);

        Assert.Single(vm.HuggingFaceResults);
        Assert.Single(vm.FilteredHuggingFaceResults);
        Assert.Equal("test-repo/model1", vm.HuggingFaceResults[0].Id);
    }

    [Fact]
    public async Task OpenHfModalAsync_And_CloseHfModal_ControlsModalState()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        mockHf.Setup(s => s.FetchQuantizationsAsync(It.IsAny<string>(), "TheBloke/Llama-2-7B-GGUF", It.IsAny<HttpClient>()))
            .ReturnsAsync(new List<HfFileQuantItem>
            {
                new HfFileQuantItem("llama-2-7b.Q4_K_M.gguf", "Q4_K_M", "4.0 GB", 4000000000L, null)
            });

        var vm = new HuggingFaceSearchViewModel(mockHf.Object);

        using var client = new HttpClient();
        await vm.OpenHfModalAsync("TheBloke/Llama-2-7B-GGUF", "http://localhost", client);

        Assert.True(vm.IsHfModalOpen);
        Assert.Equal("TheBloke/Llama-2-7B-GGUF", vm.ModalRepoId);
        Assert.Equal("TheBloke", vm.ModalAuthor);
        Assert.Single(vm.ModalHfFiles);
        Assert.NotNull(vm.ModalHfFiles[0].FitBadge);

        vm.CloseHfModal();
        Assert.False(vm.IsHfModalOpen);
    }

    [Fact]
    public async Task SearchHuggingFaceAsync_TogglesIsLoadingProperly()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var tcs = new TaskCompletionSource<List<HuggingFaceRepoItem>>();
        mockHf.Setup(s => s.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<HttpClient>()))
            .Returns(tcs.Task);

        var vm = new HuggingFaceSearchViewModel(mockHf.Object);
        vm.SelectedPipelineTag = "text-generation";
        Assert.False(vm.IsLoading);

        using var client = new HttpClient();
        var task = vm.SearchHuggingFaceAsync("http://localhost", client);
        Assert.True(vm.IsLoading);

        tcs.SetResult(new List<HuggingFaceRepoItem>
        {
            new HuggingFaceRepoItem("test-repo/model1", "test-repo", 10, "1k", "text-generation", null)
        });

        await task;
        Assert.False(vm.IsLoading);
        Assert.Single(vm.FilteredHuggingFaceResults);
    }

    [Fact]
    public async Task OpenHfModalAsync_TogglesIsModalLoadingProperly()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var tcs = new TaskCompletionSource<List<HfFileQuantItem>>();
        mockHf.Setup(s => s.FetchQuantizationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpClient>()))
            .Returns(tcs.Task);

        var vm = new HuggingFaceSearchViewModel(mockHf.Object);
        Assert.False(vm.IsModalLoading);

        using var client = new HttpClient();
        var task = vm.OpenHfModalAsync("test/repo", "http://localhost", client);
        Assert.True(vm.IsHfModalOpen);
        Assert.True(vm.IsModalLoading);

        tcs.SetResult(new List<HfFileQuantItem>
        {
            new HfFileQuantItem("test.Q4.gguf", "Q4", "2 GB", 2000000L, null)
        });

        await task;
        Assert.False(vm.IsModalLoading);
        Assert.Single(vm.ModalHfFiles);
    }

    [Fact]
    public void Constructor_LoadsCuratedStarterModels_ByDefault()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object);

        Assert.NotEmpty(vm.HuggingFaceResults);
        Assert.NotEmpty(vm.FilteredHuggingFaceResults);
        Assert.True(vm.HuggingFaceResults.Count >= 5);
        foreach (var item in vm.HuggingFaceResults)
        {
            Assert.NotNull(item.FitBadge);
        }
    }

    [Fact]
    public void ApplyPreset_SetsActivePreset_AndTogglesClearSelectedPipelineTag()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object);

        vm.SelectedPipelineTag = "custom-tag";
        vm.ApplyPreset("LLM");

        Assert.Null(vm.SelectedPipelineTag);
        Assert.Equal("LLM", vm.ActivePreset);
        Assert.True(vm.IsPresetLlm);
        Assert.False(vm.IsPresetMultimodal);

        vm.ApplyPreset("Multimodal");
        Assert.Equal("Multimodal", vm.ActivePreset);
        Assert.True(vm.IsPresetMultimodal);
        Assert.False(vm.IsPresetLlm);

        vm.ApplyPreset("Image");
        Assert.Equal("Image", vm.ActivePreset);
        Assert.True(vm.IsPresetImage);

        vm.ApplyPreset("Video");
        Assert.Equal("Video", vm.ActivePreset);
        Assert.True(vm.IsPresetVideo);

        vm.ApplyPreset("Audio");
        Assert.Equal("Audio", vm.ActivePreset);
        Assert.True(vm.IsPresetAudio);

        vm.ApplyPreset("3D");
        Assert.Equal("3D", vm.ActivePreset);
        Assert.True(vm.IsPreset3D);

        // Manually toggling an input or output clears ActivePreset
        vm.ToggleInputModality("Image");
        Assert.Null(vm.ActivePreset);
        Assert.False(vm.IsPreset3D);
    }

    [Fact]
    public void ResolvePipelineTags_ForAudio_DoesNotIncludeTextGeneration()
    {
        var tags = HuggingFaceSearchViewModel.ResolvePipelineTags(
            new[] { "Text", "Audio" },
            new[] { "Text", "Audio" });

        Assert.DoesNotContain("text-generation", tags);
        Assert.Contains("text-to-speech", tags);
        Assert.Contains("text-to-audio", tags);
        Assert.Contains("automatic-speech-recognition", tags);
        Assert.Contains("audio-to-audio", tags);
    }

    [Theory]
    [InlineData("Qwen/Qwen2-VL-7B-Instruct", "image-text-to-text", "LLM")]
    [InlineData("meta-llama/Llama-3.2-11B-Vision-Instruct", "image-to-text", "LLM")]
    [InlineData("google/docvqa-donut", "visual-question-answering", "LLM")]
    public void DetermineModality_VlmModels_ClassifiedAsLlmNotImage(string name, string tag, string expected)
    {
        var result = HuggingFaceSearchViewModel.DetermineModality(name, tag);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task LoadDefaultModelsAsync_LoadsFromService_OrRetainsCuratedStarterModels()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        mockHf.Setup(s => s.SearchRepositoriesAsync("http://localhost", "", "text-generation", It.IsAny<HttpClient>()))
            .ReturnsAsync(new List<HuggingFaceRepoItem>
            {
                new HuggingFaceRepoItem("custom/top-model", "custom", 1000, "50k", "text-generation", null)
            });

        var vm = new HuggingFaceSearchViewModel(mockHf.Object);
        using var client = new HttpClient();
        await vm.LoadDefaultModelsAsync("http://localhost", client);

        Assert.Single(vm.HuggingFaceResults);
        Assert.Equal("custom/top-model", vm.HuggingFaceResults[0].Id);
    }

    [Fact]
    public void OpenInBrowser_LaunchesHuggingFaceUrl()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object);
        BrowserLauncher.SuppressProcessStart = true;

        vm.OpenInBrowser("meta-llama/Llama-3.3-8B-Instruct-GGUF");
        vm.OpenInBrowser(null);
        vm.OpenInBrowser("   ");
    }

    [Fact]
    public async Task OpenHfModalCommand_OpensModalForRepoItem()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        mockHf.Setup(m => m.FetchQuantizationsAsync(It.IsAny<string>(), "test/repo", It.IsAny<HttpClient>()))
            .ReturnsAsync(new List<HfFileQuantItem> { new("model.gguf", "Q4_K_M", "4 GB", 4000000000L) });

        var vm = new HuggingFaceSearchViewModel(mockHf.Object);
        var item = new HuggingFaceRepoItem("test/repo", "test", 100, "1k downloads", "text-generation");
        await vm.OpenHfModalCommand.ExecuteAsync(item);

        Assert.True(vm.IsHfModalOpen);
        Assert.Equal("test/repo", vm.ModalRepoId);
        Assert.Single(vm.ModalHfFiles);
    }

    [Fact]
    public async Task DownloadHfFileAsync_SendsDownloadRequest()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object)
        {
            ModalRepoId = "meta-llama/Llama-3.3-8B-Instruct-GGUF"
        };

        var file = new HfFileQuantItem("llama-3.3.Q4_K_M.gguf", "Q4_K_M", "4.5 GB", 4500000000L);
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

        var client = new HttpClient(handlerMock.Object);
        await vm.DownloadHfFileAsync(file, "http://localhost:5246", client);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("/api/hf/download") && r.RequestUri.ToString().Contains("llama-3.3.Q4_K_M.gguf")),
            ItExpr.IsAny<System.Threading.CancellationToken>());
    }

    [Fact]
    public void PullHfGgufInOllama_TriggersCallbackWithFormattedPullString()
    {
        var mockHf = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(mockHf.Object)
        {
            ModalRepoId = "meta-llama/Llama-3.3-8B-Instruct-GGUF"
        };

        string? pulledModel = null;
        vm.OnPullModelRequested = m => pulledModel = m;

        var file = new HfFileQuantItem("llama-3.3.Q4_K_M.gguf", "Q4_K_M", "4.5 GB", 4500000000L);
        vm.PullHfGgufInOllama(file);

        Assert.Equal("hf.co/meta-llama/Llama-3.3-8B-Instruct-GGUF:q4_k_m", pulledModel);
    }
}


