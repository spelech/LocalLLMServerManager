using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StickerStudioViewModelTests
{
    private class FakeStickerService : IStickerGenerationService
    {
        public bool ShouldSucceed { get; set; } = true;
        public StickerGenerationRequest? LastRequest { get; private set; }

        public Task<StickerResult> GenerateStickerAsync(StickerGenerationRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            if (!ShouldSucceed)
            {
                return Task.FromResult(new StickerResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Engine offline"
                });
            }

            return Task.FromResult(new StickerResult
            {
                IsSuccess = true,
                OutputPngBytes = new byte[] { 1, 2, 3, 4 },
                Width = 1024,
                Height = 1024
            });
        }
    }

    [Fact]
    public void Presets_InitializedWithSixCoreStyles()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        Assert.Equal(6, vm.StylePresets.Count);
        Assert.NotNull(vm.SelectedStylePreset);
        Assert.Equal("die-cut-vinyl", vm.SelectedStylePreset.Id);
    }

    [Fact]
    public void BorderWidth_ClampedBetweenZeroAndTwentyFour()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        vm.BorderWidth = 50;
        Assert.Equal(24, vm.BorderWidth);
        vm.BorderWidth = -5;
        Assert.Equal(0, vm.BorderWidth);
    }

    [Fact]
    public async Task GenerateSticker_TransitionsThroughStagesToReady()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        vm.InputImagePath = "test.png";
        await vm.GenerateStickerCommand.ExecuteAsync(null);
        Assert.Equal(StickerPipelineStage.Ready, vm.CurrentStage);
        Assert.False(vm.IsGenerating);
        Assert.NotNull(vm.GeneratedPngBytes);
    }

    [Fact]
    public async Task GenerateSticker_TransitionsThroughAllStagesInOrder()
    {
        var stages = new List<StickerPipelineStage>();
        var vm = new StickerStudioViewModel(new FakeStickerService());
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(StickerStudioViewModel.CurrentStage))
            {
                stages.Add(vm.CurrentStage);
            }
        };

        vm.InputImagePath = "test.png";
        await vm.GenerateStickerCommand.ExecuteAsync(null);

        Assert.Equal(StickerPipelineStage.Ready, vm.CurrentStage);
        Assert.Contains(StickerPipelineStage.GeneratingDiffusion, stages);
        Assert.Contains(StickerPipelineStage.IsolatingSubject, stages);
        Assert.Contains(StickerPipelineStage.ApplyingContour, stages);
        Assert.Contains(StickerPipelineStage.Ready, stages);

        // Verify order
        int idxDiffusion = stages.IndexOf(StickerPipelineStage.GeneratingDiffusion);
        int idxIsolation = stages.IndexOf(StickerPipelineStage.IsolatingSubject);
        int idxContour = stages.IndexOf(StickerPipelineStage.ApplyingContour);
        int idxReady = stages.IndexOf(StickerPipelineStage.Ready);

        Assert.True(idxDiffusion < idxIsolation);
        Assert.True(idxIsolation < idxContour);
        Assert.True(idxContour < idxReady);
    }

    [Fact]
    public async Task GenerateSticker_WhenServiceFails_TransitionsToFailedAndSetsErrorMessage()
    {
        var fakeService = new FakeStickerService { ShouldSucceed = false };
        var vm = new StickerStudioViewModel(fakeService);
        vm.InputImagePath = "test.png";

        await vm.GenerateStickerCommand.ExecuteAsync(null);

        Assert.Equal(StickerPipelineStage.Failed, vm.CurrentStage);
        Assert.False(vm.IsGenerating);
        Assert.Equal("Engine offline", vm.ErrorMessage);
    }

    [Fact]
    public void SelectStylePresetCommand_UpdatesSelectedPresetAndBorderWidth()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        var holographic = vm.StylePresets.FirstOrDefault(p => p.Id == "holographic");
        Assert.NotNull(holographic);

        vm.SelectStylePresetCommand.Execute(holographic);

        Assert.Same(holographic, vm.SelectedStylePreset);
        Assert.Equal(holographic.DefaultBorderWidth, vm.BorderWidth);
    }

    [Fact]
    public void ClearInputCommand_ResetsInputImagePathAndBytes()
    {
        var vm = new StickerStudioViewModel(new FakeStickerService());
        vm.InputImagePath = "some/path.png";
        vm.InputImageBytes = new byte[] { 10, 20 };
        Assert.True(vm.HasInputImage);

        vm.ClearInputCommand.Execute(null);

        Assert.Null(vm.InputImagePath);
        Assert.Null(vm.InputImageBytes);
        Assert.False(vm.HasInputImage);
    }

    [Fact]
    public void DefaultPresets_ContainsAllSixExpectedStyles()
    {
        var presets = StickerGenerationService.DefaultPresets;
        Assert.Equal(6, presets.Count);

        var ids = presets.Select(p => p.Id).ToList();
        Assert.Contains("die-cut-vinyl", ids);
        Assert.Contains("holographic", ids);
        Assert.Contains("chibi-anime", ids);
        Assert.Contains("retro-80s", ids);
        Assert.Contains("pop-art", ids);
        Assert.Contains("watercolor", ids);

        foreach (var preset in presets)
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(preset.Icon));
            Assert.False(string.IsNullOrWhiteSpace(preset.PositiveTokens));
            Assert.InRange(preset.DefaultBorderWidth, 1, 24);
        }
    }

    [Fact]
    public async Task DefaultService_CanGenerateSimulatedSticker()
    {
        var service = new StickerGenerationService();
        var request = new StickerGenerationRequest
        {
            StylePresetId = "die-cut-vinyl",
            CustomPrompt = "cute cosmic astronaut cat",
            BorderWidth = 12,
            IsAutoCutoutEnabled = true
        };

        var result = await service.GenerateStickerAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputPngBytes);
        Assert.True(result.OutputPngBytes.Length > 0);
        Assert.Equal(1024, result.Width);
        Assert.Equal(1024, result.Height);
    }

    private class TestForgeHttpHandler : System.Net.Http.HttpMessageHandler
    {
        public System.Net.Http.HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        public Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage>? ResponseFactory { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        protected override async Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            if (ResponseFactory != null)
            {
                return ResponseFactory(request);
            }

            // Return minimal 1x1 PNG wrapped in Forge images JSON response
            string b64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent($"{{\"images\":[\"{b64}\"]}}", System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    [Fact]
    public async Task GenerateSticker_Txt2Img_DispatchesToForgeWithCombinedPromptsAndSteps()
    {
        var handler = new TestForgeHttpHandler();
        var httpClient = new System.Net.Http.HttpClient(handler);
        var service = new StickerGenerationService(httpClient);

        var request = new StickerGenerationRequest
        {
            StylePresetId = "die-cut-vinyl",
            CustomPrompt = "cute cosmic astronaut cat",
            NegativePrompt = "deformed",
            BorderWidth = 12,
            IsAutoCutoutEnabled = true
        };

        var result = await service.GenerateStickerAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("http://127.0.0.1:7860/sdapi/v1/txt2img", handler.LastRequest.RequestUri?.ToString());

        Assert.NotNull(handler.LastRequestBody);
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(handler.LastRequestBody);
        var root = jsonDoc.RootElement;

        string prompt = root.GetProperty("prompt").GetString()!;
        Assert.Contains("cute cosmic astronaut cat", prompt);
        Assert.Contains("bold white die-cut border", prompt);

        string negativePrompt = root.GetProperty("negative_prompt").GetString()!;
        Assert.Contains("deformed", negativePrompt);
        Assert.Contains("photorealistic", negativePrompt);

        Assert.Equal(20, root.GetProperty("steps").GetInt32());
        Assert.Equal(1024, root.GetProperty("width").GetInt32());
        Assert.Equal(1024, root.GetProperty("height").GetInt32());
    }

    [Fact]
    public async Task GenerateSticker_Img2Img_DispatchesToForgeWithInitImagesAndDenoising()
    {
        var handler = new TestForgeHttpHandler();
        var httpClient = new System.Net.Http.HttpClient(handler);
        var service = new StickerGenerationService(httpClient);

        byte[] inputImage = StickerContourProcessor.CreateMinimalTestPng(32, 32, 255, 0, 0, 255);
        string expectedBase64 = Convert.ToBase64String(inputImage);

        var request = new StickerGenerationRequest
        {
            ImageBytes = inputImage,
            StylePresetId = "holographic",
            CustomPrompt = "mecha robot",
            BorderWidth = 14,
            IsAutoCutoutEnabled = true
        };

        var result = await service.GenerateStickerAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(System.Net.Http.HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("http://127.0.0.1:7860/sdapi/v1/img2img", handler.LastRequest.RequestUri?.ToString());

        Assert.NotNull(handler.LastRequestBody);
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(handler.LastRequestBody);
        var root = jsonDoc.RootElement;

        Assert.Equal(0.65, root.GetProperty("denoising_strength").GetDouble(), 2);
        var initImages = root.GetProperty("init_images");
        Assert.Equal(1, initImages.GetArrayLength());
        Assert.Equal(expectedBase64, initImages[0].GetString());

        string prompt = root.GetProperty("prompt").GetString()!;
        Assert.Contains("mecha robot", prompt);
        Assert.Contains("holographic foil sticker", prompt);
    }

    [Fact]
    public async Task GenerateSticker_WhenEngineOffline_FallsBackGracefullyWithoutCrashing()
    {
        var handler = new TestForgeHttpHandler
        {
            ExceptionToThrow = new System.Net.Http.HttpRequestException("Connection refused")
        };
        var httpClient = new System.Net.Http.HttpClient(handler);
        var service = new StickerGenerationService(httpClient);

        var request = new StickerGenerationRequest
        {
            StylePresetId = "die-cut-vinyl",
            CustomPrompt = "offline fallback test",
            BorderWidth = 10,
            IsAutoCutoutEnabled = true
        };

        var result = await service.GenerateStickerAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputPngBytes);
        Assert.True(result.OutputPngBytes.Length > 0);
        Assert.Equal(1024, result.Width);
        Assert.Equal(1024, result.Height);
    }

    [Fact]
    public async Task GenerateSticker_WhenEngineOfflineWithInputImage_FallsBackToInputImage()
    {
        var handler = new TestForgeHttpHandler
        {
            ExceptionToThrow = new System.Net.Http.HttpRequestException("Connection refused")
        };
        var httpClient = new System.Net.Http.HttpClient(handler);
        var service = new StickerGenerationService(httpClient);

        byte[] inputImage = StickerContourProcessor.CreateMinimalTestPng(32, 32, 200, 50, 50, 255);

        var request = new StickerGenerationRequest
        {
            ImageBytes = inputImage,
            BorderWidth = 4,
            IsAutoCutoutEnabled = true
        };

        var result = await service.GenerateStickerAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputPngBytes);
        Assert.True(result.OutputPngBytes.Length > 0);
        var decoded = StickerContourProcessor.DecodePng(result.OutputPngBytes, out int w, out int h);
        Assert.Equal(32, w);
        Assert.Equal(32, h);
    }

    [Fact]
    public async Task GenerateSticker_AppliesContourAndBorderPostProcessing()
    {
        // 16x16 red square inside 32x32 white canvas
        byte[] testPixels = new byte[32 * 32 * 4];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int pi = (y * 32 + x) * 4;
                if (x >= 8 && x < 24 && y >= 8 && y < 24)
                {
                    // Red foreground
                    testPixels[pi] = 255;
                    testPixels[pi + 1] = 0;
                    testPixels[pi + 2] = 0;
                    testPixels[pi + 3] = 255;
                }
                else
                {
                    // White background
                    testPixels[pi] = 255;
                    testPixels[pi + 1] = 255;
                    testPixels[pi + 2] = 255;
                    testPixels[pi + 3] = 255;
                }
            }
        }
        byte[] rawPng = StickerContourProcessor.EncodePng(testPixels, 32, 32);
        string b64 = Convert.ToBase64String(rawPng);

        var handler = new TestForgeHttpHandler
        {
            ResponseFactory = req => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent($"{{\"images\":[\"{b64}\"]}}", System.Text.Encoding.UTF8, "application/json")
            }
        };
        var httpClient = new System.Net.Http.HttpClient(handler);
        var service = new StickerGenerationService(httpClient);

        var request = new StickerGenerationRequest
        {
            CustomPrompt = "contour test",
            BorderWidth = 4,
            IsAutoCutoutEnabled = true
        };

        var result = await service.GenerateStickerAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputPngBytes);

        var decoded = StickerContourProcessor.DecodePng(result.OutputPngBytes, out int w, out int h);
        Assert.Equal(32, w);
        Assert.Equal(32, h);

        // Foreground pixel at (16, 16) should be red
        int fgIdx = (16 * 32 + 16) * 4;
        Assert.Equal(255, decoded[fgIdx + 0]);
        Assert.Equal(0, decoded[fgIdx + 1]);
        Assert.Equal(0, decoded[fgIdx + 2]);
        Assert.Equal(255, decoded[fgIdx + 3]);

        // Border pixel at (5, 16) should be pure white border (255, 255, 255, 255)
        int borderIdx = (16 * 32 + 5) * 4;
        Assert.Equal(255, decoded[borderIdx + 0]);
        Assert.Equal(255, decoded[borderIdx + 1]);
        Assert.Equal(255, decoded[borderIdx + 2]);
        Assert.Equal(255, decoded[borderIdx + 3]);

        // Outer corner at (0, 0) should be transparent (cut out)
        int cornerIdx = (0 * 32 + 0) * 4;
        Assert.Equal(0, decoded[cornerIdx + 3]);
    }
}
