using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views.Controls;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class CivitaiProviderTests
{
    private const string SampleCivitaiSearchResponse = """
    {
      "items": [
        {
          "id": 133005,
          "name": "Juggernaut XL",
          "type": "Checkpoint",
          "modelVersions": [
            {
              "id": 456789,
              "name": "v9 + Rundiffusion",
              "images": [
                { "url": "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/juggernaut.jpeg" }
              ],
              "files": [
                {
                  "name": "juggernautXL_v9Rundiffusion.safetensors",
                  "downloadUrl": "https://civitai.com/api/download/models/456789",
                  "sizeKB": 6787109.375
                }
              ]
            }
          ]
        },
        {
          "id": 139562,
          "name": "Detail Tweaker LoRA",
          "type": "LORA",
          "modelVersions": [
            {
              "id": 123456,
              "name": "v1.0",
              "images": [
                { "url": "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/detail.jpeg" }
              ],
              "files": [
                {
                  "name": "add_detail.safetensors",
                  "downloadUrl": "https://civitai.com/api/download/models/123456",
                  "sizeKB": 140625.0
                }
              ]
            }
          ]
        },
        {
          "id": 999999,
          "name": "Minimal Model Empty Files",
          "type": "Checkpoint",
          "modelVersions": []
        }
      ]
    }
    """;

    private static HttpClient CreateMockHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) => handler(req));

        return new HttpClient(mockHandler.Object);
    }

    [Fact]
    public async Task SearchModelsAsync_ValidV1Payload_ParsesModelVersionsFilesAndImages()
    {
        using var client = CreateMockHttpClient(req =>
        {
            var uri = req.RequestUri?.ToString() ?? "";
            Assert.Contains("/api/civitai/search", uri);
            Assert.Contains("types=Checkpoint", uri);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleCivitaiSearchResponse, Encoding.UTF8, "application/json")
            };
        });

        var service = new CivitaiSearchService();
        var results = await service.SearchModelsAsync("http://127.0.0.1:5246", "juggernaut", "Checkpoint", "Most Downloaded", client);

        Assert.Equal(3, results.Count);

        var juggernaut = results[0];
        Assert.Equal(133005, juggernaut.Id);
        Assert.Equal("Juggernaut XL", juggernaut.Name);
        Assert.Equal("Checkpoint", juggernaut.Type);
        Assert.Equal("https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/juggernaut.jpeg", juggernaut.ThumbnailUrl);
        Assert.Equal("https://civitai.com/api/download/models/456789", juggernaut.DownloadUrl);
        Assert.Equal("juggernautXL_v9Rundiffusion.safetensors", juggernaut.FileName);
        Assert.True(juggernaut.SizeBytes > 6_000_000_000L);

        var lora = results[1];
        Assert.Equal(139562, lora.Id);
        Assert.Equal("Detail Tweaker LoRA", lora.Name);
        Assert.Equal("LORA", lora.Type);
        Assert.True(lora.SizeBytes > 100_000_000L);

        var minimal = results[2];
        Assert.Equal(999999, minimal.Id);
        Assert.Equal("Minimal Model Empty Files", minimal.Name);
        Assert.Equal(0, minimal.SizeBytes);
    }

    [Fact]
    public async Task SearchModelsAsync_HttpError_ReturnsEmptyListWithoutCrashing()
    {
        using var client = CreateMockHttpClient(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        var service = new CivitaiSearchService();
        var results = await service.SearchModelsAsync("http://127.0.0.1:5246", "bad-query", "Checkpoint", "Most Downloaded", client);

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task ViewModel_SearchCivitaiAsync_PopulatesResultsAndComputesFitBadges()
    {
        var serviceMock = new Mock<ICivitaiSearchService>();
        serviceMock.Setup(s => s.SearchModelsAsync(It.IsAny<string>(), "flux", "Checkpoint", It.IsAny<string>(), It.IsAny<HttpClient>()))
            .ReturnsAsync(new List<CivitaiModelItem>
            {
                new(618692, "Flux.1 Dev - FP8", "Checkpoint", "http://image.png", "http://download.url", "flux1-dev-fp8.safetensors", 4.9, 420000, null, 12000000000L)
            });

        var vm = new CivitaiSearchViewModel(serviceMock.Object);
        vm.UpdateHardwareTelemetry(totalVramMb: 16384, totalRamMb: 32768);

        vm.CivitaiSearchQuery = "flux";
        vm.SelectedCivitaiType = "Checkpoint";
        await vm.SearchCivitaiCommand.ExecuteAsync(null);

        Assert.Single(vm.CivitaiResults);
        Assert.Equal("Flux.1 Dev - FP8", vm.CivitaiResults[0].Name);
        Assert.NotNull(vm.CivitaiResults[0].FitBadge);
        Assert.Single(vm.FilteredCivitaiResults);
    }

    [Fact]
    public void ViewModel_FilterByHardwareCompatibility_FiltersResultsCorrectly()
    {
        var serviceMock = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(serviceMock.Object);

        var fullVram = new CivitaiModelItem(1, "Small LoRA", "LORA", "", "", "lora.safetensors", 4.8, 1000,
            new QuickFitBadge("Full VRAM", "#10B981", "Fits", FitVerdict.FullVram), 200_000_000L);

        var oomModel = new CivitaiModelItem(2, "Giant Checkpoint", "Checkpoint", "", "", "giant.safetensors", 4.5, 1000,
            new QuickFitBadge("Out of Memory", "#EF4444", "OOM", FitVerdict.OutOfMemory), 40_000_000_000L);

        vm.CivitaiResults.Add(fullVram);
        vm.CivitaiResults.Add(oomModel);
        vm.ApplyFilter();

        Assert.Equal(2, vm.FilteredCivitaiResults.Count);

        // Turn off OOM models
        vm.IsOomActive = false;
        Assert.Single(vm.FilteredCivitaiResults);
        Assert.Equal("Small LoRA", vm.FilteredCivitaiResults[0].Name);

        // Turn off Full VRAM
        vm.IsFullVramActive = false;
        Assert.Empty(vm.FilteredCivitaiResults);
    }

    [Fact]
    public void ViewModel_SelectCivitaiType_TogglesFilterAndStarterModels()
    {
        var serviceMock = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(serviceMock.Object);

        // Filter starter models to LoRA only
        vm.SelectCivitaiType("LORA");
        Assert.Equal("LORA", vm.SelectedCivitaiType);
        Assert.True(vm.IsTypeLoraActive);
        Assert.False(vm.IsTypeCheckpointActive);

        Assert.All(vm.FilteredStarterModels, s => Assert.Equal("LORA", s.Type));

        // Filter starter models to Checkpoint only
        vm.SelectCivitaiType("Checkpoint");
        Assert.True(vm.IsTypeCheckpointActive);
        Assert.All(vm.FilteredStarterModels, s => Assert.Equal("Checkpoint", s.Type));
    }

    [Fact]
    public void ViewModel_ApplyStarterChip_SetsQueryAndModality()
    {
        var serviceMock = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(serviceMock.Object);

        vm.ApplyStarterChip("⚡ Detail Tweaker LoRA");
        Assert.Equal("LORA", vm.SelectedCivitaiType);
        Assert.Contains("Detail Tweaker", vm.CivitaiSearchQuery);

        vm.ApplyStarterChip("📸 Realistic Photo");
        Assert.Equal("Checkpoint", vm.SelectedCivitaiType);
        Assert.Equal("Realistic Photo", vm.CivitaiSearchQuery);
    }

    [Fact]
    public void ViewModel_InspectModel_InvokesInspectionCallback()
    {
        var serviceMock = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(serviceMock.Object);

        string? inspectedModel = null;
        string? inspectedModality = null;
        vm.OnInspectModelRequested = (m, p) =>
        {
            inspectedModel = m;
            inspectedModality = p;
        };

        var item = new CivitaiModelItem(133005, "Juggernaut XL", "Checkpoint", "", "", "juggernaut.safetensors", 4.9, 1000, null, 6800000000L);
        vm.InspectModelCommand.Execute(item);

        Assert.Equal("Juggernaut XL", inspectedModel);
        Assert.Equal("Image", inspectedModality);
    }

    [AvaloniaFact]
    public void CivitaiTabControl_HeadlessUI_RendersAndInteractsCorrectly()
    {
        var serviceMock = new Mock<ICivitaiSearchService>();
        var vm = new CivitaiSearchViewModel(serviceMock.Object);

        var control = new CivitaiTabControl { DataContext = vm };
        var window = new Window { Content = control, Width = 1280, Height = 900 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Verify Search TextBox exists and binds
        var searchBox = control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        Assert.NotNull(searchBox);

        searchBox.Text = "cyberpunk";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("cyberpunk", vm.CivitaiSearchQuery);

        // Verify Type Filter Buttons
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var loraFilterBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "LORA");
        Assert.NotNull(loraFilterBtn);

        loraFilterBtn.Command?.Execute(loraFilterBtn.CommandParameter);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(vm.IsTypeLoraActive);

        window.Close();
    }
}
