using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class EngineStudioViewModelTests
{
    [Fact]
    public void EngineStudioViewModel_ModalitySwitch_UpdatesDockState()
    {
        var vm = new EngineStudioViewModel();
        Assert.Equal("Image", vm.SelectedModality);
        Assert.True(vm.IsImageModalityActive);
        Assert.False(vm.Is3DModalityActive);

        vm.SelectModalityCommand.Execute("3D Mesh");

        Assert.Equal("3D Mesh", vm.SelectedModality);
        Assert.True(vm.Is3DModalityActive);
        Assert.False(vm.IsImageModalityActive);

        Assert.False(vm.IsParametersFlyoutOpen);
        vm.ToggleParametersFlyoutCommand.Execute(null);
        Assert.True(vm.IsParametersFlyoutOpen);

        vm.ToggleParametersFlyoutCommand.Execute(null);
        Assert.False(vm.IsParametersFlyoutOpen);
    }

    [Theory]
    [InlineData("Image", true, false, false, false, false)]
    [InlineData("Text", false, true, false, false, false)]
    [InlineData("Video", false, false, true, false, false)]
    [InlineData("3D Mesh", false, false, false, true, false)]
    [InlineData("Audio", false, false, false, false, true)]
    public void EngineStudioViewModel_ModalityBooleans_MatchSelectedModality(
        string modality,
        bool expectImage,
        bool expectText,
        bool expectVideo,
        bool expect3D,
        bool expectAudio)
    {
        var vm = new EngineStudioViewModel();
        vm.SelectModalityCommand.Execute(modality);

        Assert.Equal(expectImage, vm.IsImageModalityActive);
        Assert.Equal(expectText, vm.IsTextModalityActive);
        Assert.Equal(expectVideo, vm.IsVideoModalityActive);
        Assert.Equal(expect3D, vm.Is3DModalityActive);
        Assert.Equal(expectAudio, vm.IsAudioModalityActive);
    }

    [Fact]
    public void EngineStudioViewModel_ActiveModelBadge_ReflectsModality()
    {
        var vm = new EngineStudioViewModel();

        vm.SelectModalityCommand.Execute("Image");
        Assert.Contains("SDXL", vm.ActiveModelBadge);

        vm.SelectModalityCommand.Execute("Text");
        Assert.Contains("llama", vm.ActiveModelBadge, System.StringComparison.OrdinalIgnoreCase);

        vm.SelectModalityCommand.Execute("Video");
        Assert.Contains("Wan", vm.ActiveModelBadge);

        vm.SelectModalityCommand.Execute("3D Mesh");
        Assert.Contains("TRELLIS", vm.ActiveModelBadge);

        vm.SelectModalityCommand.Execute("Audio");
        Assert.Contains("Kokoro", vm.ActiveModelBadge);
    }

    [Fact]
    public void EngineStudioViewModel_SelectModel_UpdatesActiveModelAndBadge()
    {
        var vm = new EngineStudioViewModel();
        vm.SelectModalityCommand.Execute("Image");
        Assert.Equal("SDXL Base 1.0", vm.ImageModel);

        vm.SelectModelCommand.Execute("Flux.1 [dev]");
        Assert.Equal("Flux.1 [dev]", vm.ImageModel);
        Assert.Equal("Flux.1 [dev]", vm.ActiveModelBadge);

        vm.SelectModalityCommand.Execute("Video");
        vm.SelectModelCommand.Execute("LTX-Video 2.5");
        Assert.Equal("LTX-Video 2.5", vm.VideoModel);
        Assert.Equal("LTX-Video 2.5", vm.ActiveModelBadge);
    }

    [Fact]
    public void EngineStudioViewModel_AvailableCurrentModels_PopulatesPerModality()
    {
        var vm = new EngineStudioViewModel();
        vm.SelectModalityCommand.Execute("Image");
        Assert.Contains("SDXL Base 1.0", vm.AvailableCurrentModels);
        Assert.Contains("Flux.1 [dev]", vm.AvailableCurrentModels);

        vm.SelectModalityCommand.Execute("Video");
        Assert.Contains("Wan 2.2 / LTX-2.5", vm.AvailableCurrentModels);
        Assert.Contains("LTX-Video 2.5", vm.AvailableCurrentModels);

        vm.SelectModalityCommand.Execute("Text");
        Assert.Contains("llama3.2:latest", vm.AvailableCurrentModels);
    }

    [Fact]
    public void MainViewModel_UseModelInStudio_SwitchesModalityAndModel()
    {
        var vm = new MainViewModel();
        var modelItem = new LocalLLMServerManager.Shared.Models.LocalModelItem(
            Id: "test_id",
            Name: "DreamShaper XL",
            FileName: "dreamshaper.safetensors",
            FullPath: "C:/fake/path",
            Category: LocalLLMServerManager.Shared.Models.LocalModelCategory.ImageCheckpoint,
            Architecture: "SDXL",
            SizeBytes: 1024,
            FormattedSize: "1.0 KB",
            SourceLocation: "Test",
            CreatedAt: System.DateTime.UtcNow
        );

        vm.UseModelInStudioCommand.Execute(modelItem);

        Assert.Equal(1, vm.SelectedTabIndex);
        Assert.Equal("Image", vm.SelectedModality);
        Assert.Equal("DreamShaper XL", vm.SelectedImageWorkflow);
        Assert.Equal("DreamShaper XL", vm.Studio.ImageModel);
        Assert.Equal("DreamShaper XL", vm.ActiveModelBadge);
    }

    [Fact]
    public async System.Threading.Tasks.Task MainViewModel_GenerateStudioImage_DispatchesToForgeAndDecodesImage()
    {
        byte[] fakePng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        string base64Png = System.Convert.ToBase64String(fakePng);

        var handler = new TestHttpHandler(req =>
        {
            if (req.RequestUri != null && req.RequestUri.ToString().Contains("/sdapi/v1/txt2img"))
            {
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new System.Net.Http.StringContent($"{{\"images\":[\"{base64Png}\"]}}", System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        var client = new System.Net.Http.HttpClient(handler);
        var vm = new MainViewModel(client);

        Assert.Null(vm.StudioGeneratedImageBytes);
        Assert.False(vm.HasStudioGeneratedImage);
        Assert.True(vm.ShowImageCanvasZeroState);

        vm.PromptText = "A beautiful cybernetic forest";
        vm.ActiveAspectPreset = "16:9";

        await vm.GenerateStudioImageAsync();

        Assert.False(vm.IsGeneratingStudioImage);
        Assert.NotNull(vm.StudioGeneratedImageBytes);
        Assert.True(vm.HasStudioGeneratedImage);
        Assert.False(vm.ShowImageCanvasZeroState);
        Assert.Equal(fakePng, vm.StudioGeneratedImageBytes);
        Assert.Equal("Generation complete!", vm.StudioImageGenerationStatus);

        vm.ClearStudioImageCommand.Execute(null);

        Assert.Null(vm.StudioGeneratedImageBytes);
        Assert.False(vm.HasStudioGeneratedImage);
        Assert.True(vm.ShowImageCanvasZeroState);
    }

    [Fact]
    public async System.Threading.Tasks.Task MainViewModel_GenerateStudioImage_HandlesOfflineForgeGracefully()
    {
        var handler = new TestHttpHandler(req => throw new System.Net.Http.HttpRequestException("Connection refused"));
        var client = new System.Net.Http.HttpClient(handler);
        var vm = new MainViewModel(client);

        vm.PromptText = "Offline test";
        await vm.GenerateStudioImageAsync();

        Assert.False(vm.IsGeneratingStudioImage);
        Assert.Null(vm.StudioGeneratedImageBytes);
        Assert.False(vm.HasStudioGeneratedImage);
        Assert.Contains("offline or unreachable", vm.StudioImageGenerationStatus);
    }

    [Fact]
    public async System.Threading.Tasks.Task MainViewModel_GenerateFromDock_DispatchesImageGeneration()
    {
        byte[] fakePng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 5, 6, 7, 8 };
        string base64Png = System.Convert.ToBase64String(fakePng);

        var handler = new TestHttpHandler(req => new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent($"{{\"images\":[\"{base64Png}\"]}}", System.Text.Encoding.UTF8, "application/json")
        });

        var client = new System.Net.Http.HttpClient(handler);
        var vm = new MainViewModel(client);

        vm.SelectModality("Image");
        vm.PromptText = "Floating islands in sky";

        await vm.GenerateFromDockAsync();

        Assert.NotNull(vm.StudioGeneratedImageBytes);
        Assert.True(vm.HasStudioGeneratedImage);
    }

    [Fact]
    public async System.Threading.Tasks.Task MainViewModel_AttachmentAndClipboard_FiresCallbacks()
    {
        var vm = new MainViewModel();

        byte[] fakeRef = new byte[] { 1, 2, 3, 4, 5 };
        vm.PickImageRequested += () => System.Threading.Tasks.Task.FromResult<byte[]?>(fakeRef);

        Assert.False(vm.HasAttachedImage);
        await vm.AttachImageAsync();

        Assert.True(vm.HasAttachedImage);
        Assert.Equal(fakeRef, vm.AttachedImageBytes);

        vm.RemoveAttachedImageCommand.Execute(null);
        Assert.False(vm.HasAttachedImage);
        Assert.Null(vm.AttachedImageBytes);

        // Clipboard test
        byte[] copied = null!;
        vm.CopyStudioImageRequested += bytes =>
        {
            copied = bytes;
            return System.Threading.Tasks.Task.CompletedTask;
        };

        vm.StudioGeneratedImageBytes = fakeRef;
        await vm.CopyStudioImageAsync();
        Assert.Equal(fakeRef, copied);

        // Save test
        string? savedPath = null;
        vm.SaveStudioImageRequested += bytes => System.Threading.Tasks.Task.FromResult<string?>("C:/fake/render.png");
        await vm.SaveStudioImageAsync();
    }

    [Fact]
    public void AspectPreset_HighlightingProperties_ToggleCorrectly()
    {
        var vm = new MainViewModel();

        vm.SelectAspectPreset("1:1");
        Assert.True(vm.IsAspectSquareActive);
        Assert.False(vm.IsAspectLandscapeActive);
        Assert.False(vm.IsAspectPortraitActive);
        Assert.False(vm.IsAspectStandardActive);

        vm.SelectAspectPreset("16:9");
        Assert.False(vm.IsAspectSquareActive);
        Assert.True(vm.IsAspectLandscapeActive);
        Assert.False(vm.IsAspectPortraitActive);
        Assert.False(vm.IsAspectStandardActive);

        vm.SelectAspectPreset("9:16");
        Assert.False(vm.IsAspectSquareActive);
        Assert.False(vm.IsAspectLandscapeActive);
        Assert.True(vm.IsAspectPortraitActive);
        Assert.False(vm.IsAspectStandardActive);

        vm.SelectAspectPreset("4:3");
        Assert.False(vm.IsAspectSquareActive);
        Assert.False(vm.IsAspectLandscapeActive);
        Assert.False(vm.IsAspectPortraitActive);
        Assert.True(vm.IsAspectStandardActive);
    }

    private class TestHttpHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly System.Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> _func;

        public TestHttpHandler(System.Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> func)
        {
            _func = func;
        }

        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request,
            System.Threading.CancellationToken cancellationToken)
        {
            return System.Threading.Tasks.Task.FromResult(_func(request));
        }
    }
}
