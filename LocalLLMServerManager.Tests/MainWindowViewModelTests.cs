using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void MainViewModel_InitialState_HasDefaults()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.GpuName);
        Assert.Equal(8.0, vm.VramTotalGb);
        Assert.Equal(0, vm.VramUsedGb);
        Assert.Equal(0, vm.VramPercentage);
        Assert.Equal("Checking...", vm.OllamaStatus);
        Assert.Equal("Checking...", vm.ForgeStatus);
        Assert.Equal("Checking...", vm.ComfyStatus);
        Assert.Equal("llama 3.3", vm.HfSearchQuery);
        Assert.Equal("cyberpunk", vm.CivitaiSearchQuery);
    }

    [Fact]
    public async Task MainViewModel_RefreshStatusAsync_UpdatesState()
    {
        var vm = new MainViewModel();

        await vm.RefreshStatusAsync();

        Assert.NotNull(vm.OllamaStatus);
        Assert.NotNull(vm.ForgeStatus);
        Assert.NotNull(vm.ComfyStatus);
        Assert.NotNull(vm.GpuName);
    }

    [Fact]
    public void MainViewModel_RelayCommands_AreExecutable()
    {
        var vm = new MainViewModel();

        Assert.True(vm.RefreshStatusCommand.CanExecute(null));
        Assert.True(vm.OpenWebUiInBrowserCommand.CanExecute(null));
        Assert.True(vm.ToggleEngineCommand.CanExecute("comfy"));
        Assert.True(vm.SearchHuggingFaceCommand.CanExecute(null));
        Assert.True(vm.SearchCivitaiCommand.CanExecute(null));
        Assert.True(vm.UnloadAllVramCommand.CanExecute(null));
    }

    [Fact]
    public async Task MainViewModel_HuggingFaceSearch_UpdatesResults()
    {
        var vm = new MainViewModel { HfSearchQuery = "llama 3.3" };
        await vm.SearchHuggingFaceAsync();
        Assert.NotNull(vm.HuggingFaceResults);
    }

    [Fact]
    public async Task MainViewModel_OpenHfModal_PopulatesQuantFiles()
    {
        var vm = new MainViewModel();
        await vm.OpenHfModalAsync("meta-llama/Llama-3.3-8B-Instruct-GGUF");
        Assert.Equal("meta-llama/Llama-3.3-8B-Instruct-GGUF", vm.ModalRepoId);
        Assert.Equal("meta-llama", vm.ModalAuthor);
        Assert.True(vm.IsHfModalOpen);
        vm.CloseHfModal();
        Assert.False(vm.IsHfModalOpen);
    }

    [Fact]
    public async Task MainViewModel_CivitaiSearch_UpdatesResults()
    {
        var vm = new MainViewModel { CivitaiSearchQuery = "cyberpunk" };
        await vm.SearchCivitaiAsync();
        Assert.NotNull(vm.CivitaiResults);
    }

    [Fact]
    public void ToastService_Show_AddsActiveToast()
    {
        ToastService.Instance.Show("Test notification message", ToastType.Success);
        Assert.NotEmpty(ToastService.Instance.ActiveToasts);
    }
}
