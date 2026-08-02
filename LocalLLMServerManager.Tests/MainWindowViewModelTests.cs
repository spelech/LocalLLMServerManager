using System.Threading.Tasks;
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
    }
}
