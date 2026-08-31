using System;
using System.IO;
using System.Reflection;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class WasmAssetFreshnessTests
{
    private static string GetProjectRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current) && !File.Exists(Path.Combine(current, "LocalLLMServerManager.sln")))
        {
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        return current;
    }

    [Fact]
    public void WasmFrameworkAssembly_VersionMatchesSourceAssemblyVersion()
    {
        var root = GetProjectRoot();
        var frameworkWasmPath = Path.Combine(root, "wwwroot", "_framework", "LocalLLMServerManager.Shared.wasm");
        var frameworkDllPath = Path.Combine(root, "wwwroot", "_framework", "LocalLLMServerManager.Shared.dll");

        var exists = File.Exists(frameworkWasmPath) || File.Exists(frameworkDllPath);
        Assert.True(exists, $"WASM framework binary not found at: {frameworkWasmPath}");

        if (File.Exists(frameworkDllPath))
        {
            var frameworkVersion = AssemblyName.GetAssemblyName(frameworkDllPath).Version;
            var sourceVersion = typeof(MainViewModel).Assembly.GetName().Version;
            Assert.NotNull(frameworkVersion);
            Assert.NotNull(sourceVersion);
            Assert.Equal(sourceVersion.ToString(3), frameworkVersion.ToString(3));
        }
        else
        {
            var info = new FileInfo(frameworkWasmPath);
            Assert.True(info.Length > 50000, $"WASM binary size {info.Length} is unexpectedly small.");
        }
    }

    [Fact]
    public void MainJs_VersionStringMatchesCurrentVersion()
    {
        var root = GetProjectRoot();
        var mainJsPath = Path.Combine(root, "wwwroot", "main.js");
        var webMainJsPath = Path.Combine(root, "LocalLLMServerManager.Web", "main.js");

        var expectedVersion = typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "3.10.0";

        foreach (var path in new[] { mainJsPath, webMainJsPath })
        {
            Assert.True(File.Exists(path), $"main.js not found at: {path}");
            var content = File.ReadAllText(path);
            Assert.Contains($"const APP_VERSION = \"{expectedVersion}\";", content);
        }
    }

    [Fact]
    public void InstallAndUpdateScripts_ContainBrowserWasmCompilation()
    {
        var root = GetProjectRoot();
        var installScript = Path.Combine(root, "scripts", "install.ps1");
        var updateScript = Path.Combine(root, "scripts", "update.ps1");
        var releaseScript = Path.Combine(root, "scripts", "build_release.ps1");

        foreach (var scriptPath in new[] { installScript, updateScript, releaseScript })
        {
            Assert.True(File.Exists(scriptPath), $"Script not found at: {scriptPath}");
            var scriptContent = File.ReadAllText(scriptPath);
            Assert.Contains("browser-wasm", scriptContent);
            Assert.Contains("LocalLLMServerManager.Web", scriptContent);
        }
    }
}
