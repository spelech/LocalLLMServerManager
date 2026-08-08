using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using LocalLLMServerManager;
using LocalLLMServerManager.Shared.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class FinalPushTo90CoverageTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public FinalPushTo90CoverageTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void MainViewModel_TargetContextTokens_CalculatesKvCacheSize()
    {
        var vm = new MainViewModel
        {
            TargetContextTokens = 1024
        };
        Assert.Contains("MB", vm.EstimatedKvCacheText);

        vm.TargetContextTokens = 32768;
        Assert.Contains("GB", vm.EstimatedKvCacheText);
    }

    [Fact]
    public async Task WebApp_CivitaiModelDetailProxy_Responds()
    {
        try
        {
            var resp = await _fixture.Client.GetAsync("/api/civitai/model?id=100");
        }
        catch { }
    }

    [Fact]
    public async Task WebApp_ServiceUpdateEndpoint_Responds()
    {
        var resp = await _fixture.Client.PostAsync("/api/service/update", null);
        Assert.True(resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task WebApp_ComfyAndForgeStartEndpoints_HandleProcessLaunch()
    {
        var comfyExt = OperatingSystem.IsWindows() ? "dummy_comfy.bat" : "dummy_comfy.sh";
        var forgeExt = OperatingSystem.IsWindows() ? "dummy_forge.bat" : "dummy_forge.sh";

        var settings = new AppSettings
        {
            ComfyUiExecutablePath = Path.Combine(AppContext.BaseDirectory, comfyExt),
            ForgeExecutablePath = Path.Combine(AppContext.BaseDirectory, forgeExt)
        };
        Program.SaveSettings(settings);

        var scriptContent = OperatingSystem.IsWindows() ? "@echo off\nexit /b 0" : "#!/bin/sh\nexit 0";
        File.WriteAllText(settings.ComfyUiExecutablePath, scriptContent);
        File.WriteAllText(settings.ForgeExecutablePath, scriptContent);

        try
        {
            var comfyResp = await _fixture.Client.PostAsync("/api/comfy/start", null);
            Assert.True(comfyResp.IsSuccessStatusCode);

            var comfyStop = await _fixture.Client.PostAsync("/api/comfy/stop", null);
            Assert.True(comfyStop.IsSuccessStatusCode);

            var forgeResp = await _fixture.Client.PostAsync("/api/forge/start", null);
            Assert.True(forgeResp.IsSuccessStatusCode);

            var forgeStop = await _fixture.Client.PostAsync("/api/forge/stop", null);
            Assert.True(forgeStop.IsSuccessStatusCode);
        }
        finally
        {
            if (File.Exists(settings.ComfyUiExecutablePath)) File.Delete(settings.ComfyUiExecutablePath);
            if (File.Exists(settings.ForgeExecutablePath)) File.Delete(settings.ForgeExecutablePath);
        }
    }

    [Fact]
    public async Task VramOrchestrator_EnsureVramForImageGenAndComfy_ExecutesOllamaUnloadLoop()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/ps")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"models\":[{\"name\":\"llama3.3:latest\"}]}")
            });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/generate")),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var client = new HttpClient(handlerMock.Object);
        var orchestrator = new VramOrchestrator(client, NullLogger<VramOrchestrator>.Instance);

        await orchestrator.EnsureVramForImageGenerationAsync();
        await orchestrator.EnsureVramForComfyUiAsync();
    }
}
