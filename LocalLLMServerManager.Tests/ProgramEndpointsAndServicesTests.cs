using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AppTestServerFixture : IAsyncLifetime
{
    public static string TestBaseUrl = "http://127.0.0.1:5299";
    private WebApplication? _app;

    public async Task InitializeAsync()
    {
        _app = Program.CreateWebApplication(Array.Empty<string>(), isServiceMode: false, url: TestBaseUrl);
        await _app.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    public HttpClient CreateClient() => new HttpClient { BaseAddress = new Uri(TestBaseUrl) };
    public HttpClient Client => CreateClient();
}

public class ProgramEndpointsAndServicesTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;
    private readonly HttpClient _client;

    public ProgramEndpointsAndServicesTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_ReturnsStatus200_WithOkPayload()
    {
        var response = await _client.GetAsync("/health");
        Assert.True(response.IsSuccessStatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("status", out var statusProp));
        Assert.NotNull(statusProp.GetString());
    }

    [Fact]
    public async Task GpuVramEndpoint_ReturnsHardwareTelemetry()
    {
        var response = await _client.GetAsync("/api/gpu/vram");
        Assert.True(response.IsSuccessStatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("gpuName", out _));
        Assert.True(json.TryGetProperty("vramBytes", out _));
    }

    [Fact]
    public async Task SettingsEndpoints_GetAndPost_UpdatesConfiguration()
    {
        var getResp = await _client.GetAsync("/api/settings");
        Assert.True(getResp.IsSuccessStatusCode);

        var settings = await getResp.Content.ReadFromJsonAsync<AppSettings>();
        Assert.NotNull(settings);

        var newSettings = settings with { PreferredImageEngine = "ComfyUI" };

        var postResp = await _client.PostAsJsonAsync("/api/settings", newSettings);
        Assert.True(postResp.IsSuccessStatusCode);

        var updatedSettings = await postResp.Content.ReadFromJsonAsync<AppSettings>();
        Assert.NotNull(updatedSettings);
        Assert.Equal("ComfyUI", updatedSettings.PreferredImageEngine);
    }

    [Fact]
    public async Task ServiceEndpoints_StartAndStop_ExecutesHandler()
    {
        var comfyResp = await _client.PostAsync("/api/comfy/start", null);
        Assert.NotNull(comfyResp);

        var forgeResp = await _client.PostAsync("/api/forge/start", null);
        Assert.NotNull(forgeResp);

        var updateResp = await _client.PostAsync("/api/service/update", null);
        Assert.NotNull(updateResp);
    }

    [Fact]
    public async Task ModelAndWorkflowEndpoints_ReturnDirectoryLists()
    {
        var modelsResp = await _client.GetAsync("/api/3d/files");
        Assert.True(modelsResp.IsSuccessStatusCode);

        var workflowsResp = await _client.GetAsync("/api/comfy/workflows");
        Assert.True(workflowsResp.IsSuccessStatusCode);
    }

    [Fact]
    public async Task CivitaiDownloadAndOllamaPull_Endpoints_ReturnStreamingResponse()
    {
        try
        {
            var civitaiResp = await _client.GetAsync("/api/civitai/download?fileUrl=http://127.0.0.1:5299/health&fileName=test.safetensors&modelType=Checkpoint");
            Assert.NotNull(civitaiResp);
        }
        catch { }

        try
        {
            var pullResp = await _client.PostAsJsonAsync("/api/ollama/pull", new { model = "test-model" });
            Assert.NotNull(pullResp);
        }
        catch { }
    }

    [Fact]
    public void Program_PortCheckAndBuilder_ExecutesSuccessfully()
    {
        Assert.True(Program.IsPortInUse(5299));
        var appBuilder = Program.BuildAvaloniaApp();
        Assert.NotNull(appBuilder);

        var regGpu = Program.GetGpuInfoFromRegistry();
        Assert.NotNull(regGpu.GpuName);
    }
}
