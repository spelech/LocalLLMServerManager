using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views;
using LocalLLMServerManager.Views;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class MainWindowUiTests
{
    [AvaloniaFact]
    public void MainWindow_Instantiates_AndHasCorrectTitle()
    {
        var window = new MainWindow();

        Assert.NotNull(window);
        Assert.Equal("Local LLM Server Manager v3.0.0", window.Title);
    }

    [AvaloniaFact]
    public void MainViewModel_TelemetryProperties_UpdateCorrectly()
    {
        var viewModel = new MainViewModel();
        viewModel.GpuName = "NVIDIA GeForce RTX 4070 Ti SUPER";
        viewModel.VramStatusText = "4.2 GB / 16.0 GB (26.3%)";
        viewModel.VramPercentage = 26.3;

        Assert.Equal("NVIDIA GeForce RTX 4070 Ti SUPER", viewModel.GpuName);
        Assert.Equal(26.3, viewModel.VramPercentage);
        Assert.Equal("4.2 GB / 16.0 GB (26.3%)", viewModel.VramStatusText);
    }

    [AvaloniaFact]
    public void MainViewModel_KvCacheCalculator_ComputesCorrectFootprint()
    {
        var viewModel = new MainViewModel();
        viewModel.TargetContextTokens = 8192;

        Assert.Equal("~512 MB", viewModel.EstimatedKvCacheText);
    }
}
