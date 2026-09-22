using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views.Controls;
using Moq;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class TelemetryViewModelTests
{
    [Fact]
    public void IsCollapsed_DefaultsToFalse()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var vm = new TelemetryViewModel(mockTelemetry.Object);

        Assert.False(vm.IsCollapsed);
    }

    [Fact]
    public void ToggleCollapseCommand_TogglesIsCollapsed()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var vm = new TelemetryViewModel(mockTelemetry.Object);

        Assert.False(vm.IsCollapsed);

        vm.ToggleCollapseCommand.Execute(null);
        Assert.True(vm.IsCollapsed);

        vm.ToggleCollapseCommand.Execute(null);
        Assert.False(vm.IsCollapsed);
    }

    [Fact]
    public void ToggleCollapse_PreservesServiceStatusAndVramProperties()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var vm = new TelemetryViewModel(mockTelemetry.Object)
        {
            OllamaStatus = "Online",
            ForgeStatus = "Online",
            ComfyStatus = "Offline",
            GpuName = "RTX 4090",
            VramUsedGb = 8.5,
            VramTotalGb = 24.0,
            VramPercentage = 35.4,
            VramStatusText = "8.5 GB / 24.0 GB (35%)"
        };

        vm.ToggleCollapseCommand.Execute(null);

        Assert.True(vm.IsCollapsed);
        Assert.Equal("Online", vm.OllamaStatus);
        Assert.Equal("Online", vm.ForgeStatus);
        Assert.Equal("Offline", vm.ComfyStatus);
        Assert.True(vm.IsOllamaOnline);
        Assert.True(vm.IsForgeOnline);
        Assert.False(vm.IsComfyOnline);
        Assert.Equal("#22C55E", vm.OllamaStatusColor);
        Assert.Equal("#22C55E", vm.ForgeStatusColor);
        Assert.Equal("#64748B", vm.ComfyStatusColor);
        Assert.Equal("RTX 4090", vm.GpuName);
        Assert.Equal(8.5, vm.VramUsedGb);
        Assert.Equal(24.0, vm.VramTotalGb);
        Assert.Equal(35.4, vm.VramPercentage);
        Assert.Equal("8.5 GB / 24.0 GB (35%)", vm.VramStatusText);

        vm.ToggleCollapseCommand.Execute(null);

        Assert.False(vm.IsCollapsed);
        Assert.Equal("Online", vm.OllamaStatus);
        Assert.Equal("8.5 GB / 24.0 GB (35%)", vm.VramStatusText);
    }

    [AvaloniaFact]
    public void TelemetryHeaderControl_ToggleCollapse_TogglesUIStateCleanly()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var vm = new TelemetryViewModel(mockTelemetry.Object)
        {
            VramStatusText = "4.0 GB / 16.0 GB (25%)"
        };
        var control = new TelemetryHeaderControl { DataContext = vm };
        var window = new Window { Content = control, Width = 1024, Height = 200 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Initially expanded: collapse button is present
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var collapseBtn = buttons.FirstOrDefault(b => b.Command == vm.ToggleCollapseCommand);
        Assert.NotNull(collapseBtn);

        // Toggle to collapsed
        vm.ToggleCollapseCommand.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(vm.IsCollapsed);

        var textBlocks = control.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(textBlocks, t => t.Text != null && t.Text.Contains("GB"));

        window.Close();
    }
}
