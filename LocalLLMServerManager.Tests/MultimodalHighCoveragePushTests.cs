using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Endpoints;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class MultimodalHighCoveragePushTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;
    private readonly HttpClient _client;

    public MultimodalHighCoveragePushTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    #region 1. AudioStudioViewModel Tests

    [Fact]
    public void AudioStudioViewModel_Initializes_DefaultsAreExpected()
    {
        var vm = new AudioStudioViewModel();
        Assert.NotNull(vm.Workflows);
        Assert.NotNull(vm.GeneratedAudioFiles);
        Assert.Equal("Ready", vm.StatusMessage);
        Assert.Equal("Cyberpunk atmospheric ambient drone, heavy synthesizer, cinematic low end, 48kHz stereo", vm.Prompt);
        Assert.Equal("low quality, harsh distortion", vm.NegativePrompt);
        Assert.Equal(30, vm.DurationSeconds);
        Assert.Equal(-1, vm.Seed);
        Assert.False(vm.IsGenerating);
        Assert.False(vm.IsPlaying);
        Assert.Equal("▶️ Play", vm.PlayButtonText);
        Assert.Equal("No Track Loaded", vm.PlayingTrackTitle);
    }

    [Fact]
    public void AudioStudioViewModel_OnIsPlayingChanged_UpdatesPlayButtonText()
    {
        var vm = new AudioStudioViewModel();
        Assert.Equal("▶️ Play", vm.PlayButtonText);

        vm.IsPlaying = true;
        Assert.Equal("⏸️ Pause", vm.PlayButtonText);

        vm.IsPlaying = false;
        Assert.Equal("▶️ Play", vm.PlayButtonText);
    }

    [Fact]
    public void AudioStudioViewModel_OnSelectedAudioFileChanged_UpdatesPlayingTrackTitle()
    {
        var vm = new AudioStudioViewModel();
        var item = new AudioFileItem("ambient_track_01.wav", "/output_audio/ambient_track_01.wav", 1024 * 512, DateTime.UtcNow);

        vm.SelectedAudioFile = item;
        Assert.Equal("ambient_track_01.wav", vm.PlayingTrackTitle);
    }

    [Fact]
    public async Task AudioStudioViewModel_LoadAudioWorkflowsAsync_Success_PopulatesWorkflows()
    {
        var workflowsJson = JsonSerializer.Serialize(new[]
        {
            new AudioWorkflowItem("stable_audio_open_sfx", "Stable Audio Open 3.0", "stable_audio_open_sfx.json", "", "audio", "SFX generation"),
            new AudioWorkflowItem("yue_full_song", "YuE Music", "yue_full_song.json", "", "audio", "Full song generation")
        });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/audio/workflows")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(workflowsJson, Encoding.UTF8, "application/json") });

        var client = new HttpClient(handlerMock.Object);
        var vm = new AudioStudioViewModel();

        await vm.LoadAudioWorkflowsAsync("http://127.0.0.1:5246", client);

        Assert.Equal(2, vm.Workflows.Count);
        Assert.NotNull(vm.SelectedWorkflow);
        Assert.Equal("stable_audio_open_sfx", vm.SelectedWorkflow.Id);
    }

    [Fact]
    public async Task AudioStudioViewModel_LoadAudioWorkflowsAsync_HttpException_UsesFallbackWorkflows()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new HttpClient(handlerMock.Object);
        var vm = new AudioStudioViewModel();

        await vm.LoadAudioWorkflowsAsync("http://127.0.0.1:5246", client);

        Assert.Equal(2, vm.Workflows.Count);
        Assert.NotNull(vm.SelectedWorkflow);
        Assert.Equal("stable_audio_open_sfx", vm.SelectedWorkflow.Id);
    }

    [Fact]
    public async Task AudioStudioViewModel_LoadAudioFilesAsync_Success_PopulatesFilesAndSetsSelected()
    {
        var filesJson = JsonSerializer.Serialize(new[]
        {
            new AudioFileItem("track1.mp3", "/output_audio/track1.mp3", 1024, DateTime.UtcNow),
            new AudioFileItem("track2.wav", "/output_audio/track2.wav", 2048, DateTime.UtcNow)
        });

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/audio/files")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(filesJson, Encoding.UTF8, "application/json") });

        var client = new HttpClient(handlerMock.Object);
        var vm = new AudioStudioViewModel();

        await vm.LoadAudioFilesAsync("http://127.0.0.1:5246", client);

        Assert.Equal(2, vm.GeneratedAudioFiles.Count);
        Assert.NotNull(vm.SelectedAudioFile);
        Assert.Equal("track1.mp3", vm.SelectedAudioFile.Filename);
        Assert.Equal("track1.mp3", vm.PlayingTrackTitle);
    }

    [Fact]
    public async Task AudioStudioViewModel_LoadAudioFilesAsync_HttpException_HandlesGracefully()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Server error"));

        var client = new HttpClient(handlerMock.Object);
        var vm = new AudioStudioViewModel();

        await vm.LoadAudioFilesAsync("http://127.0.0.1:5246", client);
        Assert.Empty(vm.GeneratedAudioFiles);
    }

    [Fact]
    public async Task AudioStudioViewModel_GenerateAudioAsync_Success_UpdatesStatusAndReloadsFiles()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/audio/generate")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{\"status\":\"queued\"}", Encoding.UTF8, "application/json") });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/audio/files")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("[]", Encoding.UTF8, "application/json") });

        var client = new HttpClient(handlerMock.Object);
        var vm = new AudioStudioViewModel();

        var ctx = new ParamContext("http://127.0.0.1:5246", client);
        await vm.GenerateAudioAsync(ctx);

        Assert.Contains("queued successfully", vm.StatusMessage);
        Assert.False(vm.IsGenerating);
    }

    [Fact]
    public async Task AudioStudioViewModel_GenerateAudioAsync_FailureStatusCode_UpdatesStatusMessage()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/audio/generate")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError });

        var client = new HttpClient(handlerMock.Object);
        var vm = new AudioStudioViewModel();

        var ctx = new ParamContext("http://127.0.0.1:5246", client);
        await vm.GenerateAudioAsync(ctx);

        Assert.Contains("Failed to queue", vm.StatusMessage);
        Assert.False(vm.IsGenerating);
    }

    [Fact]
    public async Task AudioStudioViewModel_GenerateAudioAsync_Exception_SetsErrorStatusMessage()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/api/audio/generate")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network failure"));

        var client = new HttpClient(handlerMock.Object);
        var vm = new AudioStudioViewModel();

        var ctx = new ParamContext("http://127.0.0.1:5246", client);
        await vm.GenerateAudioAsync(ctx);

        Assert.Contains("Error: Network failure", vm.StatusMessage);
        Assert.False(vm.IsGenerating);
    }

    [Fact]
    public async Task AudioStudioViewModel_GenerateAudioAsync_WhenAlreadyGenerating_ReturnsEarly()
    {
        var vm = new AudioStudioViewModel();
        vm.IsGenerating = true;
        vm.StatusMessage = "Initial";

        await vm.GenerateAudioAsync(null);
        Assert.Equal("Initial", vm.StatusMessage);
    }

    [Fact]
    public void AudioStudioViewModel_TogglePlay_WhenNoFiles_SetsStatusMessage()
    {
        var vm = new AudioStudioViewModel();
        vm.TogglePlay();
        Assert.Equal("No audio track selected to play.", vm.StatusMessage);
        Assert.False(vm.IsPlaying);
    }

    [Fact]
    public void AudioStudioViewModel_TogglePlay_WithFiles_TogglesIsPlaying()
    {
        var vm = new AudioStudioViewModel();
        var item = new AudioFileItem("synth_loop.wav", "/output_audio/synth_loop.wav", 2048, DateTime.UtcNow);
        vm.GeneratedAudioFiles.Add(item);

        vm.TogglePlay();
        Assert.True(vm.IsPlaying);
        Assert.Equal("synth_loop.wav", vm.PlayingTrackTitle);
        Assert.Contains("Playing: synth_loop.wav", vm.StatusMessage);

        vm.TogglePlay();
        Assert.False(vm.IsPlaying);
        Assert.Equal("⏸️ Paused", vm.StatusMessage);
    }

    #endregion

    #region 2. WorkflowEndpoints Video & Audio Presets Tests

    [Fact]
    public async Task WorkflowEndpoints_GenerateVideo_Wan2_2_i2v_WithImageUrl_SubstitutesAndQueues()
    {
        var request = new VideoGenerateRequest(
            WorkflowId: "wan2.2_i2v",
            Prompt: "Animate character looking around smoothly",
            NegativePrompt: "artifacts, glitches",
            Width: 832,
            Height: 480,
            Frames: 49,
            Fps: 16,
            Seed: 54321,
            ImageUrl: "http://127.0.0.1:5246/input/sample_portrait.png"
        );

        var response = await _client.PostAsJsonAsync("/api/video/generate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("promptId", out var promptIdProp));
        Assert.False(string.IsNullOrWhiteSpace(promptIdProp.GetString()));
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task WorkflowEndpoints_GenerateVideo_Ltx2_5_QueuesPrompt()
    {
        var request = new VideoGenerateRequest(
            WorkflowId: "ltx2.5_t2v",
            Prompt: "Drone flyover of a futuristic alpine valley",
            NegativePrompt: "worst quality",
            Width: 768,
            Height: 512,
            Frames: 65,
            Fps: 24,
            Seed: 98765
        );

        var response = await _client.PostAsJsonAsync("/api/video/generate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WorkflowEndpoints_GenerateVideo_HunyuanVideo_QueuesPrompt()
    {
        var request = new VideoGenerateRequest(
            WorkflowId: "hunyuanvideo1.5_t2v",
            Prompt: "Majestic dragon soaring above misty mountain peaks",
            NegativePrompt: "blurry, lowres",
            Width: 848,
            Height: 480,
            Frames: 45,
            Fps: 24,
            Seed: 112233
        );

        var response = await _client.PostAsJsonAsync("/api/video/generate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WorkflowEndpoints_GetAudioWorkflows_ReturnsPresetList()
    {
        var response = await _client.GetAsync("/api/audio/workflows");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, json.ValueKind);

        var foundStableAudio = false;
        var foundYue = false;

        foreach (var item in json.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var idProp))
            {
                var id = idProp.GetString();
                if (id == "stable_audio_open_sfx") foundStableAudio = true;
                if (id == "yue_full_song") foundYue = true;
            }
        }

        Assert.True(foundStableAudio, "stable_audio_open_sfx should be returned");
        Assert.True(foundYue, "yue_full_song should be returned");
    }

    [Fact]
    public async Task WorkflowEndpoints_GenerateAudio_StableAudioOpen_QueuesPrompt()
    {
        var request = new AudioGenerateRequest(
            WorkflowId: "stable_audio_open_sfx",
            Prompt: "Cyberpunk neon rain ambient cityscape synthesizer, 48khz stereo",
            NegativePrompt: "distortion, low quality",
            DurationSeconds: 30,
            Seed: 445566
        );

        var response = await _client.PostAsJsonAsync("/api/audio/generate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("promptId", out var promptIdProp));
        Assert.False(string.IsNullOrWhiteSpace(promptIdProp.GetString()));
        Assert.Equal("queued", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task WorkflowEndpoints_GenerateAudio_YuE_QueuesPrompt()
    {
        var request = new AudioGenerateRequest(
            WorkflowId: "yue_full_song",
            Prompt: "[Verse] Walking through the neon lights [Chorus] Antigravity in flight",
            NegativePrompt: "harsh distortion",
            DurationSeconds: 60,
            Seed: 778899
        );

        var response = await _client.PostAsJsonAsync("/api/audio/generate", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WorkflowEndpoints_GenerateAudio_NonExistentWorkflow_ReturnsNotFound()
    {
        var request = new AudioGenerateRequest(
            WorkflowId: "non_existent_audio_workflow_999",
            Prompt: "Test sound"
        );

        var response = await _client.PostAsJsonAsync("/api/audio/generate", request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkflowEndpoints_GetAudioFiles_ReturnsGeneratedFilesList()
    {
        var audioDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "output_audio");
        Directory.CreateDirectory(audioDir);

        var dummyAudio = Path.Combine(audioDir, $"test_audio_{Guid.NewGuid():N}.mp3");
        await File.WriteAllTextAsync(dummyAudio, "dummy audio content");

        try
        {
            var response = await _client.GetAsync("/api/audio/files");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, json.ValueKind);

            var found = false;
            foreach (var item in json.EnumerateArray())
            {
                if (item.TryGetProperty("filename", out var fnProp) && fnProp.GetString() == Path.GetFileName(dummyAudio))
                {
                    found = true;
                    Assert.Equal($"/output_audio/{Path.GetFileName(dummyAudio)}", item.GetProperty("url").GetString());
                    break;
                }
            }

            Assert.True(found, "Dummy audio file should be listed in output audio files");
        }
        finally
        {
            if (File.Exists(dummyAudio)) File.Delete(dummyAudio);
        }
    }

    #endregion

    #region 3. ComponentManagerService & ComponentEndpoints Deep Tests

    [Fact]
    public void ComponentManagerService_Properties_ReturnExpectedValues()
    {
        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());

        var service = new ComponentManagerService(settingsMock.Object);

        var isVideo = service.IsVideoPackInstalled;
        var isAudio = service.IsAudioPackInstalled;

        Assert.IsType<bool>(isVideo);
        Assert.IsType<bool>(isAudio);
    }

    [Fact]
    public async Task ComponentManagerService_InstallComponentAsync_SimulatesProgressAndCompletion()
    {
        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());

        var service = new ComponentManagerService(settingsMock.Object);

        double lastProgress = 0;
        var progress = new Progress<double>(p => lastProgress = p);

        var videoResult = await service.InstallComponentAsync("video-generation", progress);
        Assert.True(videoResult);
        Assert.True(lastProgress > 0);

        var audioResult = await service.InstallComponentAsync("audio-tts", progress);
        Assert.True(audioResult);

        var unknownResult = await service.InstallComponentAsync("unknown-pack-xyz");
        Assert.False(unknownResult);
    }

    [Fact]
    public async Task ComponentManagerService_UninstallComponentAsync_HandlesValidAndInvalidPacks()
    {
        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());

        var service = new ComponentManagerService(settingsMock.Object);

        var videoResult = await service.UninstallComponentAsync("video-generation");
        Assert.True(videoResult);

        var audioResult = await service.UninstallComponentAsync("audio-tts");
        Assert.True(audioResult);

        var unknownResult = await service.UninstallComponentAsync("unknown-pack-xyz");
        Assert.False(unknownResult);
    }

    [Fact]
    public async Task ComponentEndpoints_Install_MissingComponentId_ReturnsBadRequest()
    {
        var emptyReq = new ComponentInstallRequest { ComponentId = "" };
        var response = await _client.PostAsJsonAsync("/api/components/install", emptyReq);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ComponentEndpoints_Uninstall_MissingComponentId_ReturnsBadRequest()
    {
        var emptyReq = new ComponentInstallRequest { ComponentId = "" };
        var response = await _client.PostAsJsonAsync("/api/components/uninstall", emptyReq);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region 4. AiEngineManager Deep Tests

    [Fact]
    public async Task AiEngineManager_StartAndStopEngineAsync_HandlesAllEngines()
    {
        var manager = new AiEngineManager();

        var forgeStart = await manager.StartEngineAsync("forge");
        Assert.NotNull(forgeStart);

        var comfyStart = await manager.StartEngineAsync("comfyui");
        Assert.NotNull(comfyStart);

        var ollamaStart = await manager.StartEngineAsync("ollama");
        Assert.NotNull(ollamaStart);

        var audioStart = await manager.StartEngineAsync("audio");
        Assert.NotNull(audioStart);

        var kokoroStart = await manager.StartEngineAsync("kokoro");
        Assert.NotNull(kokoroStart);

        var alltalkStart = await manager.StartEngineAsync("alltalk");
        Assert.NotNull(alltalkStart);

        var unsupportedStart = await manager.StartEngineAsync("unsupported_engine");
        Assert.False(unsupportedStart.Success);
        Assert.Contains("Unsupported engine", unsupportedStart.Message);

        var forgeStop = await manager.StopEngineAsync("forge");
        Assert.NotNull(forgeStop);

        var comfyStop = await manager.StopEngineAsync("comfyui");
        Assert.NotNull(comfyStop);

        var audioStop = await manager.StopEngineAsync("audio");
        Assert.NotNull(audioStop);

        var ttsStop = await manager.StopEngineAsync("tts");
        Assert.NotNull(ttsStop);

        var unsupportedStop = await manager.StopEngineAsync("unsupported_engine");
        Assert.False(unsupportedStop.Success);
    }

    [Fact]
    public async Task AiEngineManager_StartAudioEngineAsync_NonExistentPath_ReturnsFalse()
    {
        var manager = new AiEngineManager();
        var result = await manager.StartAudioEngineAsync("C:\\NonExistentPath\\kokoro.bat", NullLogger.Instance);
        Assert.False(result);
    }

    [Fact]
    public void AiEngineManager_IsProcessRunning_ReturnsBoolean()
    {
        var manager = new AiEngineManager();
        var isRunning = manager.IsProcessRunning("explorer");
        Assert.IsType<bool>(isRunning);
    }

    #endregion

    #region 5. ModelProxyEndpoints OpenAI Speech Proxy & Civitai Tests

    [Fact]
    public async Task ModelProxyEndpoints_OpenAiSpeech_WithoutVoice_InjectsPreferredVoice()
    {
        var payload = new
        {
            input = "Welcome to Local LLM Server Manager speech synthesis.",
            model = "kokoro"
        };

        var response = await _client.PostAsJsonAsync("/v1/audio/speech", payload);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task ModelProxyEndpoints_OpenAiSpeech_WithVoice_ProxiesVoiceDirectly()
    {
        var payload = new
        {
            input = "Testing with custom voice parameter.",
            model = "kokoro",
            voice = "af_bella"
        };

        var response = await _client.PostAsJsonAsync("/v1/audio/speech", payload);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task ModelProxyEndpoints_CivitaiModel_ReturnsModelDetailsOrStatus()
    {
        var response = await _client.GetAsync("/api/civitai/model?id=12345");
        Assert.NotNull(response);
    }

    [Fact]
    public async Task ModelProxyEndpoints_CivitaiSearch_ReturnsSearchResponse()
    {
        var response = await _client.GetAsync("/api/civitai/search?q=cyberpunk&types=Checkpoint&sort=Highest+Rated");
        Assert.NotNull(response);
    }

    #endregion

    #region 6. SettingsViewModel & MainViewModel Coverage

    [Fact]
    public void SettingsViewModel_ThemeMapping_WorksBothWays()
    {
        Assert.Equal("OLED Pure Black", SettingsViewModel.MapThemeToString(AppTheme.OledBlack));
        Assert.Equal("Clean Light", SettingsViewModel.MapThemeToString(AppTheme.Light));
        Assert.Equal("Matte Carbon (Default)", SettingsViewModel.MapThemeToString(AppTheme.MatteCarbon));

        Assert.Equal(AppTheme.OledBlack, SettingsViewModel.MapStringToTheme("OLED Pure Black"));
        Assert.Equal(AppTheme.Light, SettingsViewModel.MapStringToTheme("Clean Light"));
        Assert.Equal(AppTheme.MatteCarbon, SettingsViewModel.MapStringToTheme("Unknown"));
    }

    [Fact]
    public void SettingsViewModel_EvaluateExecutableAndDirectoryStatus_HandlesMissingAndExisting()
    {
        Assert.Equal("⚠️ Missing", SettingsViewModel.EvaluateExecutableStatus(null));
        Assert.Equal("⚠️ Missing", SettingsViewModel.EvaluateExecutableStatus(""));
        Assert.Equal("⚠️ Missing", SettingsViewModel.EvaluateExecutableStatus("C:\\invalid\\path.exe"));

        Assert.Equal("⚠️ Missing", SettingsViewModel.EvaluateDirectoryStatus(null));
        Assert.Equal("⚠️ Missing", SettingsViewModel.EvaluateDirectoryStatus(""));
        Assert.Equal("⚠️ Missing", SettingsViewModel.EvaluateDirectoryStatus("C:\\invalid\\directory"));

        var appDir = AppContext.BaseDirectory;
        Assert.Equal("🟢 Verified", SettingsViewModel.EvaluateDirectoryStatus(appDir));
    }

    [Fact]
    public void SettingsViewModel_SwitchThemeStyle_UpdatesSelectedThemeStyle()
    {
        var vm = new SettingsViewModel();
        vm.SwitchThemeStyle("matte");
        Assert.Equal("matte", vm.SelectedThemeStyle);

        vm.SwitchThemeStyle("semi");
        Assert.Equal("semi", vm.SelectedThemeStyle);

        vm.SwitchThemeStyle("");
        Assert.Equal("semi", vm.SelectedThemeStyle);
    }

    [Fact]
    public async Task MainViewModel_GenerateVideoAsync_WhenAlreadyGenerating_ReturnsEarly()
    {
        var vm = new MainViewModel();
        vm.IsGeneratingVideo = true;
        vm.VideoGenerationProgress = 42;

        await vm.GenerateVideoAsync();
        Assert.Equal(42, vm.VideoGenerationProgress);
    }

    [Fact]
    public void MainViewModel_SelectVideo_SetsPropertiesCorrectly()
    {
        var vm = new MainViewModel();
        var item = new VideoAssetItem("city.mp4", "http://127.0.0.1:5246/output_video/city.mp4", "4.5s", "1280x720", 24, 778899, 1024 * 1024, DateTime.UtcNow);

        vm.SelectVideo(item);

        Assert.Equal("http://127.0.0.1:5246/output_video/city.mp4", vm.RenderedVideoUrl);
        Assert.Equal("4.5s", vm.VideoDurationText);
        Assert.Equal("1280x720", vm.VideoResolutionBadge);
        Assert.Equal("24 fps", vm.VideoFpsBadge);
        Assert.Equal("778899", vm.VideoSeedBadge);
    }

    [Fact]
    public void MainViewModel_ToggleVideoControls_TogglesState()
    {
        var vm = new MainViewModel();
        Assert.True(vm.IsVideoPlaying);
        Assert.True(vm.IsVideoLooping);

        vm.ToggleVideoPlay();
        Assert.False(vm.IsVideoPlaying);

        vm.ToggleVideoLoop();
        Assert.False(vm.IsVideoLooping);
    }

    #endregion
}
