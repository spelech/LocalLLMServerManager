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
}
