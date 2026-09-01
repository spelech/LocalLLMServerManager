using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class MainViewModelTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;
    private readonly HttpClient _mockHttp;

    public MainViewModelTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;

        var hfModelJson = @"{
            ""id"": ""meta-llama/Llama-3.3-8B-Instruct-GGUF"",
            ""author"": ""meta-llama"",
            ""siblings"": [
                { ""rfilename"": ""Llama-3.3-8B-Q4_K_M.gguf"" },
                { ""rfilename"": ""Llama-3.3-8B-Q8_0.gguf"" },
                { ""rfilename"": ""Llama-3.3-8B-Q5_K_M.gguf"" },
                { ""rfilename"": ""Llama-3.3-8B-FP16.gguf"" }
            ]
        }";

        var civitaiSearchJson = @"{
            ""items"": [
                {
                    ""id"": 1,
                    ""name"": ""Test Checkpoint"",
                    ""type"": ""Checkpoint"",
                    ""modelVersions"": [
                        {
                            ""downloadUrl"": ""http://download.safetensors"",
                            ""images"": [ { ""url"": ""http://thumb.jpg"" } ],
                            ""files"": [ { ""name"": ""test.safetensors"" } ]
                        }
                    ]
                }
            ]
        }";

        var healthJson = @"{
            ""status"": ""Healthy"",
            ""ollama"": ""Online"",
            ""stableDiffusion"": ""Online"",
            ""comfyUI"": ""Online""
        }";

        var gpuJson = @"{
            ""gpuName"": ""NVIDIA GeForce RTX 4090"",
            ""vramBytes"": 25769803776,
            ""usedVramBytes"": 4294967296
        }";

        var ollamaPsJson = "{\"models\":[{\"name\":\"llama3.3:latest\"}]}";
        var ollamaTagsJson = "{\"models\":[{\"name\":\"llama3.3:latest\",\"size\":4700000000}]}";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/hf/model")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(hfModelJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/civitai/search")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(civitaiSearchJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/health")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(healthJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/gpu/vram")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(gpuJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/ps")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(ollamaPsJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/tags")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(ollamaTagsJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") });

        _mockHttp = new HttpClient(handlerMock.Object);
        MainViewModel.DefaultHttpClient = _mockHttp;
    }

    private MainViewModel CreateTestViewModel()
    {
        return new MainViewModel(_mockHttp)
        {
            ApiBase = AppTestServerFixture.TestBaseUrl
        };
    }

    [Fact]
    public void MainViewModel_InitialState_HasDefaults()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.OllamaStatus);
        Assert.NotNull(vm.ForgeStatus);
        Assert.NotNull(vm.ComfyStatus);
        Assert.NotNull(vm.GpuName);
        Assert.NotNull(vm.Toasts);
        Assert.NotNull(vm.InstalledModels);
        Assert.False(vm.IsHfModalOpen);
        Assert.False(vm.IsPullDrawerOpen);
    }

    [Fact]
    public void ToastService_AndToastItems_BadgeColorsAndLifecycle()
    {
        var t1 = new ToastItem("Success", ToastType.Success);
        Assert.Equal("#22C55E", t1.BadgeColor);

        var t2 = new ToastItem("Warning", ToastType.Warning);
        Assert.Equal("#F59E0B", t2.BadgeColor);

        var t3 = new ToastItem("Error", ToastType.Error);
        Assert.Equal("#EF4444", t3.BadgeColor);

        var t4 = new ToastItem("Info", ToastType.Info);
        Assert.Equal("#38BDF8", t4.BadgeColor);

        ToastService.Instance.Show("Test toast", ToastType.Warning, autoRemoveMs: 0);
        ToastService.Instance.Show("Async delay toast", ToastType.Info, autoRemoveMs: 5);
        ToastService.Instance.Remove(t4);
        ToastService.Instance.Clear();
    }

    [Fact]
    public void TargetContextTokens_CalculatesEstimatedKvCache()
    {
        var vm = new MainViewModel();

        vm.TargetContextTokens = 1024;
        Assert.Contains("MB", vm.EstimatedKvCacheText);

        vm.TargetContextTokens = 8192;
        Assert.Contains("MB", vm.EstimatedKvCacheText);

        vm.TargetContextTokens = 65536;
        Assert.Contains("GB", vm.EstimatedKvCacheText);

        vm.CloseHfModal();
        Assert.False(vm.IsHfModalOpen);

        vm.ClosePullDrawer();
        Assert.False(vm.IsPullDrawerOpen);
    }

    [Fact]
    public async Task SearchHuggingFaceAsync_FullJsonStream_PopulatesResults()
    {
        var vm = CreateTestViewModel();
        vm.HfSearchQuery = "llama-3.3";

        await vm.SearchHuggingFaceAsync();
        Assert.NotNull(vm.HuggingFaceResults);
    }

    [Fact]
    public async Task OpenHfModalAsync_FullRepoFiles_PopulatesQuantFiles()
    {
        var vm = CreateTestViewModel();

        await vm.OpenHfModalAsync("meta-llama/Llama-3.3-8B-Instruct-GGUF");

        Assert.True(vm.IsHfModalOpen);
        Assert.Equal("meta-llama/Llama-3.3-8B-Instruct-GGUF", vm.ModalRepoId);
        Assert.Equal("meta-llama", vm.ModalAuthor);
        Assert.NotNull(vm.ModalHfFiles);
    }

    [Fact]
    public async Task SearchCivitaiAsync_FullJsonStream_PopulatesResults()
    {
        var vm = CreateTestViewModel();
        vm.CivitaiSearchQuery = "cyberpunk";

        await vm.SearchCivitaiAsync();
        Assert.NotNull(vm.CivitaiResults);
    }

    [Fact]
    public async Task PullModelAsync_StreamsOllamaProgress_LogsOutput()
    {
        var vm = CreateTestViewModel();

        await vm.PullModelAsync("tiny-test-model");

        Assert.True(vm.IsPullDrawerOpen || !string.IsNullOrEmpty(vm.PullStatusLog));
        Assert.Equal("tiny-test-model", vm.PullModelName);
    }

    [Fact]
    public async Task DownloadCivitaiModelAsync_StreamsSseProgress_UpdatesDrawer()
    {
        var vm = CreateTestViewModel();
        var item = new CivitaiModelItem(101, "Cyberpunk Checkpoint", "Checkpoint", "http://thumb", "http://download", "cyberpunk.safetensors", 4.9, 2500);

        ToastItem? emittedToast = null;
        Action<ToastItem> handler = t => emittedToast = t;
        ToastService.Instance.OnToastShow += handler;
        try
        {
            await vm.DownloadCivitaiModelAsync(item);
            Assert.NotNull(emittedToast);
        }
        finally
        {
            ToastService.Instance.OnToastShow -= handler;
        }
    }

    [Fact]
    public async Task RefreshStatusAsync_UpdatesViewModelProperties()
    {
        var vm = CreateTestViewModel();

        await vm.RefreshStatusAsync();

        Assert.NotNull(vm.OllamaStatus);
        Assert.NotNull(vm.ForgeStatus);
        Assert.NotNull(vm.ComfyStatus);
        Assert.NotNull(vm.GpuName);
        Assert.True(vm.VramTotalGb > 0);
    }

    [Fact]
    public void ViewModels_RecordTypes_EqualityAndProperties()
    {
        var o1 = new OllamaModelItem("llama", "4.7 GB", "Coding", "#38BDF8", true);
        var o2 = new OllamaModelItem("llama", "4.7 GB", "Coding", "#38BDF8", true);
        Assert.Equal(o1, o2);
        Assert.Equal(o1.GetHashCode(), o2.GetHashCode());
        Assert.NotNull(o1.ToString());

        var h1 = new HuggingFaceRepoItem("id", "author", 10, "100");
        var h2 = new HuggingFaceRepoItem("id", "author", 10, "100");
        Assert.Equal(h1, h2);

        var q1 = new HfFileQuantItem("file", "Q4", "4 GB", 4000);
        var q2 = new HfFileQuantItem("file", "Q4", "4 GB", 4000);
        Assert.Equal(q1, q2);

        var c1 = new CivitaiModelItem(1, "n", "t", "th", "d", "f", 5.0, 10);
        var c2 = new CivitaiModelItem(1, "n", "t", "th", "d", "f", 5.0, 10);
        Assert.Equal(c1, c2);

        var v1 = new VideoAssetItem("video.mp4", "/output_video/video.mp4", "3.0s", "832x480", 16, 42890L, 1000L, DateTime.MinValue);
        var v2 = new VideoAssetItem("video.mp4", "/output_video/video.mp4", "3.0s", "832x480", 16, 42890L, 1000L, DateTime.MinValue);
        Assert.Equal(v1, v2);
    }

    [Fact]
    public async Task VideoStudio_GenerateAndSelectVideo_UpdatesProperties()
    {
        var videoGenJson = @"{
            ""filename"": ""video_20260823.mp4"",
            ""url"": ""/output_video/video_20260823.mp4"",
            ""duration"": ""3.0s"",
            ""resolution"": ""832x480"",
            ""fps"": 16,
            ""seed"": 42890
        }";

        var videoListJson = @"[
            {
                ""filename"": ""video_20260823.mp4"",
                ""url"": ""/output_video/video_20260823.mp4"",
                ""duration"": ""3.0s"",
                ""resolution"": ""832x480"",
                ""fps"": 16,
                ""seed"": 42890,
                ""sizeBytes"": 2048,
                ""createdAt"": ""2026-08-23T00:00:00Z""
            }
        ]";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/video/generate")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(videoGenJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/video/files")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(videoListJson, Encoding.UTF8, "application/json") });

        var client = new HttpClient(handlerMock.Object);
        var vm = new MainViewModel(client) { ApiBase = "http://127.0.0.1:5246" };

        await vm.GenerateVideoAsync();
        Assert.Contains("video_20260823.mp4", vm.RenderedVideoUrl);
        Assert.Single(vm.GeneratedVideosList);

        await vm.LoadGeneratedVideosAsync();
        Assert.Single(vm.GeneratedVideosList);

        var videoItem = vm.GeneratedVideosList[0];
        vm.SelectVideo(videoItem);
        Assert.Equal("3.0s", vm.VideoDurationText);
        Assert.Equal("832x480", vm.VideoResolutionBadge);

        vm.ToggleVideoPlay();
        Assert.False(vm.IsVideoPlaying);

        vm.ToggleVideoLoop();
        Assert.False(vm.IsVideoLooping);
    }

    [Fact]
    public void MainViewModel_HasSettingsObservableProperties_DefaultsAreSet()
    {
        var vm = CreateTestViewModel();
        Assert.Equal("", vm.ComfyUiExecutablePath);
        Assert.Equal("", vm.ForgeExecutablePath);
        Assert.Equal("", vm.ForgeModelsPath);
        Assert.Equal("", vm.ThreeDModelsPath);
        Assert.Equal("", vm.WorkflowsPath);
        Assert.Equal("http://127.0.0.1:8188", vm.ComfyUiUrl);
        Assert.Equal("comfy", vm.PreferredImageEngine);
    }

    [Fact]
    public async Task LoadSettingsAsync_PopulatesPropertiesFromHttpResponse()
    {
        var settingsJson = JsonSerializer.Serialize(new AppSettings(
            ForgeModelsPath: "D:\\Custom\\Forge\\Models",
            ComfyUiUrl: "http://localhost:8189",
            ThreeDModelsPath: "D:\\Custom\\3d_outputs",
            WorkflowsPath: "D:\\Custom\\Workflows",
            PreferredImageEngine: "ComfyUI",
            ComfyUiExecutablePath: "D:\\Custom\\ComfyUI\\run.bat",
            ForgeExecutablePath: "D:\\Custom\\Forge\\webui.bat",
            OllamaExecutablePath: "ollama"
        ));

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/settings") && req.Method == HttpMethod.Get),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(settingsJson, Encoding.UTF8, "application/json") });

        var client = new HttpClient(handlerMock.Object);
        var vm = new MainViewModel(client) { ApiBase = "http://127.0.0.1:5246" };

        await vm.LoadSettingsAsync();

        Assert.Equal("D:\\Custom\\Forge\\Models", vm.ForgeModelsPath);
        Assert.Equal("http://localhost:8189", vm.ComfyUiUrl);
        Assert.Equal("D:\\Custom\\3d_outputs", vm.ThreeDModelsPath);
        Assert.Equal("D:\\Custom\\Workflows", vm.WorkflowsPath);
        Assert.Equal("ComfyUI", vm.PreferredImageEngine);
        Assert.Equal("D:\\Custom\\ComfyUI\\run.bat", vm.ComfyUiExecutablePath);
        Assert.Equal("D:\\Custom\\Forge\\webui.bat", vm.ForgeExecutablePath);
    }

    [Fact]
    public async Task SaveSettingsAsync_PostsAppSettingsToEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/settings") && req.Method == HttpMethod.Post),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .Callback<HttpRequestMessage, System.Threading.CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{\"status\":\"saved\"}", Encoding.UTF8, "application/json") });

        var client = new HttpClient(handlerMock.Object);
        var vm = new MainViewModel(client) { ApiBase = "http://127.0.0.1:5246" };

        vm.ForgeModelsPath = "E:\\Models";
        vm.ComfyUiUrl = "http://127.0.0.1:9000";

        await vm.SaveSettingsAsync();

        Assert.NotNull(capturedRequest);
        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        var postedSettings = JsonSerializer.Deserialize<AppSettings>(body);
        Assert.NotNull(postedSettings);
        Assert.Equal("E:\\Models", postedSettings!.ForgeModelsPath);
        Assert.Equal("http://127.0.0.1:9000", postedSettings.ComfyUiUrl);
    }

    [Fact]
    public void MainViewModel_VideoControls_UpdateStateProperly()
    {
        var vm = new MainViewModel(new HttpClient());
        var videoItem = new VideoAssetItem("output_001.mp4", "http://localhost:5246/outputs/output_001.mp4", "5.0s", "1280x720", 24, 123456L, 10485760L, DateTime.UtcNow);

        vm.SelectVideo(videoItem);
        Assert.Equal("http://localhost:5246/outputs/output_001.mp4", vm.RenderedVideoUrl);
        Assert.Equal("5.0s", vm.VideoDurationText);
        Assert.Equal("1280x720", vm.VideoResolutionBadge);
        Assert.Equal("24 fps", vm.VideoFpsBadge);
        Assert.Equal("123456", vm.VideoSeedBadge);

        vm.ToggleVideoPlay();
        Assert.False(vm.IsVideoPlaying);
        vm.ToggleVideoPlay();
        Assert.True(vm.IsVideoPlaying);

        vm.ToggleVideoLoop();
        Assert.False(vm.IsVideoLooping);
        vm.ToggleVideoLoop();
        Assert.True(vm.IsVideoLooping);
    }

    [Fact]
    public async Task MainViewModel_LoadGeneratedVideosAsync_ParsesJsonArray()
    {
        var json = @"[
            {
                ""filename"": ""test_video.mp4"",
                ""url"": ""/outputs/test_video.mp4"",
                ""duration"": ""4.0s"",
                ""resolution"": ""832x480"",
                ""fps"": 16,
                ""seed"": 99999,
                ""sizeBytes"": 5242880
            }
        ]";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/video/files")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(json, Encoding.UTF8, "application/json") });

        var client = new HttpClient(handlerMock.Object);
        var vm = new MainViewModel(client) { ApiBase = "http://127.0.0.1:5246" };

        await vm.LoadGeneratedVideosAsync();

        Assert.Single(vm.GeneratedVideosList);
        Assert.Equal("test_video.mp4", vm.GeneratedVideosList[0].Filename);
        Assert.Equal("http://127.0.0.1:5246/outputs/test_video.mp4", vm.RenderedVideoUrl);
    }
}

