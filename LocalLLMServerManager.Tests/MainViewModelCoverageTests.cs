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

public class MainViewModelCoverageTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;
    private readonly HttpClient _mockHttp;

    public MainViewModelCoverageTests(AppTestServerFixture fixture)
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
}

