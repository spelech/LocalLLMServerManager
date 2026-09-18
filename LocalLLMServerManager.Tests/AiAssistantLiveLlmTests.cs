using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using Moq;
using Xunit;

namespace LocalLLMServerManager.Tests;

/// <summary>
/// Live integration test suite for verifying connectivity, tool calling, and streaming
/// against real OpenAI-compatible endpoints (such as LiteLLM, Vertex AI Gemini Flash, or Ollama).
///
/// To run these live tests against your local or hosted LiteLLM proxy:
/// Set the environment variables:
///   - LITELLM_ENDPOINT (e.g. http://127.0.0.1:4000/v1)
///   - LITELLM_API_KEY  (e.g. sk-litellm-secret-key)
///   - LITELLM_MODEL    (e.g. vertex_ai/gemini-2.5-flash)
///
/// If these variables are not set or the endpoint is unreachable, tests skip gracefully.
/// </summary>
public class AiAssistantLiveLlmTests
{
    private static string? GetLiveEndpoint() =>
        Environment.GetEnvironmentVariable("LITELLM_ENDPOINT")
        ?? Environment.GetEnvironmentVariable("AI_ASSISTANT_ENDPOINT")
        ?? Environment.GetEnvironmentVariable("OPENAI_BASE_URL");

    private static string? GetLiveApiKey() =>
        Environment.GetEnvironmentVariable("LITELLM_API_KEY")
        ?? Environment.GetEnvironmentVariable("AI_ASSISTANT_API_KEY")
        ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    private static string GetLiveModel() =>
        Environment.GetEnvironmentVariable("LITELLM_MODEL")
        ?? Environment.GetEnvironmentVariable("AI_ASSISTANT_MODEL")
        ?? "vertex_ai/gemini-2.5-flash";

    private static async Task<bool> IsEndpointReachableAsync(string endpoint)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var url = endpoint.TrimEnd('/') + "/models";
            var resp = await client.GetAsync(url);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static AiAssistantService CreateLiveAssistantService(string? endpoint, string? apiKey, string? model)
    {
        var mockHttpFactory = new Mock<IHttpClientFactory>();
        mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.LoadSettings()).Returns(new AppSettings(
            AiAssistantEnabled: true,
            AiAssistantEndpoint: endpoint,
            AiAssistantApiKey: apiKey,
            AiAssistantModel: model
        ));

        var promptService = new PromptManagementService();
        var telemetryProvider = new Mock<IGpuTelemetryProvider>();
        telemetryProvider.Setup(t => t.GetGpuInfo()).Returns(("NVIDIA GeForce RTX 4070 Ti SUPER", 16384L * 1024 * 1024, 4096L * 1024 * 1024));

        var tools = new AiAppTools(
            telemetryProvider.Object,
            new Mock<IAiEngineManager>().Object,
            new Mock<IOllamaModelService>().Object,
            mockSettings.Object,
            new Mock<ICanIRunItService>().Object,
            mockHttpFactory.Object
        );

