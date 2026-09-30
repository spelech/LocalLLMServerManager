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
}
