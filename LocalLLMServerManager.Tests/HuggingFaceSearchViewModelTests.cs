using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
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
}
