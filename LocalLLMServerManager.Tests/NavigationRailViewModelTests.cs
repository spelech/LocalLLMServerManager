using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class NavigationRailViewModelTests
{
    [Fact]
    public void InitialState_IsCollapsed_AndStudioSelected()
    {
        var vm = new NavigationRailViewModel();
        Assert.False(vm.IsExpanded);
        Assert.Equal(NavDomain.Studio, vm.SelectedDomain);
    }

    [Fact]
    public void ToggleRailExpanded_FlipsIsExpanded()
    {
        var vm = new NavigationRailViewModel();
        vm.ToggleRailExpandedCommand.Execute(null);
        Assert.True(vm.IsExpanded);
        vm.ToggleRailExpandedCommand.Execute(null);
        Assert.False(vm.IsExpanded);
    }

    [Fact]
    public void SelectDomain_UpdatesSelectedDomain()
    {
        var vm = new NavigationRailViewModel();
        vm.SelectDomainCommand.Execute(NavDomain.Models);
        Assert.Equal(NavDomain.Models, vm.SelectedDomain);
    }

    [Fact]
    public void ToggleRailExpanded_DirectMethod_FlipsIsExpanded()
    {
        var vm = new NavigationRailViewModel();
        vm.ToggleRailExpanded();
        Assert.True(vm.IsExpanded);
        vm.ToggleRailExpanded();
        Assert.False(vm.IsExpanded);
    }

    [Fact]
    public void SelectDomain_DirectMethod_UpdatesSelectedDomain()
    {
        var vm = new NavigationRailViewModel();
        vm.SelectDomain(NavDomain.Settings);
        Assert.Equal(NavDomain.Settings, vm.SelectedDomain);
    }

    [Fact]
    public void DomainSelectionFlags_ReflectSelectedDomain()
    {
        var vm = new NavigationRailViewModel();
        Assert.True(vm.IsStudioSelected);
        Assert.False(vm.IsModelsSelected);
        Assert.False(vm.IsHardwareFitSelected);
        Assert.False(vm.IsSettingsSelected);

        vm.SelectDomain(NavDomain.Models);
        Assert.False(vm.IsStudioSelected);
        Assert.True(vm.IsModelsSelected);
        Assert.False(vm.IsHardwareFitSelected);
        Assert.False(vm.IsSettingsSelected);

        vm.SelectDomain(NavDomain.HardwareFit);
        Assert.False(vm.IsStudioSelected);
        Assert.False(vm.IsModelsSelected);
        Assert.True(vm.IsHardwareFitSelected);
        Assert.False(vm.IsSettingsSelected);

        vm.SelectDomain(NavDomain.Settings);
        Assert.False(vm.IsStudioSelected);
        Assert.False(vm.IsModelsSelected);
        Assert.False(vm.IsHardwareFitSelected);
        Assert.True(vm.IsSettingsSelected);
    }

    [Fact]
    public void UtilityCommands_TriggerEvents()
    {
        var vm = new NavigationRailViewModel();
        bool docFired = false;
        bool assistFired = false;

        vm.DocumentationRequested += () => docFired = true;
        vm.AiAssistRequested += () => assistFired = true;

        vm.DocumentationCommand.Execute(null);
        Assert.True(docFired);

        vm.AiAssistCommand.Execute(null);
        Assert.True(assistFired);
    }

    [Fact]
    public void StickerStylePreset_DefaultProperties()
    {
        var preset = new StickerStylePreset();
        Assert.Equal(12, preset.DefaultBorderWidth);
        Assert.Empty(preset.Id);
        Assert.Empty(preset.DisplayName);
    }

    [Fact]
    public void StickerGenerationRequest_DefaultProperties()
    {
        var request = new StickerGenerationRequest();
        Assert.Equal(12, request.BorderWidth);
        Assert.True(request.IsAutoCutoutEnabled);
    }
}
