using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class SettingsViewModelTests
{
    private readonly string _tempDir;
    private readonly string _tempFile;

    public SettingsViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LLMServerManager_TestDir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _tempFile = Path.Combine(_tempDir, "test_runner.bat");
        File.WriteAllText(_tempFile, "@echo off\necho Test");
    }

    [Fact]
    public void DefaultState_HasExpectedPropertiesAndMissingStatus()
    {
        var vm = new SettingsViewModel();

        Assert.Equal("http://127.0.0.1:8188", vm.ComfyUiUrl);
        Assert.Equal("semi", vm.SelectedThemeStyle);
        Assert.Equal("http://127.0.0.1:5246", vm.LanAccessUrl);
        Assert.Equal("Matte Carbon (Default)", vm.SelectedTheme);
        Assert.Equal(3, vm.AvailableThemes.Count);

        Assert.Contains("Missing", vm.ForgeModelsStatus);
        Assert.Contains("Missing", vm.ThreeDModelsStatus);
        Assert.Contains("Missing", vm.WorkflowsStatus);
        Assert.Contains("Missing", vm.ComfyUiExecutableStatus);
        Assert.Contains("Missing", vm.ForgeExecutableStatus);
        Assert.Contains("Missing", vm.AudioEngineExecutableStatus);
    }

    [Fact]
    public void AudioEngineProperties_UpdateStatusAndDefaults()
    {
        var vm = new SettingsViewModel();

        Assert.Equal("", vm.AudioEngineExecutablePath);
        Assert.Equal("http://127.0.0.1:8880", vm.AudioEngineUrl);
        Assert.Equal("af_heart", vm.PreferredAudioVoice);
        Assert.Contains("Missing", vm.AudioEngineExecutableStatus);

        vm.AudioEngineExecutablePath = _tempFile;
        Assert.True(vm.AudioEngineExecutableStatus.Contains("Verified") || vm.AudioEngineExecutableStatus.Contains("Found"));
    }

    [Fact]
    public void PathChanges_UpdateStatusIndicatorsDynamically()
    {
        var vm = new SettingsViewModel();

        // Initially missing
        Assert.Contains("Missing", vm.ComfyUiExecutableStatus);
        Assert.Contains("Missing", vm.ForgeModelsStatus);

        // Point to existing file/dir
        vm.ComfyUiExecutablePath = _tempFile;
        Assert.True(vm.ComfyUiExecutableStatus.Contains("Verified") || vm.ComfyUiExecutableStatus.Contains("Found"));

        vm.ForgeModelsPath = _tempDir;
        Assert.True(vm.ForgeModelsStatus.Contains("Verified") || vm.ForgeModelsStatus.Contains("Found"));

        vm.ThreeDModelsPath = _tempDir;
        Assert.True(vm.ThreeDModelsStatus.Contains("Verified") || vm.ThreeDModelsStatus.Contains("Found"));

        vm.WorkflowsPath = _tempDir;
        Assert.True(vm.WorkflowsStatus.Contains("Verified") || vm.WorkflowsStatus.Contains("Found"));

        vm.ForgeExecutablePath = _tempFile;
        Assert.True(vm.ForgeExecutableStatus.Contains("Verified") || vm.ForgeExecutableStatus.Contains("Found"));

        vm.ComfyModelsPath = _tempDir;
        Assert.True(vm.ComfyModelsStatus.Contains("Verified") || vm.ComfyModelsStatus.Contains("Found"));

        // Point to non-existent path
        vm.ComfyUiExecutablePath = Path.Combine(_tempDir, "non_existent.exe");
        Assert.Contains("Missing", vm.ComfyUiExecutableStatus);

        vm.ForgeModelsPath = Path.Combine(_tempDir, "non_existent_folder");
        Assert.Contains("Missing", vm.ForgeModelsStatus);
    }

    [Fact]
    public async Task AutoDetectToolsAsync_PopulatesEmptyPathsAndDiscoveredStatus()
    {
        var vm = new SettingsViewModel();

        var detectPayload = new
        {
            ollama = new
            {
                isInstalled = true,
                executablePath = "C:\\Tools\\ollama.exe",
                rootDirectory = "C:\\Tools",
                modelsDirectory = "C:\\Tools\\models",
                statusMessage = "Found"
            },
            comfyUi = new
            {
                isInstalled = true,
                executablePath = _tempFile,
                rootDirectory = _tempDir,
                modelsDirectory = _tempDir,
                workflowsDirectory = _tempDir,
                statusMessage = "Found"
            },
            forge = new
            {
                isInstalled = true,
                executablePath = _tempFile,
                rootDirectory = _tempDir,
                modelsDirectory = _tempDir,
                statusMessage = "Found"
            },
            suggestedThreeDPath = _tempDir,
            suggestedWorkflowsPath = _tempDir
        };

        var json = JsonSerializer.Serialize(detectPayload);
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("/api/system/tools/detect")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(mockHandler.Object);
        await vm.AutoDetectToolsAsync("http://127.0.0.1:5246", http);

        Assert.Equal(_tempFile, vm.ComfyUiExecutablePath);
        Assert.Equal(_tempFile, vm.ForgeExecutablePath);
        Assert.Equal(_tempDir, vm.ForgeModelsPath);
        Assert.Equal(_tempDir, vm.ComfyModelsPath);
        Assert.Equal(_tempDir, vm.ThreeDModelsPath);
        Assert.Equal(_tempDir, vm.WorkflowsPath);
        Assert.Equal("C:\\Tools\\ollama.exe", vm.OllamaExecutablePath);

        Assert.True(vm.ComfyUiExecutableStatus.Contains("Auto-Discovered") || vm.ComfyUiExecutableStatus.Contains("Verified") || vm.ComfyUiExecutableStatus.Contains("Found"));
        Assert.True(vm.ForgeModelsStatus.Contains("Auto-Discovered") || vm.ForgeModelsStatus.Contains("Verified") || vm.ForgeModelsStatus.Contains("Found"));
    }

    [Fact]
    public async Task AutoDetectToolsAsync_DoesNotOverwriteExistingConfiguredPaths()
    {
        var customPath = "C:\\Custom\\ComfyUI\\custom.bat";
        var vm = new SettingsViewModel
        {
            ComfyUiExecutablePath = customPath
        };

        var detectPayload = new
        {
            comfyUi = new
            {
                isInstalled = true,
                executablePath = _tempFile,
                rootDirectory = _tempDir,
                modelsDirectory = _tempDir,
                statusMessage = "Found"
            },
            forge = new { isInstalled = false },
            ollama = new { isInstalled = false },
            suggestedThreeDPath = "",
            suggestedWorkflowsPath = ""
        };

        var json = JsonSerializer.Serialize(detectPayload);
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(mockHandler.Object);
        await vm.AutoDetectToolsAsync("http://127.0.0.1:5246", http);

        // Should NOT have overwritten custom path
        Assert.Equal(customPath, vm.ComfyUiExecutablePath);
    }

    [Fact]
    public async Task AutoDetectToolsAsync_HandlesNetworkFailureGracefully()
    {
        var vm = new SettingsViewModel();
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Server offline"));

        var http = new HttpClient(mockHandler.Object);
        await vm.AutoDetectToolsAsync("http://127.0.0.1:5246", http);

        // Should not throw, should remain empty
        Assert.Equal("", vm.ComfyUiExecutablePath);
    }

    [Fact]
    public async Task BrowseFileCommands_WithStorageProvider_SetsSelectedPath()
    {
        var vm = new SettingsViewModel();

        var mockFile = new Mock<IStorageFile>();
        var filePath = "C:\\AI\\ComfyUI\\run_nvidia_gpu.bat";
        mockFile.Setup(f => f.Path).Returns(new Uri($"file:///{filePath.Replace('\\', '/')}"));
        mockFile.Setup(f => f.Name).Returns("run_nvidia_gpu.bat");

        var mockStorage = new Mock<IStorageProvider>();
        mockStorage.Setup(s => s.OpenFilePickerAsync(It.IsAny<FilePickerOpenOptions>()))
            .ReturnsAsync(new List<IStorageFile> { mockFile.Object });

        // Set StorageProvider property
        vm.StorageProvider = mockStorage.Object;

        // Comfy Executable
        await vm.BrowseComfyExecutableCommand.ExecuteAsync(null);
        Assert.Contains("run_nvidia_gpu.bat", vm.ComfyUiExecutablePath);

        // Forge Executable
        await vm.BrowseForgeExecutableCommand.ExecuteAsync(null);
        Assert.Contains("run_nvidia_gpu.bat", vm.ForgeExecutablePath);

        // Ollama Executable
        await vm.BrowseOllamaExecutableCommand.ExecuteAsync(null);
        Assert.Contains("run_nvidia_gpu.bat", vm.OllamaExecutablePath);

        // Audio Executable
        await vm.BrowseAudioEngineExecutableCommand.ExecuteAsync(mockStorage.Object);
        Assert.Contains("run_nvidia_gpu.bat", vm.AudioEngineExecutablePath);
    }

    [Fact]
    public async Task TestVoiceSynthesizerAsync_SendsPostToProxy()
    {
        var vm = new SettingsViewModel
        {
            PreferredAudioVoice = "af_bella"
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().Contains("/v1/audio/speech")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("audio_binary_bytes", Encoding.UTF8, "audio/mpeg")
            });

        var http = new HttpClient(mockHandler.Object);
        await vm.TestVoiceSynthesizerAsync("http://127.0.0.1:5246", http);
    }

    [Fact]
    public async Task BrowseFolderCommands_WithStorageProvider_SetsSelectedPath()
    {
        var vm = new SettingsViewModel();

        var mockFolder = new Mock<IStorageFolder>();
        var folderPath = "C:\\AI\\Forge\\models";
        mockFolder.Setup(f => f.Path).Returns(new Uri($"file:///{folderPath.Replace('\\', '/')}"));
        mockFolder.Setup(f => f.Name).Returns("models");

        var mockStorage = new Mock<IStorageProvider>();
        mockStorage.Setup(s => s.OpenFolderPickerAsync(It.IsAny<FolderPickerOpenOptions>()))
            .ReturnsAsync(new List<IStorageFolder> { mockFolder.Object });

        // Forge Models
        await vm.BrowseForgeModelsCommand.ExecuteAsync(mockStorage.Object);
        Assert.Contains("models", vm.ForgeModelsPath);

        // Comfy Models
        await vm.BrowseComfyModelsCommand.ExecuteAsync(mockStorage.Object);
        Assert.Contains("models", vm.ComfyModelsPath);

        // 3D Models
        await vm.BrowseThreeDModelsCommand.ExecuteAsync(mockStorage.Object);
        Assert.Contains("models", vm.ThreeDModelsPath);

        // Workflows
        await vm.BrowseWorkflowsCommand.ExecuteAsync(mockStorage.Object);
        Assert.Contains("models", vm.WorkflowsPath);
    }

    [Fact]
    public async Task BrowseCommands_WhenPickerCancelled_KeepsOriginalPath()
    {
        var vm = new SettingsViewModel
        {
            ForgeModelsPath = "C:\\Original\\Path"
        };

        var mockStorage = new Mock<IStorageProvider>();
        mockStorage.Setup(s => s.OpenFolderPickerAsync(It.IsAny<FolderPickerOpenOptions>()))
            .ReturnsAsync(new List<IStorageFolder>()); // Empty / Cancelled

        await vm.BrowseForgeModelsCommand.ExecuteAsync(mockStorage.Object);
        Assert.Equal("C:\\Original\\Path", vm.ForgeModelsPath);
    }

    [Fact]
    public async Task LoadAndSaveSettings_IncludesAllToolPaths()
    {
        var vm = new SettingsViewModel();

        var settings = new
        {
            forgeModelsPath = "C:\\AI\\Forge\\models",
            comfyModelsPath = "C:\\AI\\ComfyUI\\models",
            threeDModelsPath = "C:\\AI\\3D",
            workflowsPath = "C:\\AI\\Workflows",
            preferredImageEngine = "forge",
            comfyUiExecutablePath = "C:\\AI\\ComfyUI\\run.bat",
            forgeExecutablePath = "C:\\AI\\Forge\\run.bat",
            ollamaExecutablePath = "C:\\AI\\Ollama\\ollama.exe",
            comfyUiUrl = "http://127.0.0.1:8188",
            lanAccessUrl = "http://192.168.1.50:5246",
            selectedThemeStyle = "fluent"
        };

        var json = JsonSerializer.Serialize(settings);
        string savedJson = "";

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.ToString().Contains("/api/settings")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().Contains("/api/settings")),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                if (req.Content != null)
                {
                    savedJson = await req.Content.ReadAsStringAsync(ct);
                }
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(mockHandler.Object);

        // Test Load
        await vm.LoadSettingsAsync("http://127.0.0.1:5246", http);
        Assert.Equal("C:\\AI\\Forge\\models", vm.ForgeModelsPath);
        Assert.Equal("C:\\AI\\ComfyUI\\models", vm.ComfyModelsPath);
        Assert.Equal("C:\\AI\\3D", vm.ThreeDModelsPath);
        Assert.Equal("C:\\AI\\Workflows", vm.WorkflowsPath);
        Assert.Equal("forge", vm.PreferredImageEngine);
        Assert.Equal("C:\\AI\\ComfyUI\\run.bat", vm.ComfyUiExecutablePath);
        Assert.Equal("C:\\AI\\Forge\\run.bat", vm.ForgeExecutablePath);
        Assert.Equal("C:\\AI\\Ollama\\ollama.exe", vm.OllamaExecutablePath);
        Assert.Equal("fluent", vm.SelectedThemeStyle);

        // Test Save
        await vm.SaveSettingsAsync("http://127.0.0.1:5246", http);
        Assert.Contains("C:\\\\AI\\\\Forge\\\\models", savedJson);
        Assert.Contains("C:\\\\AI\\\\Ollama\\\\ollama.exe", savedJson);
    }

    [Fact]
    public void SwitchThemeStyle_UpdatesSelectedThemeStyle()
    {
        var vm = new SettingsViewModel();
        vm.SwitchThemeStyleCommand.Execute("fluent");
        Assert.Equal("fluent", vm.SelectedThemeStyle);

        vm.SwitchThemeStyleCommand.Execute("semi");
        Assert.Equal("semi", vm.SelectedThemeStyle);
    }

    [Fact]
    public void Presets_InitializedWithDefaults_AllAndFilteredCollectionsPopulated()
    {
        var vm = new SettingsViewModel();

        Assert.NotEmpty(vm.AllPresets);
        Assert.NotEmpty(vm.FilteredPresets);
        Assert.Equal(vm.AllPresets.Count, vm.FilteredPresets.Count);
        Assert.Contains(vm.AllPresets, p => p.Modality == StudioModality.Video);
        Assert.Contains(vm.AllPresets, p => p.Modality == StudioModality.Image);
        Assert.Contains(vm.AllPresets, p => p.Modality == StudioModality.Audio);
    }

    [Fact]
    public void FilterPresetsCommand_FiltersPresetsByModality()
    {
        var vm = new SettingsViewModel();

        vm.FilterPresetsCommand.Execute("Video");
        Assert.Equal("Video", vm.SelectedPresetModalityFilter);
        Assert.All(vm.FilteredPresets, p => Assert.Equal(StudioModality.Video, p.Modality));

        vm.FilterPresetsCommand.Execute("Image");
        Assert.Equal("Image", vm.SelectedPresetModalityFilter);
        Assert.All(vm.FilteredPresets, p => Assert.Equal(StudioModality.Image, p.Modality));

        vm.FilterPresetsCommand.Execute("Audio");
        Assert.Equal("Audio", vm.SelectedPresetModalityFilter);
        Assert.All(vm.FilteredPresets, p => Assert.Equal(StudioModality.Audio, p.Modality));

        vm.FilterPresetsCommand.Execute("All");
        Assert.Equal("All", vm.SelectedPresetModalityFilter);
        Assert.Equal(vm.AllPresets.Count, vm.FilteredPresets.Count);
    }

    [Fact]
    public void CreatePresetCommand_AddsCustomPreset()
    {
        var vm = new SettingsViewModel();
        var initialCount = vm.AllPresets.Count;

        vm.FilterPresetsCommand.Execute("Video");
        vm.CreatePresetCommand.Execute(null);

        Assert.Equal(initialCount + 1, vm.AllPresets.Count);
        var created = vm.AllPresets.Last();
        Assert.False(created.IsBuiltIn);
        Assert.True(created.IsCustom);
        Assert.Equal(StudioModality.Video, created.Modality);
        Assert.Contains(created, vm.FilteredPresets);
    }

    [Fact]
    public void EditPresetCommand_UpdatesCustomPreset_GuardsBuiltIn()
    {
        var vm = new SettingsViewModel();
        var builtIn = vm.AllPresets.First(p => p.IsBuiltIn);

        // Attempting to edit a built-in should be guarded
        var modifiedBuiltIn = builtIn with { Name = "Hacked Builtin" };
        vm.EditPresetCommand.Execute(modifiedBuiltIn);
        Assert.DoesNotContain(vm.AllPresets, p => p.Name == "Hacked Builtin");

        // Custom preset editing
        var custom = new StudioPreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = "My Custom Preset",
            Modality = StudioModality.Image,
            Width = 512,
            Height = 512,
            IsBuiltIn = false
        };
        vm.CreatePresetCommand.Execute(custom);
        Assert.Contains(vm.AllPresets, p => p.Name == "My Custom Preset");

        var updatedCustom = custom with { Name = "My Renamed Preset", Width = 768 };
        vm.EditPresetCommand.Execute(updatedCustom);
        Assert.Contains(vm.AllPresets, p => p.Name == "My Renamed Preset" && p.Width == 768);
    }

    [Fact]
    public void DeletePresetCommand_DeletesCustomPreset_GuardsBuiltIn()
    {
        var vm = new SettingsViewModel();
        var builtIn = vm.AllPresets.First(p => p.IsBuiltIn);

        // Attempting to delete built-in should fail
        vm.DeletePresetCommand.Execute(builtIn);
        Assert.Contains(vm.AllPresets, p => p.Id == builtIn.Id);

        // Custom preset deletion
        var custom = new StudioPreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = "To Delete",
            Modality = StudioModality.Audio,
            IsBuiltIn = false
        };
        vm.CreatePresetCommand.Execute(custom);
        Assert.Contains(vm.AllPresets, p => p.Id == custom.Id);

        vm.DeletePresetCommand.Execute(custom);
        Assert.DoesNotContain(vm.AllPresets, p => p.Id == custom.Id);
    }

    [Fact]
    public void DuplicatePresetCommand_DuplicatesExistingPreset()
    {
        var vm = new SettingsViewModel();
        var builtIn = vm.AllPresets.First(p => p.IsBuiltIn);
        var initialCount = vm.AllPresets.Count;

        vm.DuplicatePresetCommand.Execute(builtIn);
        Assert.Equal(initialCount + 1, vm.AllPresets.Count);

        var copy = vm.AllPresets.FirstOrDefault(p => p.Name.Contains(builtIn.Name) && p.Name.Contains("(Copy)"));
        Assert.NotNull(copy);
        Assert.False(copy.IsBuiltIn);
        Assert.NotEqual(builtIn.Id, copy.Id);
    }

    [Fact]
    public void ExportAndImportPresetsCommands_WorkCorrectly()
    {
        var vm = new SettingsViewModel();
        var custom = new StudioPreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = "ExportImportTest",
            Modality = StudioModality.Video,
            Width = 1920,
            Height = 1080,
            IsBuiltIn = false
        };
        vm.CreatePresetCommand.Execute(custom);

        vm.ExportPresetsCommand.Execute(null);
        Assert.False(string.IsNullOrWhiteSpace(vm.PresetsJson));
        Assert.Contains("ExportImportTest", vm.PresetsJson);

        var targetVm = new SettingsViewModel();
        targetVm.ImportPresetsCommand.Execute(vm.PresetsJson);

        Assert.Contains(targetVm.AllPresets, p => p.Name == "ExportImportTest" && p.Width == 1920);
    }

    [Fact]
    public void ResetPresetsToDefaultCommand_ClearsCustomPresets()
    {
        var vm = new SettingsViewModel();
        var custom = new StudioPreset
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Temporary Custom",
            Modality = StudioModality.Image,
            IsBuiltIn = false
        };
        vm.CreatePresetCommand.Execute(custom);
        Assert.Contains(vm.AllPresets, p => p.Name == "Temporary Custom");

        vm.ResetPresetsToDefaultCommand.Execute(null);
        Assert.DoesNotContain(vm.AllPresets, p => p.Name == "Temporary Custom");
        Assert.All(vm.AllPresets, p => Assert.True(p.IsBuiltIn));
    }

    [Fact]
    public async Task LoadAndSaveSettings_SynchronizesCustomPresets()
    {
        var vm = new SettingsViewModel();
        var customPreset = new StudioPreset
        {
            Id = "custom-123",
            Name = "Synchronized Video Preset",
            Modality = StudioModality.Video,
            Width = 1280,
            Height = 720,
            IsBuiltIn = false
        };
        vm.CreatePresetCommand.Execute(customPreset);

        string savedJson = "";
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post && r.RequestUri!.ToString().Contains("/api/settings")),
                ItExpr.IsAny<CancellationToken>()
            )
            .Callback<HttpRequestMessage, CancellationToken>(async (req, ct) =>
            {
                if (req.Content != null)
                {
                    savedJson = await req.Content.ReadAsStringAsync(ct);
                }
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var http = new HttpClient(mockHandler.Object);
        await vm.SaveSettingsAsync("http://127.0.0.1:5246", http);

        Assert.Contains("Synchronized Video Preset", savedJson);

        // Test loading
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Get && r.RequestUri!.ToString().Contains("/api/settings")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(savedJson, Encoding.UTF8, "application/json")
            });

        var freshVm = new SettingsViewModel();
        await freshVm.LoadSettingsAsync("http://127.0.0.1:5246", http);

        Assert.Contains(freshVm.AllPresets, p => p.Name == "Synchronized Video Preset" && p.Id == "custom-123");
    }
}
