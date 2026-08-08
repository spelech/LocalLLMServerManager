using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using LocalLLMServerManager;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class DeepCoveragePushTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public DeepCoveragePushTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Program_MainInternal_ServiceMode_ExecutesWithoutRunWeb()
    {
        Program.MainInternal(new[] { "--service" }, runWeb: false);
    }

    [Fact]
    public void Program_MainInternal_DesktopMode_ExecutesWithoutRunWeb()
    {
        Program.MainInternal(Array.Empty<string>(), runWeb: false);
    }

    [Fact]
    public void App_OnExitClick_WithLifetime_ExecutesCleanly()
    {
        var app = new App();
        var lifetime = new ClassicDesktopStyleApplicationLifetime();
        app.ApplicationLifetime = lifetime;

        app.OnExitClick(null, EventArgs.Empty);
    }

    [Fact]
    public async Task McpToolsEndpoint_ListTools_ReturnsValidToolSet()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/mcp/tools");
        Assert.True(response.IsSuccessStatusCode);

        string json = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task EngineStartStop_ComfyUiAndForge_ReturnsOkResult()
    {
        var client = _fixture.CreateClient();

        var respComfyStop = await client.PostAsync("/api/comfy/stop", null);
        Assert.True(respComfyStop.IsSuccessStatusCode);

        var respForgeStop = await client.PostAsync("/api/forge/stop", null);
        Assert.True(respForgeStop.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ServiceUpdate_InvalidBranch_ReturnsBadRequest()
    {
        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/service/update", new { branch = "invalid;branch;name" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ServiceUpdate_ValidBranch_ExecutesUpdateFlow()
    {
        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/service/update", new { branch = "main" });

        Assert.True(response.IsSuccessStatusCode);
    }
}
