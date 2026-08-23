using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ToolDiscoveryServiceTests : IDisposable
{
    private readonly string _tempDirectory;

    public ToolDiscoveryServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ToolDiscoveryTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup failures in test teardown
        }
    }

    [Fact]
    public void ValidatePath_WithNullOrWhiteSpace_ReturnsInvalid()
    {
        var service = new ToolDiscoveryService();

        var nullResult = service.ValidatePath(null, PathTargetType.Executable);
        Assert.False(nullResult.Exists);
        Assert.False(nullResult.IsValid);
        Assert.NotNull(nullResult.ErrorMessage);

        var emptyResult = service.ValidatePath("   ", PathTargetType.Directory);
        Assert.False(emptyResult.Exists);
        Assert.False(emptyResult.IsValid);
        Assert.NotNull(emptyResult.ErrorMessage);
    }

    [Fact]
    public void ValidatePath_ForExistingExecutableAndDirectory_ReturnsValid()
    {
        var service = new ToolDiscoveryService();
        var tempFile = Path.Combine(_tempDirectory, "test_app.exe");
        File.WriteAllText(tempFile, "dummy");

        var fileResult = service.ValidatePath(tempFile, PathTargetType.Executable);
        Assert.True(fileResult.Exists);
        Assert.True(fileResult.IsValid);
        Assert.Null(fileResult.ErrorMessage);

        var dirResult = service.ValidatePath(_tempDirectory, PathTargetType.Directory);
        Assert.True(dirResult.Exists);
        Assert.True(dirResult.IsValid);
        Assert.Null(dirResult.ErrorMessage);
    }

    [Fact]
    public void ValidatePath_ForNonExistentFileOrDirectory_ReturnsNotFound()
    {
        var service = new ToolDiscoveryService();
        var nonExistentFile = Path.Combine(_tempDirectory, "does_not_exist.exe");
        var nonExistentDir = Path.Combine(_tempDirectory, "missing_subdir");

        var fileResult = service.ValidatePath(nonExistentFile, PathTargetType.Executable);
        Assert.False(fileResult.Exists);
        Assert.False(fileResult.IsValid);
        Assert.Contains("not found", fileResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var dirResult = service.ValidatePath(nonExistentDir, PathTargetType.Directory);
        Assert.False(dirResult.Exists);
        Assert.False(dirResult.IsValid);
        Assert.Contains("not found", dirResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePath_ExpandsEnvironmentVariables()
    {
        var service = new ToolDiscoveryService();
        var tempVarName = "TOOL_DISCOVERY_TEST_VAR_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(tempVarName, _tempDirectory);

        try
        {
            var rawDir = $"%{tempVarName}%";
            var dirResult = service.ValidatePath(rawDir, PathTargetType.Directory);
            Assert.True(dirResult.Exists);
            Assert.True(dirResult.IsValid);
        }
        finally
        {
            Environment.SetEnvironmentVariable(tempVarName, null);
        }
    }

    [Fact]
    public void ValidatePath_BareExecutableOnPath_ReturnsValid()
    {
        var service = new ToolDiscoveryService();
        var cmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "sh";
        var result = service.ValidatePath(cmd, PathTargetType.Executable);

        Assert.True(result.Exists);
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void DetectOllama_WhenInstalledInCustomRoot_DiscoversProperties()
    {
        var customOllamaDir = Path.Combine(_tempDirectory, "Programs", "Ollama");
        Directory.CreateDirectory(customOllamaDir);
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ollama.exe" : "ollama";
        var exePath = Path.Combine(customOllamaDir, exeName);
        File.WriteAllText(exePath, "fake ollama binary");

        var customModelsDir = Path.Combine(_tempDirectory, "ollama_models");
        Directory.CreateDirectory(customModelsDir);

        var service = new ToolDiscoveryService(searchRoots: new[] { _tempDirectory }, ollamaModelsOverride: customModelsDir);
        var result = service.DetectOllama();

        Assert.True(result.IsInstalled);
        Assert.NotNull(result.ExecutablePath);
        Assert.True(File.Exists(result.ExecutablePath));
        Assert.Equal(customModelsDir, result.ModelsDirectory);
        Assert.Contains("Ollama", result.StatusMessage);
    }

    [Fact]
    public void DetectOllama_WhenNotFound_ReturnsNotInstalled()
    {
        var emptyDir = Path.Combine(_tempDirectory, "empty_root");
        Directory.CreateDirectory(emptyDir);

        var service = new ToolDiscoveryService(searchRoots: new[] { emptyDir });
        var result = service.DetectOllama();

        if (!result.IsInstalled)
        {
            Assert.Null(result.ExecutablePath);
            Assert.Contains("not detected", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void DetectComfyUi_WhenPortableInstalled_DiscoversBatchAndDirectories()
    {
        var comfyRoot = Path.Combine(_tempDirectory, "ComfyUI_windows_portable");
        var comfyInner = Path.Combine(comfyRoot, "ComfyUI");
        var modelsDir = Path.Combine(comfyInner, "models");
        var workflowsDir = Path.Combine(comfyInner, "user", "default", "workflows");
        Directory.CreateDirectory(modelsDir);
        Directory.CreateDirectory(workflowsDir);

        var runnerBat = Path.Combine(comfyRoot, "run_nvidia_gpu.bat");
        File.WriteAllText(runnerBat, "@echo off\npython main.py");

        var service = new ToolDiscoveryService(searchRoots: new[] { _tempDirectory });
        var result = service.DetectComfyUi();

        Assert.True(result.IsInstalled);
        Assert.Equal(runnerBat, result.ExecutablePath);
        Assert.Equal(comfyRoot, result.RootDirectory);
        Assert.Equal(modelsDir, result.ModelsDirectory);
        Assert.Equal(workflowsDir, result.WorkflowsDirectory);
        Assert.Contains("ComfyUI", result.StatusMessage);
    }

    [Fact]
    public void DetectComfyUi_WhenNotFound_ReturnsNotInstalled()
    {
        var emptyDir = Path.Combine(_tempDirectory, "empty_root");
        Directory.CreateDirectory(emptyDir);

        var service = new ToolDiscoveryService(searchRoots: new[] { emptyDir });
        var result = service.DetectComfyUi();

        Assert.False(result.IsInstalled);
        Assert.Null(result.ExecutablePath);
        Assert.Contains("not detected", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetectForge_WhenInstalled_DiscoversBatchAndModelsDirectory()
    {
        var forgeRoot = Path.Combine(_tempDirectory, "webui_forge_cu121_torch231");
        var sdModelsDir = Path.Combine(forgeRoot, "webui", "models", "Stable-diffusion");
        Directory.CreateDirectory(sdModelsDir);

        var runnerBat = Path.Combine(forgeRoot, "run.bat");
        File.WriteAllText(runnerBat, "@echo off\ncall webui-user.bat");

        var service = new ToolDiscoveryService(searchRoots: new[] { _tempDirectory });
        var result = service.DetectForge();

        Assert.True(result.IsInstalled);
        Assert.Equal(runnerBat, result.ExecutablePath);
        Assert.Equal(forgeRoot, result.RootDirectory);
        Assert.Equal(sdModelsDir, result.ModelsDirectory);
        Assert.Contains("Forge", result.StatusMessage);
    }

    [Fact]
    public void DetectForge_WhenNotFound_ReturnsNotInstalled()
    {
        var emptyDir = Path.Combine(_tempDirectory, "empty_root");
        Directory.CreateDirectory(emptyDir);

        var service = new ToolDiscoveryService(searchRoots: new[] { emptyDir });
        var result = service.DetectForge();

        Assert.False(result.IsInstalled);
        Assert.Null(result.ExecutablePath);
        Assert.Contains("not detected", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetectAudioEngine_WhenInstalled_DiscoversScriptAndProperties()
    {
        var kokoroDir = Path.Combine(_tempDirectory, "Kokoro-FastAPI");
        Directory.CreateDirectory(kokoroDir);
        var mainPy = Path.Combine(kokoroDir, "main.py");
        File.WriteAllText(mainPy, "# kokoro fastapi entrypoint");

        var service = new ToolDiscoveryService(searchRoots: new[] { _tempDirectory });
        var result = service.DetectAudioEngine();

        Assert.True(result.IsInstalled);
        Assert.Equal(mainPy, result.ExecutablePath);
        Assert.Equal(kokoroDir, result.RootDirectory);
        Assert.Contains("Audio Engine", result.StatusMessage);
    }

    [Fact]
    public void DetectAudioEngine_WhenNotFound_ReturnsNotInstalledOrDocker()
    {
        var emptyDir = Path.Combine(_tempDirectory, "empty_audio_root");
        Directory.CreateDirectory(emptyDir);

        var service = new ToolDiscoveryService(searchRoots: new[] { emptyDir });
        var result = service.DetectAudioEngine();

        Assert.NotNull(result);
        if (!result.IsInstalled)
        {
            Assert.Null(result.ExecutablePath);
            Assert.Contains("not detected", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task DetectAllToolsAsync_ReturnsAggregatedResultsAndSuggestions()
    {
        var comfyRoot = Path.Combine(_tempDirectory, "ComfyUI");
        var modelsDir = Path.Combine(comfyRoot, "models");
        var workflowsDir = Path.Combine(comfyRoot, "workflows");
        Directory.CreateDirectory(modelsDir);
        Directory.CreateDirectory(workflowsDir);
        var comfyBat = Path.Combine(comfyRoot, "run_cpu.bat");
        File.WriteAllText(comfyBat, "comfy");

        var service = new ToolDiscoveryService(searchRoots: new[] { _tempDirectory });
        var allResults = await service.DetectAllToolsAsync();

        Assert.NotNull(allResults);
        Assert.NotNull(allResults.Ollama);
        Assert.NotNull(allResults.ComfyUi);
        Assert.NotNull(allResults.Forge);
        Assert.True(allResults.ComfyUi.IsInstalled);
        Assert.Equal(modelsDir, allResults.SuggestedThreeDPath);
        Assert.Equal(workflowsDir, allResults.SuggestedWorkflowsPath);
    }

    [Fact]
    public async Task DetectAllToolsAsync_DisambiguatesComfyAndForge_InSameSearchRootWithRootRunBat()
    {
        // Setup root with a top-level run.bat (which might fool naive scanners)
        var topLevelRunBat = Path.Combine(_tempDirectory, "run.bat");
        File.WriteAllText(topLevelRunBat, "@echo off\ncall webui-user.bat");

        // Setup ComfyUI subfolder
        var comfyFolder = Path.Combine(_tempDirectory, "ComfyUI");
        var comfyModels = Path.Combine(comfyFolder, "models");
        Directory.CreateDirectory(comfyModels);
        var comfyBat = Path.Combine(comfyFolder, "run_nvidia_gpu.bat");
        File.WriteAllText(comfyBat, "@echo off\npython main.py");

        // Setup SD_Forge subfolder
        var forgeFolder = Path.Combine(_tempDirectory, "SD_Forge");
        var forgeModels = Path.Combine(forgeFolder, "models");
        Directory.CreateDirectory(forgeModels);
        var forgeBat = Path.Combine(forgeFolder, "webui-user.bat");
        File.WriteAllText(forgeBat, "@echo off\npython launch.py");

        var service = new ToolDiscoveryService(searchRoots: new[] { _tempDirectory });
        var results = await service.DetectAllToolsAsync();

        Assert.True(results.ComfyUi.IsInstalled);
        Assert.Equal(comfyBat, results.ComfyUi.ExecutablePath);
        Assert.Equal(comfyFolder, results.ComfyUi.RootDirectory);
        Assert.Equal(comfyModels, results.ComfyUi.ModelsDirectory);

        Assert.True(results.Forge.IsInstalled);
        Assert.Equal(forgeBat, results.Forge.ExecutablePath);
        Assert.Equal(forgeFolder, results.Forge.RootDirectory);
        Assert.Equal(forgeModels, results.Forge.ModelsDirectory);

        // Ensure they did NOT resolve to the top level or each other
        Assert.NotEqual(results.ComfyUi.ExecutablePath, results.Forge.ExecutablePath);
        Assert.NotEqual(results.ComfyUi.RootDirectory, results.Forge.RootDirectory);
    }

    [Fact]
    public async Task DetectAllToolsAsync_OnDefaultSearchRoots_DiscoversUniqueToolRoots()
    {
        var service = new ToolDiscoveryService();
        var results = await service.DetectAllToolsAsync();

        if (results.ComfyUi.IsInstalled && results.Forge.IsInstalled)
        {
            Assert.NotEqual(results.ComfyUi.ExecutablePath, results.Forge.ExecutablePath);
            Assert.NotEqual(results.ComfyUi.RootDirectory, results.Forge.RootDirectory);
            Assert.Contains("ComfyUI", results.ComfyUi.ExecutablePath, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                results.Forge.ExecutablePath.Contains("SD_Forge", StringComparison.OrdinalIgnoreCase) ||
                results.Forge.ExecutablePath.Contains("Forge", StringComparison.OrdinalIgnoreCase) ||
                results.Forge.ExecutablePath.Contains("webui", StringComparison.OrdinalIgnoreCase)
            );
        }
    }
}
