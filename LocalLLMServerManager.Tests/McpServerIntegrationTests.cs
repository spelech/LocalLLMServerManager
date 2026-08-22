using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class McpServerIntegrationTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;
    private readonly HttpClient _client;
    private readonly Mock<IGpuTelemetryProvider> _mockTelemetry = new();
    private readonly Mock<IAiEngineManager> _mockEngine = new();
    private readonly Mock<IOllamaModelService> _mockOllama = new();
    private readonly Mock<IToolDiscoveryService> _mockDiscovery = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();

    public McpServerIntegrationTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    private LocalLlmMcpTools CreateTools(HttpMessageHandler? handler = null)
    {
        var httpHandler = handler ?? new MockHttpMessageHandler(HttpStatusCode.OK, "{}");
        var client = new HttpClient(httpHandler);
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        return new LocalLlmMcpTools(
            _mockTelemetry.Object,
            _mockEngine.Object,
            _mockOllama.Object,
            _mockDiscovery.Object,
            _mockHttpFactory.Object
        );
    }

    [Fact]
    public async Task GetGpuVram_ReturnsTelemetryData()
    {
        _mockTelemetry.Setup(t => t.GetTelemetryAsync())
            .ReturnsAsync(new GpuTelemetryResult("NVIDIA GeForce RTX 4090", 24576, 4096, 20480, 16.7));

        var tools = CreateTools();
        var result = await tools.GetGpuVramAsync();

        Assert.NotNull(result);
        Assert.Contains("RTX 4090", result);
        Assert.Contains("24576", result);
    }

    [Fact]
    public async Task CheckHealth_ReturnsStatusForBackends()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"healthy\"}");
        var tools = CreateTools(handler);

        var result = await tools.CheckHealthAsync();

        Assert.NotNull(result);
        Assert.Contains("ollama", result);
        Assert.Contains("sdForge", result);
        Assert.Contains("comfyUi", result);
        Assert.Contains("online", result);
    }

    [Fact]
    public async Task ListModels_ReturnsInstalledOllamaModels()
    {
        var models = new List<OllamaModelItem>
        {
            new("llama3.2:latest", "2.0 GB", "Coding", "#38BDF8", false),
            new("qwen2.5-coder:7b", "4.7 GB", "Coding", "#38BDF8", false)
        };
        _mockOllama.Setup(o => o.GetInstalledModelsAsync()).ReturnsAsync(models);

        var tools = CreateTools();
        var result = await tools.ListModelsAsync();

        Assert.NotNull(result);
        Assert.Contains("llama3.2:latest", result);
        Assert.Contains("qwen2.5-coder:7b", result);
    }

    [Fact]
    public async Task PullModel_ValidName_InitiatesPull()
    {
        _mockOllama.Setup(o => o.PullModelAsync("deepseek-r1:7b")).ReturnsAsync(true);

        var tools = CreateTools();
        var result = await tools.PullModelAsync("deepseek-r1:7b");

        Assert.NotNull(result);
        Assert.Contains("deepseek-r1:7b", result);
        Assert.Contains("true", result.ToLowerInvariant());
    }

    [Fact]
    public async Task PullModel_EmptyName_ReturnsError()
    {
        var tools = CreateTools();
        var result = await tools.PullModelAsync("");

        Assert.NotNull(result);
        Assert.Contains("error", result.ToLowerInvariant());
    }

    [Fact]
    public async Task UnloadVram_SendsKeepAliveZeroToOllama()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"success\"}");
        var tools = CreateTools(handler);

        var result = await tools.UnloadVramAsync();

        Assert.NotNull(result);
        Assert.Contains("true", result.ToLowerInvariant());
    }

    [Fact]
    public async Task StartEngine_CallsEngineManagerAndReturnsResult()
    {
        _mockEngine.Setup(e => e.StartEngineAsync("forge"))
            .ReturnsAsync(new EngineOperationResult(true, "forge", "Started SD Forge process", 4521));

        var tools = CreateTools();
        var result = await tools.StartEngineAsync("forge");

        Assert.NotNull(result);
        Assert.Contains("forge", result);
        Assert.Contains("4521", result);
    }

    [Fact]
    public async Task StopEngine_CallsEngineManagerAndReturnsResult()
    {
        _mockEngine.Setup(e => e.StopEngineAsync("comfyui"))
            .ReturnsAsync(new EngineOperationResult(true, "comfyui", "Stopped ComfyUI process"));

        var tools = CreateTools();
        var result = await tools.StopEngineAsync("comfyui");

        Assert.NotNull(result);
        Assert.Contains("comfyui", result);
        Assert.Contains("Stopped ComfyUI process", result);
    }

    [Fact]
    public async Task DetectTools_ReturnsDiscoveredToolsResult()
    {
        var discovered = new DiscoveredToolsResult(
            new DiscoveredToolInfo(true, @"C:\Program Files\Ollama\ollama.exe", @"C:\Program Files\Ollama", @"C:\Users\Alias\.ollama\models", null, "Installed"),
            new DiscoveredToolInfo(false, null, null, null, null, "Not found"),
            new DiscoveredToolInfo(true, @"C:\AI\webui\webui-user.bat", @"C:\AI\webui", @"C:\AI\webui\models", null, "Installed"),
            @"C:\AI\3D",
            @"C:\AI\workflows"
        );

        _mockDiscovery.Setup(d => d.DetectAllToolsAsync()).ReturnsAsync(discovered);

        var tools = CreateTools();
        var result = await tools.DetectToolsAsync();

        Assert.NotNull(result);
        Assert.Contains("ollama.exe", result);
        Assert.Contains("webui-user.bat", result);
    }

    [Fact]
    public async Task LegacyMcpToolsEndpoint_ReturnsAllToolMetadata()
    {
        var response = await _client.GetAsync("/api/mcp/tools");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("get_gpu_vram", json);
        Assert.Contains("check_health", json);
        Assert.Contains("list_models", json);
        Assert.Contains("pull_model", json);
        Assert.Contains("unload_vram", json);
        Assert.Contains("start_engine", json);
        Assert.Contains("stop_engine", json);
        Assert.Contains("detect_tools", json);
        Assert.Contains("/mcp", json);
    }

    [Fact]
    public async Task McpEndpoint_IsRegisteredAndAccessible()
    {
        // MCP HTTP transport in ModelContextProtocol.AspNetCore accepts POST (JSON-RPC) or GET (SSE)
        var postContent = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/mcp", postContent);
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }
}

internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseContent;

    public MockHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
    {
        _statusCode = statusCode;
        _responseContent = responseContent;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
