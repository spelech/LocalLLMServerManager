using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
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
}
