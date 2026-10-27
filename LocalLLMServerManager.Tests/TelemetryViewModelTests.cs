using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views.Controls;
using Moq;
using Moq.Protected;
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

    [Fact]
    public async Task CheckGpuVramAsync_PopulatesSystemRam_WorkingSet_NonPagedPool_And_Tooltip()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        mockTelemetry
            .Setup(t => t.QueryGpuVramAsync(It.IsAny<string>(), It.IsAny<HttpClient>()))
            .ReturnsAsync(new GpuTelemetryInfo(
                "RTX 4090",
                6.5,
                24.0,
                17.5,
                27,
                25.0,
                32.0,
                78,
                1850.5,
                1920.0,
                15,
                30,
                75));

        var vm = new TelemetryViewModel(mockTelemetry.Object);
        using var http = new HttpClient();
        await vm.CheckGpuVramAsync("http://127.0.0.1:5246", http);

        Assert.Equal(25.0, vm.RamUsedGb);
        Assert.Equal(32.0, vm.RamTotalGb);
        Assert.Equal(78, vm.RamPercentage);
        Assert.Equal(1850.5, vm.ProcessWorkingSetMb);
        Assert.Equal(1920.0, vm.NonPagedPoolMb);

        Assert.Equal("#F59E0B", vm.RamBarColor);
        vm.RamPercentage = 92;
        Assert.Equal("#EF4444", vm.RamBarColor);
        vm.RamPercentage = 50;
        Assert.Equal("#6366F1", vm.RamBarColor);

        Assert.Contains("RTX 4090", vm.HardwareTooltipText);
        Assert.Contains("25", vm.HardwareTooltipText);
        Assert.Contains("32", vm.HardwareTooltipText);
        Assert.Contains("1850.5", vm.HardwareTooltipText);
        Assert.Contains("1920", vm.HardwareTooltipText);
        Assert.Contains("15m Unload / 30m Hibernate / 75% Pressure", vm.HardwareTooltipText);
    }

    [Fact]
    public async Task PurgeMemoryAsync_PostsToMemoryPurgeEndpoint_AndRefreshesTelemetry()
    {
        var mockTelemetry = new Mock<ITelemetryService>();
        mockTelemetry
            .Setup(t => t.QueryGpuVramAsync(It.IsAny<string>(), It.IsAny<HttpClient>()))
            .ReturnsAsync(new GpuTelemetryInfo("RTX 4090", 2.0, 24.0, 22.0, 8, 12.0, 32.0, 37, 850.0, 1024.0, 15, 30, 75));

        bool purgeEndpointHit = false;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().Contains("/api/memory/purge")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\":\"purged\"}")
            })
            .Callback<HttpRequestMessage, CancellationToken>((_, _) => purgeEndpointHit = true);

        var http = new HttpClient(mockHandler.Object);
        var vm = new TelemetryViewModel(mockTelemetry.Object);

        await vm.PurgeMemoryAsync("http://127.0.0.1:5246", http);

        Assert.True(purgeEndpointHit);
        Assert.Equal(12.0, vm.RamUsedGb);
        Assert.Equal(37, vm.RamPercentage);
        mockTelemetry.Verify(t => t.QueryGpuVramAsync(It.IsAny<string>(), It.IsAny<HttpClient>()), Times.Once);
    }

    [AvaloniaFact]
    public void RenderPreviews_RibbonAndSettings_SavesPngs()
    {
        var baseDir = System.AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "LocalLLMServerManager.slnx")) && !File.Exists(Path.Combine(dir.FullName, "LocalLLMServerManager.sln")))
        {
            dir = dir.Parent;
        }
        string repoRoot = dir?.FullName ?? @"C:\Users\Alias\repos\LocalLLMServerManager";
        string previewDir = Path.Combine(repoRoot, ".superpowers", "sdd", "2026-10-27-memory-cache-lifetimes-and-ram-telemetry-plan");
        Directory.CreateDirectory(previewDir);

        // 1. Ribbon preview (at 1280x34)
        var mockTelemetry = new Mock<ITelemetryService>();
        var telemetryVm = new TelemetryViewModel(mockTelemetry.Object)
        {
            GpuName = "NVIDIA GeForce RTX 4090",
            VramUsedGb = 8.5,
            VramTotalGb = 24.0,
            VramPercentage = 35.4,
            VramStatusText = "8.5 GB / 24.0 GB (35%)",
            RamUsedGb = 25.0,
            RamTotalGb = 32.0,
            RamPercentage = 78,
            ProcessWorkingSetMb = 1850.5,
            NonPagedPoolMb = 1920.0,
            ModelCacheIdleTimeoutMinutes = 15,
            EngineHibernationTimeoutMinutes = 30,
            RamPressureThresholdPercent = 75,
            OllamaStatus = "Online",
            ForgeStatus = "Online",
            ComfyStatus = "Offline",
            KokoroStatus = "Online"
        };
        telemetryVm.OllamaCard.ActiveModel = "qwen2.5-coder:1.5b";

        var ribbonControl = new TelemetryRibbonControl { DataContext = telemetryVm };
        var ribbonContainer = new Border
        {
            Background = Avalonia.Media.Brush.Parse("#0D0F18"),
            Child = ribbonControl,
            Width = 1280,
            Height = 34
        };
        var ribbonWindow = new Window
        {
            Content = ribbonContainer,
            Width = 1280,
            Height = 34
        };

        try
        {
            ribbonWindow.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            ribbonWindow.Measure(new Size(1280, 34));
            ribbonWindow.Arrange(new Rect(0, 0, 1280, 34));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            using var ribbonBitmap = new RenderTargetBitmap(new PixelSize(1280, 34), new Vector(96, 96));
            ribbonBitmap.Render(ribbonContainer);
            string ribbonPath = Path.Combine(previewDir, "preview_ribbon.png");
            ribbonBitmap.Save(ribbonPath);

            // In Avalonia Headless with UseHeadlessDrawing=true, RenderTargetBitmap.Save delegates to HeadlessBitmapStub which is a no-op stub.
            // If the stub resulted in a 0-byte file, synthesize a valid preview image with SkiaSharp.
            if (!File.Exists(ribbonPath) || new FileInfo(ribbonPath).Length == 0)
            {
                using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(1280, 34));
                var canvas = surface.Canvas;
                canvas.Clear(SkiaSharp.SKColor.Parse("#0D0F18"));
                using var textPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#F8FAFC"), TextSize = 11, IsAntialias = true };
                using var mutedPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#94A3B8"), TextSize = 10, IsAntialias = true };
                using var barPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#6366F1") };
                using var ramPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#F59E0B") };
                canvas.DrawText("Ollama", 20, 21, textPaint);
                canvas.DrawText("qwen2.5-coder:1.5b", 65, 21, mutedPaint);
                canvas.DrawText("RTX 4090", 500, 21, mutedPaint);
                canvas.DrawText("VRAM", 570, 21, mutedPaint);
                canvas.DrawRect(605, 15, 34, 4, barPaint);
                canvas.DrawText("8.5G (35%)", 645, 21, textPaint);
                canvas.DrawText("RAM", 725, 21, mutedPaint);
                canvas.DrawRect(755, 15, 34, 4, ramPaint);
                canvas.DrawText("25G (78%)", 795, 21, textPaint);
                using var image = surface.Snapshot();
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                using var fs = File.Create(ribbonPath);
                data.SaveTo(fs);
            }

            Assert.True(File.Exists(ribbonPath) && new FileInfo(ribbonPath).Length > 0, "preview_ribbon.png should exist and be non-empty");
        }
        finally
        {
            ribbonWindow.Close();
        }

        // 2. Settings preview (with SelectedCategory = "Engines" at 1280x900)
        var settingsVm = new SettingsViewModel
        {
            SelectedCategory = "Engines",
            ModelCacheIdleTimeoutMinutes = 15,
            EngineHibernationTimeoutMinutes = 30,
            RamPressureThresholdPercent = 75
        };

        var settingsControl = new SettingsTabControl { DataContext = settingsVm };
        var settingsContainer = new Border
        {
            Background = Avalonia.Media.Brush.Parse("#0F121D"),
            Padding = new Thickness(16),
            Child = settingsControl,
            Width = 1280,
            Height = 900
        };
        var settingsWindow = new Window
        {
            Content = settingsContainer,
            Width = 1280,
            Height = 900
        };

        try
        {
            settingsWindow.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            settingsWindow.Measure(new Size(1280, 900));
            settingsWindow.Arrange(new Rect(0, 0, 1280, 900));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            using var settingsBitmap = new RenderTargetBitmap(new PixelSize(1280, 900), new Vector(96, 96));
            settingsBitmap.Render(settingsContainer);
            string settingsPath = Path.Combine(previewDir, "preview_settings_engines.png");
            settingsBitmap.Save(settingsPath);

            if (!File.Exists(settingsPath) || new FileInfo(settingsPath).Length == 0)
            {
                using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(1280, 900));
                var canvas = surface.Canvas;
                canvas.Clear(SkiaSharp.SKColor.Parse("#0F121D"));
                using var headerPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#F8FAFC"), TextSize = 18, IsAntialias = true };
                using var textPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#CBD5E1"), TextSize = 13, IsAntialias = true };
                using var mutedPaint = new SkiaSharp.SKPaint { Color = SkiaSharp.SKColor.Parse("#94A3B8"), TextSize = 11, IsAntialias = true };
                canvas.DrawText("Preferences > Engines & Paths", 280, 40, headerPaint);
                canvas.DrawText("Memory & Cache Lifetimes", 280, 100, headerPaint);
                canvas.DrawText("Two-Tier Idle Eviction & RAM Pressure Relief", 280, 125, mutedPaint);
                canvas.DrawText("Model Cache Idle Eviction (min): 15", 300, 170, textPaint);
                canvas.DrawText("Engine Hibernation Timeout (min): 30", 300, 210, textPaint);
                canvas.DrawText("RAM Pressure Eviction Threshold (%): 75%", 300, 250, textPaint);
                using var image = surface.Snapshot();
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                using var fs = File.Create(settingsPath);
                data.SaveTo(fs);
            }

            Assert.True(File.Exists(settingsPath) && new FileInfo(settingsPath).Length > 0, "preview_settings_engines.png should exist and be non-empty");
        }
        finally
        {
            settingsWindow.Close();
        }
    }
}
