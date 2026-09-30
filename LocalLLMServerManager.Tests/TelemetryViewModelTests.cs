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

    [Fact]
    public void ManageService_WhenCallbackConfigured_InvokesCallbackWithServiceName()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var vm = new TelemetryViewModel(mockTelemetry.Object);
        string? requestedService = null;
        vm.OnManageServiceRequested = name => requestedService = name;

        vm.ManageService("Ollama");

        Assert.Equal("Ollama", requestedService);
        Assert.False(vm.IsManageServiceModalOpen);
    }

    [Theory]
    [InlineData("Ollama", "Offline", true)]
    [InlineData("Ollama", "Online", false)]
    [InlineData("Forge SD", "Offline", true)]
    [InlineData("Forge SD", "Online", false)]
    [InlineData("ComfyUI", "Offline", true)]
    [InlineData("ComfyUI", "Online", false)]
    public void ManageService_WhenNoCallback_ResolvesStartOrStopCorrectly(string service, string initialStatus, bool expectedIsStart)
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        var vm = new TelemetryViewModel(mockTelemetry.Object);
        if (service == "Ollama") vm.OllamaStatus = initialStatus;
        else if (service == "Forge SD") vm.ForgeStatus = initialStatus;
        else if (service == "ComfyUI") vm.ComfyStatus = initialStatus;

        vm.ManageService(service);

        Assert.True(vm.IsManageServiceModalOpen);
        Assert.Equal(service, vm.ManageServiceTarget);
        Assert.Equal(expectedIsStart, vm.ManageServiceIsStart);
        string actionWord = expectedIsStart ? "start" : "stop";
        Assert.Contains(actionWord, vm.ManageServicePrompt);
    }

    [Fact]
    public void TelemetryViewModel_EngineCards_ReflectEngineStatus()
    {
        var vm = new TelemetryViewModel();
        Assert.NotNull(vm.OllamaCard);
        Assert.NotNull(vm.ComfyUiCard);
        Assert.NotNull(vm.ForgeCard);
        Assert.NotNull(vm.KokoroCard);

        vm.OllamaStatus = "Running";
        vm.OllamaModelName = "qwen2.5-coder:1.5b";
        Assert.True(vm.OllamaCard.IsOnline);
        Assert.Equal("qwen2.5-coder:1.5b", vm.OllamaCard.ActiveModel);
    }

    [Fact]
    public void TelemetryViewModel_EngineCards_InitialProperties()
    {
        var vm = new TelemetryViewModel();
        Assert.Equal("ollama", vm.OllamaCard.EngineKey);
        Assert.Equal("Ollama", vm.OllamaCard.DisplayName);
        Assert.Equal(11434, vm.OllamaCard.Port);

        Assert.Equal("comfyui", vm.ComfyUiCard.EngineKey);
        Assert.Equal("ComfyUI", vm.ComfyUiCard.DisplayName);
        Assert.Equal(8188, vm.ComfyUiCard.Port);

        Assert.Equal("forge", vm.ForgeCard.EngineKey);
        Assert.Equal("SD Forge", vm.ForgeCard.DisplayName);
        Assert.Equal(7860, vm.ForgeCard.Port);

        Assert.Equal("kokoro", vm.KokoroCard.EngineKey);
        Assert.Equal("Kokoro TTS", vm.KokoroCard.DisplayName);
        Assert.Equal(8880, vm.KokoroCard.Port);
    }

    [Fact]
    public void TelemetryViewModel_UpdateFromTelemetry_UpdatesCardsProperly()
    {
        var vm = new TelemetryViewModel();
        var data = new LocalLLMServerManager.Shared.Models.TelemetryData
        {
            OllamaOnline = true,
            OllamaModel = "llama3:8b",
            ComfyOnline = false,
            ComfyStarting = true,
            ForgeOnline = true,
            ForgeModel = "sd_xl_base",
            KokoroOnline = true,
            KokoroModel = "af_sky"
        };

        vm.UpdateFromTelemetry(data);

        Assert.True(vm.OllamaCard.IsOnline);
        Assert.Equal("llama3:8b", vm.OllamaCard.ActiveModel);
        Assert.False(vm.ComfyUiCard.IsOnline);
        Assert.True(vm.ComfyUiCard.IsStarting);
        Assert.True(vm.ForgeCard.IsOnline);
        Assert.Equal("sd_xl_base", vm.ForgeCard.ActiveModel);
        Assert.True(vm.KokoroCard.IsOnline);
        Assert.Equal("af_sky", vm.KokoroCard.ActiveModel);
    }
}
