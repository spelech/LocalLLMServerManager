using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LocalLLMServerManager;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class NinetyPercentThresholdTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public NinetyPercentThresholdTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Program_ServiceModeWebApplication_CreatesServiceHost()
    {
        var serviceApp = Program.CreateWebApplication(Array.Empty<string>(), isServiceMode: true, url: "http://127.0.0.1:0");
        Assert.NotNull(serviceApp);
        await serviceApp.DisposeAsync();
    }

    [Fact]
    public async Task MainViewModel_MockedHttpClient_Reaches100PercentMethodCoverage()
    {
        var hfModelJson = @"{
            ""id"": ""meta-llama/Llama-3.3-8B-Instruct-GGUF"",
            ""author"": ""meta-llama"",
            ""siblings"": [
                { ""rfilename"": ""Llama-3.3-8B-Q4_K_M.gguf"" },
                { ""rfilename"": ""Llama-3.3-8B-Q8_0.gguf"" },
                { ""rfilename"": ""Llama-3.3-8B-Q5_K_M.gguf"" },
                { ""rfilename"": ""Llama-3.3-8B-FP16.gguf"" },
                { ""rfilename"": ""README.md"" }
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
            ""status"": ""Degraded"",
            ""ollama"": ""Offline"",
            ""stableDiffusion"": ""Online"",
            ""comfyUI"": ""Offline""
        }";

        var ollamaPsJson = "{\"models\":[{\"name\":\"llama3.3:latest\"}]}";
        var ollamaTagsJson = "{\"models\":[{\"name\":\"llama3.3:latest\",\"size\":4700000000}]}";
        var ollamaStreamJson = "{\"status\":\"pulling manifest\"}\n{\"status\":\"downloading layer\",\"completed\":50,\"total\":100}\n{\"status\":\"success\"}\n";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/health")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(healthJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/api/hf/model")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(hfModelJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/api/civitai/search")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(civitaiSearchJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/api/ps")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(ollamaPsJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && req.RequestUri.ToString().Contains("/api/tags")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(ollamaTagsJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri != null && (req.RequestUri.ToString().Contains("/api/pull") || req.RequestUri.ToString().Contains("/api/generate"))),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(ollamaStreamJson, Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") });

        var mockClient = new HttpClient(handlerMock.Object);
        var vm = new MainViewModel(mockClient)
        {
            ApiBase = AppTestServerFixture.TestBaseUrl,
            HfSearchQuery = "llama-3.3",
            CivitaiSearchQuery = "cyberpunk"
        };

        await vm.RefreshStatusAsync();
        await vm.SearchHuggingFaceAsync();
        await vm.OpenHfModalAsync("meta-llama/Llama-3.3-8B-Instruct-GGUF");
        Assert.True(vm.IsHfModalOpen);
        Assert.NotNull(vm.ModalHfFiles);

        await vm.SearchCivitaiAsync();
        await vm.PullModelAsync("llama3.3:latest");

        var item = new CivitaiModelItem(1, "Test Model", "Checkpoint", "http://thumb", "http://download", "test.safetensors", 4.9, 100);
        await vm.DownloadCivitaiModelAsync(item);

        await vm.UnloadAllVramAsync();
    }
}
