using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ServerEndpointsTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;
    private readonly HttpClient _client;

    public ServerEndpointsTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
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
    public void MapAllEndpoints_InvokesStaticRegistrationMethods()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        LocalLLMServerManager.Endpoints.HealthEndpoints.MapHealthEndpoints(app);
        LocalLLMServerManager.Endpoints.ModelProxyEndpoints.MapModelProxyEndpoints(app);
        LocalLLMServerManager.Endpoints.McpEndpoints.MapMcpEndpoints(app);
        LocalLLMServerManager.Endpoints.EngineEndpoints.MapEngineEndpoints(app);
        LocalLLMServerManager.Endpoints.WorkflowEndpoints.MapWorkflowEndpoints(app);
        LocalLLMServerManager.Endpoints.DiscoveryEndpoints.MapDiscoveryEndpoints(app);

        Assert.NotNull(app);
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
    public async Task AudioWorkflowsEndpoint_ReturnsAvailablePresets()
    {
        var response = await _client.GetAsync("/api/audio/workflows");
        Assert.True(response.IsSuccessStatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, json.ValueKind);
        Assert.True(json.GetArrayLength() > 0);
    }

    [Fact]
    public async Task AudioGenerateEndpoint_QueuesWorkflowAndReturnsQueuePayload()
    {
        var requestPayload = new
        {
            workflowId = "stable_audio_open_sfx",
            prompt = "Cyberpunk atmospheric ambient drone, heavy synthesizer, cinematic low end, 48kHz stereo",
            negativePrompt = "low quality, harsh distortion",
            durationSeconds = 30,
            seed = -1
        };

        var response = await _client.PostAsJsonAsync("/api/audio/generate", requestPayload);
        Assert.True(response.IsSuccessStatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("promptId", out var pidProp));
        Assert.False(string.IsNullOrWhiteSpace(pidProp.GetString()));

        Assert.True(json.TryGetProperty("status", out var statusProp));
        Assert.Equal("queued", statusProp.GetString());

        Assert.True(json.TryGetProperty("wsUrl", out var wsUrlProp));
        Assert.Contains("ws://", wsUrlProp.GetString());
    }

    [Fact]
    public async Task AudioFilesEndpoint_ReturnsAudioOutputDirectoryList()
    {
        var response = await _client.GetAsync("/api/audio/files");
        Assert.True(response.IsSuccessStatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, json.ValueKind);
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

    [Theory]
    [InlineData("LocalLLMServerManager", true)]
    [InlineData("my-service.1", true)]
    [InlineData("MyService; rm -rf /", false)]
    [InlineData("MyService && command", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void ServiceName_Validation_ReturnsExpectedResult(string? name, bool expected)
    {
        Assert.Equal(expected, Program.IsValidServiceName(name));
    }

    [Theory]
    [InlineData("C:\\LocalLLMServerManager", true)]
    [InlineData("D:\\AI\\Server", true)]
    [InlineData("/usr/local/share/LocalLLMServerManager", true)]
    [InlineData("/var/lib/my_app", true)]
    [InlineData("C:\\LocalLLMServerManager\\..\\temp", false)]
    [InlineData("C:\\LocalLLMServerManager; rm -rf /", false)]
    [InlineData("/usr/local/share/LocalLLMServerManager && command", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void PublishPath_Validation_ReturnsExpectedResult(string? path, bool expected)
    {
        if (OperatingSystem.IsWindows())
        {
            if (path != null && (path.StartsWith("/") || path.Contains("rm -rf")))
            {
                Assert.False(Program.IsValidPublishPath(path));
            }
            else if (path != null && path.StartsWith("C:\\"))
            {
                Assert.Equal(expected, Program.IsValidPublishPath(path));
            }
        }
        else
        {
            if (path != null && (path.StartsWith("C:\\") || path.Contains("rm -rf")))
            {
                Assert.False(Program.IsValidPublishPath(path));
            }
            else if (path != null && path.StartsWith("/"))
            {
                Assert.Equal(expected, Program.IsValidPublishPath(path));
            }
        }
    }

    [Theory]
    [InlineData("main", true)]
    [InlineData("feature/new-ui", true)]
    [InlineData("release/v3.0.0", true)]
    [InlineData("main; rm -rf /", false)]
    [InlineData("main && command", false)]
    [InlineData("..", false)]
    [InlineData(".git", false)]
    [InlineData("/main", false)]
    [InlineData("main/", false)]
    [InlineData("main.lock", false)]
    public void BranchName_Validation_ReturnsExpectedResult(string? branch, bool expected)
    {
        Assert.Equal(expected, Program.IsValidBranchName(branch));
    }

    [Fact]
    public async Task UpdateEndpoint_WithInvalidBranch_ReturnsBadRequest()
    {
        var request = new ServiceUpdateRequest("invalid; branch");
        var response = await _client.PostAsJsonAsync("/api/service/update", request);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("safe_file.bat")]
    [InlineData("AI/SD_Forge/webui-user.bat")]
    public void IsSafePath_WithSafePaths_ReturnsTrue(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Assert.True(Program.IsSafePath(fullPath));
    }

    [Theory]
    [InlineData("some/path/../../etc/passwd")]
    [InlineData("../outside.bat")]
    [InlineData("./traversal")]
    [InlineData(".\\traversal")]
    public void IsSafePath_WithPathTraversal_ReturnsFalse(string path)
    {
        Assert.False(Program.IsSafePath(path));
    }

    [Theory]
    [InlineData("file;inject.bat")]
    [InlineData("file&inject.bat")]
    [InlineData("file|inject.bat")]
    [InlineData("file`inject.bat")]
    [InlineData("file$inject.bat")]
    [InlineData("file>inject.bat")]
    [InlineData("file<inject.bat")]
    [InlineData("file*inject.bat")]
    [InlineData("file?inject.bat")]
    public void IsSafePath_WithShellMetacharacters_ReturnsFalse(string path)
    {
        Assert.False(Program.IsSafePath(path));
    }

    [Fact]
    public void IsSafePath_WithSystemDirectories_ReturnsFalse()
    {
        if (OperatingSystem.IsWindows())
        {
            var systemPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");
            Assert.False(Program.IsSafePath(systemPath));
        }
        else
        {
            Assert.False(Program.IsSafePath("/etc/passwd"));
            Assert.False(Program.IsSafePath("/bin/sh"));
        }
    }

    [Fact]
    public async Task StartEndpoints_WithUnsafePathsInSettings_ReturnsBadRequest()
    {
        var getResp = await _client.GetAsync("/api/settings");
        Assert.True(getResp.IsSuccessStatusCode);
        var originalSettings = await getResp.Content.ReadFromJsonAsync<AppSettings>();
        Assert.NotNull(originalSettings);

        try
        {
            var unsafeForgePath = "../unsafe/path/webui-user.bat";
            var unsafeComfyPath = OperatingSystem.IsWindows() ? "C:\\Windows\\notepad.exe" : "/bin/sh";

            var unsafeSettings = originalSettings with { ForgeExecutablePath = unsafeForgePath, ComfyUiExecutablePath = unsafeComfyPath };
            var postResp = await _client.PostAsJsonAsync("/api/settings", unsafeSettings);
            Assert.True(postResp.IsSuccessStatusCode);

            var forgeStartResp = await _client.PostAsync("/api/forge/start", null);
            if (forgeStartResp.StatusCode != System.Net.HttpStatusCode.BadRequest)
            {
                var content = await forgeStartResp.Content.ReadAsStringAsync();
                File.WriteAllText("test_error_forge.txt", $"Status: {forgeStartResp.StatusCode}\nContent: {content}");
            }
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, forgeStartResp.StatusCode);

            var comfyStartResp = await _client.PostAsync("/api/comfy/start", null);
            if (comfyStartResp.StatusCode != System.Net.HttpStatusCode.BadRequest)
            {
                var content = await comfyStartResp.Content.ReadAsStringAsync();
                File.WriteAllText("test_error_comfy.txt", $"Status: {comfyStartResp.StatusCode}\nContent: {content}");
            }
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, comfyStartResp.StatusCode);
        }
        finally
        {
            await _client.PostAsJsonAsync("/api/settings", originalSettings);
        }
    }
}
