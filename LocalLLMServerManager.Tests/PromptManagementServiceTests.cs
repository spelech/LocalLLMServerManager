using System;
using System.IO;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class PromptManagementServiceTests
{
    [Fact]
    public async Task GetSystemPrompt_ReturnsValidPrompt_FromDiskOrFallback()
    {
        var service = new PromptManagementService();
        var prompt = await service.GetSystemPromptAsync();

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.Contains("LocalLLMServerManager", prompt);
    }

    [Fact]
    public async Task GetCapabilitiesPrompt_ReturnsValidPrompt()
    {
        var service = new PromptManagementService();
        var prompt = await service.GetCapabilitiesPromptAsync();

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.Contains("Capabilities", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetWorkflowsPrompt_ReturnsValidPrompt()
    {
        var service = new PromptManagementService();
        var prompt = await service.GetWorkflowsPromptAsync();

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.Contains("Workflow", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAppControlPrompt_ReturnsValidPrompt()
    {
        var service = new PromptManagementService();
        var prompt = await service.GetAppControlPromptAsync();

        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.Contains("Control", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildFullSystemPrompt_AssemblesAllSections()
    {
        var service = new PromptManagementService();
        var fullPrompt = await service.BuildFullSystemPromptAsync();

        Assert.False(string.IsNullOrWhiteSpace(fullPrompt));
        Assert.Contains("LocalLLMServerManager", fullPrompt);
        Assert.Contains("---", fullPrompt);
    }

    [Fact]
    public async Task GetAllPrompts_ReturnsDictionaryWithAllExpectedKeys()
    {
        var service = new PromptManagementService();
        var all = await service.GetAllPromptsAsync();

        Assert.NotNull(all);
        Assert.Equal(8, all.Count);
        Assert.True(all.ContainsKey(PromptManagementService.SystemPromptFileName));
        Assert.True(all.ContainsKey(PromptManagementService.CapabilitiesFileName));
        Assert.True(all.ContainsKey(PromptManagementService.WorkflowsFileName));
        Assert.True(all.ContainsKey(PromptManagementService.AppControlFileName));
        Assert.True(all.ContainsKey("system-prompt"));
        Assert.True(all.ContainsKey("capabilities"));
        Assert.True(all.ContainsKey("workflows"));
        Assert.True(all.ContainsKey("app-control"));
    }

    [Fact]
    public async Task CustomPromptDirectory_LoadsCustomFilesAndInvalidateCacheClearsMemory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "PromptTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, PromptManagementService.SystemPromptFileName), "Custom System Prompt V1");
            var service = new PromptManagementService();

            var loaded1 = await service.GetSystemPromptAsync(tempDir);
            Assert.Equal("Custom System Prompt V1", loaded1);

            // Update file on disk
            await File.WriteAllTextAsync(Path.Combine(tempDir, PromptManagementService.SystemPromptFileName), "Custom System Prompt V2");
            // Cache should still return V1
            var loadedCached = await service.GetSystemPromptAsync(tempDir);
            Assert.Equal("Custom System Prompt V1", loadedCached);

            // Invalidate cache
            service.InvalidateCache();
            var loadedUpdated = await service.GetSystemPromptAsync(tempDir);
            Assert.Equal("Custom System Prompt V2", loadedUpdated);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task NonExistentDirectory_FallsBackToBuiltInDefaultsGracefully()
    {
        var service = new PromptManagementService();
        var nonExistent = Path.Combine(Path.GetTempPath(), "DoesNotExist_" + Guid.NewGuid().ToString("N"));

        var prompt = await service.GetSystemPromptAsync(nonExistent);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
    }
}
