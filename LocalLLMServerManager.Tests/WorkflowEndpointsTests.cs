using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using LocalLLMServerManager.Endpoints;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class WorkflowEndpointsTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;
    private readonly HttpClient _client;

    public WorkflowEndpointsTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task GetVideoWorkflows_ReturnsPresetList()
    {
        var response = await _client.GetAsync("/api/video/workflows");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, json.ValueKind);

        var foundWanT2v = false;
        var foundWanI2v = false;
        var foundLtx = false;
        var foundHunyuan = false;

        foreach (var item in json.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetString();
                if (id == "wan2.2_t2v") foundWanT2v = true;
                if (id == "wan2.2_i2v") foundWanI2v = true;
                if (id == "ltx2.5_t2v") foundLtx = true;
                if (id == "hunyuanvideo1.5_t2v") foundHunyuan = true;
            }
        }

        Assert.True(foundWanT2v, "wan2.2_t2v workflow preset should be listed");
        Assert.True(foundWanI2v, "wan2.2_i2v workflow preset should be listed");
        Assert.True(foundLtx, "ltx2.5_t2v workflow preset should be listed");
        Assert.True(foundHunyuan, "hunyuanvideo1.5_t2v workflow preset should be listed");
    }

    [Fact]
    public async Task GenerateVideo_QueuesPrompt_AndReturnsResponse()
    {
        var request = new VideoGenerateRequest(
            WorkflowId: "wan2.2_t2v",
            Prompt: "Cinematic shot of a neon cyberpunk city at night, rain reflections, 4k",
            NegativePrompt: "blurry, low quality, distorted",
            Width: 832,
            Height: 480,
            Frames: 49,
            Fps: 16,
            Seed: 12345
        );

        var response = await _client.PostAsJsonAsync("/api/video/generate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("promptId", out var promptIdProp));
        Assert.False(string.IsNullOrWhiteSpace(promptIdProp.GetString()));

        Assert.True(json.TryGetProperty("status", out var statusProp));
        Assert.Equal("queued", statusProp.GetString());

        Assert.True(json.TryGetProperty("wsUrl", out var wsUrlProp));
        Assert.StartsWith("ws://", wsUrlProp.GetString());
    }

    [Fact]
    public async Task GenerateVideo_WithNonExistentWorkflow_ReturnsNotFound()
    {
        var request = new VideoGenerateRequest(
            WorkflowId: "non_existent_workflow_xyz_999",
            Prompt: "Test prompt"
        );

        var response = await _client.PostAsJsonAsync("/api/video/generate", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetVideoFiles_ReturnsVideoOutputsList()
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "output_video");
        Directory.CreateDirectory(outputDir);

        var dummyVideo = Path.Combine(outputDir, $"test_output_{Guid.NewGuid():N}.mp4");
        await File.WriteAllTextAsync(dummyVideo, "dummy video content");

        try
        {
            var response = await _client.GetAsync("/api/video/files");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, json.ValueKind);

            var foundDummy = false;
            foreach (var item in json.EnumerateArray())
            {
                if (item.TryGetProperty("filename", out var fnProp) && fnProp.GetString() == Path.GetFileName(dummyVideo))
                {
                    foundDummy = true;
                    Assert.True(item.TryGetProperty("url", out var urlProp));
                    Assert.Equal($"/output_video/{Path.GetFileName(dummyVideo)}", urlProp.GetString());
                    Assert.True(item.TryGetProperty("sizeBytes", out var sizeProp));
                    Assert.True(sizeProp.GetInt64() > 0);
                    break;
                }
            }

            Assert.True(foundDummy, "Dummy video file should be returned in video files list");
        }
        finally
        {
            if (File.Exists(dummyVideo))
            {
                File.Delete(dummyVideo);
            }
        }
    }
}
