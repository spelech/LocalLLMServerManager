using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AiAssistantServiceTests
{
    private readonly Mock<ISettingsService> _mockSettings = new();
    private readonly Mock<IPromptManagementService> _mockPromptService = new();
    private readonly Mock<IAiAppTools> _mockAppTools = new();
    private readonly Mock<IHttpClientFactory> _mockHttpFactory = new();

    private AiAssistantService CreateService(HttpMessageHandler? handler = null, AppSettings? customSettings = null)
    {
        var httpHandler = handler ?? new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":[{\"id\":\"google/gemini-2.5-flash\"}]}");
        var client = new HttpClient(httpHandler);
        _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        _mockSettings.Setup(s => s.LoadSettings()).Returns(customSettings ?? new AppSettings(
            AiAssistantEndpoint: "http://127.0.0.1:4000/v1",
            AiAssistantApiKey: "test-key-123",
            AiAssistantModel: "google/gemini-2.5-flash"
        ));

        _mockPromptService.Setup(p => p.BuildFullSystemPromptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("System instructions");

        return new AiAssistantService(
            _mockSettings.Object,
            _mockPromptService.Object,
            _mockAppTools.Object,
            _mockHttpFactory.Object
        );
    }

    [Fact]
    public async Task ValidateConnection_EmptyEndpoint_ReturnsFailure()
    {
        var service = CreateService(customSettings: new AppSettings(AiAssistantEndpoint: ""));

        var result = await service.ValidateConnectionAsync(endpoint: "");

        Assert.False(result.Success);
        Assert.Contains("Endpoint URL is required", result.Message);
    }

    [Fact]
    public async Task ValidateConnection_SuccessfulResponse_ParsesModelsAndLatency()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":[{\"id\":\"google/gemini-2.5-flash\"},{\"id\":\"vertex/gemini-1.5-flash\"}]}");
        var service = CreateService(handler);

        var result = await service.ValidateConnectionAsync(
            endpoint: "http://127.0.0.1:4000/v1",
            apiKey: "valid-key",
            model: null
        );

        Assert.True(result.Success);
        Assert.Equal(2, result.AvailableModels.Count);
        Assert.Contains("google/gemini-2.5-flash", result.AvailableModels);
    }

    [Fact]
    public async Task ValidateConnection_ServerError_ReturnsFormattedErrorMessage()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Invalid API key provided\"}}");
        var service = CreateService(handler);

        var result = await service.ValidateConnectionAsync(
            endpoint: "http://127.0.0.1:4000/v1",
            apiKey: "bad-key",
            model: "google/gemini-2.5-flash"
        );

        Assert.False(result.Success);
        Assert.Contains("401", result.Message);
        Assert.Contains("Invalid API key", result.Message);
    }

    [Fact]
    public void BuildAiFunctions_RegistersAll12AppBridgeTools()
    {
        var service = CreateService();
        var functions = service.BuildAiFunctions();

        Assert.NotNull(functions);
        Assert.Equal(12, functions.Count);

        var names = new HashSet<string>();
        foreach (var f in functions)
        {
            names.Add(f.Name);
        }

        Assert.Contains("get_gpu_vram_telemetry", names);
        Assert.Contains("check_services_health", names);
        Assert.Contains("list_installed_models", names);
        Assert.Contains("start_ai_engine", names);
        Assert.Contains("stop_ai_engine", names);
        Assert.Contains("unload_vram", names);
        Assert.Contains("get_app_settings", names);
        Assert.Contains("update_app_setting", names);
        Assert.Contains("calculate_hardware_fit", names);
        Assert.Contains("generate_image", names);
        Assert.Contains("synthesize_speech", names);
        Assert.Contains("query_app_documentation", names);
    }

    [Fact]
    public async Task SendChat_UnreachableEndpoint_ReturnsFailureMessage()
    {
        var service = CreateService();

        var request = new AiChatRequest(new List<AiChatMessageItem>
        {
            new() { Role = "user", Content = "Hello copilot" }
        });

        var response = await service.SendChatAsync(request);

        Assert.False(response.Success);
        Assert.False(string.IsNullOrWhiteSpace(response.Error));
    }

    [Fact]
    public async Task SendChat_WithImageAttachment_HandlesMultimodalMessage()
    {
        var service = CreateService();

        var request = new AiChatRequest(new List<AiChatMessageItem>
        {
            new()
            {
                Role = "user",
                Content = "Look at this",
                Attachments = new List<AiChatMessageAttachment>
                {
                    new() { FileName = "pic.png", ContentType = "image/png", Base64Data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=" }
                }
            }
        });

        var response = await service.SendChatAsync(request);

        Assert.False(response.Success);
        Assert.False(string.IsNullOrWhiteSpace(response.Error));
    }

    [Fact]
    public void AiModelCapabilityInfo_FormattingAndProperties_AreValid()
    {
        var model = new AiModelCapabilityInfo(
            Id: "vertex_ai/gemini-2.5-flash",
            DisplayName: "Gemini 2.5 Flash",
            Provider: "vertex_ai",
            Mode: "chat",
            SupportsVision: true,
            SupportsFunctionCalling: true,
            MaxInputTokens: 1000000,
            MaxOutputTokens: 8192,
            IsLocal: false
        );

        Assert.Equal("vertex_ai/gemini-2.5-flash", model.Id);
        Assert.True(model.SupportsVision);
        Assert.True(model.SupportsFunctionCalling);
        Assert.False(model.IsLocal);
        Assert.Contains("👁️", model.SummaryBadge);
        Assert.Contains("⚡", model.SummaryBadge);
        Assert.Contains("1M", model.SummaryBadge);
        Assert.Contains("[vertex_ai]", model.SummaryBadge);
    }

    [Fact]
    public void AiModelCapabilityInfo_SummaryBadge_FormatsDifferentTokenCountsAndCapabilities()
    {
        var modelKilo = new AiModelCapabilityInfo("ollama/llama3", "Llama 3", "ollama", MaxInputTokens: 8192, SupportsVision: false, SupportsFunctionCalling: false);
        Assert.DoesNotContain("👁️", modelKilo.SummaryBadge);
        Assert.DoesNotContain("⚡", modelKilo.SummaryBadge);
        Assert.Contains("8k", modelKilo.SummaryBadge);
        Assert.Contains("[ollama]", modelKilo.SummaryBadge);

        var modelSmall = new AiModelCapabilityInfo("local/tiny", "Tiny", "local", MaxInputTokens: 500, SupportsVision: false, SupportsFunctionCalling: false);
        Assert.Contains("500", modelSmall.SummaryBadge);

        var modelNone = new AiModelCapabilityInfo("unknown/model", "Model", "unknown", MaxInputTokens: null, SupportsVision: false, SupportsFunctionCalling: false);
        Assert.Equal("[unknown]", modelNone.SummaryBadge);
    }

    [Fact]
    public void AiChatMessageItem_WithAttachments_ReportsHasAttachmentsTrue()
    {
        var defaultItem = new AiChatMessageItem();
        Assert.NotNull(defaultItem.Attachments);
        Assert.Empty(defaultItem.Attachments);
        Assert.False(defaultItem.HasAttachments);

        var msg = new AiChatMessageItem
        {
            Role = "user",
            Content = "Inspect this image",
            Attachments = new List<AiChatMessageAttachment>
            {
                new() { FileName = "test.png", ContentType = "image/png", Base64Data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=" }
            }
        };

        Assert.True(msg.HasAttachments);
        Assert.Single(msg.Attachments);
        Assert.Equal("image/png", msg.Attachments[0].ContentType);
        Assert.Equal("test.png", msg.Attachments[0].FileName);
        Assert.False(string.IsNullOrWhiteSpace(msg.Attachments[0].Id));
    }

    [Fact]
    public void ParseModelCapabilities_ExcludesLocalModels_WhenIncludeLocalIsFalse()
    {
        var sampleLiteLlmJson = """
        {
          "data": [
            {
              "id": "vertex_ai/gemini-2.5-flash",
              "model_info": {
                "mode": "chat",
                "litellm_provider": "vertex_ai",
                "supports_vision": true,
                "supports_function_calling": true,
                "max_input_tokens": 1048576,
                "max_output_tokens": 8192
              }
            },
            {
              "id": "openai/gpt-4o",
              "model_info": {
                "mode": "chat",
                "litellm_provider": "openai",
                "supports_vision": true,
                "supports_function_calling": true,
                "max_tokens": 128000
              }
            },
            {
              "id": "ollama/llama3.2:latest",
              "model_info": {
                "mode": "chat",
                "litellm_provider": "ollama",
                "supports_vision": false,
                "supports_function_calling": true
              }
            },
            {
              "id": "local/mistral-7b",
              "model_info": {
                "mode": "chat",
                "litellm_provider": "local"
              }
            }
          ]
        }
        """;

        var models = AiAssistantService.ParseModelCapabilitiesFromJson(sampleLiteLlmJson, includeLocal: false);

        Assert.Equal(2, models.Count);
        Assert.Contains(models, m => m.Id == "vertex_ai/gemini-2.5-flash" && m.SupportsVision && m.MaxInputTokens == 1048576);
        Assert.Contains(models, m => m.Id == "openai/gpt-4o" && m.SupportsVision && m.MaxInputTokens == 128000);
        Assert.DoesNotContain(models, m => m.Id.StartsWith("ollama/"));
        Assert.DoesNotContain(models, m => m.Id.StartsWith("local/"));
    }

    [Fact]
    public void ParseModelCapabilities_IncludesLocalModels_WhenIncludeLocalIsTrue()
    {
        var sampleLiteLlmJson = """
        {
          "data": [
            {
              "id": "vertex_ai/gemini-2.5-flash",
              "model_info": { "litellm_provider": "vertex_ai" }
            },
            {
              "id": "ollama/llama3.2",
              "model_info": { "litellm_provider": "ollama" }
            }
          ]
        }
        """;

        var models = AiAssistantService.ParseModelCapabilitiesFromJson(sampleLiteLlmJson, includeLocal: true);

        Assert.Equal(2, models.Count);
        Assert.Contains(models, m => m.Id == "ollama/llama3.2" && m.IsLocal);
    }

    [Theory]
    [InlineData("custom-model", "ollama", true)]
    [InlineData("custom-model", "local", true)]
    [InlineData("custom-model", "llama.cpp", true)]
    [InlineData("custom-model", "vllm_local", true)]
    [InlineData("ollama/llama3.2", null, true)]
    [InlineData("local/mistral", null, true)]
    [InlineData("llama/model-7b", null, true)]
    [InlineData("ollama_chat/qwen", null, true)]
    [InlineData("vertex_ai/gemini-2.5-flash", "vertex_ai", false)]
    [InlineData("openai/gpt-4o", "openai", false)]
    [InlineData("anthropic/claude-3-5-sonnet", "anthropic", false)]
    public void IsLocalModel_DetectsExpectedLocalPatterns(string id, string? provider, bool expectedLocal)
    {
        var isLocal = AiAssistantService.IsLocalModel(id, provider);
        Assert.Equal(expectedLocal, isLocal);
    }

    [Fact]
    public void ParseModelCapabilities_SparseJson_AppliesHeuristics()
    {
        var sparseJson = """
        {
          "data": [
            { "id": "google/gemini-2.5-flash" },
            { "id": "openai/gpt-4o" },
            { "id": "anthropic/claude-3-opus" }
          ]
        }
        """;

        var models = AiAssistantService.ParseModelCapabilitiesFromJson(sparseJson, includeLocal: false);
        Assert.Equal(3, models.Count);
        Assert.All(models, m => Assert.True(m.SupportsVision));
        Assert.All(models, m => Assert.True(m.SupportsFunctionCalling));
    }

    [Fact]
    public async Task GetModelCapabilitiesAsync_WhenModelInfoReturns404_FallsBackToModelsEndpoint()
    {
        var handler = new RoutingHttpMessageHandler(req =>
        {
            if (req.RequestUri != null && (req.RequestUri.AbsolutePath.EndsWith("/model/info") || req.RequestUri.AbsolutePath.EndsWith("/v1/model_info")))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"data\":[{\"id\":\"openai/gpt-4o\"}]}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var service = CreateService(handler);
        var capabilities = await service.GetModelCapabilitiesAsync("http://127.0.0.1:4000/v1");

        Assert.Single(capabilities);
        Assert.Equal("openai/gpt-4o", capabilities[0].Id);
        Assert.True(capabilities[0].SupportsVision);
    }

    [Fact]
    public async Task GetModelCapabilitiesAsync_WhenPrimaryModelInfoReturns404_FallsBackToV1ModelInfo()
    {
        var handler = new RoutingHttpMessageHandler(req =>
        {
            if (req.RequestUri != null && req.RequestUri.AbsolutePath.EndsWith("/model/info"))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            if (req.RequestUri != null && req.RequestUri.AbsolutePath.EndsWith("/v1/model_info"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"data\":[{\"id\":\"from-v1-model-info\",\"model_info\":{\"max_tokens\":128000.0}}]}", System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = CreateService(handler);
        var capabilities = await service.GetModelCapabilitiesAsync("http://127.0.0.1:4000/v1");

        Assert.Single(capabilities);
        Assert.Equal("from-v1-model-info", capabilities[0].Id);
        Assert.Equal(128000, capabilities[0].MaxInputTokens);
    }

    [Fact]
    public void ParseModelCapabilities_FloatingPointTokenValues_ParsesSuccessfully()
    {
        var json = """
        {
          "data": [
            {
              "id": "openai/gpt-4o",
              "model_info": {
                "max_input_tokens": 128000.0,
                "max_output_tokens": 4096.0
              }
            },
            {
              "id": "google/gemini-2.5-pro",
              "max_tokens": 2000000.0
            }
          ]
        }
        """;

        var models = AiAssistantService.ParseModelCapabilitiesFromJson(json);
        Assert.Equal(2, models.Count);
        Assert.Equal(128000, models[0].MaxInputTokens);
        Assert.Equal(4096, models[0].MaxOutputTokens);
        Assert.Equal(2000000, models[1].MaxInputTokens);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ReturnsExtractedModelIds()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{\"data\":[{\"id\":\"vertex_ai/gemini-2.5-flash\"},{\"id\":\"ollama/llama3.2\"}]}");
        var service = CreateService(handler);

        var models = await service.GetAvailableModelsAsync();

        Assert.Single(models);
        Assert.Equal("vertex_ai/gemini-2.5-flash", models[0]);
    }

    [Fact]
    public async Task GetModelCapabilitiesAsync_EmptyEndpoint_ThrowsInvalidOperationException()
    {
        var service = CreateService(customSettings: new AppSettings(AiAssistantEndpoint: ""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetModelCapabilitiesAsync(endpoint: ""));
    }

    [Fact]
    public void BuildExtensionsAiChatMessage_WithImageAttachment_CreatesMultimodalChatMessage()
    {
        var item = new AiChatMessageItem
        {
            Role = "user",
            Content = "What is in this diagram?",
            Attachments = new List<AiChatMessageAttachment>
            {
                new()
                {
                    FileName = "diagram.png",
                    ContentType = "image/png",
                    Base64Data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
                }
            }
        };

        var chatMessage = AiAssistantService.ToExtensionsAiChatMessage(item);

        Assert.Equal(Microsoft.Extensions.AI.ChatRole.User, chatMessage.Role);
        Assert.Equal(2, chatMessage.Contents.Count);
        Assert.IsType<Microsoft.Extensions.AI.TextContent>(chatMessage.Contents[0]);
        Assert.IsType<Microsoft.Extensions.AI.ImageContent>(chatMessage.Contents[1]);
    }

    [Fact]
    public void BuildExtensionsAiChatMessage_WithoutAttachments_ReturnsSimpleChatMessage()
    {
        var item = new AiChatMessageItem
        {
            Role = "assistant",
            Content = "I can help with that."
        };

        var chatMessage = AiAssistantService.ToExtensionsAiChatMessage(item);

        Assert.Equal(Microsoft.Extensions.AI.ChatRole.Assistant, chatMessage.Role);
        Assert.Equal("I can help with that.", chatMessage.Text);
    }

    [Fact]
    public void BuildExtensionsAiChatMessage_WithRawBytesAttachment_CreatesImageContentWithBytes()
    {
        var raw = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var item = new AiChatMessageItem
        {
            Role = "user",
            Content = "Raw image test",
            Attachments = new List<AiChatMessageAttachment>
            {
                new()
                {
                    FileName = "raw.png",
                    ContentType = "image/png",
                    RawBytes = raw
                }
            }
        };

        var chatMessage = AiAssistantService.ToExtensionsAiChatMessage(item);

        Assert.Equal(2, chatMessage.Contents.Count);
        var imgContent = Assert.IsType<Microsoft.Extensions.AI.ImageContent>(chatMessage.Contents[1]);
        Assert.Equal("image/png", imgContent.MediaType);
    }

    [Fact]
    public void BuildExtensionsAiChatMessage_WithDataUriPrefix_CreatesImageContentWithUri()
    {
        var item = new AiChatMessageItem
        {
            Role = "user",
            Content = "",
            Attachments = new List<AiChatMessageAttachment>
            {
                new()
                {
                    FileName = "datauri.png",
                    ContentType = "image/png",
                    Base64Data = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
                }
            }
        };

        var chatMessage = AiAssistantService.ToExtensionsAiChatMessage(item);

        Assert.Single(chatMessage.Contents);
        var imgContent = Assert.IsType<Microsoft.Extensions.AI.ImageContent>(chatMessage.Contents[0]);
        Assert.NotNull(imgContent.Uri);
        Assert.StartsWith("data:image/png;base64,", imgContent.Uri);
    }


    private class RoutingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public RoutingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}


