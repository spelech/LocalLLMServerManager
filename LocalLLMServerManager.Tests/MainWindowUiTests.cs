using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Views;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class MainWindowUiTests
{
    [Fact]
    public void MainViewModel_GpuNameAndTokenFormatting_RendersCorrectly()
    {
        var vm = new MainViewModel
        {
            GpuName = "NVIDIA GeForce RTX 4090",
            TargetContextTokens = 16384
        };

        Assert.Equal("NVIDIA GeForce RTX 4090", vm.GpuName);
        Assert.Equal(16384, vm.TargetContextTokens);
        Assert.Equal("~1.0 GB", vm.EstimatedKvCacheText);
    }
}
