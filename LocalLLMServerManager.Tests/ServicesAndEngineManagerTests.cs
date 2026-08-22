using System;
using System.IO;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ServicesAndEngineManagerTests
{
    [Fact]
    public void GpuTelemetryProvider_ParsesVariousTelemetrySources()
    {
        var (gpuName, totalVram, usedVram) = Program.GetGpuInfo();
        Assert.NotNull(gpuName);

        var regInfo = Program.GetGpuInfoFromRegistry();
        Assert.NotNull(regInfo.GpuName);

        var linuxInfo = Program.GetLinuxMemoryInfo();
        // May be null on Windows, should not throw
        if (OperatingSystem.IsLinux())
        {
            Assert.NotNull(linuxInfo);
        }

        var sampleNvidiaSmi = "NVIDIA GeForce RTX 4070 Ti SUPER, 16376, 1024";
        var parsed = Program.ParseNvidiaSmiOutput(sampleNvidiaSmi);
        Assert.NotNull(parsed);
        Assert.Contains("4070 Ti SUPER", parsed.Value.GpuName);
        Assert.True(parsed.Value.TotalVramBytes > 0);
        Assert.True(parsed.Value.UsedVramBytes > 0);
    }
    [Fact]
    public void GitUpdateService_IsValidBranchName_ValidatesCorrectly()
    {
        var service = new GitUpdateService();

        Assert.True(service.IsValidBranchName("main"));
        Assert.True(service.IsValidBranchName("feature/cool-stuff"));
        Assert.True(service.IsValidBranchName("fix_bug_123"));

        Assert.False(service.IsValidBranchName(""));
        Assert.False(service.IsValidBranchName(null!));
        Assert.False(service.IsValidBranchName("-invalid"));
        Assert.False(service.IsValidBranchName("/invalid"));
        Assert.False(service.IsValidBranchName(".invalid"));
        Assert.False(service.IsValidBranchName("branch.lock"));
        Assert.False(service.IsValidBranchName("branch/"));
        Assert.False(service.IsValidBranchName("branch..name"));
        Assert.False(service.IsValidBranchName("branch@{1}"));
        Assert.False(service.IsValidBranchName("branch//name"));
        Assert.False(service.IsValidBranchName("branch with space"));
        Assert.False(service.IsValidBranchName("branch~1"));
        Assert.False(service.IsValidBranchName("branch^2"));
        Assert.False(service.IsValidBranchName("branch:name"));
    }

    [Fact]
    public async Task GitUpdateService_RunCommandAsync_NonExistentApp_ReturnsFailure()
    {
        var service = new GitUpdateService();
        var (success, output, error) = await service.RunCommandAsync("non_existent_app_executable_12345.exe", new[] { "--version" }, Directory.GetCurrentDirectory());

        Assert.False(success);
        Assert.NotEmpty(error);
    }

    [Fact]
    public async Task AiEngineManager_Methods_HandleNonExistentPathsAndProcessesGracefully()
    {
        var manager = new AiEngineManager();
        var logger = NullLogger.Instance;

        Assert.Null(manager.ComfyProcess);
        Assert.Null(manager.ForgeProcess);

        bool comfyStarted = await manager.StartComfyUiAsync("non_existent_comfy_path.exe", logger);
        Assert.False(comfyStarted);

        bool forgeStarted = await manager.StartForgeAsync("non_existent_forge_path.exe", logger);
        Assert.False(forgeStarted);

        bool comfyStopped = await manager.StopComfyUiAsync(logger);
        Assert.True(comfyStopped);

        bool forgeStopped = await manager.StopForgeAsync(logger);
        Assert.True(forgeStopped);

        bool isRunning = manager.IsProcessRunning("non_existent_process_name_999");
        Assert.False(isRunning);
    }
}
