using System.Net;
using System.Net.Http.Json;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Models;
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
        var client = _fixture.CreateClient();
        var vm = new LocalLLMServerManager.Shared.ViewModels.SettingsViewModel();

        Assert.False(vm.IsVideoPackInstalled);
        Assert.False(vm.IsAudioPackInstalled);

        await vm.RefreshComponentStatusesAsync("", client);

        Assert.True(vm.IsVideoPackInstalled);
        Assert.True(vm.IsAudioPackInstalled);
        Assert.Equal("🟢 Installed", vm.VideoPackStatusText);
        Assert.Equal("🟢 Installed", vm.AudioPackStatusText);
    }
}
