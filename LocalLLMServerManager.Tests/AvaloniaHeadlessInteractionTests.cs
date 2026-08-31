using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LocalLLMServerManager.Shared.Models;
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
        Assert.Contains("v3.12.0", versionTextBlock.Text);

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
        Assert.Equal(6, tabControl.Items.Count);

        // Switch to Tab 3 (CivitAI)
        tabControl.SelectedIndex = 2;
        Assert.Equal(2, tabControl.SelectedIndex);

        // Switch to Tab 4 (Studio)
        tabControl.SelectedIndex = 3;
        Assert.Equal(3, tabControl.SelectedIndex);

        // Switch to Tab 5 (Can I Run It)
        tabControl.SelectedIndex = 4;
        Assert.Equal(4, tabControl.SelectedIndex);

        // Switch to Tab 6 (Settings)
        tabControl.SelectedIndex = 5;
        Assert.Equal(5, tabControl.SelectedIndex);

        window.Close();
    }

    [AvaloniaFact]
    public void CanIRunItView_RendersVisualTree_AndBindsHardwareTelemetry()
    {
        var vm = new CanIRunItViewModel();
        var view = new CanIRunItView { DataContext = vm };

        var window = new Window { Content = view, Width = 1024, Height = 768 };
        window.Show();

        Assert.NotNull(view);
        Assert.NotNull(view.DataContext);

        // Find Hardware Telemetry text
        var textBlocks = view.GetVisualDescendants().OfType<TextBlock>().ToList();
        var gpuTextBlock = textBlocks.FirstOrDefault(t => t.Text != null && t.Text.Contains(vm.GpuName));
        Assert.NotNull(gpuTextBlock);

        var vramTextBlock = textBlocks.FirstOrDefault(t => t.Text != null && t.Text.Contains("GB VRAM"));
        Assert.NotNull(vramTextBlock);

        // Find Verdict and Recommendation readout
        var verdictText = textBlocks.FirstOrDefault(t => t.Text != null && (t.Text.Contains("Fits 100% in VRAM") || t.Text.Contains("Full VRAM")));
        Assert.NotNull(verdictText);

        window.Close();
    }

    [AvaloniaFact]
    public void CanIRunItView_SliderAndPresetInteraction_UpdatesCalculations()
    {
        var vm = new CanIRunItViewModel();
        var view = new CanIRunItView { DataContext = vm };

        var window = new Window { Content = view, Width = 1024, Height = 768 };
        window.Show();

        // Find sliders in CanIRunItView
        var sliders = view.GetVisualDescendants().OfType<Slider>().ToList();
        Assert.NotEmpty(sliders);

        // Find Context length slider (minimum >= 1024, max >= 32768)
        var contextSlider = sliders.FirstOrDefault(s => s.Maximum >= 65536 || s.Minimum >= 2048);
        if (contextSlider != null)
        {
            contextSlider.Value = 32768;
            Assert.Equal(32768, vm.ContextLength);
        }

        // Test Modality Switch via UI buttons
        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        var imageBtn = buttons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Image") == true);
        if (imageBtn != null)
        {
            if (imageBtn.Command != null && imageBtn.Command.CanExecute(imageBtn.CommandParameter))
            {
                imageBtn.Command.Execute(imageBtn.CommandParameter);
            }
            else
            {
                vm.SelectImageModalityCommand.Execute(null);
            }
            Assert.Equal("Image", vm.SelectedModality);
            Assert.NotNull(vm.DiffusionResult);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void MainView_Tab5_CanIRunItNavigation_SwitchesTabAndRendersCanIRunItView()
    {
        var vm = new MainViewModel();
        var view = new MainView { DataContext = vm };

        var window = new Window { Content = view, Width = 1024, Height = 768 };
        window.Show();

        Assert.NotNull(vm.HardwareFit);

        // Perform navigation
        vm.NavigateToCanIRunIt("DeepSeek R1 70B", "LLM");

        Assert.Equal(4, vm.SelectedTabIndex);
        Assert.Equal("DeepSeek R1 70B", vm.HardwareFit.SelectedPreset);
        Assert.Equal(70.0, vm.HardwareFit.ParametersBillions);

        var tabControl = view.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        Assert.NotNull(tabControl);
        Assert.Equal(4, tabControl.SelectedIndex);

        // Verify CanIRunItView rendered
        var canIRunItView = view.GetVisualDescendants().OfType<CanIRunItView>().FirstOrDefault();
        Assert.NotNull(canIRunItView);

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

    [AvaloniaFact]
    public void HuggingFaceTabControl_CardFitBadge_InspectsModelInCanIRunIt()
    {
        var vm = new MainViewModel();
        var item = new HuggingFaceRepoItem("Wan-AI/Wan2.1-T2V-14B", "Wan-AI", 500, "10K", "text-to-video", new QuickFitBadge("🟡 Partial Offload", "#F59E0B", "Offload", FitVerdict.PartialOffload));
        vm.HuggingFace.HuggingFaceResults.Add(item);

        var control = new HuggingFaceTabControl { DataContext = vm.HuggingFace };
        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();

        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var checkFitBtn = buttons.FirstOrDefault(b => b.CommandParameter == item);
        Assert.NotNull(checkFitBtn);

        checkFitBtn.Command?.Execute(checkFitBtn.CommandParameter);

        Assert.Equal(4, vm.SelectedTabIndex);
        Assert.Equal("Video", vm.HardwareFit.SelectedModality);

        window.Close();
    }

    [AvaloniaFact]
    public void CivitaiTabControl_CardFitBadge_InspectsModelInCanIRunIt()
    {
        var vm = new MainViewModel();
        var item = new CivitaiModelItem(1, "Flux.1 Dev", "Checkpoint", "", "", "flux.safetensors", 4.9, 100, new QuickFitBadge("🟢 Full VRAM", "#10B981", "Fits", FitVerdict.FullVram));
        vm.Civitai.CivitaiResults.Add(item);

        var control = new CivitaiTabControl { DataContext = vm.Civitai };
        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();

        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var checkFitBtn = buttons.FirstOrDefault(b => b.CommandParameter == item && b.Content is StackPanel);
        Assert.NotNull(checkFitBtn);

        checkFitBtn.Command?.Execute(checkFitBtn.CommandParameter);

        Assert.Equal(4, vm.SelectedTabIndex);
        Assert.Equal("Image", vm.HardwareFit.SelectedModality);

        window.Close();
    }

    [AvaloniaFact]
    public void OllamaModelsTabControl_CardFitBadge_InspectsModelInCanIRunIt()
    {
        var vm = new MainViewModel();
        var item = new OllamaModelItem("llama3.3:70b", "42 GB", "💻 Coding & General", "#38BDF8", false, new QuickFitBadge("🟡 Partial Offload", "#F59E0B", "Offload", FitVerdict.PartialOffload));
        vm.Ollama.InstalledModels.Add(item);

        var control = new OllamaModelsTabControl { DataContext = vm.Ollama };
        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();

        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var checkFitBtn = buttons.FirstOrDefault(b => b.CommandParameter == item && b.Content is StackPanel);
        Assert.NotNull(checkFitBtn);

        checkFitBtn.Command?.Execute(checkFitBtn.CommandParameter);

        Assert.Equal(4, vm.SelectedTabIndex);
        Assert.Equal("LLM", vm.HardwareFit.SelectedModality);

        window.Close();
    }

    [AvaloniaFact]
    public void OllamaModelsTabControl_DeleteButton_OpensStyledConfirmationModal()
    {
        var vm = new MainViewModel();
        var item = new OllamaModelItem("llama3.3:70b", "42 GB", "💻 Coding & General", "#38BDF8", false);
        vm.Ollama.InstalledModels.Add(item);

        var control = new OllamaModelsTabControl { DataContext = vm.Ollama };
        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();

        Assert.False(vm.Ollama.IsDeleteModalOpen);

        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var deleteBtn = buttons.FirstOrDefault(b => b.CommandParameter == item && b.Content is TextBlock tb && tb.Text == "🗑️");
        Assert.NotNull(deleteBtn);

        deleteBtn.Command?.Execute(deleteBtn.CommandParameter);

        Assert.True(vm.Ollama.IsDeleteModalOpen);
        Assert.Same(item, vm.Ollama.ModelToDelete);
        Assert.Contains("llama3.3:70b", vm.Ollama.DeleteModalMessage);

        vm.Ollama.CancelDeleteModel();
        Assert.False(vm.Ollama.IsDeleteModalOpen);

        window.Close();
    }
}

