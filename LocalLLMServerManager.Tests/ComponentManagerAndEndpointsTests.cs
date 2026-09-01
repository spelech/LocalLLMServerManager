using System.Net;
using System.Net.Http.Json;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Models;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ComponentManagerAndEndpointsTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public ComponentManagerAndEndpointsTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetComponents_ReturnsExpectedFeaturePacks()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/components");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var components = await response.Content.ReadFromJsonAsync<List<ComponentPackInfo>>();
        Assert.NotNull(components);
        Assert.Equal(3, components.Count);

        var videoPack = components.FirstOrDefault(c => c.Id == "video-generation");
        Assert.NotNull(videoPack);
        Assert.Equal("ComfyUI Video Generation Pack", videoPack.Name);
        Assert.Equal("14.2 GB", videoPack.DiskSizeEstimate);

        var audioPack = components.FirstOrDefault(c => c.Id == "audio-tts");
        Assert.NotNull(audioPack);
        Assert.Equal("Kokoro TTS & Audio Engine", audioPack.Name);
        Assert.Equal("350 MB", audioPack.DiskSizeEstimate);

        var musicPack = components.FirstOrDefault(c => c.Id == "audio-music");
        Assert.NotNull(musicPack);
        Assert.Equal("MusicGen & Stable Audio Studio", musicPack.Name);
        Assert.Equal("6.8 GB", musicPack.DiskSizeEstimate);
    }

    [Fact]
    public async Task InstallAndUninstallComponent_Succeeds()
    {
        var client = _fixture.CreateClient();

        // Install request
        var installReq = new ComponentInstallRequest { ComponentId = "audio-tts" };
        var installResponse = await client.PostAsJsonAsync("/api/components/install", installReq);
        Assert.Equal(HttpStatusCode.OK, installResponse.StatusCode);

        // Uninstall request
        var uninstallReq = new ComponentInstallRequest { ComponentId = "audio-tts" };
        var uninstallResponse = await client.PostAsJsonAsync("/api/components/uninstall", uninstallReq);
        Assert.Equal(HttpStatusCode.OK, uninstallResponse.StatusCode);
    }

    [Fact]
    public async Task UninstallInvalidComponent_ReturnsBadRequest()
    {
        var client = _fixture.CreateClient();

        var uninstallReq = new ComponentInstallRequest { ComponentId = "non-existent-pack" };
        var uninstallResponse = await client.PostAsJsonAsync("/api/components/uninstall", uninstallReq);
        Assert.Equal(HttpStatusCode.BadRequest, uninstallResponse.StatusCode);
    }

    [Fact]
    public async Task SettingsViewModel_RefreshComponentStatusesAsync_UpdatesInstalledFlags()
    {
        var jsonResponse = @"[
            { ""id"": ""video-generation"", ""name"": ""Video"", ""isInstalled"": true },
            { ""id"": ""audio-tts"", ""name"": ""Audio"", ""isInstalled"": true }
        ]";

        var handlerMock = new Moq.Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                Moq.Protected.ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("/api/components")),
                Moq.Protected.ItExpr.IsAny<System.Threading.CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(handlerMock.Object);
        var vm = new LocalLLMServerManager.Shared.ViewModels.SettingsViewModel();

        Assert.False(vm.IsVideoPackInstalled);
        Assert.False(vm.IsAudioPackInstalled);

        await vm.RefreshComponentStatusesAsync("http://localhost:5246", client);

        Assert.True(vm.IsVideoPackInstalled);
        Assert.True(vm.IsAudioPackInstalled);
        Assert.Equal("🟢 Installed", vm.VideoPackStatusText);
        Assert.Equal("🟢 Installed", vm.AudioPackStatusText);
    }
}
