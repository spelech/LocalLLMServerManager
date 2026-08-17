using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using LocalLLMServerManager.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class DiscoveryEndpointsTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;
    private readonly HttpClient _client;

    public DiscoveryEndpointsTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task DetectToolsEndpoint_ReturnsDiscoveredToolsResult()
    {
        var response = await _client.GetAsync("/api/system/tools/detect");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<DiscoveredToolsResult>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(result);
        Assert.NotNull(result.Ollama);
        Assert.NotNull(result.ComfyUi);
        Assert.NotNull(result.Forge);
        Assert.False(string.IsNullOrWhiteSpace(result.SuggestedThreeDPath));
        Assert.False(string.IsNullOrWhiteSpace(result.SuggestedWorkflowsPath));
    }

    [Fact]
    public async Task ApplyDetectedToolsEndpoint_UpdatesEmptySettingsWithoutOverridingExisting()
    {
        var settingsService = new SettingsService();
        var originalSettings = new AppSettings(
            ComfyUiExecutablePath: @"C:\CustomPath\ComfyUI\custom_run.bat",
            ForgeExecutablePath: "",
            ForgeModelsPath: "",
            ComfyModelsPath: ""
        );
        settingsService.SaveSettings(originalSettings);

        try
        {
            var response = await _client.PostAsync("/api/system/tools/apply-detected", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var updated = await response.Content.ReadFromJsonAsync<AppSettings>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(updated);
            // Custom explicitly set path must be preserved
            Assert.Equal(@"C:\CustomPath\ComfyUI\custom_run.bat", updated.ComfyUiExecutablePath);

            // Persisted settings must match
            var loaded = settingsService.LoadSettings();
            Assert.Equal(@"C:\CustomPath\ComfyUI\custom_run.bat", loaded.ComfyUiExecutablePath);
        }
        finally
        {
            // Restore clean defaults
            settingsService.SaveSettings(new AppSettings());
        }
    }

    [Fact]
    public async Task ValidatePathsEndpoint_ValidatesBatchOfPaths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DiscoveryTestDir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "executable.bat");
        await File.WriteAllTextAsync(tempFile, "@echo off\r\necho Hello");

        var nonExistentFile = Path.Combine(tempDir, "missing_runner.bat");
        var nonExistentDir = Path.Combine(tempDir, "missing_sub_dir");

        try
        {
            var request = new ValidatePathsRequest(
                Items: new List<PathValidationItem>
                {
                    new(tempFile, PathTargetType.Executable, "validFile"),
                    new(tempDir, PathTargetType.Directory, "validDir"),
                    new(nonExistentFile, PathTargetType.Executable, "missingFile"),
                    new(nonExistentDir, PathTargetType.Directory, "missingDir")
                }
            );

            var response = await _client.PostAsJsonAsync("/api/system/tools/validate", request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ValidatePathsResponse>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(result);
            Assert.False(result.AllValid);
            Assert.True(result.Results.ContainsKey("validFile"));
            Assert.True(result.Results["validFile"].IsValid);
            Assert.True(result.Results["validFile"].Exists);

            Assert.True(result.Results.ContainsKey("validDir"));
            Assert.True(result.Results["validDir"].IsValid);

            Assert.True(result.Results.ContainsKey("missingFile"));
            Assert.False(result.Results["missingFile"].IsValid);

            Assert.True(result.Results.ContainsKey("missingDir"));
            Assert.False(result.Results["missingDir"].IsValid);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ValidatePathsEndpoint_AllValid_ReturnsAllValidTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DiscoveryAllValid_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var request = new ValidatePathsRequest(
                Items: new List<PathValidationItem>
                {
                    new(tempDir, PathTargetType.Directory, "existingDir")
                }
            );

            var response = await _client.PostAsJsonAsync("/api/system/tools/validate", request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ValidatePathsResponse>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(result);
            Assert.True(result.AllValid);
            Assert.True(result.Results["existingDir"].IsValid);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ValidatePathsEndpoint_WithNamedProperties_ValidatesSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DiscoveryNamedProps_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempExec = Path.Combine(tempDir, "run_forge.bat");
        await File.WriteAllTextAsync(tempExec, "@echo off");

        try
        {
            var request = new ValidatePathsRequest(
                ForgeExecutablePath: tempExec,
                ForgeModelsPath: tempDir
            );

            var response = await _client.PostAsJsonAsync("/api/system/tools/validate", request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ValidatePathsResponse>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(result);
            Assert.True(result.AllValid);
            Assert.True(result.Results.ContainsKey("ForgeExecutablePath"));
            Assert.True(result.Results["ForgeExecutablePath"].IsValid);
            Assert.True(result.Results.ContainsKey("ForgeModelsPath"));
            Assert.True(result.Results["ForgeModelsPath"].IsValid);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ValidatePathsEndpoint_WithPathsDictionary_ValidatesSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "DiscoveryDictPaths_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var request = new ValidatePathsRequest(
                Paths: new Dictionary<string, PathTargetType>
                {
                    { tempDir, PathTargetType.Directory },
                    { "C:\\non_existent_folder_xyz_123", PathTargetType.Directory }
                }
            );

            var response = await _client.PostAsJsonAsync("/api/system/tools/validate", request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ValidatePathsResponse>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(result);
            Assert.False(result.AllValid);
            Assert.True(result.Results[tempDir].IsValid);
            Assert.False(result.Results["C:\\non_existent_folder_xyz_123"].IsValid);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ValidatePathsEndpoint_WithAllPropertiesNull_ReturnsEmptyResultsAndAllValidTrue()
    {
        var request = new ValidatePathsRequest();
        var response = await _client.PostAsJsonAsync("/api/system/tools/validate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ValidatePathsResponse>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(result);
        Assert.True(result.AllValid);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task ApplyDetectedTools_WhenAllPropertiesSet_DoesNotOverwriteAny()
    {
        var settingsService = new SettingsService();
        var fullyConfiguredSettings = new AppSettings(
            ForgeModelsPath: @"D:\MyCustom\Forge\Models",
            ComfyUiUrl: "http://127.0.0.1:9999",
            ThreeDModelsPath: @"D:\MyCustom\3D",
            WorkflowsPath: @"D:\MyCustom\Workflows",
            PreferredImageEngine: "forge",
            ComfyUiExecutablePath: @"D:\MyCustom\ComfyUI\run.bat",
            ForgeExecutablePath: @"D:\MyCustom\Forge\run.bat",
            OllamaExecutablePath: @"D:\MyCustom\Ollama\ollama.exe",
            ServiceName: "CustomServiceName",
            PublishOutputPath: @"D:\CustomPublish",
            ComfyModelsPath: @"D:\MyCustom\ComfyUI\models",
            LanAccessUrl: "http://192.168.1.100:5246",
            SelectedThemeStyle: "fluent"
        );
        settingsService.SaveSettings(fullyConfiguredSettings);

        try
        {
            var response = await _client.PostAsync("/api/system/tools/apply-detected", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var updated = await response.Content.ReadFromJsonAsync<AppSettings>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(updated);
            Assert.Equal(fullyConfiguredSettings.ForgeModelsPath, updated.ForgeModelsPath);
            Assert.Equal(fullyConfiguredSettings.ComfyUiUrl, updated.ComfyUiUrl);
            Assert.Equal(fullyConfiguredSettings.ThreeDModelsPath, updated.ThreeDModelsPath);
            Assert.Equal(fullyConfiguredSettings.WorkflowsPath, updated.WorkflowsPath);
            Assert.Equal(fullyConfiguredSettings.PreferredImageEngine, updated.PreferredImageEngine);
            Assert.Equal(fullyConfiguredSettings.ComfyUiExecutablePath, updated.ComfyUiExecutablePath);
            Assert.Equal(fullyConfiguredSettings.ForgeExecutablePath, updated.ForgeExecutablePath);
            Assert.Equal(fullyConfiguredSettings.OllamaExecutablePath, updated.OllamaExecutablePath);
            Assert.Equal(fullyConfiguredSettings.ServiceName, updated.ServiceName);
            Assert.Equal(fullyConfiguredSettings.PublishOutputPath, updated.PublishOutputPath);
            Assert.Equal(fullyConfiguredSettings.ComfyModelsPath, updated.ComfyModelsPath);
            Assert.Equal(fullyConfiguredSettings.LanAccessUrl, updated.LanAccessUrl);
            Assert.Equal(fullyConfiguredSettings.SelectedThemeStyle, updated.SelectedThemeStyle);
        }
        finally
        {
            settingsService.SaveSettings(new AppSettings());
        }
    }
}

