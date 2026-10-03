using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views.Controls;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ForgeProviderTests
{
    private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private const string SampleForgeProgressJson = """
    {
      "progress": 0.48,
      "eta_relative": 12.5,
      "state": {
        "skipped": false,
        "interrupted": false,
        "job": "txt2img",
        "job_count": 1,
        "job_timestamp": "20261003120000",
        "job_no": 0,
        "sampling_step": 12,
        "sampling_steps": 25
      },
      "current_image": null,
      "textinfo": "Sampling step 12/25"
    }
    """;

    private const string SampleSdModelsResponseJson = """
    [
      {
        "title": "sd_xl_base_1.0.safetensors [31e35c80fc]",
        "model_name": "sd_xl_base_1.0",
        "hash": "31e35c80fc",
        "sha256": "31e35c80fc4829d14f90153f4ef74c30f9d87fa240de4f99ab0cb67d79934f82",
        "filename": "C:\\AI\\Forge\\models\\Stable-diffusion\\sd_xl_base_1.0.safetensors",
        "config": null
      },
      {
        "title": "v1-5-pruned-emaonly.safetensors [6ce0161609]",
        "model_name": "v1-5-pruned-emaonly",
        "hash": "6ce0161609",
        "sha256": "6ce0161609b8c70ec2d137f5688f4ced209671a4204d7332e6d44f36e4060ec8",
        "filename": "C:\\AI\\Forge\\models\\Stable-diffusion\\v1-5-pruned-emaonly.safetensors",
        "config": null
      }
    ]
    """;

    private MainViewModel CreateMainViewModel(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var telemetryMock = new Mock<ITelemetryService>();
        var ollamaMock = new Mock<IOllamaModelService>();
        var hfMock = new Mock<IHuggingFaceSearchService>();
        var civitaiMock = new Mock<ICivitaiSearchService>();

        var vm = new MainViewModel(
            httpClient,
            telemetryMock.Object,
            ollamaMock.Object,
            hfMock.Object,
            civitaiMock.Object,
            new StudioPresetService(),
            new CanIRunItService()
        );
        return vm;
    }

    [Fact]
    public async Task Txt2Img_WithDefaultPromptAndAspectRatio_DispatchesCorrectPayloadAndDecodesImage()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                capturedRequest = req;
                if (req.Content != null)
                {
                    capturedBody = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                }

                if (req.RequestUri != null && req.RequestUri.AbsolutePath.Contains("/sdapi/v1/txt2img"))
                {
                    var responseJson = $$"""{"images": ["{{TinyPngBase64}}"]}""";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                    });
                }

                // Default fallback (e.g. progress polling)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"progress\": 0.0}", Encoding.UTF8, "application/json")
                });
            });

        var vm = CreateMainViewModel(handlerMock.Object);
        vm.SelectModality("Image");
        vm.ActiveAspectPreset = "16:9";
        vm.StudioSteps = 28;
        vm.StudioCfgScale = 7.5;
        vm.StudioSeed = 424242;
        vm.PromptText = "Cyberpunk street market in rainy neo-tokyo";
        vm.NegativePromptText = "lowres, blurry, bad anatomy";

        await vm.GenerateStudioImageAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Contains("/sdapi/v1/txt2img", capturedRequest.RequestUri?.AbsolutePath);

        Assert.NotNull(capturedBody);
        var json = JsonNode.Parse(capturedBody);
        Assert.NotNull(json);

        // Validate payload parameters against Forge specifications
        Assert.Equal("Cyberpunk street market in rainy neo-tokyo", json["prompt"]?.GetValue<string>());
        Assert.Equal("lowres, blurry, bad anatomy", json["negative_prompt"]?.GetValue<string>());
        Assert.Equal(1344, json["width"]?.GetValue<int>());
        Assert.Equal(768, json["height"]?.GetValue<int>());
        Assert.Equal(28, json["steps"]?.GetValue<int>());
        Assert.Equal(7.5, json["cfg_scale"]?.GetValue<double>());
        Assert.Equal(424242, json["seed"]?.GetValue<long>());

        // Validate output decoding
        Assert.NotNull(vm.StudioGeneratedImageBytes);
        Assert.True(vm.StudioGeneratedImageBytes.Length > 0);
        Assert.True(vm.HasStudioGeneratedImage);
        Assert.False(vm.ShowImageCanvasZeroState);
        Assert.Equal("Generation complete!", vm.StudioImageGenerationStatus);
        Assert.True(vm.IsForgeOnline);
    }

    [Fact]
    public async Task Img2Img_WithAttachedImage_DispatchesCorrectPayloadWithInitImagesAndDenoising()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                capturedRequest = req;
                if (req.Content != null)
                {
                    capturedBody = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                }

                if (req.RequestUri != null && req.RequestUri.AbsolutePath.Contains("/sdapi/v1/img2img"))
                {
                    var responseJson = $$"""{"images": ["{{TinyPngBase64}}"]}""";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"progress\": 0.0}", Encoding.UTF8, "application/json")
                });
            });

        var vm = CreateMainViewModel(handlerMock.Object);
        vm.SelectModality("Image");
        vm.ActiveAspectPreset = "1:1";
        vm.AttachedImageBytes = new byte[] { 10, 20, 30, 40, 50 };
        vm.HasAttachedImage = true;
        vm.StudioDenoise = 0.55;
        vm.PromptText = "Transform into oil painting";

        await vm.GenerateStudioImageAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Contains("/sdapi/v1/img2img", capturedRequest.RequestUri?.AbsolutePath);

        Assert.NotNull(capturedBody);
        var json = JsonNode.Parse(capturedBody);
        Assert.NotNull(json);

        // Verify init_images array contains the base64 string
        var initImages = json["init_images"]?.AsArray();
        Assert.NotNull(initImages);
        Assert.Single(initImages);
        Assert.Equal(Convert.ToBase64String(vm.AttachedImageBytes), initImages[0]?.GetValue<string>());

        // Verify denoising strength and dimensions
        Assert.Equal(0.55, json["denoising_strength"]?.GetValue<double>());
        Assert.Equal(1024, json["width"]?.GetValue<int>());
        Assert.Equal(1024, json["height"]?.GetValue<int>());

        Assert.NotNull(vm.StudioGeneratedImageBytes);
        Assert.Equal("Generation complete!", vm.StudioImageGenerationStatus);
    }

    [Theory]
    [InlineData("1:1", 1024, 1024)]
    [InlineData("16:9", 1344, 768)]
    [InlineData("9:16", 768, 1344)]
    [InlineData("4:3", 1152, 864)]
    public async Task AspectPresets_CalculatesCorrectResolutions_ForForgePayloads(string preset, int expectedWidth, int expectedHeight)
    {
        string? capturedBody = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                if (req.Content != null) capturedBody = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{"images": ["{{TinyPngBase64}}"]}""", Encoding.UTF8, "application/json")
                });
            });

        var vm = CreateMainViewModel(handlerMock.Object);
        vm.SelectAspectPreset(preset);
        vm.PromptText = "Resolution test prompt";

        await vm.GenerateStudioImageAsync();

        Assert.NotNull(capturedBody);
        var json = JsonNode.Parse(capturedBody);
        Assert.Equal(expectedWidth, json?["width"]?.GetValue<int>());
        Assert.Equal(expectedHeight, json?["height"]?.GetValue<int>());
    }

    [Fact]
    public async Task ProgressPolling_StateUpdates_TracksSamplingSteps()
    {
        int progressCallCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                if (req.RequestUri != null && req.RequestUri.AbsolutePath.Contains("/sdapi/v1/progress"))
                {
                    progressCallCount++;
                    var prog = Math.Min(1.0, progressCallCount * 0.35);
                    var step = (int)(prog * 25);
                    var json = $$"""
                    {
                      "progress": {{prog}},
                      "state": {
                        "sampling_step": {{step}},
                        "sampling_steps": 25
                      }
                    }
                    """;
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                }

                // Simulate brief generation delay so background progress task can poll
                await Task.Delay(400, ct);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{"images": ["{{TinyPngBase64}}"]}""", Encoding.UTF8, "application/json")
                };
            });

        var vm = CreateMainViewModel(handlerMock.Object);
        vm.PromptText = "Progress test prompt";

        await vm.GenerateStudioImageAsync();

        Assert.True(progressCallCount >= 1, "Background progress polling should have triggered at least once.");
        Assert.Equal("Generation complete!", vm.StudioImageGenerationStatus);
        Assert.Equal(1.0, vm.StudioImageGenerationProgress);
    }

    [Fact]
    public async Task SdModelsDiscovery_AndOptionsSwitching_SendsCheckpointOverride()
    {
        string? capturedBody = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                if (req.Content != null) capturedBody = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{"images": ["{{TinyPngBase64}}"]}""", Encoding.UTF8, "application/json")
                });
            });

        var vm = CreateMainViewModel(handlerMock.Object);
        // Select custom safetensors checkpoint
        vm.SelectStudioModel("juggernautXL_v9.safetensors");
        vm.PromptText = "Testing model checkpoint override";

        await vm.GenerateStudioImageAsync();

        Assert.NotNull(capturedBody);
        var json = JsonNode.Parse(capturedBody);
        Assert.NotNull(json);

        // Verify override_settings.sd_model_checkpoint is included
        var overrideSettings = json["override_settings"];
        Assert.NotNull(overrideSettings);
        Assert.Equal("juggernautXL_v9.safetensors", overrideSettings["sd_model_checkpoint"]?.GetValue<string>());
    }

    [Fact]
    public async Task OfflineHandling_HttpRequestException_SetsOfflineStatusAndShowsWarning()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused (Forge not running)"));

        var vm = CreateMainViewModel(handlerMock.Object);
        vm.SelectModality("Image");
        vm.PromptText = "Should fail gracefully";

        await vm.GenerateStudioImageAsync();

        Assert.False(vm.IsForgeOnline);
        Assert.True(vm.ShowForgeOfflineWarning);
        Assert.Contains("offline or unreachable", vm.StudioImageGenerationStatus);
        Assert.Null(vm.StudioGeneratedImageBytes);
    }

    [Fact]
    public void ProactiveWarning_ModalitySwitch_UpdatesWarningVisibility()
    {
        var vm = new EngineStudioViewModel();
        vm.IsForgeOnline = false;

        // Image modality -> warning should show
        vm.SelectModality("Image");
        Assert.True(vm.ShowForgeOfflineWarning);

        // Text modality -> warning should not show
        vm.SelectModality("Text");
        Assert.False(vm.ShowForgeOfflineWarning);

        // Image modality again, then mark online -> warning should hide
        vm.SelectModality("Image");
        Assert.True(vm.ShowForgeOfflineWarning);
        vm.IsForgeOnline = true;
        Assert.False(vm.ShowForgeOfflineWarning);
    }

    [Fact]
    public async Task StartForgeEngineCommand_TriggersToggleEngineAndUpdatesStatus()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"started\"}", Encoding.UTF8, "application/json")
            });

        var vm = CreateMainViewModel(handlerMock.Object);
        vm.IsForgeOnline = false;
        Assert.True(vm.ShowForgeOfflineWarning);

        // Simulate start
        vm.Telemetry.ForgeStatus = "Online";
        await vm.StartForgeEngineCommand.ExecuteAsync(null);

        Assert.True(vm.IsForgeOnline);
        Assert.False(vm.ShowForgeOfflineWarning);
    }

    [AvaloniaFact]
    public void HeadlessUI_EngineStudioTabControl_RendersProactiveWarningAndDockPill()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"images": ["{{TinyPngBase64}}"]}""", Encoding.UTF8, "application/json")
            });

        var vm = CreateMainViewModel(handlerMock.Object);
        vm.SelectModality("Image");
        vm.IsForgeOnline = false;

        var control = new EngineStudioTabControl { DataContext = vm };
        var window = new Window { Content = control, Width = 1280, Height = 900 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 1. Verify proactive warning is visible in visual tree
        var textBlocks = control.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(textBlocks, t => t.Text != null && t.Text.Contains("Stable Diffusion Forge is Offline"));
        Assert.Contains(textBlocks, t => t.Text != null && t.Text.Contains("Forge Offline"));

        // 2. Find the 1-Click "Start Forge" button
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var startForgeBtn = buttons.FirstOrDefault(b => b.Content != null && b.Content.ToString()!.Contains("Start Forge"));
        Assert.NotNull(startForgeBtn);

        // 3. Mark Forge as online, and verify warning disappears
        vm.IsForgeOnline = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var offlineBanners = control.GetVisualDescendants()
            .OfType<Border>()
            .Where(b => b.IsVisible && b.DataContext == vm)
            .ToList();

        Assert.False(vm.ShowForgeOfflineWarning);

        window.Close();
    }
}
