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
                ""downloads"": 45000
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

        var results = await service.SearchRepositoriesAsync("http://localhost", "llama", client);
        Assert.NotEmpty(results);
        Assert.Equal("meta-llama/Llama-3.3-8B-Instruct-GGUF", results[0].Id);
        Assert.Equal("meta-llama", results[0].Author);
        Assert.Equal(1200, results[0].Likes);
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
}
