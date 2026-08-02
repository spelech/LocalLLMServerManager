using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public void MainViewModel_InitialState_HasDefaults()
    {
        var vm = new MainViewModel();

        Assert.NotNull(vm.OllamaStatus);
        Assert.NotNull(vm.ForgeStatus);
        Assert.NotNull(vm.ComfyStatus);
        Assert.NotNull(vm.GpuName);
        Assert.NotNull(vm.Toasts);
        Assert.NotNull(vm.InstalledModels);
        Assert.False(vm.IsHfModalOpen);
        Assert.False(vm.IsPullDrawerOpen);
    }
}
