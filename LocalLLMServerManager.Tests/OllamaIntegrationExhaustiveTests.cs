using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class OllamaIntegrationExhaustiveTests
{
    private static HttpClient CreateMockClient(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage req, CancellationToken token) => handlerFunc(req));

        return new HttpClient(handlerMock.Object);
    }

    [Fact]
    public async Task PullModelAsync_ValidNDJsonStream_UpdatesProgressAndStatusLog()
    {
        string ndjson = "{\"status\":\"pulling manifest\"}\n" +
                        "{\"status\":\"downloading layer\",\"completed\":500000000,\"total\":1000000000}\n" +
                        "{\"status\":\"verifying sha256\"}\n" +
                        "{\"status\":\"success\"}\n";

        var client = CreateMockClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ndjson, Encoding.UTF8, "application/x-ndjson")
        });

        var serviceMock = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(serviceMock.Object);

        await vm.PullModelAsync("qwen2.5-coder:7b", client);

        Assert.Equal("qwen2.5-coder:7b", vm.PullModelName);
        Assert.True(vm.PullProgressPercent >= 50.0);
        Assert.Contains("success", vm.PullStatusLog);
        Assert.Contains("476.8 MB / 953.7 MB", vm.PullProgressBytesText);
    }

    [Fact]
    public async Task PullModelAsync_ZeroOrMissingTotalBytes_DoesNotDivideByZero()
    {
        string ndjson = "{\"status\":\"pulling layer\",\"completed\":100,\"total\":0}\n" +
                        "{\"status\":\"verifying\"}\n";

        var client = CreateMockClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(ndjson, Encoding.UTF8, "application/x-ndjson")
        });

        var serviceMock = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(serviceMock.Object);

        await vm.PullModelAsync("tiny-model", client);

        Assert.Equal("tiny-model", vm.PullModelName);
        Assert.Contains("verifying", vm.PullStatusLog);
    }

    [Fact]
    public async Task PullModelAsync_NetworkExceptionMidStream_HandlesGracefully()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection closed abruptly"));

        var client = new HttpClient(handlerMock.Object);
        var serviceMock = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(serviceMock.Object);

        await vm.PullModelAsync("faulty-model", client);

        Assert.Contains("Error: Connection closed abruptly", vm.PullStatusLog);
    }

    [Fact]
    public async Task PullModelAsync_EmptyPullString_ReturnsImmediately()
    {
        var serviceMock = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(serviceMock.Object);

        await vm.PullModelAsync("", new HttpClient());
        Assert.False(vm.IsPullDrawerOpen);
    }

    [Fact]
    public async Task UnloadAllVramAsync_MultipleModelsLoaded_SendsKeepAliveZeroToAll()
    {
        int evictionCount = 0;
        var client = CreateMockClient(req =>
        {
            var uri = req.RequestUri?.ToString() ?? "";
            if (uri.Contains("/api/ollama/ps") || uri.Contains("/api/ps"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"models\":[{\"name\":\"llama3:8b\"},{\"name\":\"mistral:7b\"}]}")
                };
            }
            if (uri.Contains("/api/generate"))
            {
                Interlocked.Increment(ref evictionCount);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"done\":true}")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new OllamaModelService();
        var success = await service.UnloadAllVramAsync("http://127.0.0.1:5246", client);

        Assert.True(success);
        Assert.Equal(2, evictionCount);
    }

    [Fact]
    public async Task UnloadAllVramAsync_NoModelsLoaded_ReturnsTrueWithZeroEvictions()
    {
        int evictionCount = 0;
        var client = CreateMockClient(req =>
        {
            var uri = req.RequestUri?.ToString() ?? "";
            if (uri.Contains("/api/ollama/ps") || uri.Contains("/api/ps"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"models\":[]}")
                };
            }
            if (uri.Contains("/api/generate"))
            {
                Interlocked.Increment(ref evictionCount);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new OllamaModelService();
        var success = await service.UnloadAllVramAsync("http://127.0.0.1:5246", client);

        Assert.True(success);
        Assert.Equal(0, evictionCount);
    }

    [Fact]
    public async Task UnloadAllVramAsync_ProxyFails_FallsBackToDirectOllamaPs()
    {
        var client = CreateMockClient(req =>
        {
            var uri = req.RequestUri?.ToString() ?? "";
            if (uri.Contains("5246/api/ollama/ps"))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
            if (uri.Contains("11434/api/ps"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"models\":[{\"name\":\"fallback-model\"}]}")
                };
            }
            if (uri.Contains("/api/generate"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new OllamaModelService();
        var success = await service.UnloadAllVramAsync("http://127.0.0.1:5246", client);

        Assert.True(success);
    }

    [Fact]
    public async Task LoadInstalledModelsAsync_ProxySuccess_ParsesSizesAndBadges()
    {
        string modelsPayload = "{\"models\":[" +
                               "{\"name\":\"deepseek-r1:8b\",\"size\":4900000000}," +
                               "{\"name\":\"math-stral:7b\",\"size\":4100000000}," +
                               "{\"name\":\"generic-coder:latest\",\"size\":0}" +
                               "]}";

        var client = CreateMockClient(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(modelsPayload)
        });

        var service = new OllamaModelService();
        var models = await service.LoadInstalledModelsAsync("http://127.0.0.1:5246", client);

        Assert.Equal(3, models.Count);
        Assert.Equal("deepseek-r1:8b", models[0].Name);
        Assert.Contains("Reasoning", models[0].CapabilityTag);
        Assert.Equal("#A855F7", models[0].CapabilityColor);

        Assert.Equal("math-stral:7b", models[1].Name);
        Assert.Contains("Mathematics", models[1].CapabilityTag);
        Assert.Equal("#C084FC", models[1].CapabilityColor);

        Assert.Equal("generic-coder:latest", models[2].Name);
        Assert.Equal("N/A", models[2].FormatSize);
    }

    [Fact]
    public async Task LoadInstalledModelsAsync_ProxyThrows_FallsBackToDirectTags()
    {
        var client = CreateMockClient(req =>
        {
            var uri = req.RequestUri?.ToString() ?? "";
            if (uri.Contains("5246/api/models"))
            {
                throw new HttpRequestException("Server down");
            }
            if (uri.Contains("11434/api/tags"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"models\":[{\"name\":\"direct-tag-model:latest\",\"size\":2147483648}]}")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new OllamaModelService();
        var models = await service.LoadInstalledModelsAsync("http://127.0.0.1:5246", client);

        Assert.Single(models);
        Assert.Equal("direct-tag-model:latest", models[0].Name);
        Assert.Equal("2 GB", models[0].FormatSize);
    }

    [Fact]
    public async Task PreloadModelAsync_SuccessAndFailure_ReturnsExpectedBooleans()
    {
        var client = CreateMockClient(req =>
        {
            var uri = req.RequestUri?.ToString() ?? "";
            if (uri.Contains("11434/api/generate"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var service = new OllamaModelService();
        var ok = await service.PreloadModelAsync("http://127.0.0.1:5246", "phi4:latest", client);
        Assert.True(ok);

        var failClient = CreateMockClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var fail = await service.PreloadModelAsync("http://127.0.0.1:5246", "phi4:latest", failClient);
        Assert.False(fail);

        var throwClient = CreateMockClient(_ => throw new HttpRequestException("Network failure"));
        var throwRes = await service.PreloadModelAsync("http://127.0.0.1:5246", "phi4:latest", throwClient);
        Assert.False(throwRes);
    }

    [Theory]
    [InlineData(0, "~0 MB")]
    [InlineData(2048, "~128 MB")]
    [InlineData(8192, "~512 MB")]
    [InlineData(16384, "~1.0 GB")]
    [InlineData(32768, "~2.0 GB")]
    [InlineData(131072, "~8.0 GB")]
    public void TargetContextTokens_EstimatesKvCache_Correctly(double tokens, string expectedText)
    {
        var serviceMock = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(serviceMock.Object);

        vm.TargetContextTokens = -1;
        vm.TargetContextTokens = tokens;
        Assert.Equal(expectedText, vm.EstimatedKvCacheText);
    }

    [Fact]
    public async Task ViewModel_LoadInstalledModelsAsync_PopulatesObservableCollection()
    {
        var serviceMock = new Mock<IOllamaModelService>();
        serviceMock.Setup(s => s.LoadInstalledModelsAsync(It.IsAny<string>(), It.IsAny<HttpClient>()))
            .ReturnsAsync(new List<OllamaModelItem>
            {
                new("llama3:8b", "4.7 GB", "Coding", "#38BDF8", false),
                new("mistral:7b", "4.1 GB", "General", "#38BDF8", true)
            });

        var vm = new OllamaLibraryViewModel(serviceMock.Object);
        await vm.LoadInstalledModelsAsync("http://127.0.0.1:5246", new HttpClient());

        Assert.Equal(2, vm.InstalledModels.Count);
        Assert.Equal("llama3:8b", vm.InstalledModels[0].Name);
        Assert.Equal("mistral:7b", vm.InstalledModels[1].Name);
    }

    [Fact]
    public void ViewModel_ClosePullDrawer_SetsDrawerOpenFalse()
    {
        var serviceMock = new Mock<IOllamaModelService>();
        var vm = new OllamaLibraryViewModel(serviceMock.Object)
        {
            IsPullDrawerOpen = true
        };

        vm.ClosePullDrawer();
        Assert.False(vm.IsPullDrawerOpen);
    }
}
