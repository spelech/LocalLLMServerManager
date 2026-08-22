using System;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ThemeServiceTests
{
    [Fact]
    public void DefaultTheme_IsMatteCarbon()
    {
        var dict = new ResourceDictionary();
        var service = new ThemeService(dict);

        Assert.Equal(AppTheme.MatteCarbon, service.CurrentTheme);
    }

    [Fact]
    public void SetTheme_OledBlack_UpdatesCurrentThemeAndFiresThemeChangedAndMutatesResources()
    {
        var dict = new ResourceDictionary();
        var service = new ThemeService(dict);

        AppTheme? changedTheme = null;
        service.ThemeChanged += (_, theme) => changedTheme = theme;

        service.SetTheme(AppTheme.OledBlack);

        Assert.Equal(AppTheme.OledBlack, service.CurrentTheme);
        Assert.Equal(AppTheme.OledBlack, changedTheme);

        // Assert brushes
        Assert.Equal(Color.Parse("#000000"), ((SolidColorBrush)dict["BgDarkBrush"]!).Color);
        Assert.Equal(Color.Parse("#121212"), ((SolidColorBrush)dict["BgSurfaceBrush"]!).Color);
        Assert.Equal(Color.Parse("#18181b"), ((SolidColorBrush)dict["BgCardBrush"]!).Color);
        Assert.Equal(Color.Parse("#27272a"), ((SolidColorBrush)dict["GlassBorderBrush"]!).Color);
        Assert.Equal(Color.Parse("#27272a"), ((SolidColorBrush)dict["BorderColorBrush"]!).Color);
        Assert.Equal(Color.Parse("#f4f4f5"), ((SolidColorBrush)dict["TextMainBrush"]!).Color);
        Assert.Equal(Color.Parse("#71717a"), ((SolidColorBrush)dict["TextMutedBrush"]!).Color);
        Assert.Equal(Color.Parse("#3b82f6"), ((SolidColorBrush)dict["PrimaryBrush"]!).Color);
        Assert.Equal(Color.Parse("#60a5fa"), ((SolidColorBrush)dict["SecondaryBrush"]!).Color);
        Assert.Equal(Color.Parse("#60a5fa"), ((SolidColorBrush)dict["AccentBrush"]!).Color);
        Assert.Equal(Color.Parse("#000000"), ((SolidColorBrush)dict["GlassBackgroundBrush"]!).Color);
        Assert.Equal(Color.Parse("#3b82f6"), ((SolidColorBrush)dict["PrimaryGradientBrush"]!).Color);

        var glow = (SolidColorBrush)dict["BorderGlowBrush"]!;
        Assert.Equal(Color.Parse("#3b82f6"), glow.Color);
        Assert.Equal(0.4, glow.Opacity, precision: 2);

        // Assert colors
        Assert.Equal(Color.Parse("#000000"), (Color)dict["BgDarkColor"]!);
        Assert.Equal(Color.Parse("#121212"), (Color)dict["BgSurfaceColor"]!);
        Assert.Equal(Color.Parse("#18181b"), (Color)dict["BgCardColor"]!);
        Assert.Equal(Color.Parse("#27272a"), (Color)dict["BorderColor"]!);
        Assert.Equal(Color.Parse("#f4f4f5"), (Color)dict["TextMainColor"]!);
        Assert.Equal(Color.Parse("#71717a"), (Color)dict["TextMutedColor"]!);
        Assert.Equal(Color.Parse("#3b82f6"), (Color)dict["PrimaryColor"]!);
        Assert.Equal(Color.Parse("#60a5fa"), (Color)dict["SecondaryColor"]!);
        Assert.Equal(Color.Parse("#60a5fa"), (Color)dict["AccentColor"]!);
    }

    [Fact]
    public void SetTheme_Light_UpdatesCurrentThemeAndFiresThemeChangedAndMutatesResources()
    {
        var dict = new ResourceDictionary();
        var service = new ThemeService(dict);

        AppTheme? changedTheme = null;
        service.ThemeChanged += (_, theme) => changedTheme = theme;

        service.SetTheme(AppTheme.Light);

        Assert.Equal(AppTheme.Light, service.CurrentTheme);
        Assert.Equal(AppTheme.Light, changedTheme);

        // Assert brushes
        Assert.Equal(Color.Parse("#ffffff"), ((SolidColorBrush)dict["BgDarkBrush"]!).Color);
        Assert.Equal(Color.Parse("#f6f8fa"), ((SolidColorBrush)dict["BgSurfaceBrush"]!).Color);
        Assert.Equal(Color.Parse("#ffffff"), ((SolidColorBrush)dict["BgCardBrush"]!).Color);
        Assert.Equal(Color.Parse("#d0d7de"), ((SolidColorBrush)dict["GlassBorderBrush"]!).Color);
        Assert.Equal(Color.Parse("#d0d7de"), ((SolidColorBrush)dict["BorderColorBrush"]!).Color);
        Assert.Equal(Color.Parse("#1f2328"), ((SolidColorBrush)dict["TextMainBrush"]!).Color);
        Assert.Equal(Color.Parse("#656d76"), ((SolidColorBrush)dict["TextMutedBrush"]!).Color);
        Assert.Equal(Color.Parse("#0969da"), ((SolidColorBrush)dict["PrimaryBrush"]!).Color);
        Assert.Equal(Color.Parse("#218bff"), ((SolidColorBrush)dict["SecondaryBrush"]!).Color);
        Assert.Equal(Color.Parse("#218bff"), ((SolidColorBrush)dict["AccentBrush"]!).Color);
        Assert.Equal(Color.Parse("#ffffff"), ((SolidColorBrush)dict["GlassBackgroundBrush"]!).Color);
        Assert.Equal(Color.Parse("#0969da"), ((SolidColorBrush)dict["PrimaryGradientBrush"]!).Color);

        var glow = (SolidColorBrush)dict["BorderGlowBrush"]!;
        Assert.Equal(Color.Parse("#0969da"), glow.Color);
        Assert.Equal(0.4, glow.Opacity, precision: 2);
    }

    [Fact]
    public void SetTheme_MatteCarbon_UpdatesCurrentThemeAndFiresThemeChangedAndMutatesResources()
    {
        var dict = new ResourceDictionary();
        var service = new ThemeService(dict);

        // Switch to Light first, then back to MatteCarbon
        service.SetTheme(AppTheme.Light);

        AppTheme? changedTheme = null;
        service.ThemeChanged += (_, theme) => changedTheme = theme;

        service.SetTheme(AppTheme.MatteCarbon);

        Assert.Equal(AppTheme.MatteCarbon, service.CurrentTheme);
        Assert.Equal(AppTheme.MatteCarbon, changedTheme);

        Assert.Equal(Color.Parse("#0d1117"), ((SolidColorBrush)dict["BgDarkBrush"]!).Color);
        Assert.Equal(Color.Parse("#161b22"), ((SolidColorBrush)dict["BgSurfaceBrush"]!).Color);
        Assert.Equal(Color.Parse("#1c2128"), ((SolidColorBrush)dict["BgCardBrush"]!).Color);
        Assert.Equal(Color.Parse("#30363d"), ((SolidColorBrush)dict["GlassBorderBrush"]!).Color);
        Assert.Equal(Color.Parse("#f0f6fc"), ((SolidColorBrush)dict["TextMainBrush"]!).Color);
        Assert.Equal(Color.Parse("#8b949e"), ((SolidColorBrush)dict["TextMutedBrush"]!).Color);
        Assert.Equal(Color.Parse("#388bfd"), ((SolidColorBrush)dict["PrimaryBrush"]!).Color);
        Assert.Equal(Color.Parse("#58a6ff"), ((SolidColorBrush)dict["SecondaryBrush"]!).Color);
        Assert.Equal(Color.Parse("#79c0ff"), ((SolidColorBrush)dict["AccentBrush"]!).Color);
    }

    [Fact]
    public void SetTheme_WithExistingBrushInstances_MutatesColorInPlace()
    {
        var dict = new ResourceDictionary();
        var existingBrush = new SolidColorBrush(Color.Parse("#111111"));
        dict["BgDarkBrush"] = existingBrush;

        var service = new ThemeService(dict);
        service.SetTheme(AppTheme.Light);

        // Ensure the exact instance in memory was updated
        Assert.Same(existingBrush, dict["BgDarkBrush"]);
        Assert.Equal(Color.Parse("#ffffff"), existingBrush.Color);
    }

    [AvaloniaFact]
    public void SetTheme_WithHeadlessApp_MutatesApplicationRequestedThemeVariant()
    {
        var service = new ThemeService();
        service.SetTheme(AppTheme.Light);
        Assert.Equal(ThemeVariant.Light, Avalonia.Application.Current?.RequestedThemeVariant);

        service.SetTheme(AppTheme.OledBlack);
        Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current?.RequestedThemeVariant);

        service.SetTheme(AppTheme.MatteCarbon);
        Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current?.RequestedThemeVariant);
    }

    [Fact]
    public void SettingsViewModel_ThemeIntegration_ChangesThemeOnPropertySet()
    {
        var mockThemeService = new Mock<IThemeService>();
        mockThemeService.SetupGet(t => t.CurrentTheme).Returns(AppTheme.MatteCarbon);

        var vm = new SettingsViewModel(mockThemeService.Object);

        Assert.Equal("Matte Carbon (Default)", vm.SelectedTheme);
        Assert.Equal(3, vm.AvailableThemes.Count);

        vm.SelectedTheme = "OLED Pure Black";
        mockThemeService.Verify(t => t.SetTheme(AppTheme.OledBlack), Times.Once);

        vm.SelectedTheme = "Clean Light";
        mockThemeService.Verify(t => t.SetTheme(AppTheme.Light), Times.Once);

        vm.SelectedTheme = "Matte Carbon (Default)";
        mockThemeService.Verify(t => t.SetTheme(AppTheme.MatteCarbon), Times.Once);
    }

    [Fact]
    public void SettingsViewModel_MappingHelpers_CoverAllThemeVariants()
    {
        Assert.Equal("Matte Carbon (Default)", SettingsViewModel.MapThemeToString(AppTheme.MatteCarbon));
        Assert.Equal("OLED Pure Black", SettingsViewModel.MapThemeToString(AppTheme.OledBlack));
        Assert.Equal("Clean Light", SettingsViewModel.MapThemeToString(AppTheme.Light));

        Assert.Equal(AppTheme.MatteCarbon, SettingsViewModel.MapStringToTheme("Matte Carbon (Default)"));
        Assert.Equal(AppTheme.OledBlack, SettingsViewModel.MapStringToTheme("OLED Pure Black"));
        Assert.Equal(AppTheme.Light, SettingsViewModel.MapStringToTheme("Clean Light"));
        Assert.Equal(AppTheme.MatteCarbon, SettingsViewModel.MapStringToTheme("unknown"));
        Assert.Equal(AppTheme.MatteCarbon, SettingsViewModel.MapStringToTheme(null));
    }
}
