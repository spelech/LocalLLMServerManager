using System.Linq;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class DocumentationViewModelTests
{
    [Fact]
    public void DocumentationViewModel_InitializesWithAllSte100Topics()
    {
        var vm = new DocumentationViewModel();

        Assert.NotEmpty(vm.Sections);
        Assert.True(vm.Sections.Count >= 7);
        Assert.NotNull(vm.SelectedSection);
        Assert.Equal("image-generation", vm.SelectedSection.Id);
    }

    [Fact]
    public void DocumentationViewModel_EachSectionHasValidStepsAndPrerequisites()
    {
        var vm = new DocumentationViewModel();

        foreach (var section in vm.Sections)
        {
            Assert.False(string.IsNullOrWhiteSpace(section.Id));
            Assert.False(string.IsNullOrWhiteSpace(section.Title));
            Assert.False(string.IsNullOrWhiteSpace(section.Summary));
            Assert.False(string.IsNullOrWhiteSpace(section.Prerequisite));
            Assert.NotEmpty(section.Steps);

            foreach (var step in section.Steps)
            {
                Assert.True(step.StepNumber > 0);
                Assert.False(string.IsNullOrWhiteSpace(step.Title));
                Assert.False(string.IsNullOrWhiteSpace(step.Action));
                Assert.False(string.IsNullOrWhiteSpace(step.ExpectedResult));
            }
        }
    }

    [Fact]
    public void SelectSection_SwitchesActiveDocument()
    {
        var vm = new DocumentationViewModel();

        vm.SelectSection("video-generation");
        Assert.NotNull(vm.SelectedSection);
        Assert.Equal("video-generation", vm.SelectedSection.Id);
        Assert.Contains("Video", vm.SelectedSection.Title);

        vm.SelectSection("can-i-run-it");
        Assert.NotNull(vm.SelectedSection);
        Assert.Equal("can-i-run-it", vm.SelectedSection.Id);

        vm.SelectSection("non-existent-section");
        // Should retain previous selection if target not found
        Assert.Equal("can-i-run-it", vm.SelectedSection.Id);
    }

    [Fact]
    public void FloatingOverlay_TogglesAndNavigatesSteps()
    {
        var vm = new DocumentationViewModel();
        Assert.False(vm.IsFloatingOverlayOpen);

        vm.ToggleFloatingOverlay();
        Assert.True(vm.IsFloatingOverlayOpen);
        Assert.False(vm.IsMinimizedToPill);
        Assert.Equal(0, vm.CurrentStepIndex);
        Assert.NotNull(vm.CurrentStep);

        vm.NextStep();
        Assert.Equal(1, vm.CurrentStepIndex);

        vm.PreviousStep();
        Assert.Equal(0, vm.CurrentStepIndex);

        vm.ToggleMinimizeToPill();
        Assert.True(vm.IsMinimizedToPill);

        vm.ToggleMinimizeToPill();
        Assert.False(vm.IsMinimizedToPill);

        vm.ToggleFloatingOverlay();
        Assert.False(vm.IsFloatingOverlayOpen);
    }

    [Fact]
    public void JumpToStepTab_InvokesCallbackWithCorrectTargetTab()
    {
        var vm = new DocumentationViewModel();
        vm.SelectSection("image-generation");

        int receivedTab = -1;
        vm.OnNavigateToTabRequested = tab => receivedTab = tab;

        Assert.NotNull(vm.CurrentStep);
        Assert.Equal(1, vm.CurrentStep.TargetTab);

        vm.JumpToStepTab(null);
        Assert.Equal(1, receivedTab);
    }
}
