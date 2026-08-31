using System;
using System.Net.Http;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class HttpHelperTests
{
    [Fact]
    public void FormatEndpoint_FormatsRelativeAndAbsoluteUrlsCorrectly()
    {
        Assert.Equal("/health", HttpHelper.FormatEndpoint("", "health"));
        Assert.Equal("/health", HttpHelper.FormatEndpoint("", "/health"));
        Assert.Equal("http://127.0.0.1:5246/health", HttpHelper.FormatEndpoint("http://127.0.0.1:5246", "health"));
        Assert.Equal("http://127.0.0.1:5246/health", HttpHelper.FormatEndpoint("http://127.0.0.1:5246/", "/health"));
    }

    [Fact]
    public void CreateClient_WithAbsoluteBase_ConfiguresBaseAddress()
    {
        var client = HttpHelper.CreateClient("https://localllms.wileyriley.com");
        Assert.NotNull(client.BaseAddress);
        Assert.Equal("https://localllms.wileyriley.com/", client.BaseAddress.ToString());
    }

    [Fact]
    public void MainViewModel_PropagatesApiBaseToAllSubViewModels()
    {
        var customHttp = new HttpClient { BaseAddress = new Uri("http://10.0.0.21:5246") };
        var vm = new MainViewModel(customHttp);

        Assert.Equal("http://10.0.0.21:5246", vm.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.Telemetry.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.Ollama.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.HuggingFace.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.Civitai.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.Settings.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.Audio.ApiBase);
    }
}
