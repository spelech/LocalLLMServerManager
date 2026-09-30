using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using LocalLLMServerManager.Shared.ViewModels;
using LocalLLMServerManager.Shared.Views;
using LocalLLMServerManager.Shared.Views.Controls;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class FluidStudioAndCopilotUiTests
{
    [AvaloniaFact]
    public void TelemetryRibbonControl_RendersEngineCards_AndTriggersToggleCommand()
    {
        var vm = new TelemetryViewModel();
        string? toggledEngine = null;
        vm.OnManageServiceRequested = key => toggledEngine = key;

        var control = new TelemetryRibbonControl { DataContext = vm };
        var window = new Window { Content = control, Width = 1280, Height = 100 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 1. Verify all 4 engine card labels exist in the visual tree
        var textBlocks = control.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(textBlocks, t => t.Text == "Ollama");
        Assert.Contains(textBlocks, t => t.Text == "ComfyUI");
        Assert.Contains(textBlocks, t => t.Text == "SD Forge");
        Assert.Contains(textBlocks, t => t.Text == "Kokoro TTS");

        // 2. Find Ollama card action button and execute click
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var ollamaBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "ollama");
        Assert.NotNull(ollamaBtn);

        ollamaBtn.Command?.Execute(ollamaBtn.CommandParameter);
        Assert.Equal("Ollama", toggledEngine);

        window.Close();
    }

    [AvaloniaFact]
    public void MainView_CopilotSidebar_TabsRenderAndSwitchInVisualTree()
    {
        var vm = new MainViewModel();
        var mainView = new MainView { DataContext = vm };
        var window = new Window { Content = mainView, Width = 1280, Height = 800 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 1. Initially sidebar is closed
        var sidebar = mainView.FindControl<Border>("DocumentationDrawer");
        Assert.NotNull(sidebar);
        Assert.False(sidebar.IsVisible);

        // 2. Open Copilot sidebar on Assistant tab
        vm.ToggleCopilotSidebar("Assistant");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(sidebar.IsVisible);

        // 3. Find tab buttons within sidebar
        var sidebarButtons = sidebar.GetVisualDescendants().OfType<Button>().ToList();
        var copilotTabBtn = sidebarButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Copilot") == true);
        var knowledgeTabBtn = sidebarButtons.FirstOrDefault(b => b.Content?.ToString()?.Contains("Knowledge") == true);
        Assert.NotNull(copilotTabBtn);
        Assert.NotNull(knowledgeTabBtn);

        // 4. Click Knowledge tab button
        knowledgeTabBtn.Command?.Execute(knowledgeTabBtn.CommandParameter);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Docs", vm.SelectedCopilotTab);
        Assert.True(vm.IsDocsTabActive);
        Assert.False(vm.IsAssistantTabActive);

        // 5. Click Copilot tab button
        copilotTabBtn.Command?.Execute(copilotTabBtn.CommandParameter);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Assistant", vm.SelectedCopilotTab);
        Assert.True(vm.IsAssistantTabActive);
        Assert.False(vm.IsDocsTabActive);

        window.Close();
    }

    [AvaloniaFact]
    public void EngineStudioTabControl_ModalityPills_UpdateModalityInVisualTree()
    {
        var vm = new MainViewModel();
        var studio = new EngineStudioTabControl { DataContext = vm };
        var window = new Window { Content = studio, Width = 1280, Height = 800 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 1. Initial modality is Image
        Assert.Equal("Image", vm.Studio.SelectedModality);
        Assert.True(vm.Studio.IsImageModalityActive);

        // 2. Find modality selector buttons
        var buttons = studio.GetVisualDescendants().OfType<Button>().ToList();
        var videoBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "Video");
        var mesh3dBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "3D Mesh");
        var audioBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "Audio");

        Assert.NotNull(videoBtn);
        Assert.NotNull(mesh3dBtn);
        Assert.NotNull(audioBtn);

        // 3. Click Video modality pill
        videoBtn.Command?.Execute("Video");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Video", vm.Studio.SelectedModality);
        Assert.True(vm.Studio.IsVideoModalityActive);
        Assert.False(vm.Studio.IsImageModalityActive);

        // 4. Click 3D Mesh modality pill
        mesh3dBtn.Command?.Execute("3D Mesh");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("3D Mesh", vm.Studio.SelectedModality);
        Assert.True(vm.Studio.Is3DModalityActive);

        // 5. Click Audio modality pill
        audioBtn.Command?.Execute("Audio");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Audio", vm.Studio.SelectedModality);
        Assert.True(vm.Studio.IsAudioModalityActive);

        window.Close();
    }

    [AvaloniaFact]
    public void EngineStudioTabControl_PromptDock_ParametersToggleInVisualTree()
    {
        var vm = new MainViewModel();
        var studio = new EngineStudioTabControl { DataContext = vm };
        var window = new Window { Content = studio, Width = 1280, Height = 800 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 1. Verify prompt dock input exists
        var textBoxes = studio.GetVisualDescendants().OfType<TextBox>().ToList();
        var promptInput = textBoxes.FirstOrDefault(t => t.AcceptsReturn);
        Assert.NotNull(promptInput);

        // 2. Parameters Flyout is initially closed
        Assert.False(vm.Studio.IsParametersFlyoutOpen);

        // 3. Find parameters toggle button
        var buttons = studio.GetVisualDescendants().OfType<Button>().ToList();
        var settingsBtn = buttons.FirstOrDefault(b => b.Command == vm.Studio.ToggleParametersFlyoutCommand);
        Assert.NotNull(settingsBtn);

        // 4. Toggle parameters flyout open
        settingsBtn.Command?.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(vm.Studio.IsParametersFlyoutOpen);

        // 5. Toggle parameters flyout closed
        settingsBtn.Command?.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(vm.Studio.IsParametersFlyoutOpen);

        window.Close();
    }

    [AvaloniaFact]
    public void EngineStudioTabControl_ModalitySelection_SwapsViewportsInVisualTree()
    {
        var vm = new MainViewModel();
        var studio = new EngineStudioTabControl { DataContext = vm };
        var window = new Window { Content = studio, Width = 1280, Height = 900 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = studio.GetVisualDescendants().OfType<Button>().ToList();

        // 1. Switch to Text Modality
        var textBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "Text");
        Assert.NotNull(textBtn);
        textBtn.Command?.Execute("Text");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(vm.Studio.IsTextModalityActive);
        Assert.False(vm.Studio.IsImageModalityActive);
        Assert.Contains(studio.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Local LLM Reasoning Stream") == true);

        // 2. Switch to Video Modality
        var videoBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "Video");
        Assert.NotNull(videoBtn);
        videoBtn.Command?.Execute("Video");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(vm.Studio.IsVideoModalityActive);
        Assert.False(vm.Studio.IsTextModalityActive);
        Assert.Contains(studio.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Interactive Video Player Viewport") == true);

        // 3. Switch to 3D Mesh Modality
        var meshBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "3D Mesh");
        Assert.NotNull(meshBtn);
        meshBtn.Command?.Execute("3D Mesh");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(vm.Studio.Is3DModalityActive);
        Assert.False(vm.Studio.IsVideoModalityActive);
        Assert.Contains(studio.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Interactive 3D Mesh") == true);

        // 4. Switch to Audio Modality
        var audioBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "Audio");
        Assert.NotNull(audioBtn);
        audioBtn.Command?.Execute("Audio");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(vm.Studio.IsAudioModalityActive);
        Assert.False(vm.Studio.Is3DModalityActive);
        Assert.Contains(studio.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Waveform Audio") == true);

        // 5. Switch back to Image Modality
        var imageBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "Image");
        Assert.NotNull(imageBtn);
        imageBtn.Command?.Execute("Image");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(vm.Studio.IsImageModalityActive);
        Assert.False(vm.Studio.IsAudioModalityActive);
        Assert.Contains(studio.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Interactive Image Canvas") == true);

        window.Close();
    }

    [AvaloniaFact]
    public void EngineStudioTabControl_PromptDock_SlidersAndAspectPills_MutateParametersInVisualTree()
    {
        var vm = new MainViewModel();
        var studio = new EngineStudioTabControl { DataContext = vm };
        var window = new Window { Content = studio, Width = 1280, Height = 900 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 1. Open parameters flyout
        vm.Studio.IsParametersFlyoutOpen = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 2. Locate sliders for fine-tuning
        var sliders = studio.GetVisualDescendants().OfType<Slider>().ToList();
        var stepsSlider = sliders.FirstOrDefault(s => s.Minimum == 10 && s.Maximum == 100);
        var cfgSlider = sliders.FirstOrDefault(s => s.Minimum >= 1.0 && s.Maximum <= 20.0);

        Assert.NotNull(stepsSlider);
        Assert.NotNull(cfgSlider);

        // Mutate steps and cfg scale via visual tree sliders
        stepsSlider.Value = 42;
        cfgSlider.Value = 8.5;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Equal(42, vm.StudioSteps);
        Assert.Equal(8.5, vm.StudioCfgScale);

        // 3. Locate Aspect Ratio buttons
        var buttons = studio.GetVisualDescendants().OfType<Button>().ToList();
        var pill16x9 = buttons.FirstOrDefault(b => b.Classes.Contains("aspect-pill") && b.Content?.ToString() == "16:9");
        var pill1x1 = buttons.FirstOrDefault(b => b.Classes.Contains("aspect-pill") && b.Content?.ToString() == "1:1");
        var pill9x16 = buttons.FirstOrDefault(b => b.Classes.Contains("aspect-pill") && b.Content?.ToString() == "9:16");

        Assert.NotNull(pill16x9);
        Assert.NotNull(pill1x1);
        Assert.NotNull(pill9x16);

        // Click 1:1
        pill1x1.Command?.Execute("1:1");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("1:1", vm.ActiveAspectPreset);
        Assert.Equal("1:1", vm.Studio.ActiveAspectPreset);

        // Click 9:16
        pill9x16.Command?.Execute("9:16");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("9:16", vm.ActiveAspectPreset);
        Assert.Equal("9:16", vm.Studio.ActiveAspectPreset);

        // 4. Prompt dock input and starter chip
        var promptBox = studio.FindControl<TextBox>("PromptDockInput");
        Assert.NotNull(promptBox);
        promptBox.Text = "A futuristic cyberpunk city rendered in octane, 8k";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("A futuristic cyberpunk city rendered in octane, 8k", vm.PromptText);

        window.Close();
    }

    [AvaloniaFact]
    public void TelemetryHeaderControl_EngineCards_RenderAndTriggerInVisualTree()
    {
        var vm = new TelemetryViewModel();
        string? invokedKey = null;
        vm.OnManageServiceRequested = key => invokedKey = key;

        var control = new TelemetryHeaderControl { DataContext = vm };
        var window = new Window { Content = control, Width = 1280, Height = 200 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 1. Verify all 4 engine card titles appear in visual tree
        var textBlocks = control.GetVisualDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(textBlocks, t => t.Text == "Ollama");
        Assert.Contains(textBlocks, t => t.Text == "SD Forge");
        Assert.Contains(textBlocks, t => t.Text == "ComfyUI");
        Assert.Contains(textBlocks, t => t.Text == "Kokoro TTS");

        // 2. Find and trigger each action button
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();

        var ollamaBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "ollama");
        Assert.NotNull(ollamaBtn);
        ollamaBtn.Command?.Execute("ollama");
        Assert.Equal("Ollama", invokedKey);

        var forgeBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "forge");
        Assert.NotNull(forgeBtn);
        forgeBtn.Command?.Execute("forge");
        Assert.Equal("SD Forge", invokedKey);

        var comfyBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "comfyui");
        Assert.NotNull(comfyBtn);
        comfyBtn.Command?.Execute("comfyui");
        Assert.Equal("ComfyUI", invokedKey);

        var kokoroBtn = buttons.FirstOrDefault(b => b.CommandParameter?.ToString() == "kokoro");
        Assert.NotNull(kokoroBtn);
        kokoroBtn.Command?.Execute("kokoro");
        Assert.Equal("Kokoro TTS", invokedKey);

        window.Close();
    }

    [AvaloniaFact]
    public void DocumentationTabControl_AskCopilot_TransfersGuideContextToAssistantInVisualTree()
    {
        var vm = new MainViewModel();
        var mainView = new MainView { DataContext = vm };
        var window = new Window { Content = mainView, Width = 1400, Height = 900 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 1. Open docked Copilot sidebar on Knowledge / Docs tab
        vm.ToggleCopilotSidebar("Docs");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsDocsTabActive);
        Assert.False(vm.IsAssistantTabActive);

        // 2. Locate DocumentationTabControl in visual tree
        var docControl = mainView.GetVisualDescendants().OfType<DocumentationTabControl>().FirstOrDefault();
        Assert.NotNull(docControl);

        // Select first section (this triggers IsDetailActive = true in narrow sidebar mode)
        var firstSection = vm.Documentation.Sections.First();
        vm.Documentation.SelectSection(firstSection.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var expectedGuideTitle = firstSection.Title;

        // 3. Find "Ask Copilot" button in DocumentationTabControl
        var buttons = docControl.GetVisualDescendants().OfType<Button>().ToList();
        var askCopilotBtn = buttons.FirstOrDefault(b => b.Command == vm.Documentation.AskCopilotAboutGuideCommand
            || b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Ask Copilot"));
        Assert.NotNull(askCopilotBtn);

        // 4. Click "Ask Copilot"
        askCopilotBtn.Command?.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 5. Verify tab automatically switched to Assistant and prompt was transferred
        Assert.Equal("Assistant", vm.SelectedCopilotTab);
        Assert.True(vm.IsAssistantTabActive);
        Assert.False(vm.IsDocsTabActive);
        Assert.Contains(expectedGuideTitle, vm.Assistant.InputText);

        window.Close();
    }

    [AvaloniaFact]
    public void AiAssistantTabControl_VisualTree_InputAndQuickActions_InteractSuccessfully()
    {
        var vm = new AiAssistantViewModel();
        var control = new AiAssistantTabControl { DataContext = vm };
        var window = new Window { Content = control, Width = 600, Height = 800 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // 1. Locate prompt input TextBox
        var textBoxes = control.GetVisualDescendants().OfType<TextBox>().ToList();
        var promptBox = textBoxes.FirstOrDefault(tb => tb.Watermark != null && tb.Watermark.Contains("Ask anything"));
        Assert.NotNull(promptBox);

        // Type input into prompt box
        promptBox.Text = "Explain how VRAM paging works";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Explain how VRAM paging works", vm.InputText);

        // 2. Locate suggestion chips in visual tree
        var buttons = control.GetVisualDescendants().OfType<Button>().ToList();
        var suggestionBtn = buttons.FirstOrDefault(b => b.CommandParameter is string s && vm.SuggestionChips.Contains(s));
        Assert.NotNull(suggestionBtn);

        var clickedPrompt = suggestionBtn.CommandParameter?.ToString();
        Assert.NotNull(clickedPrompt);

        // Click suggestion chip
        suggestionBtn.Command?.Execute(clickedPrompt);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Verify InputText or message list updated with clicked suggestion
        Assert.True(vm.InputText == clickedPrompt || vm.Messages.Any(m => m.Content == clickedPrompt));

        window.Close();
    }

    [AvaloniaFact]
    public void EngineStudioTabControl_EndToEndGenerationFromDock_TriggersExpectedWorkflowAcrossAllModalities()
    {
        MainViewModel.EnableAutomaticPolling = false;
        var vm = new MainViewModel();
        var studio = new EngineStudioTabControl { DataContext = vm };
        var window = new Window { Content = studio, Width = 1280, Height = 900 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var buttons = studio.GetVisualDescendants().OfType<Button>().ToList();
        var generateBtn = buttons.FirstOrDefault(b => b.Command == vm.GenerateFromDockCommand);
        Assert.NotNull(generateBtn);

        var promptBox = studio.FindControl<TextBox>("PromptDockInput");
        Assert.NotNull(promptBox);

        // 1. Image Modality Generation from Dock
        vm.SelectModality("Image");
        vm.PromptText = "Cyberpunk neon street, octane render";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        generateBtn.Command?.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Cyberpunk neon street, octane render", vm.ImagePrompt);

        // 2. Text Modality Generation from Dock
        vm.SelectModality("Text");
        vm.PromptText = "Explain quantum computing";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        generateBtn.Command?.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Explain quantum computing", vm.OllamaPrompt);
        Assert.True(vm.IsGeneratingOllamaText || !string.IsNullOrWhiteSpace(vm.OllamaResponseText));

        // 3. Video Modality Generation from Dock
        vm.SelectModality("Video");
        vm.PromptText = "Cinematic drone shot flying over waterfalls";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        generateBtn.Command?.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Cinematic drone shot flying over waterfalls", vm.VideoPrompt);

        // 4. 3D Mesh Modality Generation from Dock
        vm.SelectModality("3D Mesh");
        vm.PromptText = "Ornate ancient sword with glowing crystal blade";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        generateBtn.Command?.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Ornate ancient sword with glowing crystal blade", vm.Prompt3D);

        // 5. Audio Modality Generation from Dock
        vm.SelectModality("Audio");
        vm.PromptText = "Narrate a story about space exploration";
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        generateBtn.Command?.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Narrate a story about space exploration", vm.Audio?.Prompt);

        // 6. Diagnostic Test Flight Execution from UI Button
        var testFlightBtn = studio.FindControl<Button>("DiagnosticTestFlightButton");
        Assert.NotNull(testFlightBtn);
        testFlightBtn.Command?.Execute(null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(vm.IsTestFlightOpen);
        var modal = studio.GetVisualDescendants().OfType<TestFlightModalControl>().FirstOrDefault();
        Assert.NotNull(modal);
        Assert.True(modal.IsVisible);

        window.Close();
    }
}
