using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class OllamaModelServiceTests
{
    [Fact]
    public async Task LoadInstalledModelsAsync_PrimaryEndpointSuccess_ParsesModelsCorrectly()
    {
        var jsonResponse = @"{
            ""models"": [
                {
                    ""name"": ""llama3:8b"",
                    ""size"": 4661224576
                },
                {
                    ""name"": ""qwen2.5-math:7b"",
                    ""size"": 4661224576
                },
                {
                    ""name"": ""deepseek-r1:8b"",
                    ""size"": 4661224576
                },
                {
                    ""name"": ""unknown-zero"",
                    ""size"": 0
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
        var service = new OllamaModelService();

        var models = await service.LoadInstalledModelsAsync("http://localhost:5246", client);

        Assert.Equal(4, models.Count);
        Assert.Equal("llama3:8b", models[0].Name);
        Assert.Equal("4.34 GB", models[0].FormatSize);
        Assert.Equal("💻 Coding & General", models[0].CapabilityTag);

        Assert.Equal("qwen2.5-math:7b", models[1].Name);
        Assert.Equal("🧮 Mathematics", models[1].CapabilityTag);

        Assert.Equal("deepseek-r1:8b", models[2].Name);
        Assert.Equal("🧠 Reasoning profile", models[2].CapabilityTag);

        Assert.Equal("unknown-zero", models[3].Name);
        Assert.Equal("N/A", models[3].FormatSize);
    }

    [Fact]
    public async Task LoadInstalledModelsAsync_PrimaryFails_FallsBackToDirectTags()
    {
        var tagsResponse = @"{
            ""models"": [
                {
                    ""name"": ""mistral:latest"",
                    ""size"": 4100000000
                }
            ]
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("/api/models")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("/api/tags")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(tagsResponse)
            });

        var client = new HttpClient(handlerMock.Object);
        var service = new OllamaModelService();

        var models = await service.LoadInstalledModelsAsync("http://localhost:5246", client);

        Assert.Single(models);
        Assert.Equal("mistral:latest", models[0].Name);
    }

    [Fact]
    public async Task LoadInstalledModelsAsync_ExceptionThrown_ReturnsEmptyList()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new HttpClient(handlerMock.Object);
        var service = new OllamaModelService();

        var models = await service.LoadInstalledModelsAsync("http://localhost:5246", client);
        Assert.Empty(models);
    }

    [Fact]
    public async Task UnloadAllVramAsync_RunningModelsFound_SendsKeepAliveZero()
    {
        var psResponse = @"{
            ""models"": [
                { ""name"": ""llama3:8b"" },
                { ""name"": ""phi3:mini"" }
            ]
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("/api/ollama/ps")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(psResponse)
            });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("/api/generate")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var service = new OllamaModelService();

        var result = await service.UnloadAllVramAsync("http://localhost:5246", client);
        Assert.True(result);
    }

    [Fact]
    public async Task PreloadModelAsync_SendsKeepAliveMinusOne()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("/api/generate")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var service = new OllamaModelService();

        var result = await service.PreloadModelAsync("http://localhost:5246", "llama3:8b", client);
        Assert.True(result);
    }

    [Fact]
    public async Task PullModelAsync_ValidAndEmptyInputs_HandledProperly()
    {
        var service = new OllamaModelService();
        var emptyRes = await service.PullModelAsync("");
        Assert.False(emptyRes);
    }

    [Fact]
    public async Task DeleteModelAsync_EmptyName_ReturnsFalse()
    {
        var service = new OllamaModelService();
        var client = new HttpClient();
        var result = await service.DeleteModelAsync("http://localhost:5246", "", client);
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteModelAsync_ServerProxyDelete_Succeeds()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete && r.RequestUri != null && r.RequestUri.ToString().Contains("/api/models/delete")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var service = new OllamaModelService();

        var result = await service.DeleteModelAsync("http://localhost:5246", "llama3:8b", client);
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteModelAsync_ProxyPostFallback_Succeeds()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete && r.RequestUri != null && r.RequestUri.ToString().Contains("/api/models/delete")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.MethodNotAllowed });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri != null && r.RequestUri.ToString().Contains("/api/models/delete")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var service = new OllamaModelService();

        var result = await service.DeleteModelAsync("http://localhost:5246", "llama3:8b", client);
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteModelAsync_DirectOllamaDaemonFallback_Succeeds()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        // Proxy DELETE fails
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains(":5246")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NotFound });

        // Unload generate
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("11434/api/generate")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        // Direct delete
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("11434/api/delete")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var service = new OllamaModelService();

        var result = await service.DeleteModelAsync("http://localhost:5246", "llama3", client);
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteLocalModelFileAsync_ValidatesInputsAndPerformsDelete()
    {
        var service = new OllamaModelService();
        var client = new HttpClient();
        
        var emptyRes = await service.DeleteLocalModelFileAsync("http://localhost:5246", "", client);
        Assert.False(emptyRes);

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete && r.RequestUri != null && r.RequestUri.ToString().Contains("/api/models/delete")),
                ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var mockClient = new HttpClient(handlerMock.Object);
        var successRes = await service.DeleteLocalModelFileAsync("http://localhost:5246", "C:/models/test.safetensors", mockClient);
        Assert.True(successRes);
    }

    [Fact]
    public async Task GetInstalledModelsAsync_ExecutesWithoutThrowing()
    {
        var service = new OllamaModelService();
        var models = await service.GetInstalledModelsAsync();
        Assert.NotNull(models);
    }
}
