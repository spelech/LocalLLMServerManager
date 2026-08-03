using System.Text.Json;
using LocalLLMServerManager;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultValues_UseAppDataAiPaths()
    {
        var settings = new AppSettings();

        Assert.Contains("%APPDATA%", settings.ForgeModelsPath);
        Assert.Equal("http://127.0.0.1:8188", settings.ComfyUiUrl);
        Assert.Contains("%APPDATA%", settings.ThreeDModelsPath);
        Assert.Contains("%APPDATA%", settings.WorkflowsPath);
        Assert.Equal("Forge", settings.PreferredImageEngine);
        Assert.Contains("%APPDATA%", settings.ComfyUiExecutablePath);
        Assert.Contains("%APPDATA%", settings.ForgeExecutablePath);
        Assert.Equal("ollama", settings.OllamaExecutablePath);
    }

    [Fact]
    public void ResolvePath_ExpandsEnvironmentVariables_Correctly()
    {
        var raw = "%APPDATA%\\AI\\test.bat";
        var resolved = Program.ResolvePath(raw, "%APPDATA%\\AI\\fallback.bat");
        Assert.DoesNotContain("%APPDATA%", resolved);
        Assert.EndsWith("test.bat", resolved);
    }

    [Fact]
    public void ResolvePath_WithNullOrEmpty_UsesFallback()
    {
        var resolved = Program.ResolvePath(null, "%APPDATA%\\AI\\fallback.bat");
        Assert.DoesNotContain("%APPDATA%", resolved);
        Assert.EndsWith("fallback.bat", resolved);

        var resolvedEmpty = Program.ResolvePath("  ", "%APPDATA%\\AI\\fallback.bat");
        Assert.DoesNotContain("%APPDATA%", resolvedEmpty);
        Assert.EndsWith("fallback.bat", resolvedEmpty);
    }

    [Fact]
    public void AppSettings_SerializationAndDeserialization_PreservesData()
    {
        var original = new AppSettings(
            ForgeModelsPath: @"C:\AI\Models",
            ComfyUiUrl: "http://127.0.0.1:8189",
            ThreeDModelsPath: @"C:\AI\3D",
            WorkflowsPath: @"C:\AI\Workflows",
            PreferredImageEngine: "ComfyUI",
            ComfyUiExecutablePath: @"C:\AI\ComfyUI\run.bat",
            ForgeExecutablePath: @"C:\AI\Forge\run.bat",
            OllamaExecutablePath: "ollama"
        );

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original, deserialized);
    }
}
