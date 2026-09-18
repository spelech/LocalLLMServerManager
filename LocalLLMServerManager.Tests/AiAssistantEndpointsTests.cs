using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalLLMServerManager.Shared.Models;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AiAssistantEndpointsTests : IClassFixture<AppTestServerFixture>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AiAssistantEndpointsTests(AppTestServerFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetAiStatus_Returns200AndStatusInformation()
    {
        var response = await _client.GetAsync("/api/ai/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("enabled", out _));
        Assert.True(root.TryGetProperty("isInstalled", out var isInstalledProp));
        Assert.True(isInstalledProp.GetBoolean());
        Assert.True(root.TryGetProperty("configured", out _));
        Assert.True(root.TryGetProperty("loadedPromptSections", out var sectionsProp));
        Assert.True(sectionsProp.GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetAiPrompts_Returns200AndPromptSections()
    {
        var response = await _client.GetAsync("/api/ai/prompts");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("promptsDirectory", out var dirProp));
        Assert.NotNull(dirProp.GetString());

        Assert.True(root.TryGetProperty("sections", out var secProp));
        Assert.True(secProp.TryGetProperty("system-prompt", out _));
        Assert.True(secProp.TryGetProperty("capabilities", out _));
        Assert.True(secProp.TryGetProperty("workflows", out _));
        Assert.True(secProp.TryGetProperty("app-control", out _));
    }

    [Fact]
    public async Task PostAiPromptsReload_Returns200AndSuccess()
    {
        var response = await _client.PostAsync("/api/ai/prompts/reload", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.GetProperty("sections").TryGetProperty("system-prompt", out _));
    }

    [Fact]
    public async Task PostAiValidate_InvalidEndpoint_ReturnsValidationResultWithFailure()
    {
        var payload = new
        {
            endpoint = "http://127.0.0.1:9999/v1",
            apiKey = "test-key",
            model = "gpt-4o"
        };

        var response = await _client.PostAsJsonAsync("/api/ai/validate", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AiValidationResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public async Task GetAiModels_InvalidEndpoint_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/ai/models?endpoint=http://invalid-endpoint-xyz-999:1234/v1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAiChat_EmptyMessages_ReturnsBadRequest()
    {
        var request = new AiChatRequest(new List<AiChatMessageItem>());
        var response = await _client.PostAsJsonAsync("/api/ai/chat", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAiChat_NullBody_ReturnsBadRequest()
    {
        var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/ai/chat", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAiChat_UnconfiguredEndpoint_ReturnsGracefulFailure()
    {
        var request = new AiChatRequest(new List<AiChatMessageItem>
        {
            new() { Role = "user", Content = "Hello AI" }
        });

        var response = await _client.PostAsJsonAsync("/api/ai/chat", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatResponse = await response.Content.ReadFromJsonAsync<AiChatResponse>(JsonOptions);
        Assert.NotNull(chatResponse);
        // Since endpoint is unconfigured in default settings, it should report failure gracefully
        Assert.False(chatResponse.Success);
        Assert.False(string.IsNullOrWhiteSpace(chatResponse.Error));
    }

    [Fact]
    public async Task PostAiChat_StreamMode_UnconfiguredEndpoint_StreamsFinalChunkWithError()
    {
        var request = new AiChatRequest(
            Messages: new List<AiChatMessageItem>
            {
                new() { Role = "user", Content = "Hello AI stream" }
            },
            Stream: true
        );

        var response = await _client.PostAsJsonAsync("/api/ai/chat", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var streamData = await response.Content.ReadAsStringAsync();
        Assert.Contains("data:", streamData);
        Assert.Contains("error", streamData, StringComparison.OrdinalIgnoreCase);
    }
}
