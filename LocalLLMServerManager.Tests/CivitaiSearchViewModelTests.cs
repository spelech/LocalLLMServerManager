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
}
