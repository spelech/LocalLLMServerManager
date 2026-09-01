using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class SearchServicesTests
{
    [Fact]
    public async Task CivitaiSearchService_SearchModelsAsync_ParsesJsonResponse()
    {
        var jsonResponse = @"{
            ""items"": [
                {
                    ""id"": 101,
                    ""name"": ""Cyberpunk Model"",
                    ""type"": ""Checkpoint"",
                    ""modelVersions"": [
                        {
                            ""images"": [ { ""url"": ""http://localhost/img.png"" } ],
                            ""files"": [
                                { ""downloadUrl"": ""http://localhost/model.gguf"", ""name"": ""model.gguf"" }
                            ]
                        }
                    ]
                }
            ]
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var client = new HttpClient(handlerMock.Object);
        var service = new CivitaiSearchService();

        var results = await service.SearchModelsAsync("http://localhost", "cyberpunk", "Checkpoint", "Most Downloaded", client);
        Assert.NotEmpty(results);
        Assert.Equal(101, results[0].Id);
        Assert.Equal("Cyberpunk Model", results[0].Name);
        Assert.Equal("http://localhost/model.gguf", results[0].DownloadUrl);
        Assert.Equal("http://localhost/img.png", results[0].ThumbnailUrl);
    }

    [Fact]
    public async Task CivitaiSearchService_SearchModelsAsync_HandlesExceptionGracefully()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Network error"));

        var client = new HttpClient(handlerMock.Object);
        var service = new CivitaiSearchService();

        var results = await service.SearchModelsAsync("http://localhost", "query", "Checkpoint", "Most Downloaded", client);
        Assert.Empty(results);
    }

    [Fact]
    public async Task HuggingFaceSearchService_SearchRepositoriesAsync_ParsesJsonResponse()
    {
        var jsonResponse = @"[
            {
                ""id"": ""meta-llama/Llama-3.3-8B-Instruct-GGUF"",
                ""author"": ""meta-llama"",
                ""likes"": 1200,
                ""downloads"": 45000,
                ""pipeline_tag"": ""text-generation""
            }
        ]";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var client = new HttpClient(handlerMock.Object);
        var service = new HuggingFaceSearchService();

        var results = await service.SearchRepositoriesAsync("http://localhost", "llama", "text-generation", client);
        Assert.NotEmpty(results);
        Assert.Equal("meta-llama/Llama-3.3-8B-Instruct-GGUF", results[0].Id);
        Assert.Equal("meta-llama", results[0].Author);
        Assert.Equal(1200, results[0].Likes);
        Assert.Equal("text-generation", results[0].PipelineTag);
    }

    [Theory]
    [InlineData("text-to-video", "ltx.safetensors", "ComfyUI/models/diffusion_models")]
    [InlineData("image-to-video", "wan.safetensors", "ComfyUI/models/diffusion_models")]
    [InlineData("text-to-speech", "kokoro.pt", "models/tts")]
    [InlineData("text-to-audio", "f5tts.pt", "models/tts")]
    [InlineData("text-to-3d", "trellis.safetensors", "models/3d")]
    [InlineData("Lora", "style.safetensors", "models/Lora")]
    [InlineData("Checkpoint", "sd.safetensors", "models/checkpoints")]
    public void DownloadManager_ResolveTargetDirectory_RoutesCorrectly(string tagOrType, string fileName, string expectedSubdir)
    {
        var root = "/test/root";
        var resolved = DownloadManager.ResolveTargetDirectory(tagOrType, fileName, root);
        var normalizedExpected = System.IO.Path.Combine(root, expectedSubdir.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Assert.Equal(normalizedExpected, resolved);
    }

    [Fact]
    public async Task HuggingFaceSearchService_FetchQuantizationsAsync_ParsesFilesResponse()
    {
        var jsonResponse = @"{
            ""siblings"": [
                { ""rfilename"": ""llama-3.3-Q4_K_M.gguf"", ""size"": 4294967296 },
                { ""rfilename"": ""llama-3.3-Q8_0.gguf"", ""size"": 8589934592 }
            ]
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var client = new HttpClient(handlerMock.Object);
        var service = new HuggingFaceSearchService();

        var quants = await service.FetchQuantizationsAsync("http://localhost", "meta-llama/Llama-3.3-8B-Instruct-GGUF", client);
        Assert.NotEmpty(quants);
        Assert.Equal(2, quants.Count);
        Assert.Equal("Q4_K_M", quants[0].Quantization);
        Assert.Equal("Q8_0", quants[1].Quantization);
    }

    [Fact]
    public async Task HuggingFaceSearchService_SearchRepositoriesAsync_MultiTags_CombinesDistinctResults()
    {
        var jsonResponse = @"[
            { ""id"": ""repo/audio-1"", ""author"": ""meta"", ""likes"": 10, ""downloads"": 100, ""pipeline_tag"": ""text-to-speech"" },
            { ""id"": ""repo/audio-2"", ""author"": ""meta"", ""likes"": 20, ""downloads"": 200, ""pipeline_tag"": ""text-to-audio"" }
        ]";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var client = new HttpClient(handlerMock.Object);
        var service = new HuggingFaceSearchService();

        var results = await service.SearchRepositoriesAsync("http://localhost", "query", new[] { "text-to-speech", "text-to-audio" }, client);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task HuggingFaceSearchService_FetchQuantizationsAsync_ParsesDifferentQuantizations()
    {
        var jsonResponse = @"{
            ""siblings"": [
                { ""rfilename"": ""model-Q5_K_M.gguf"", ""size"": 5000000000 },
                { ""rfilename"": ""model-FP16.gguf"", ""size"": 16000000000 },
                { ""rfilename"": ""readme.md"", ""size"": 1024 }
            ]
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var client = new HttpClient(handlerMock.Object);
        var service = new HuggingFaceSearchService();

        var quants = await service.FetchQuantizationsAsync("http://localhost", "org/model", client);
        Assert.Equal(2, quants.Count);
        Assert.Equal("Q5_K_M", quants[0].Quantization);
        Assert.Equal("FP16", quants[1].Quantization);
    }

    [Fact]
    public void HuggingFaceSearchViewModel_ResolvePipelineTags_MultimodalVlmReturnsExpectedTags()
    {
        var inputs = new[] { "Text", "Image" };
        var outputs = new[] { "Text" };

        var tags = LocalLLMServerManager.Shared.ViewModels.HuggingFaceSearchViewModel.ResolvePipelineTags(inputs, outputs);

        Assert.Contains("image-text-to-text", tags);
        Assert.Contains("image-to-text", tags);
        Assert.Contains("visual-question-answering", tags);
    }

    [Fact]
    public void HuggingFaceSearchViewModel_ApplyPreset_UpdatesActiveModalities()
    {
        var vm = new LocalLLMServerManager.Shared.ViewModels.HuggingFaceSearchViewModel(new Mock<LocalLLMServerManager.Shared.Interfaces.IHuggingFaceSearchService>().Object);

        vm.ApplyPreset("Multimodal");
        Assert.True(vm.IsInputTextActive);
        Assert.True(vm.IsInputImageActive);
        Assert.False(vm.IsInputAudioActive);
        Assert.True(vm.IsOutputTextActive);
        Assert.False(vm.IsOutputImageActive);

        vm.ApplyPreset("Video");
        Assert.True(vm.IsInputTextActive);
        Assert.True(vm.IsOutputVideoActive);
        Assert.False(vm.IsOutputTextActive);
    }

    [Fact]
    public void HuggingFaceSearchViewModel_FilterByFitVerdict_FiltersResultsCorrectly()
    {
        var vm = new LocalLLMServerManager.Shared.ViewModels.HuggingFaceSearchViewModel(new Mock<LocalLLMServerManager.Shared.Interfaces.IHuggingFaceSearchService>().Object);
        vm.HuggingFaceResults.Add(new LocalLLMServerManager.Shared.ViewModels.HuggingFaceRepoItem("repo1", "author", 10, "100 downloads", "text-generation",
            new LocalLLMServerManager.Shared.Models.QuickFitBadge("🟢 Full VRAM", "#10B981", "", LocalLLMServerManager.Shared.Models.FitVerdict.FullVram)));
        vm.HuggingFaceResults.Add(new LocalLLMServerManager.Shared.ViewModels.HuggingFaceRepoItem("repo2", "author", 20, "200 downloads", "text-generation",
            new LocalLLMServerManager.Shared.Models.QuickFitBadge("🔴 Won't Fit (OOM)", "#EF4444", "", LocalLLMServerManager.Shared.Models.FitVerdict.OutOfMemory)));

        vm.IsOomActive = false;
        Assert.Single(vm.FilteredHuggingFaceResults);
        Assert.Equal("repo1", vm.FilteredHuggingFaceResults[0].Id);

        vm.ToggleFitVerdict("oom");
        Assert.Equal(2, vm.FilteredHuggingFaceResults.Count);
    }

    [Fact]
    public void CivitaiSearchViewModel_FilterByFitVerdict_FiltersResultsCorrectly()
    {
        var vm = new LocalLLMServerManager.Shared.ViewModels.CivitaiSearchViewModel(new Mock<LocalLLMServerManager.Shared.Interfaces.ICivitaiSearchService>().Object);
        vm.CivitaiResults.Add(new LocalLLMServerManager.Shared.ViewModels.CivitaiModelItem(1, "Flux Dev", "Checkpoint", "http://img/1", "http://dl/1", "flux.safetensors", 5.0, 500,
            new LocalLLMServerManager.Shared.Models.QuickFitBadge("🟢 Full VRAM", "#10B981", "", LocalLLMServerManager.Shared.Models.FitVerdict.FullVram)));
        vm.CivitaiResults.Add(new LocalLLMServerManager.Shared.ViewModels.CivitaiModelItem(2, "Mega Checkpoint", "Checkpoint", "http://img/2", "http://dl/2", "mega.safetensors", 4.5, 100,
            new LocalLLMServerManager.Shared.Models.QuickFitBadge("🔴 Won't Fit (OOM)", "#EF4444", "", LocalLLMServerManager.Shared.Models.FitVerdict.OutOfMemory)));

        vm.IsOomActive = false;
        Assert.Single(vm.FilteredCivitaiResults);
        Assert.Equal("Flux Dev", vm.FilteredCivitaiResults[0].Name);

        vm.ToggleFitVerdict("oom");
        Assert.Equal(2, vm.FilteredCivitaiResults.Count);
    }

    [Fact]
    public async Task SearchServices_EmptyResponses_ReturnEmptyLists()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{ \"items\": [] }")
            });

        var client = new HttpClient(handlerMock.Object);
        var civitai = new CivitaiSearchService();
        var civResults = await civitai.SearchModelsAsync("http://localhost", "none", "Checkpoint", "Most Downloaded", client);
        Assert.Empty(civResults);

        var hf = new HuggingFaceSearchService();
        var hfResults = await hf.SearchRepositoriesAsync("http://localhost", "query", client);
        Assert.Empty(hfResults);
    }

    [Fact]
    public async Task HuggingFaceSearchService_SearchModelsAsync_ExecutesGracefully()
    {
        var service = new HuggingFaceSearchService();
        using var cts = new System.Threading.CancellationTokenSource();
        cts.Cancel(); // Cancel immediately to avoid network calls during offline unit tests
        var results = await service.SearchModelsAsync("llama", "text-generation", cts.Token);
        Assert.Empty(results);
    }
}
