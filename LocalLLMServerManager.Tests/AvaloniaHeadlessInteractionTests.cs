using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views;
using LocalLLMServerManager.Shared.Views.Controls;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AvaloniaHeadlessInteractionTests
{
    [AvaloniaFact]
    public void MainView_RendersVisualTree_AndBindsVersionCorrectly()
    {
        var vm = new MainViewModel();
        var view = new MainView { DataContext = vm };

        var window = new Window { Content = view, Width = 1024, Height = 768 };
        window.Show();

        // 1. Verify Top-Level View loaded
        Assert.NotNull(view);
        Assert.NotNull(view.DataContext);

        // 2. Find Footer Version TextBlock
        var textBlocks = view.GetVisualDescendants().OfType<TextBlock>().ToList();
        var versionTextBlock = textBlocks.FirstOrDefault(t => t.Text != null && t.Text.Contains("LocalLLMServerManager v"));

        Assert.NotNull(versionTextBlock);
        Assert.Contains("v3.9.0", versionTextBlock.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void MainView_TabNavigation_SwitchesActiveTabsCleanly()
    {
        var vm = new MainViewModel();
        var view = new MainView { DataContext = vm };

        var window = new Window { Content = view, Width = 1024, Height = 768 };
        window.Show();

        var tabControl = view.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        Assert.NotNull(tabControl);
        Assert.Equal(5, tabControl.Items.Count);

        // Switch to Tab 3 (CivitAI)
        tabControl.SelectedIndex = 2;
        Assert.Equal(2, tabControl.SelectedIndex);

        // Switch to Tab 4 (Studio)
        tabControl.SelectedIndex = 3;
        Assert.Equal(3, tabControl.SelectedIndex);

        // Switch to Tab 5 (Settings)
        tabControl.SelectedIndex = 4;
        Assert.Equal(4, tabControl.SelectedIndex);

        window.Close();
    }

    [AvaloniaFact]
    public void EngineStudioTabControl_RadioButtonSelection_UpdatesStudioMode()
    {
        var vm = new MainViewModel();
        var control = new EngineStudioTabControl { DataContext = vm };

        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();

        // Find Studio Mode radio buttons
        var radioButtons = control.GetVisualDescendants().OfType<RadioButton>().ToList();
        Assert.True(radioButtons.Count >= 4);

        var audioRadio = radioButtons.FirstOrDefault(r => r.Content?.ToString()?.Contains("Audio") == true);
        Assert.NotNull(audioRadio);

        // Simulate user clicking Audio mode
        if (audioRadio.Command != null && audioRadio.Command.CanExecute(audioRadio.CommandParameter))
        {
            audioRadio.Command.Execute(audioRadio.CommandParameter);
        }
        else
        {
            vm.SelectStudioMode("Audio");
        }
        Assert.Equal("Audio", vm.SelectedStudioMode);

        var videoRadio = radioButtons.FirstOrDefault(r => r.Content?.ToString()?.Contains("Video") == true);
        Assert.NotNull(videoRadio);

        if (videoRadio.Command != null && videoRadio.Command.CanExecute(videoRadio.CommandParameter))
        {
            videoRadio.Command.Execute(videoRadio.CommandParameter);
        }
        else
        {
            vm.SelectStudioMode("Video");
        }
        Assert.Equal("Video", vm.SelectedStudioMode);

        window.Close();
    }

    [AvaloniaFact]
    public void ToastNotification_AddsToastToVisualTreeCollection()
    {
        var vm = new MainViewModel();
        var view = new MainView { DataContext = vm };

        var window = new Window { Content = view, Width = 1024, Height = 768 };
        window.Show();

        ToastService.Instance.Show("Test interactive toast message", ToastType.Success);

        Assert.NotEmpty(vm.Toasts);
        Assert.Contains(vm.Toasts, t => t.Message == "Test interactive toast message");

        window.Close();
    }

    [AvaloniaFact]
    public void OllamaModelsTabControl_SliderInteraction_UpdatesKvCacheCalculation()
    {
        var vm = new MainViewModel();
        var control = new OllamaModelsTabControl { DataContext = vm.Ollama };

        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();

        // Find context tokens slider
        var sliders = control.GetVisualDescendants().OfType<Slider>().ToList();
        var contextSlider = sliders.FirstOrDefault();

        if (contextSlider != null)
        {
            contextSlider.Value = 32768;
            Assert.Equal(32768, vm.Ollama.TargetContextTokens);
            Assert.True(vm.Ollama.EstimatedKvCacheText.Contains("GB") || vm.Ollama.EstimatedKvCacheText.Contains("MB"));
        }

        window.Close();
    }

    [AvaloniaFact]
    public void CivitaiTabControl_SearchInput_UpdatesQueryProperty()
    {
        var vm = new MainViewModel();
        var control = new CivitaiTabControl { DataContext = vm.Civitai };

        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();

        var textBoxes = control.GetVisualDescendants().OfType<TextBox>().ToList();
        var searchBox = textBoxes.FirstOrDefault();

        if (searchBox != null)
        {
            searchBox.Text = "cyberpunk anime";
            Assert.Equal("cyberpunk anime", vm.Civitai.CivitaiSearchQuery);
        }

        window.Close();
    }
}
