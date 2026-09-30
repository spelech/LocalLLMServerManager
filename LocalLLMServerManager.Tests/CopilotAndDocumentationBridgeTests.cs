using System.Linq;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class CopilotAndDocumentationBridgeTests
{
    [Fact]
    public void MainViewModel_ToggleCopilotSidebar_TogglesStateCorrectly()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsCopilotSidebarOpen);

        // Open with Assistant
        vm.ToggleCopilotSidebar("Assistant");
        Assert.True(vm.IsCopilotSidebarOpen);
        Assert.Equal("Assistant", vm.SelectedCopilotTab);
        Assert.True(vm.IsAssistantTabActive);
        Assert.False(vm.IsDocsTabActive);

        // Switch to Docs while open
        vm.ToggleCopilotSidebar("Docs");
        Assert.True(vm.IsCopilotSidebarOpen);
        Assert.Equal("Docs", vm.SelectedCopilotTab);
        Assert.True(vm.IsDocsTabActive);
        Assert.False(vm.IsAssistantTabActive);

        // Toggle Docs again to close
        vm.ToggleCopilotSidebar("Docs");
        Assert.False(vm.IsCopilotSidebarOpen);
    }

    [Fact]
    public void MainViewModel_SelectCopilotTab_OpensAndSetsActiveTab()
    {
        var vm = new MainViewModel();
        Assert.False(vm.IsCopilotSidebarOpen);

        vm.SelectCopilotTab("Docs");
        Assert.True(vm.IsCopilotSidebarOpen);
        Assert.Equal("Docs", vm.SelectedCopilotTab);

        vm.SelectCopilotTab("Assistant");
        Assert.True(vm.IsCopilotSidebarOpen);
        Assert.Equal("Assistant", vm.SelectedCopilotTab);
    }

    [Fact]
    public void DocumentationViewModel_AskCopilotAboutGuide_TransfersPromptToAssistant()
    {
        var vm = new MainViewModel();
        Assert.NotNull(vm.Documentation.SelectedSection);

        var sectionTitle = vm.Documentation.SelectedSection.Title;
        vm.Documentation.AskCopilotAboutGuideCommand.Execute(null);

        Assert.True(vm.IsCopilotSidebarOpen);
        Assert.Equal("Assistant", vm.SelectedCopilotTab);
        Assert.Contains(sectionTitle, vm.Assistant.InputText);
    }
}
