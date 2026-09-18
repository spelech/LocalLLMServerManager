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
}
