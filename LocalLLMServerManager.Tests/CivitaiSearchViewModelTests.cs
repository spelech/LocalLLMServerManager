using System;
using System.Collections.Generic;
using System.Net;
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

public class CivitaiSearchViewModelTests
{
    [Fact]
    public void InitialState_DefaultValues_ConfiguredCorrectly()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(mockCivitai.Object);

        Assert.Equal("", vm.CivitaiSearchQuery);
        Assert.Equal("Checkpoint", vm.SelectedCivitaiType);
        Assert.True(vm.IsFullVramActive);
        Assert.True(vm.IsPartialOffloadActive);
        Assert.True(vm.IsCpuOnlyActive);
        Assert.True(vm.IsOomActive);
        Assert.Equal(16384.0, vm.TotalVramMb);
        Assert.Equal(32768.0, vm.TotalRamMb);
    }

    [Fact]
    public void ToggleFitVerdict_TogglesFilterFlagsAndAppliesFilter()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(mockCivitai.Object);

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
    public void NavigateToCanIRunIt_And_InspectModel_InvokeCallbacks()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(mockCivitai.Object);

        string? inspectedModel = null;
        string? inspectedModality = null;
        vm.OnInspectModelRequested = (m, mod) =>
        {
            inspectedModel = m;
            inspectedModality = mod;
        };

        vm.NavigateToCanIRunIt("Flux-Dev-Checkpoint");
        Assert.Equal("Flux-Dev-Checkpoint", inspectedModel);
        Assert.Equal("Image", inspectedModality);

        var item = new CivitaiModelItem(42, "SDXL Turbo", "Checkpoint", "http://img.jpg", "http://dl.safetensors", "sdxl.safetensors", 4.8, 1200);
        vm.InspectModel(item);
        Assert.Equal("SDXL Turbo", inspectedModel);
    }

    [Fact]
    public void UpdateHardwareTelemetry_UpdatesVramAndRecalculatesBadges()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(mockCivitai.Object);

        vm.CivitaiResults.Add(new CivitaiModelItem(1, "Flux Dev", "Checkpoint", "http://img", "http://dl", "flux.safetensors", 5.0, 500));
        vm.UpdateHardwareTelemetry(8192.0, 16384.0);

        Assert.Equal(8192.0, vm.TotalVramMb);
        Assert.Equal(16384.0, vm.TotalRamMb);
        Assert.NotNull(vm.CivitaiResults[0].FitBadge);
    }

    [Fact]
    public async Task SearchCivitaiAsync_PopulatesResultsAndAppliesFilter()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        mockCivitai.Setup(s => s.SearchModelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpClient>()))
            .ReturnsAsync(new List<CivitaiModelItem>
            {
                new CivitaiModelItem(10, "SD 1.5 Photoreal", "Checkpoint", "http://img.png", "http://dl.gguf", "sd15.safetensors", 4.5, 300)
            });

        var vm = new CivitaiSearchViewModel(mockCivitai.Object);
        using var client = new HttpClient();
        await vm.SearchCivitaiAsync("http://localhost:5246", client);

        Assert.Single(vm.CivitaiResults);
        Assert.Single(vm.FilteredCivitaiResults);
        Assert.Equal("SD 1.5 Photoreal", vm.CivitaiResults[0].Name);
    }

    [Fact]
    public async Task DownloadCivitaiModelAsync_SendsDownloadRequest()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(mockCivitai.Object);

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("/api/civitai/download")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var item = new CivitaiModelItem(10, "SD 1.5 Photoreal", "Checkpoint", "http://img.png", "http://dl.gguf", "sd15.safetensors", 4.5, 300);

        await vm.DownloadCivitaiModelAsync(item, "http://localhost:5246", client);
    }

    [Fact]
    public void StarterModels_ArePopulatedOnInitialization_WithQuickFitBadges()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(mockCivitai.Object);

        Assert.NotEmpty(vm.StarterModels);
        Assert.True(vm.StarterModels.Count >= 5);
        Assert.All(vm.StarterModels, s => Assert.NotNull(s.FitBadge));
    }

    [Fact]
    public void ApplyStarterChip_SetsQueryAndSelectedType()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(mockCivitai.Object);

        vm.ApplyStarterChip("🎭 Detail LoRA");
        Assert.Equal("LORA", vm.SelectedCivitaiType);
        Assert.Equal("Detail", vm.CivitaiSearchQuery);

        vm.ApplyStarterChip("🌟 SDXL");
        Assert.Equal("Checkpoint", vm.SelectedCivitaiType);
        Assert.Equal("SDXL", vm.CivitaiSearchQuery);
    }

    [Fact]
    public async Task SearchCivitaiAsync_TogglesIsLoadingProperly()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        var tcs = new TaskCompletionSource<List<CivitaiModelItem>>();
        mockCivitai.Setup(s => s.SearchModelsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HttpClient>()))
            .Returns(tcs.Task);

        var vm = new CivitaiSearchViewModel(mockCivitai.Object);
        Assert.False(vm.IsLoading);

        using var client = new HttpClient();
        var task = vm.SearchCivitaiAsync("http://localhost:5246", client);
        Assert.True(vm.IsLoading);

        tcs.SetResult(new List<CivitaiModelItem>
        {
            new CivitaiModelItem(1, "Test Checkpoint", "Checkpoint", "http://img", "http://dl", "test.safetensors", 4.5, 100)
        });

        await task;
        Assert.False(vm.IsLoading);
        Assert.Single(vm.CivitaiResults);
    }

    [Fact]
    public void ApplyFilter_FiltersStarterModels_ByVerdictAndType()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(mockCivitai.Object);

        // Initial state: default starter models loaded (DefaultStarterModels has Checkpoints and LORA)
        Assert.NotEmpty(vm.FilteredStarterModels);
        // Default selected type is Checkpoint
        Assert.All(vm.FilteredStarterModels, s => Assert.Equal("Checkpoint", s.Type));

        // When switching to LORA type
        vm.SelectCivitaiType("LORA");
        Assert.NotEmpty(vm.FilteredStarterModels);
        Assert.All(vm.FilteredStarterModels, s => Assert.Equal("LORA", s.Type));

        // When switching to All
        vm.SelectCivitaiType("All");
        Assert.Equal(vm.StarterModels.Count, vm.FilteredStarterModels.Count);

        // When toggling off all verdicts
        vm.IsFullVramActive = false;
        vm.IsPartialOffloadActive = false;
        vm.IsCpuOnlyActive = false;
        vm.IsOomActive = false;
        Assert.Empty(vm.FilteredStarterModels);
    }

    [Fact]
    public void SelectCivitaiType_UpdatesActiveBooleansAndFilter()
    {
        var mockCivitai = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(mockCivitai.Object);

        vm.SelectCivitaiType("LORA");
        Assert.True(vm.IsTypeLoraActive);
        Assert.False(vm.IsTypeCheckpointActive);
        Assert.False(vm.IsTypeAllActive);

        vm.SelectCivitaiType("Checkpoint");
        Assert.False(vm.IsTypeLoraActive);
        Assert.True(vm.IsTypeCheckpointActive);
        Assert.False(vm.IsTypeAllActive);

        vm.SelectCivitaiType("All");
        Assert.False(vm.IsTypeLoraActive);
        Assert.False(vm.IsTypeCheckpointActive);
        Assert.True(vm.IsTypeAllActive);

        vm.SelectCivitaiType(null);
        Assert.True(vm.IsTypeAllActive);
    }

    [Fact]
    public void OpenInBrowser_LaunchesCivitaiUrl()
    {
        var mockCiv = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(mockCiv.Object);
        BrowserLauncher.SuppressProcessStart = true;

        var item = new CivitaiModelItem(1234, "Model Name", "Checkpoint", "", "http://download", "model.safetensors", 4.9, 100);
        vm.OpenInBrowser(item);
        vm.OpenInBrowser(null);
        vm.OpenInBrowser(item with { Id = 0 });
    }
}


