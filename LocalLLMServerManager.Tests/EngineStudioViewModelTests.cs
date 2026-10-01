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
}
