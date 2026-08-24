using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
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
    public async Task CheckHealth_ReturnsStatusForBackends_WhenOnline()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"healthy\"}");
        var tools = CreateTools(handler);

        var result = await tools.CheckHealthAsync();

        Assert.NotNull(result);
        Assert.Contains("ollama", result);
        Assert.Contains("sdForge", result);
        Assert.Contains("comfyUi", result);
        Assert.Contains("online", result);
        Assert.Contains("latencyMs", result);
    }

    [Fact]
    public async Task CheckHealth_WhenHttpExceptionThrown_ReturnsErrorGracefully()
    {
        var throwingHandler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
        var tools = CreateTools(throwingHandler);

        var result = await tools.CheckHealthAsync();

        Assert.NotNull(result);
        Assert.Contains("ollama", result);
        Assert.Contains("sdForge", result);
        Assert.Contains("comfyUi", result);
        Assert.Contains("Connection refused", result);

        var doc = JsonNode.Parse(result);
        Assert.NotNull(doc);
        Assert.False(doc?["ollama"]?["online"]?.GetValue<bool>());
        Assert.False(doc?["sdForge"]?["online"]?.GetValue<bool>());
        Assert.False(doc?["comfyUi"]?["online"]?.GetValue<bool>());
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
    public async Task ListModels_WhenNoModelsInstalled_ReturnsEmptyArray()
    {
        _mockOllama.Setup(o => o.GetInstalledModelsAsync()).ReturnsAsync(new List<OllamaModelItem>());

        var tools = CreateTools();
        var result = await tools.ListModelsAsync();

        Assert.NotNull(result);
        var doc = JsonNode.Parse(result)?.AsArray();
        Assert.NotNull(doc);
        Assert.Empty(doc);
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
        Assert.Contains("Model pull initiated", result);
    }

    [Fact]
    public async Task PullModel_ValidName_WhenServiceReturnsFalse_ReturnsFailureMessage()
    {
        _mockOllama.Setup(o => o.PullModelAsync("unknown:model")).ReturnsAsync(false);

        var tools = CreateTools();
        var result = await tools.PullModelAsync("unknown:model");

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("Failed to initiate pull", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task PullModel_NullOrWhitespaceName_ReturnsError(string? modelName)
    {
        var tools = CreateTools();
        var result = await tools.PullModelAsync(modelName!);

        Assert.NotNull(result);
        Assert.Contains("error", result.ToLowerInvariant());
        Assert.Contains("modelName is required", result);
    }

    [Fact]
    public async Task UnloadVram_SendsKeepAliveZeroToOllama_Success()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"success\"}");
        var tools = CreateTools(handler);

        var result = await tools.UnloadVramAsync();

        Assert.NotNull(result);
        Assert.Contains("true", result.ToLowerInvariant());
        Assert.Contains("VRAM unload requested", result);
    }

    [Fact]
    public async Task UnloadVram_WhenOllamaReturnsServerError_ReturnsFailureStatus()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "{\"error\":\"server busy\"}");
        var tools = CreateTools(handler);

        var result = await tools.UnloadVramAsync();

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("500", result);
    }

    [Fact]
    public async Task UnloadVram_WhenHttpExceptionThrown_CatchesAndReturnsError()
    {
        var throwingHandler = new ThrowingHttpMessageHandler(new HttpRequestException("Ollama daemon unreachable"));
        var tools = CreateTools(throwingHandler);

        var result = await tools.UnloadVramAsync();

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("Ollama daemon unreachable", result);
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
    public async Task StartEngine_WhenStartFails_ReturnsFailureResult()
    {
        _mockEngine.Setup(e => e.StartEngineAsync("unknown"))
            .ReturnsAsync(new EngineOperationResult(false, "unknown", "Unknown engine 'unknown'"));

        var tools = CreateTools();
        var result = await tools.StartEngineAsync("unknown");

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("Unknown engine", result);
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
    public async Task StopEngine_WhenStopFails_ReturnsFailureResult()
    {
        _mockEngine.Setup(e => e.StopEngineAsync("forge"))
            .ReturnsAsync(new EngineOperationResult(false, "forge", "Process was not running"));

        var tools = CreateTools();
        var result = await tools.StopEngineAsync("forge");

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("Process was not running", result);
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
    public async Task GenerateVideo_ValidPrompt_ReturnsMediaUrlAndStatus()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"prompt_id\":\"vid_123\"}");
        var tools = CreateTools(handler);

        var result = await tools.GenerateVideoAsync("A Cyberpunk city skyline at night", "wan2.2_t2v", 832, 480, 49);

        Assert.NotNull(result);
        Assert.Contains("true", result.ToLowerInvariant());
        Assert.Contains("wan2.2_t2v", result);
        Assert.Contains("/output/video_", result);
        Assert.Contains("queued", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GenerateVideo_NullOrEmptyPrompt_ReturnsError(string? prompt)
    {
        var tools = CreateTools();
        var result = await tools.GenerateVideoAsync(prompt!);

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("prompt is required", result);
    }

    [Fact]
    public async Task GenerateVideo_WhenHttpExceptionThrown_ReturnsErrorGracefully()
    {
        var throwingHandler = new ThrowingHttpMessageHandler(new HttpRequestException("ComfyUI endpoint offline"));
        var tools = CreateTools(throwingHandler);

        var result = await tools.GenerateVideoAsync("A sunset landscape");

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("ComfyUI endpoint offline", result);
    }

    [Fact]
    public async Task SynthesizeSpeech_ValidText_ReturnsAudioUrlAndStatus()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"ok\"}");
        var tools = CreateTools(handler);

        var result = await tools.SynthesizeSpeechAsync("Hello, welcome to the local AI assistant.", "af_heart", "mp3");

        Assert.NotNull(result);
        Assert.Contains("true", result.ToLowerInvariant());
        Assert.Contains("af_heart", result);
        Assert.Contains("/output/speech_", result);
        Assert.Contains("completed", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SynthesizeSpeech_NullOrEmptyText_ReturnsError(string? text)
    {
        var tools = CreateTools();
        var result = await tools.SynthesizeSpeechAsync(text!);

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("text is required", result);
    }

    [Fact]
    public async Task SynthesizeSpeech_WhenHttpExceptionThrown_ReturnsErrorGracefully()
    {
        var throwingHandler = new ThrowingHttpMessageHandler(new HttpRequestException("TTS server unreachable"));
        var tools = CreateTools(throwingHandler);

        var result = await tools.SynthesizeSpeechAsync("Testing TTS failure");

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("TTS server unreachable", result);
    }

    [Fact]
    public async Task GenerateAudio_ValidPrompt_ReturnsAudioUrlAndStatus()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"status\":\"queued\"}");
        var tools = CreateTools(handler);

        var result = await tools.GenerateAudioAsync("Cinematic sci-fi ambient synth loop", 15);

        Assert.NotNull(result);
        Assert.Contains("true", result.ToLowerInvariant());
        Assert.Contains("15", result);
        Assert.Contains("/output/audio_", result);
        Assert.Contains("queued", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GenerateAudio_NullOrEmptyPrompt_ReturnsError(string? prompt)
    {
        var tools = CreateTools();
        var result = await tools.GenerateAudioAsync(prompt!);

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("prompt is required", result);
    }

    [Fact]
    public async Task GenerateAudio_WhenHttpExceptionThrown_ReturnsErrorGracefully()
    {
        var throwingHandler = new ThrowingHttpMessageHandler(new HttpRequestException("Audio model timeout"));
        var tools = CreateTools(throwingHandler);

        var result = await tools.GenerateAudioAsync("Rainforest sounds");

        Assert.NotNull(result);
        Assert.Contains("false", result.ToLowerInvariant());
        Assert.Contains("Audio model timeout", result);
    }

    [Fact]
    public void McpToolsClass_HasCorrectAttributesAndDescriptions()
    {
        var toolType = typeof(LocalLlmMcpTools);

        // Class-level attribute
        Assert.NotNull(toolType.GetCustomAttribute<McpServerToolTypeAttribute>());

        // 11 Expected Tool Methods
        var expectedMethods = new[]
        {
            "GetGpuVramAsync",
            "CheckHealthAsync",
            "ListModelsAsync",
            "PullModelAsync",
            "UnloadVramAsync",
            "StartEngineAsync",
            "StopEngineAsync",
            "DetectToolsAsync",
            "GenerateVideoAsync",
            "SynthesizeSpeechAsync",
            "GenerateAudioAsync"
        };

        foreach (var methodName in expectedMethods)
        {
            var method = toolType.GetMethod(methodName);
            Assert.NotNull(method);
            Assert.NotNull(method.GetCustomAttribute<McpServerToolAttribute>());
            
            var desc = method.GetCustomAttribute<DescriptionAttribute>();
            Assert.NotNull(desc);
            Assert.False(string.IsNullOrWhiteSpace(desc.Description));
        }

        // Check parameter descriptions
        var pullModelMethod = toolType.GetMethod("PullModelAsync");
        var modelNameParam = pullModelMethod?.GetParameters().FirstOrDefault(p => p.Name == "modelName");
        Assert.NotNull(modelNameParam?.GetCustomAttribute<DescriptionAttribute>());

        var startEngineMethod = toolType.GetMethod("StartEngineAsync");
        var startEngineParam = startEngineMethod?.GetParameters().FirstOrDefault(p => p.Name == "engine");
        Assert.NotNull(startEngineParam?.GetCustomAttribute<DescriptionAttribute>());

        var stopEngineMethod = toolType.GetMethod("StopEngineAsync");
        var stopEngineParam = stopEngineMethod?.GetParameters().FirstOrDefault(p => p.Name == "engine");
        Assert.NotNull(stopEngineParam?.GetCustomAttribute<DescriptionAttribute>());

        var generateVideoMethod = toolType.GetMethod("GenerateVideoAsync");
        var videoPromptParam = generateVideoMethod?.GetParameters().FirstOrDefault(p => p.Name == "prompt");
        Assert.NotNull(videoPromptParam?.GetCustomAttribute<DescriptionAttribute>());

        var synthesizeSpeechMethod = toolType.GetMethod("SynthesizeSpeechAsync");
        var speechTextParam = synthesizeSpeechMethod?.GetParameters().FirstOrDefault(p => p.Name == "text");
        Assert.NotNull(speechTextParam?.GetCustomAttribute<DescriptionAttribute>());

        var generateAudioMethod = toolType.GetMethod("GenerateAudioAsync");
        var audioPromptParam = generateAudioMethod?.GetParameters().FirstOrDefault(p => p.Name == "prompt");
        Assert.NotNull(audioPromptParam?.GetCustomAttribute<DescriptionAttribute>());
    }

    [Fact]
    public void McpServer_DependencyInjectionResolution_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddSingleton(_mockTelemetry.Object);
        services.AddSingleton(_mockEngine.Object);
        services.AddSingleton(_mockOllama.Object);
        services.AddSingleton(_mockDiscovery.Object);

        services.AddMcpServer()
            .WithHttpTransport()
            .WithTools<LocalLlmMcpTools>();

        var provider = services.BuildServiceProvider();

        var toolsInstance = ActivatorUtilities.CreateInstance<LocalLlmMcpTools>(provider);
        Assert.NotNull(toolsInstance);
    }

    [Fact]
    public async Task McpEndpoint_IsRegisteredAndAccessible()
    {
        // Standard MCP HTTP transport in ModelContextProtocol.AspNetCore accepts POST (JSON-RPC)
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

internal class ThrowingHttpMessageHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw _exception;
    }
}
