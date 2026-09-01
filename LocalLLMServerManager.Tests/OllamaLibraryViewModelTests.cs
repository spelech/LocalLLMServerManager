using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class OllamaLibraryViewModelTests
{
    [Fact]
    public void InitialState_DefaultValues_ConfiguredCorrectly()
    {
        var mockOllama = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(mockOllama.Object);

        Assert.True(vm.IsFullVramActive);
        Assert.True(vm.IsPartialOffloadActive);
        Assert.True(vm.IsCpuOnlyActive);
        Assert.True(vm.IsOomActive);
        Assert.False(vm.IsDeleteModalOpen);
        Assert.False(vm.IsPullDrawerOpen);
        Assert.Equal(8192, vm.TargetContextTokens);
        Assert.Equal("~0.5 GB", vm.EstimatedKvCacheText);
    }

    [Fact]
    public void TargetContextTokens_ChangingSlider_UpdatesEstimatedKvCacheText()
    {
        var mockOllama = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(mockOllama.Object);

        vm.TargetContextTokens = 131072;
        Assert.Contains("GB", vm.EstimatedKvCacheText);

        vm.TargetContextTokens = 2048;
        Assert.Contains("MB", vm.EstimatedKvCacheText);
    }

    [Fact]
    public void ToggleFitVerdict_TogglesFlagsAndFiltersModels()
    {
        var mockOllama = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(mockOllama.Object);

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
        var mockOllama = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(mockOllama.Object);

        string? inspected = null;
        string? modality = null;
        vm.OnInspectModelRequested = (m, mod) =>
        {
            inspected = m;
            modality = mod;
        };

        vm.NavigateToCanIRunIt("llama3:8b");
        Assert.Equal("llama3:8b", inspected);
        Assert.Equal("LLM", modality);

        var modelItem = new OllamaModelItem("mistral:7b", "4.1 GB", "General", "#38BDF8", false);
        vm.InspectModel(modelItem);
        Assert.Equal("mistral:7b", inspected);
    }

    [Fact]
    public async Task LoadInstalledModelsAsync_PopulatesCollectionWithBadges()
    {
        var mockOllama = new Mock<IOllamaModelService>();
        mockOllama.Setup(m => m.LoadInstalledModelsAsync(It.IsAny<string>(), It.IsAny<HttpClient>()))
            .ReturnsAsync(new List<OllamaModelItem>
            {
                new OllamaModelItem("llama3.1:8b", "4.7 GB", "General", "#38BDF8", false, null, 4700000000L)
            });

        var vm = new OllamaLibraryViewModel(mockOllama.Object);
        using var client = new HttpClient();
        await vm.LoadInstalledModelsAsync("http://localhost:5246", client);

        Assert.Single(vm.InstalledModels);
        Assert.Single(vm.FilteredInstalledModels);
        Assert.NotNull(vm.InstalledModels[0].FitBadge);
    }

    [Fact]
    public void UpdateHardwareTelemetry_UpdatesVramAndRecalculatesBadges()
    {
        var mockOllama = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(mockOllama.Object);

        vm.InstalledModels.Add(new OllamaModelItem("llama3.1:8b", "4.7 GB", "General", "#38BDF8", false));
        vm.UpdateHardwareTelemetry(8192.0, 16384.0);

        Assert.Equal(8192.0, vm.TotalVramMb);
        Assert.Equal(16384.0, vm.TotalRamMb);
        Assert.NotNull(vm.InstalledModels[0].FitBadge);
    }

    [Fact]
    public async Task PullModelAsync_StreamsProgressAndClosesDrawer()
    {
        var mockOllama = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(mockOllama.Object);

        var streamLines = "{\"status\":\"downloading\",\"completed\":500000000,\"total\":1000000000}\n{\"status\":\"verifying sha256 digest\"}\n{\"status\":\"success\"}\n";
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(streamLines, Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handlerMock.Object);
        await vm.PullModelAsync("deepseek-r1:8b", client);

        Assert.True(vm.IsPullDrawerOpen);
        Assert.Equal(50.0, vm.PullProgressPercent);
        Assert.Contains("success", vm.PullStatusLog);

        vm.ClosePullDrawer();
        Assert.False(vm.IsPullDrawerOpen);
    }
}
