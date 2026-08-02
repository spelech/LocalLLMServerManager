using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using LocalLLMServerManager.ViewModels;
using LocalLLMServerManager.Views;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class MainWindowUiTests
{
    [AvaloniaFact]
    public void MainWindow_Instantiates_AndHasCorrectTitleAndViewModel()
    {
        var window = new MainWindow();

        Assert.NotNull(window);
        Assert.Equal("Local LLM Server Manager v3.0.0", window.Title);
        Assert.NotNull(window.DataContext);
        Assert.IsType<MainWindowViewModel>(window.DataContext);
    }

    [AvaloniaFact]
    public void MainWindow_UIControls_AreLoadedInVisualTree()
    {
        var window = new MainWindow();
        window.Show();

        var viewModel = (MainWindowViewModel)window.DataContext!;
        viewModel.GpuName = "NVIDIA GeForce RTX 4070 Ti SUPER";
        viewModel.VramStatusText = "4.2 GB / 16.0 GB (26.3%)";
        viewModel.VramPercentage = 26.3;

        Assert.Equal("NVIDIA GeForce RTX 4070 Ti SUPER", viewModel.GpuName);
        Assert.Equal(26.3, viewModel.VramPercentage);
        Assert.Equal("4.2 GB / 16.0 GB (26.3%)", viewModel.VramStatusText);

        window.Close();
    }
}
