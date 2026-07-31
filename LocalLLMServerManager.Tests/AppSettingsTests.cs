using System.Text.Json;
using LocalLLMServerManager;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AppSettingsTests
{
    [Fact]
    public void AppSettings_DefaultValues_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.Equal("", settings.ForgeModelsPath);
        Assert.Equal("http://127.0.0.1:8188", settings.ComfyUiUrl);
        Assert.Equal("", settings.ThreeDModelsPath);
        Assert.Equal("Forge", settings.PreferredImageEngine);
        Assert.Equal("", settings.ComfyUiExecutablePath);
        Assert.Equal("", settings.ForgeExecutablePath);
    }

    [Fact]
    public void AppSettings_SerializationAndDeserialization_PreservesData()
    {
        var original = new AppSettings(
            ForgeModelsPath: @"C:\AI\Models",
            ComfyUiUrl: "http://127.0.0.1:8189",
            ThreeDModelsPath: @"C:\AI\3D",
            PreferredImageEngine: "ComfyUI",
            ComfyUiExecutablePath: @"C:\AI\ComfyUI\run.bat",
            ForgeExecutablePath: @"C:\AI\Forge\run.bat"
        );

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(original, deserialized);
    }
}
