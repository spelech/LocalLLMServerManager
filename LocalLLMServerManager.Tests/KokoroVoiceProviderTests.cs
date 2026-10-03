using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Endpoints;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class KokoroVoiceProviderTests
{
    private static byte[] CreateDummyAudioBytes(int length = 256)
    {
        var bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = (byte)(i % 256);
        }
        return bytes;
    }

    [Fact]
    public async Task SpeechProxy_WithoutVoiceParam_InjectsDefaultVoiceAfHeart()
    {
        HttpRequestMessage? capturedTargetReq = null;
        string? capturedBody = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                capturedTargetReq = req;
                if (req.Content != null)
                {
                    capturedBody = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(CreateDummyAudioBytes())
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg") }
                    }
                });
            });

        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handlerMock.Object));

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(
            AudioEngineUrl: "http://127.0.0.1:8880",
            PreferredAudioVoice: "af_heart"
        ));

        // Create HttpContext with incoming speech request lacking "voice" parameter
        var context = new DefaultHttpContext();
        var incomingJson = """{"model":"kokoro","input":"Hello, this is a test of Kokoro speech synthesis."}""";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(incomingJson));
        context.Response.Body = new MemoryStream();

        // Invoke the endpoint handler logic directly
        await ModelProxyEndpoints.HandleSpeechProxyAsync(context, settingsMock.Object, clientFactoryMock.Object);

        Assert.NotNull(capturedTargetReq);
        Assert.Equal("http://127.0.0.1:8880/v1/audio/speech", capturedTargetReq.RequestUri?.ToString());

        Assert.NotNull(capturedBody);
        var doc = JsonNode.Parse(capturedBody);
        Assert.NotNull(doc);
        Assert.Equal("af_heart", doc["voice"]?.GetValue<string>());
        Assert.Equal("Hello, this is a test of Kokoro speech synthesis.", doc["input"]?.GetValue<string>());

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("audio/mpeg", context.Response.ContentType);
        Assert.Equal(256, context.Response.Body.Length);
    }

    [Fact]
    public async Task SpeechProxy_WithExplicitVoiceParam_PreservesRequestedVoice()
    {
        string? capturedBody = null;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                if (req.Content != null) capturedBody = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(CreateDummyAudioBytes(512))
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg") }
                    }
                });
            });

        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handlerMock.Object));

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(PreferredAudioVoice: "af_heart"));

        var context = new DefaultHttpContext();
        var incomingJson = """{"model":"kokoro","input":"Custom voice requested","voice":"bf_emma","speed":1.1}""";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(incomingJson));
        context.Response.Body = new MemoryStream();

        await ModelProxyEndpoints.HandleSpeechProxyAsync(context, settingsMock.Object, clientFactoryMock.Object);

        Assert.NotNull(capturedBody);
        var doc = JsonNode.Parse(capturedBody);
        Assert.Equal("bf_emma", doc?["voice"]?.GetValue<string>());
        Assert.Equal("Custom voice requested", doc?["input"]?.GetValue<string>());
        Assert.Equal(512, context.Response.Body.Length);
    }

    [Fact]
    public async Task SpeechProxy_UpstreamReturnsAudioWav_StreamsBinaryBytesToClient()
    {
        var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 }; // RIFF...WAVE

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(wavBytes)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav") }
                }
            });

        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handlerMock.Object));

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"input":"Test WAV output","response_format":"wav"}"""));
        context.Response.Body = new MemoryStream();

        await ModelProxyEndpoints.HandleSpeechProxyAsync(context, settingsMock.Object, clientFactoryMock.Object);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("audio/wav", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBytes = ((MemoryStream)context.Response.Body).ToArray();
        Assert.Equal(wavBytes, responseBytes);
    }

    [Fact]
    public async Task SpeechProxy_WhenUpstreamFails_Returns502BadGatewayWithJsonError()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Kokoro daemon unreachable on port 8880"));

        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handlerMock.Object));

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());

        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"input":"Should fail"}"""));
        context.Response.Body = new MemoryStream();

        await ModelProxyEndpoints.HandleSpeechProxyAsync(context, settingsMock.Object, clientFactoryMock.Object);

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var jsonText = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("error", jsonText);
        Assert.Contains("Kokoro daemon unreachable", jsonText);
    }

    [Fact]
    public async Task VoicesCatalog_WhenUpstreamOnline_ReturnsUpstreamVoicesJson()
    {
        var sampleVoicesJson = """{"voices":["af_heart","af_bella","am_adam","bf_emma"]}""";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sampleVoicesJson, Encoding.UTF8, "application/json")
            });

        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handlerMock.Object));

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(AudioEngineUrl: "http://127.0.0.1:8880"));

        var result = await EngineEndpoints.HandleGetVoicesAsync(settingsMock.Object, clientFactoryMock.Object);
        Assert.NotNull(result);

        var contentResult = result as ContentHttpResult;
        Assert.NotNull(contentResult);
        Assert.Equal("application/json", contentResult.ContentType);
        Assert.Contains("af_heart", contentResult.ResponseContent);
        Assert.Contains("bf_emma", contentResult.ResponseContent);
    }

    [Fact]
    public async Task VoicesCatalog_WhenUpstreamOffline_ReturnsDefaultVoicesFallback()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handlerMock.Object));

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings(PreferredAudioVoice: "af_heart"));

        var result = await EngineEndpoints.HandleGetVoicesAsync(settingsMock.Object, clientFactoryMock.Object);
        Assert.NotNull(result);

        var value = (result as IValueHttpResult)?.Value;
        Assert.NotNull(value);
        var json = JsonSerializer.Serialize(value);
        Assert.Contains("af_heart", json);
        Assert.Contains("am_adam", json);
        Assert.Contains("bf_emma", json);
    }

    [Fact]
    public void AudioStudioViewModel_VoiceProfile_AndPresetManagement()
    {
        var presetService = new StudioPresetService();
        var canIRunIt = new CanIRunItService();
        var vm = new AudioStudioViewModel(presetService, canIRunIt);

        Assert.Equal("af_heart", vm.VoiceProfile);
        Assert.NotEmpty(vm.AudioPresets);

        // Select different voice
        vm.VoiceProfile = "am_michael";
        Assert.Equal("am_michael", vm.VoiceProfile);

        // Save custom preset
        vm.Prompt = "A cozy fireplace crackling with gentle rain outside, 48kHz stereo";
        vm.DurationSeconds = 45;
        vm.SaveCurrentAsAudioPreset("Cozy Rain Ambience");

        var customPreset = vm.AudioPresets.FirstOrDefault(p => p.Name == "Cozy Rain Ambience");
        Assert.NotNull(customPreset);
        Assert.Equal("am_michael", customPreset.VoiceProfile);
        Assert.Equal(45, customPreset.DurationSeconds);
        Assert.Equal("A cozy fireplace crackling with gentle rain outside, 48kHz stereo", customPreset.SamplePrompt);

        // Duplicate custom preset
        vm.DuplicateCurrentAudioPreset(customPreset);
        var dupPreset = vm.AudioPresets.FirstOrDefault(p => p.Name.Contains("Cozy Rain Ambience (Copy)"));
        Assert.NotNull(dupPreset);

        // Delete custom presets
        vm.DeleteCurrentAudioPreset(dupPreset);
        Assert.DoesNotContain(vm.AudioPresets, p => p.Id == dupPreset.Id);

        vm.DeleteCurrentAudioPreset(customPreset);
        Assert.DoesNotContain(vm.AudioPresets, p => p.Id == customPreset.Id);
    }

    [Fact]
    public void AudioStudioViewModel_LiveAudioPlayback_TogglesPlayStateAndTrackTitle()
    {
        var vm = new AudioStudioViewModel();

        Assert.False(vm.IsPlaying);
        Assert.Equal("▶️ Play", vm.PlayButtonText);

        vm.IsPlaying = true;
        Assert.Equal("⏸️ Pause", vm.PlayButtonText);

        vm.IsPlaying = false;
        Assert.Equal("▶️ Play", vm.PlayButtonText);

        // Track title updates on SelectedAudioFile change
        var track = new AudioFileItem("synth_chillwave_01.wav", "/output_audio/synth_chillwave_01.wav", 1024000, DateTime.UtcNow);
        vm.GeneratedAudioFiles.Add(track);
        vm.SelectedAudioFile = track;
        Assert.Equal("synth_chillwave_01.wav", vm.PlayingTrackTitle);
    }

    [Fact]
    public void AudioStudioViewModel_CancelGeneration_ResetsAllStagesAndStatus()
    {
        var vm = new AudioStudioViewModel();

        // Simulate mid-generation state
        vm.IsGenerating = true;
        vm.GenerationStage = 2;
        vm.GenerationStageTitle = "2. Denoising & Synthesis";
        vm.Stage1Status = "Complete";
        vm.Stage2Status = "Active";
        vm.ProgressValue = 50;

        vm.CancelGeneration();

        Assert.False(vm.IsGenerating);
        Assert.Equal(0, vm.GenerationStage);
        Assert.Equal("Cancelled", vm.GenerationStageTitle);
        Assert.Equal("Pending", vm.Stage1Status);
        Assert.Equal("Pending", vm.Stage2Status);
        Assert.Equal("Pending", vm.Stage3Status);
        Assert.Equal("Pending", vm.Stage4Status);
        Assert.Equal(0.0, vm.ProgressValue);
        Assert.Equal("Generation cancelled.", vm.StatusMessage);
    }

    [Fact]
    public void AudioStudioViewModel_RecalculateHardwareFit_UpdatesBadge()
    {
        var vm = new AudioStudioViewModel();

        // Low VRAM
        vm.RecalculateHardwareFit(2000, 4000);
        Assert.NotNull(vm.AudioHardwareFit);
        Assert.False(string.IsNullOrWhiteSpace(vm.HardwareStatusBadgeText));

        // High VRAM
        vm.RecalculateHardwareFit(16000, 24000);
        Assert.NotNull(vm.AudioHardwareFit);
        Assert.False(string.IsNullOrWhiteSpace(vm.HardwareStatusBadgeText));
    }
}
