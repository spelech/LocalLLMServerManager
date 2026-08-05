using System;
using System.Diagnostics;
using LocalLLMServerManager;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ServerInfrastructureAndAppTests
{
    [Fact]
    public void JobObject_Instantiates_AndHandlesChildProcess()
    {
        using var job = new JobObject();
        Assert.NotNull(job);

        Assert.Throws<ArgumentNullException>(() => job.AddProcess((Process)null!));

        // Spawn a dummy background child process to test AssignProcessToJobObject
        using var childProc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "sleep",
                Arguments = OperatingSystem.IsWindows() ? "/c choice /t 2 /d y" : "2",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        childProc.Start();
        job.AddProcess(childProc);

        if (!childProc.HasExited)
        {
            childProc.Kill();
        }
    }
}
