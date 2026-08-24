using System.IO;
using System.Text.Json;
using LocalLLMServerManager;
using LocalLLMServerManager.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultValues_HaveEmptyDynamicPathsAndSensibleDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal("", settings.ForgeModelsPath);
        Assert.Equal("http://127.0.0.1:8188", settings.ComfyUiUrl);
        Assert.Equal("", settings.ThreeDModelsPath);
        Assert.Equal("", settings.WorkflowsPath);
        Assert.Equal("comfy", settings.PreferredImageEngine);
        Assert.Equal("", settings.ComfyUiExecutablePath);
        Assert.Equal("", settings.ForgeExecutablePath);
        Assert.Equal("ollama", settings.OllamaExecutablePath);
        Assert.Equal("LocalLLMServerManager", settings.ServiceName);
        Assert.Equal(@"C:\LocalLLMServerManager", settings.PublishOutputPath);
        Assert.Equal("", settings.ComfyModelsPath);
        Assert.Equal("http://127.0.0.1:5246", settings.LanAccessUrl);
        Assert.Equal("semi", settings.SelectedThemeStyle);
        Assert.Equal("", settings.AudioEngineExecutablePath);
        Assert.Equal("http://127.0.0.1:8880", settings.AudioEngineUrl);
        Assert.Equal("af_heart", settings.PreferredAudioVoice);
        Assert.Equal("", settings.VideoModelsPath);
        Assert.Equal("", settings.VideoOutputPath);
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
    public void ResolvePath_WithNullOrEmpty_AndEmptyFallback_ReturnsEmpty()
    {
        var resolvedNull = Program.ResolvePath(null, "");
        Assert.Equal("", resolvedNull);

        var resolvedEmpty = Program.ResolvePath("  ", "");
        Assert.Equal("", resolvedEmpty);
    }

    [Fact]
    public void AppSettings_SerializationAndDeserialization_PreservesData()
    {
        var original = new AppSettings(
            ForgeModelsPath: @"C:\AI\Models",
            ComfyUiUrl: "http://127.0.0.1:8189",
            ThreeDModelsPath: @"C:\AI\3D",
            WorkflowsPath: @"C:\AI\Workflows",
            PreferredImageEngine: "comfy",
            ComfyUiExecutablePath: @"C:\AI\ComfyUI\run.bat",
            ForgeExecutablePath: @"C:\AI\Forge\run.bat",
            OllamaExecutablePath: "ollama",
            ServiceName: "CustomService",
            PublishOutputPath: @"D:\Publish",
            ComfyModelsPath: @"C:\AI\ComfyUI\models",
            LanAccessUrl: "http://192.168.1.50:5246",
            SelectedThemeStyle: "dark",
            AudioEngineExecutablePath: @"C:\AI\Kokoro-FastAPI\main.py",
            AudioEngineUrl: "http://127.0.0.1:8880",
            PreferredAudioVoice: "af_bella",
            VideoModelsPath: @"C:\AI\VideoModels",
            VideoOutputPath: @"C:\AI\VideoOutput"
        );

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void SettingsService_SaveAndLoad_RoundTripsSuccessfully()
    {
        var service = new SettingsService();
        var original = service.LoadSettings();

        try
        {
            var customSettings = new AppSettings(
                ForgeModelsPath: @"C:\CustomForge",
                ComfyModelsPath: @"C:\CustomComfy"
            );
            service.SaveSettings(customSettings);

            var loaded = service.LoadSettings();
            Assert.Equal(@"C:\CustomForge", loaded.ForgeModelsPath);
            Assert.Equal(@"C:\CustomComfy", loaded.ComfyModelsPath);
        }
        finally
        {
            service.SaveSettings(original);
        }
    }
}
