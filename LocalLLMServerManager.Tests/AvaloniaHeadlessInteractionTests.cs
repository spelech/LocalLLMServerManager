using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views;
using LocalLLMServerManager.Shared.Views.Controls;
using LocalLLMServerManager.Views;
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
        Assert.Contains("v3.15.1", versionTextBlock.Text);

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
        Assert.Equal(4, tabControl.Items.Count);

        // Switch to Tab 2 (Workflows)
        tabControl.SelectedIndex = 1;
        Assert.Equal(1, tabControl.SelectedIndex);

        // Switch to Tab 3 (Can I Run It)
        tabControl.SelectedIndex = 2;
        Assert.Equal(2, tabControl.SelectedIndex);

        // Switch to Tab 4 (Settings)
        tabControl.SelectedIndex = 3;
        Assert.Equal(3, tabControl.SelectedIndex);

        // Verify standalone pop-out buttons exist in the navigation bar
        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        var aiAssistPopBtn = buttons.FirstOrDefault(b => b.Name == "NavAiAssistBtn");
        Assert.NotNull(aiAssistPopBtn);

        var docPopBtn = buttons.FirstOrDefault(b => b.Name == "NavDocumentationBtn");
        Assert.NotNull(docPopBtn);

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

        Assert.Equal(2, vm.SelectedTabIndex);
        Assert.Equal("DeepSeek R1 70B", vm.HardwareFit.SelectedPreset);
        Assert.Equal(70.0, vm.HardwareFit.ParametersBillions);

        var tabControl = view.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        Assert.NotNull(tabControl);
        Assert.Equal(2, tabControl.SelectedIndex);

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

        // Find Studio Mode ComboBox
        var comboBoxes = control.GetVisualDescendants().OfType<ComboBox>().ToList();
        var modeComboBox = comboBoxes.FirstOrDefault(c => c.Items.Cast<object>().Any(i => i.ToString() == "Images"));
        Assert.NotNull(modeComboBox);

        // Simulate user selecting Audio mode
        modeComboBox.SelectedItem = "Audio";
        Assert.Equal("Audio", vm.SelectedStudioMode);

        // Simulate user selecting Video mode
        modeComboBox.SelectedItem = "Video";
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

        Assert.Equal(2, vm.SelectedTabIndex);
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

        Assert.Equal(2, vm.SelectedTabIndex);
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

        Assert.Equal(2, vm.SelectedTabIndex);
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

    [AvaloniaFact]
    public void SettingsTabControl_ThemeSwitching_DoesNotCrashUI()
    {
        var vm = new MainViewModel();
        var view = new MainView { DataContext = vm };
        var window = new Window { Content = view, Width = 1024, Height = 768 };
        window.Show();

        // Switch to Settings Tab
        var tabControl = view.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        Assert.NotNull(tabControl);
        tabControl.SelectedIndex = 3;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var settingsControl = view.GetVisualDescendants().OfType<SettingsTabControl>().FirstOrDefault();
        Assert.NotNull(settingsControl);

        // 1. Test Theme Palette ComboBox UI element
        var comboBoxes = settingsControl.GetVisualDescendants().OfType<ComboBox>().ToList();
        var themeComboBox = comboBoxes.FirstOrDefault(c => c.ItemsSource == vm.Settings.AvailableThemes);
        Assert.NotNull(themeComboBox);

        themeComboBox.SelectedItem = "Clean Light";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(AppTheme.Light, ThemeService.Instance.CurrentTheme);

        themeComboBox.SelectedItem = "OLED Pure Black";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(AppTheme.OledBlack, ThemeService.Instance.CurrentTheme);

        themeComboBox.SelectedItem = "Matte Carbon (Default)";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(AppTheme.MatteCarbon, ThemeService.Instance.CurrentTheme);

        // 2. Test Framework Theme Style switching via UI buttons
        var buttons = settingsControl.GetVisualDescendants().OfType<Button>().ToList();
        var fluentBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "fluent");
        Assert.NotNull(fluentBtn);
        fluentBtn.Command?.Execute(fluentBtn.CommandParameter);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal(AppTheme.OledBlack, ThemeService.Instance.CurrentTheme);

        var semiBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "semi");
        Assert.NotNull(semiBtn);
        semiBtn.Command?.Execute(semiBtn.CommandParameter);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("semi", vm.Settings.SelectedThemeStyle);
        Assert.Equal(AppTheme.MatteCarbon, ThemeService.Instance.CurrentTheme);

        window.Close();
    }

    [AvaloniaFact]
    public void SettingsTabControl_AllActionsAndPickers_InteractCleanly()
    {
        var vm = new MainViewModel();
        var view = new MainView { DataContext = vm };
        var window = new Window { Content = view, Width = 1024, Height = 768 };
        window.Show();

        var tabControl = view.GetVisualDescendants().OfType<TabControl>().FirstOrDefault();
        Assert.NotNull(tabControl);
        tabControl.SelectedIndex = 3;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var settingsControl = view.GetVisualDescendants().OfType<SettingsTabControl>().FirstOrDefault();
        Assert.NotNull(settingsControl);

        var buttons = settingsControl.GetVisualDescendants().OfType<Button>().ToList();

        // 1. Auto-Detect Tools Button
        var autoDetectBtn = buttons.FirstOrDefault(b => b.Command == vm.Settings.AutoDetectToolsCommand);
        Assert.NotNull(autoDetectBtn);
        if (autoDetectBtn.Command.CanExecute(null))
        {
            autoDetectBtn.Command.Execute(null);
        }

        // 2. Refresh Status Button
        var refreshBtn = buttons.FirstOrDefault(b => b.Command == vm.Settings.RefreshComponentStatusesCommand);
        Assert.NotNull(refreshBtn);
        if (refreshBtn.Command.CanExecute(null))
        {
            refreshBtn.Command.Execute(null);
        }

        // 3. Toggle Video and Audio Pack Buttons
        var videoToggleBtn = buttons.FirstOrDefault(b => b.Command == vm.Settings.ToggleVideoPackCommand);
        Assert.NotNull(videoToggleBtn);
        var audioToggleBtn = buttons.FirstOrDefault(b => b.Command == vm.Settings.ToggleAudioPackCommand);
        Assert.NotNull(audioToggleBtn);

        // 4. Presets Manager Actions
        var createPresetBtn = buttons.FirstOrDefault(b => b.Command == vm.Settings.CreatePresetCommand);
        Assert.NotNull(createPresetBtn);
        createPresetBtn.Command.Execute(null);
        Assert.NotEmpty(vm.Settings.AllPresets);

        var resetDefaultsBtn = buttons.FirstOrDefault(b => b.Command == vm.Settings.ResetPresetsToDefaultCommand);
        Assert.NotNull(resetDefaultsBtn);
        resetDefaultsBtn.Command.Execute(null);

        // Preset filter buttons
        var filterBtns = buttons.Where(b => b.Command == vm.Settings.FilterPresetsCommand).ToList();
        Assert.NotEmpty(filterBtns);
        foreach (var filterBtn in filterBtns)
        {
            filterBtn.Command.Execute(filterBtn.CommandParameter);
        }

        // Export and Import JSON buttons
        var exportBtn = buttons.FirstOrDefault(b => b.Command == vm.Settings.ExportPresetsCommand);
        Assert.NotNull(exportBtn);
        exportBtn.Command.Execute(null);
        Assert.False(string.IsNullOrWhiteSpace(vm.Settings.PresetsJson));

        var importBtn = buttons.FirstOrDefault(b => b.Command == vm.Settings.ImportPresetsCommand);
        Assert.NotNull(importBtn);
        importBtn.Command.Execute(vm.Settings.PresetsJson);

        // Save Settings button
        var saveBtn = buttons.FirstOrDefault(b => b.Command == vm.Settings.SaveSettingsCommand);
        Assert.NotNull(saveBtn);
        if (saveBtn.Command.CanExecute(null))
        {
            saveBtn.Command.Execute(null);
        }

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.Close();
    }

    [AvaloniaFact]
    public void TelemetryHeaderControl_ActionsAndBadges_InteractCleanly()
    {
        var vm = new MainViewModel();
        var header = new TelemetryHeaderControl { DataContext = vm.Telemetry };
        var window = new Window { Content = header, Width = 1024, Height = 200 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Refresh telemetry status button
        var buttons = header.GetVisualDescendants().OfType<Button>().ToList();
        var refreshBtn = buttons.FirstOrDefault(b => b.Command == vm.Telemetry.RefreshStatusCommand);
        Assert.NotNull(refreshBtn);
        if (refreshBtn.Command.CanExecute(null))
        {
            refreshBtn.Command.Execute(null);
        }

        // Telemetry indicators
        var textBlocks = header.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(textBlocks, t => t.Text != null && t.Text.Contains("GB"));

        window.Close();
    }

    [AvaloniaFact]
    public void EngineStudioTabControl_GenerationAndTestFlight_InteractCleanly()
    {
        var vm = new MainViewModel();
        var studio = new EngineStudioTabControl { DataContext = vm };
        var window = new Window { Content = studio, Width = 1024, Height = 768 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = studio.GetVisualDescendants().OfType<Button>().ToList();

        // Test Flight button opens modal
        var testFlightBtn = buttons.FirstOrDefault(b => b.Command == vm.OpenTestFlightCommand);
        Assert.NotNull(testFlightBtn);
        testFlightBtn.Command.Execute(null);
        Assert.True(vm.IsTestFlightOpen);

        // Test Flight Modal Control is in visual tree
        var modalControl = studio.GetVisualDescendants().OfType<TestFlightModalControl>().FirstOrDefault();
        Assert.NotNull(modalControl);

        // Close Test Flight
        vm.CloseTestFlight();
        Assert.False(vm.IsTestFlightOpen);

        // Engine toggles
        var forgeToggleBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "forge");
        Assert.NotNull(forgeToggleBtn);
        var comfyToggleBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "comfy");
        Assert.NotNull(comfyToggleBtn);

        // Studio Preset bar controls
        var presetBar = studio.GetVisualDescendants().OfType<StudioPresetBarControl>().FirstOrDefault();
        Assert.NotNull(presetBar);

        window.Close();
    }

    [AvaloniaFact]
    public void OllamaModelsTabControl_ActionsAndFilter_InteractCleanly()
    {
        var vm = new MainViewModel();
        var item = new OllamaModelItem("llama3.3:70b", "42 GB", "💻 Coding & General", "#38BDF8", false);
        vm.Ollama.InstalledModels.Add(item);

        var control = new OllamaModelsTabControl { DataContext = vm.Ollama };
        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Context token slider
        var slider = control.GetVisualDescendants().OfType<Slider>().FirstOrDefault();
        Assert.NotNull(slider);
        slider.Value = 16384;
        Assert.Equal(16384, vm.Ollama.TargetContextTokens);

        // Unload All VRAM button
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var unloadBtn = buttons.FirstOrDefault(b => b.Command == vm.Ollama.UnloadAllVramCommand);
        Assert.NotNull(unloadBtn);
        if (unloadBtn.Command.CanExecute(null))
        {
            unloadBtn.Command.Execute(null);
        }

        // Fit verdict pill filter buttons
        var verdictBtns = buttons.Where(b => b.Command == vm.Ollama.ToggleFitVerdictCommand).ToList();
        Assert.NotEmpty(verdictBtns);
        foreach (var vBtn in verdictBtns)
        {
            vBtn.Command.Execute(vBtn.CommandParameter);
        }

        // Delete modal confirmation flow
        vm.Ollama.RequestDeleteModel(item);
        Assert.True(vm.Ollama.IsDeleteModalOpen);
        vm.Ollama.CancelDeleteModel();
        Assert.False(vm.Ollama.IsDeleteModalOpen);

        window.Close();
    }

    [AvaloniaFact]
    public void HuggingFaceTabControl_PresetsAndActions_InteractCleanly()
    {
        var vm = new MainViewModel();
        var control = new HuggingFaceTabControl { DataContext = vm.HuggingFace };
        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Search input text box
        var searchBox = control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        Assert.NotNull(searchBox);
        searchBox.Text = "DeepSeek";
        Assert.Equal("DeepSeek", vm.HuggingFace.HfSearchQuery);

        // Search button
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var searchBtn = buttons.FirstOrDefault(b => b.Command == vm.HuggingFace.SearchHuggingFaceCommand);
        Assert.NotNull(searchBtn);

        // Preset pill buttons (Multimodal, LLM, Image, Video, Audio, 3D)
        var presetBtns = buttons.Where(b => b.Command == vm.HuggingFace.ApplyPresetCommand).ToList();
        Assert.NotEmpty(presetBtns);
        foreach (var pBtn in presetBtns)
        {
            pBtn.Command.Execute(pBtn.CommandParameter);
        }

        // Modality toggle buttons
        var inputModalityBtns = buttons.Where(b => b.Command == vm.HuggingFace.ToggleInputModalityCommand).ToList();
        Assert.NotEmpty(inputModalityBtns);
        inputModalityBtns[0].Command.Execute(inputModalityBtns[0].CommandParameter);

        var outputModalityBtns = buttons.Where(b => b.Command == vm.HuggingFace.ToggleOutputModalityCommand).ToList();
        Assert.NotEmpty(outputModalityBtns);
        outputModalityBtns[0].Command.Execute(outputModalityBtns[0].CommandParameter);

        window.Close();
    }

    [AvaloniaFact]
    public void CivitaiTabControl_PresetsAndActions_InteractCleanly()
    {
        var vm = new MainViewModel();
        var control = new CivitaiTabControl { DataContext = vm.Civitai };
        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Search input
        var searchBox = control.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        Assert.NotNull(searchBox);
        searchBox.Text = "photorealistic";
        Assert.Equal("photorealistic", vm.Civitai.CivitaiSearchQuery);

        // Search button
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var searchBtn = buttons.FirstOrDefault(b => b.Command == vm.Civitai.SearchCivitaiCommand);
        Assert.NotNull(searchBtn);

        // Fit verdict pill buttons
        var verdictBtns = buttons.Where(b => b.Command == vm.Civitai.ToggleFitVerdictCommand).ToList();
        Assert.NotEmpty(verdictBtns);
        foreach (var vBtn in verdictBtns)
        {
            vBtn.Command.Execute(vBtn.CommandParameter);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void AiAssistantTabControl_VisualTreeRenders_AndBindsViewModel()
    {
        var vm = new AiAssistantViewModel();
        var control = new AiAssistantTabControl { DataContext = vm };
        var window = new Window { Content = control, Width = 1024, Height = 768 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotNull(control);
        Assert.NotNull(control.DataContext);

        // Find header title
        var textBlocks = control.GetVisualDescendants().OfType<TextBlock>().ToList();
        var titleBlock = textBlocks.FirstOrDefault(t => t.Text != null && t.Text.Contains("AI Assist"));
        Assert.NotNull(titleBlock);

        // Find model badge
        var modelBlock = textBlocks.FirstOrDefault(t => t.Text != null && t.Text == vm.SelectedModel);
        Assert.NotNull(modelBlock);

        // Find buttons (Setup, Reload, Clear, Send)
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var setupBtn = buttons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Setup") == true);
        Assert.NotNull(setupBtn);

        // Toggle setup card
        setupBtn.Command?.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(vm.IsSetupCardVisible);

        // Find prompt input box
        var textBoxes = control.GetVisualDescendants().OfType<TextBox>().ToList();
        var inputBox = textBoxes.FirstOrDefault(tb => tb.Watermark != null && tb.Watermark.Contains("Ask anything"));
        Assert.NotNull(inputBox);

        inputBox.Text = "Hello AI Assist!";
        Assert.Equal("Hello AI Assist!", vm.InputText);

        window.Close();
    }

    [AvaloniaFact]
    public void MainWindow_OpeningCompanionWindows_RegistersWithWindowSnapManager()
    {
        var mainWindow = new MainWindow();
        mainWindow.Position = new PixelPoint(500, 200);
        mainWindow.Show();

        var vm = (MainViewModel)mainWindow.DataContext!;

        // Open Docs
        vm.Documentation.PopOutNativeWindow();
        // Open AI Assist
        vm.Assistant.RequestPopOut();

        // Verify TabControl remained on its current tab (SelectedTabIndex == 0)
        Assert.Equal(0, vm.SelectedTabIndex);

        // Verify registered and snapped with WindowSnapManager
        Assert.NotNull(mainWindow.DocWindow);
        Assert.NotNull(mainWindow.AiAssistWindow);
        Assert.True(WindowSnapManager.Instance.IsSnapped(mainWindow.DocWindow!));
        Assert.True(WindowSnapManager.Instance.IsSnapped(mainWindow.AiAssistWindow!));

        mainWindow.DocWindow?.Close();
        mainWindow.AiAssistWindow?.Close();
        mainWindow.Close();
    }
}