        return new AiAssistantService(mockSettings.Object, promptService, tools, mockHttpFactory.Object);
    }

    [Fact]
    public void EnvironmentConfiguration_HasReasonableDefaults()
    {
        var model = GetLiveModel();
        Assert.False(string.IsNullOrWhiteSpace(model));
    }

    [Fact]
    public async Task LiveEndpoint_ValidateConnection_ReturnsSuccess_WhenReachable()
    {
        var endpoint = GetLiveEndpoint();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Assert.Skip("Live LLM endpoint not configured (set LITELLM_ENDPOINT or AI_ASSISTANT_ENDPOINT).");
            return;
        }

        var isReachable = await IsEndpointReachableAsync(endpoint);
        if (!isReachable)
        {
            Assert.Skip($"Configured endpoint '{endpoint}' is currently unreachable.");
            return;
        }

        var apiKey = GetLiveApiKey();
        var model = GetLiveModel();

        var service = CreateLiveAssistantService(endpoint, apiKey, model);
        var result = await service.ValidateConnectionAsync(endpoint, apiKey, model);

        Assert.True(result.Success, $"Expected connection success but got: {result.Message}");
        Assert.True(result.LatencyMs > 0);
    }

    [Fact]
    public async Task LiveEndpoint_SendChat_ExecutesToolCall_WhenReachable()
    {
        var endpoint = GetLiveEndpoint();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Assert.Skip("Live LLM endpoint not configured (set LITELLM_ENDPOINT or AI_ASSISTANT_ENDPOINT).");
            return;
        }

        var isReachable = await IsEndpointReachableAsync(endpoint);
        if (!isReachable)
        {
            Assert.Skip($"Configured endpoint '{endpoint}' is currently unreachable.");
            return;
        }

        var apiKey = GetLiveApiKey();
        var model = GetLiveModel();

        var service = CreateLiveAssistantService(endpoint, apiKey, model);

        var request = new AiChatRequest(new List<AiChatMessageItem>
        {
            new() { Role = "user", Content = "What is my current VRAM usage? Call the tool to check." }
        }, Model: model);

        var response = await service.SendChatAsync(request);

        Assert.True(response.Success);
        Assert.NotNull(response.Message);
        Assert.False(string.IsNullOrWhiteSpace(response.Message.Content));
        Assert.True(response.Message.HasToolCalls);
    }

    [Fact]
    public async Task LiveEndpoint_StreamChat_YieldsTokens_WhenReachable()
    {
        var endpoint = GetLiveEndpoint();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Assert.Skip("Live LLM endpoint not configured (set LITELLM_ENDPOINT or AI_ASSISTANT_ENDPOINT).");
            return;
        }

        var isReachable = await IsEndpointReachableAsync(endpoint);
        if (!isReachable)
        {
            Assert.Skip($"Configured endpoint '{endpoint}' is currently unreachable.");
            return;
        }

        var apiKey = GetLiveApiKey();
        var model = GetLiveModel();

        var service = CreateLiveAssistantService(endpoint, apiKey, model);

        var request = new AiChatRequest(new List<AiChatMessageItem>
        {
            new() { Role = "user", Content = "Say 'Antigravity AI is live!' and nothing else." }
        }, Stream: true, Model: model);

        var chunks = new List<AiChatChunk>();
        await foreach (var chunk in service.StreamChatAsync(request))
        {
            chunks.Add(chunk);
        }

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task LiveEndpoint_DiscoverModelCapabilities_ExcludesLocal_WhenReachable()
    {
        var endpoint = GetLiveEndpoint();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Assert.Skip("Live LLM endpoint not configured.");
            return;
        }

        if (!await IsEndpointReachableAsync(endpoint))
        {
            Assert.Skip($"Configured endpoint '{endpoint}' is currently unreachable.");
            return;
        }

        var service = CreateLiveAssistantService(endpoint, GetLiveApiKey(), GetLiveModel());
        var models = await service.GetModelCapabilitiesAsync(endpoint, GetLiveApiKey(), includeLocal: false);

        Assert.NotEmpty(models);
        Assert.DoesNotContain(models, m => m.Id.StartsWith("ollama/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(models, m => m.IsLocal);
    }

    [Fact]
    public async Task LiveEndpoint_MultimodalChat_SendsImageAndReceivesResponse_WhenReachable()
    {
        var endpoint = GetLiveEndpoint();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Assert.Skip("Live LLM endpoint not configured.");
            return;
        }

        if (!await IsEndpointReachableAsync(endpoint))
        {
            Assert.Skip($"Configured endpoint '{endpoint}' is currently unreachable.");
            return;
        }

        var service = CreateLiveAssistantService(endpoint, GetLiveApiKey(), GetLiveModel());
        var models = await service.GetModelCapabilitiesAsync(endpoint, GetLiveApiKey(), includeLocal: false);
        var visionModel = models.FirstOrDefault(m => m.SupportsVision)?.Id ?? GetLiveModel();

        // 1x1 red PNG base64
        var samplePngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

        var request = new AiChatRequest(
            Messages: new List<AiChatMessageItem>
            {
                new()
                {
                    Role = "user",
                    Content = "What color is this single pixel image? Respond in one word.",
                    Attachments = new List<AiChatMessageAttachment>
                    {
                        new()
                        {
                            FileName = "pixel.png",
                            ContentType = "image/png",
                            Base64Data = samplePngBase64
                        }
                    }
                }
            },
            Model: visionModel
        );

        var response = await service.SendChatAsync(request);
        Assert.True(response.Success);
        Assert.NotNull(response.Message);
        Assert.False(string.IsNullOrWhiteSpace(response.Message.Content));
    }

    [Fact]
    public async Task LiveEndpoint_MultimodalToolCall_ExecutesSuccessfully_WhenReachable()
    {
        var endpoint = GetLiveEndpoint();
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            Assert.Skip("Live LLM endpoint not configured.");
            return;
        }

        if (!await IsEndpointReachableAsync(endpoint))
        {
            Assert.Skip($"Configured endpoint '{endpoint}' is currently unreachable.");
            return;
        }

        var service = CreateLiveAssistantService(endpoint, GetLiveApiKey(), GetLiveModel());
        var models = await service.GetModelCapabilitiesAsync(endpoint, GetLiveApiKey(), includeLocal: false);
        var visionModel = models.FirstOrDefault(m => m.SupportsVision && m.SupportsFunctionCalling)?.Id ?? GetLiveModel();

        var samplePngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

        var request = new AiChatRequest(
            Messages: new List<AiChatMessageItem>
            {
                new()
                {
                    Role = "user",
                    Content = "Here is an image. Query live VRAM telemetry and tell me how much VRAM is free.",
                    Attachments = new List<AiChatMessageAttachment>
                    {
                        new()
                        {
                            FileName = "status.png",
                            ContentType = "image/png",
                            Base64Data = samplePngBase64
                        }
                    }
                }
            },
            Model: visionModel
        );

        var response = await service.SendChatAsync(request);
        Assert.True(response.Success);
        Assert.NotNull(response.Message);
        Assert.True(response.Message.HasToolCalls);
    }
}
