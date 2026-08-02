using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class VramOrchestratorTests
{
    [Fact]
    public async Task IsOllamaHealthyAsync_ReturnsTrue_WhenApiReturnsSuccess()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("Ollama is running")
            });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        var isHealthy = await orchestrator.IsOllamaHealthyAsync();

        Assert.True(isHealthy);
    }

    [Fact]
    public async Task IsOllamaHealthyAsync_ReturnsFalse_WhenApiFails()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        var isHealthy = await orchestrator.IsOllamaHealthyAsync();

        Assert.False(isHealthy);
    }

    [Fact]
    public async Task IsForgeHealthyAsync_ReturnsTrue_WhenProgressEndpointReturns200()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/sdapi/v1/progress")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        var isHealthy = await orchestrator.IsForgeHealthyAsync();

        Assert.True(isHealthy);
    }

    [Fact]
    public async Task IsComfyUiHealthyAsync_ReturnsTrue_WhenSystemStatsReturns200()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/system_stats")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        var isHealthy = await orchestrator.IsComfyUiHealthyAsync("http://127.0.0.1:8188");

        Assert.True(isHealthy);
    }

    [Fact]
    public async Task FreeComfyUiVramAsync_SendsPostToFreeEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        await orchestrator.FreeComfyUiVramAsync("http://127.0.0.1:8188");

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("http://127.0.0.1:8188/free", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task EnsureVramForImageGenerationAsync_ExecutesCleanly()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        await orchestrator.EnsureVramForImageGenerationAsync();
    }

    [Fact]
    public async Task EnsureVramForComfyUiAsync_ExecutesCleanly()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        await orchestrator.EnsureVramForComfyUiAsync();
    }
}
