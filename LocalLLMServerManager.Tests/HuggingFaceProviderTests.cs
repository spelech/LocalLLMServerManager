using System;
using System.Collections.Generic;
using System.IO;
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

public class HuggingFaceProviderTests
{
    private const string SampleHfSearchResponse = """
    [
      {
        "id": "Qwen/Qwen2.5-Coder-7B-Instruct-GGUF",
        "author": "Qwen",
        "downloads": 125840,
        "likes": 842,
        "pipeline_tag": "text-generation"
      },
      {
        "id": "bartowski/DeepSeek-R1-Distill-Qwen-14B-GGUF",
        "author": "bartowski",
        "downloads": 94320,
        "likes": 612,
        "pipeline_tag": "text-generation"
      },
      {
        "id": "black-forest-labs/FLUX.1-dev",
        "author": "black-forest-labs",
        "downloads": 542000,
        "likes": 4210,
        "pipeline_tag": "text-to-image"
      }
    ]
    """;

    private const string SampleHfModelDetailResponse = """
    {
      "id": "Qwen/Qwen2.5-Coder-7B-Instruct-GGUF",
      "author": "Qwen",
      "siblings": [
        { "rfilename": "README.md", "size": 15420 },
        { "rfilename": "qwen2.5-coder-7b-instruct-q4_k_m.gguf", "size": 4680000000 },
        { "rfilename": "qwen2.5-coder-7b-instruct-q8_0.gguf", "size": 8120000000 },
        { "rfilename": "qwen2.5-coder-7b-instruct-fp16.gguf", "size": 15200000000 },
        { "rfilename": "model-00001-of-00002.gguf", "size": 4000000000 },
        { "rfilename": "model-00002-of-00002.gguf", "size": 3500000000 },
        { "rfilename": "diffusion_pytorch_model.safetensors", "size": 9500000000 },
        { "rfilename": "model.onnx", "size": 2500000000 }
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
    public async Task SearchRepositoriesAsync_ParsesPayloadAndPopulatesItems()
    {
        using var client = CreateMockHttpClient(req =>
        {
            Assert.Contains("/api/hf/search", req.RequestUri?.ToString() ?? "");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleHfSearchResponse, Encoding.UTF8, "application/json")
            };
        });

        var service = new HuggingFaceSearchService();
        var results = await service.SearchRepositoriesAsync("http://127.0.0.1:5246", "qwen", client);

        Assert.Equal(3, results.Count);
        Assert.Equal("Qwen/Qwen2.5-Coder-7B-Instruct-GGUF", results[0].Id);
        Assert.Equal("Qwen", results[0].Author);
        Assert.Equal(842, results[0].Likes);
        Assert.Contains("125,840", results[0].Downloads);
        Assert.Equal("text-generation", results[0].PipelineTag);

        Assert.Equal("black-forest-labs/FLUX.1-dev", results[2].Id);
        Assert.Equal("text-to-image", results[2].PipelineTag);
    }

    [Fact]
    public async Task SearchRepositoriesAsync_MultiPipelineTags_QueriesEachAndDeduplicates()
    {
        var queriedTags = new List<string>();
        using var client = CreateMockHttpClient(req =>
        {
            var uri = req.RequestUri?.ToString() ?? "";
            if (uri.Contains("pipeline_tag="))
            {
                var queryIndex = uri.IndexOf("pipeline_tag=", StringComparison.Ordinal);
                var tagPart = uri[(queryIndex + "pipeline_tag=".Length)..];
                var endIdx = tagPart.IndexOf('&');
                var tag = endIdx >= 0 ? tagPart[..endIdx] : tagPart;
                queriedTags.Add(Uri.UnescapeDataString(tag));
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleHfSearchResponse, Encoding.UTF8, "application/json")
            };
        });

        var service = new HuggingFaceSearchService();
        var results = await service.SearchRepositoriesAsync("http://127.0.0.1:5246", "ai", new[] { "text-generation", "text-to-image", "text-generation" }, client);

        Assert.Equal(3, results.Count); // Deduplicated by ID
        Assert.Contains("text-generation", queriedTags);
        Assert.Contains("text-to-image", queriedTags);
    }

    [Fact]
    public async Task FetchQuantizationsAsync_ParsesGgufQuantsShardsAndSafetensors()
    {
        using var client = CreateMockHttpClient(req =>
        {
            Assert.Contains("/api/hf/model", req.RequestUri?.ToString() ?? "");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleHfModelDetailResponse, Encoding.UTF8, "application/json")
            };
        });

        var service = new HuggingFaceSearchService();
        var files = await service.FetchQuantizationsAsync("http://127.0.0.1:5246", "Qwen/Qwen2.5-Coder-7B-Instruct-GGUF", client);

        Assert.Equal(7, files.Count); // Ignores README.md
        var q4 = files.FirstOrDefault(f => f.Filename.Contains("q4_k_m"));
        Assert.NotNull(q4);
        Assert.Equal("Q4_K_M", q4.Quantization);
        Assert.True(q4.SizeBytes > 4_000_000_000L);

        var q8 = files.FirstOrDefault(f => f.Filename.Contains("q8_0"));
        Assert.NotNull(q8);
        Assert.Equal("Q8_0", q8.Quantization);

        var fp16 = files.FirstOrDefault(f => f.Filename.Contains("fp16"));
        Assert.NotNull(fp16);
        Assert.Equal("FP16", fp16.Quantization);

        var shards = files.Where(f => f.Filename.Contains("of-00002")).ToList();
        Assert.Equal(2, shards.Count);

        var safetensors = files.FirstOrDefault(f => f.Filename.EndsWith(".safetensors"));
        Assert.NotNull(safetensors);
        Assert.Equal("Safetensors", safetensors.Quantization);

        var onnx = files.FirstOrDefault(f => f.Filename.EndsWith(".onnx"));
        Assert.NotNull(onnx);
        Assert.Equal("ONNX", onnx.Quantization);
    }

    [Fact]
    public async Task FetchQuantizationsAsync_HttpError_ReturnsEmptyListWithoutCrashing()
    {
        using var client = CreateMockHttpClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var service = new HuggingFaceSearchService();
        var files = await service.FetchQuantizationsAsync("http://127.0.0.1:5246", "invalid/repo", client);

        Assert.NotNull(files);
        Assert.Empty(files);
    }

    [Fact]
    public async Task ViewModel_SearchExecution_PopulatesResultsAndComputesFitBadges()
    {
        var serviceMock = new Mock<IHuggingFaceSearchService>();
        serviceMock.Setup(s => s.SearchRepositoriesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>?>(), It.IsAny<HttpClient>()))
            .ReturnsAsync(new List<HuggingFaceRepoItem>
            {
                new("Qwen/Qwen2.5-Coder-7B-Instruct-GGUF", "Qwen", 850, "100k downloads", "text-generation"),
                new("deepseek-ai/DeepSeek-R1-Distill-Qwen-70B", "deepseek-ai", 3200, "500k downloads", "text-generation")
            });

        var vm = new HuggingFaceSearchViewModel(serviceMock.Object);
        vm.UpdateHardwareTelemetry(totalVramMb: 16384, totalRamMb: 32768);

        vm.HfSearchQuery = "Qwen";
        await vm.SearchHuggingFaceCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.HuggingFaceResults.Count);
        Assert.NotEmpty(vm.FilteredHuggingFaceResults);
        Assert.NotNull(vm.FilteredHuggingFaceResults[0].FitBadge);
    }

    [Fact]
    public async Task ViewModel_OpenHfModal_FetchesQuantizationsAndSetsModalOpen()
    {
        var serviceMock = new Mock<IHuggingFaceSearchService>();
        serviceMock.Setup(s => s.FetchQuantizationsAsync(It.IsAny<string>(), "meta-llama/Llama-3.2-3B", It.IsAny<HttpClient>()))
            .ReturnsAsync(new List<HfFileQuantItem>
            {
                new("llama-3.2-3b-q4_k_m.gguf", "Q4_K_M", "1.9 GB", 2040000000L),
                new("llama-3.2-3b-fp16.gguf", "FP16", "6.2 GB", 6650000000L)
            });

        var vm = new HuggingFaceSearchViewModel(serviceMock.Object);
        var repo = new HuggingFaceRepoItem("meta-llama/Llama-3.2-3B", "meta-llama", 1500, "200k", "text-generation");

        await vm.OpenHfModalCommand.ExecuteAsync(repo);

        Assert.True(vm.IsHfModalOpen);
        Assert.Equal("meta-llama/Llama-3.2-3B", vm.ModalRepoId);
        Assert.Equal(2, vm.ModalHfFiles.Count);
        Assert.Equal("Q4_K_M", vm.ModalHfFiles[0].Quantization);
    }

    [Fact]
    public void ViewModel_PresetSwitching_ConfiguresModalityTogglesAndPipelineTags()
    {
        var serviceMock = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(serviceMock.Object);

        vm.ApplyPreset("Image");
        Assert.True(vm.IsPresetImage);
        Assert.True(vm.IsInputTextActive);
        Assert.True(vm.IsOutputImageActive);
        Assert.False(vm.IsOutputTextActive);

        vm.ApplyPreset("Audio");
        Assert.True(vm.IsPresetAudio);
        Assert.True(vm.IsOutputAudioActive);
        Assert.False(vm.IsOutputImageActive);

        vm.ApplyPreset("Video");
        Assert.True(vm.IsPresetVideo);
        Assert.True(vm.IsOutputVideoActive);

        vm.ApplyPreset("3D");
        Assert.True(vm.IsPreset3D);
        Assert.True(vm.IsOutputThreeDActive);
    }

    [Fact]
    public void ViewModel_InspectModel_InvokesCallback()
    {
        var serviceMock = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(serviceMock.Object);

        string? requestedModel = null;
        string? requestedModality = null;
        vm.OnInspectModelRequested = (m, p) =>
        {
            requestedModel = m;
            requestedModality = p;
        };

        var repo = new HuggingFaceRepoItem("bartowski/Llama-3-8B-GGUF", "bartowski", 500, "100k", "text-generation");
        vm.InspectModelCommand.Execute(repo);

        Assert.Equal("bartowski/Llama-3-8B-GGUF", requestedModel);
        Assert.Equal("LLM", requestedModality);
    }

    [AvaloniaFact]
    public void HuggingFaceTabControl_HeadlessUI_RendersAndInteractsCorrectly()
    {
        var serviceMock = new Mock<IHuggingFaceSearchService>();
        var vm = new HuggingFaceSearchViewModel(serviceMock.Object);

        var control = new HuggingFaceTabControl { DataContext = vm };
        var window = new Window { Content = control, Width = 1280, Height = 900 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Verify Search Input exists
        var searchBox = control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        Assert.NotNull(searchBox);

        searchBox.Text = "qwen2.5";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("qwen2.5", vm.HfSearchQuery);

        // Verify preset buttons interact
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var videoPresetBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "Video");
        Assert.NotNull(videoPresetBtn);

        videoPresetBtn.Command?.Execute(videoPresetBtn.CommandParameter);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(vm.IsPresetVideo);

        window.Close();
    }
}
