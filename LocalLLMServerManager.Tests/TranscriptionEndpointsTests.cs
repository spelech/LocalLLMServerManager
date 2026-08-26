using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class TranscriptionEndpointsTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;
    private readonly HttpClient _client;

    public TranscriptionEndpointsTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public void MapTranscriptionEndpoints_ExtensionMethod_InvokesSuccessfully()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        var returnedApp = LocalLLMServerManager.Endpoints.TranscriptionEndpoints.MapTranscriptionEndpoints(app);
        Assert.NotNull(returnedApp);
    }

    [Fact]
    public async Task Transcriptions_MissingFile_ReturnsBadRequest()
    {
        using var emptyContent = new MultipartFormDataContent();
        var response = await _client.PostAsync("/v1/audio/transcriptions", emptyContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out var errorProp));
        Assert.True(errorProp.TryGetProperty("message", out var msgProp));
        Assert.Contains("file", msgProp.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Translations_MissingFile_ReturnsBadRequest()
    {
        using var emptyContent = new MultipartFormDataContent();
        var response = await _client.PostAsync("/v1/audio/translations", emptyContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out var errorProp));
        Assert.True(errorProp.TryGetProperty("message", out var msgProp));
        Assert.Contains("file", msgProp.GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transcriptions_ValidWavFile_Returns200WithText()
    {
        using var form = new MultipartFormDataContent();
        var dummyWav = CreateDummyWavBytes();
        var fileContent = new ByteArrayContent(dummyWav);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", "test.wav");
        form.Add(new StringContent("whisper-large-v3-turbo"), "model");

        var response = await _client.PostAsync("/v1/audio/transcriptions", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("text", out var textProp));
        Assert.False(string.IsNullOrWhiteSpace(textProp.GetString()));
    }

    [Fact]
    public async Task Translations_ValidWavFile_Returns200WithText()
    {
        using var form = new MultipartFormDataContent();
        var dummyWav = CreateDummyWavBytes();
        var fileContent = new ByteArrayContent(dummyWav);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", "test.wav");
        form.Add(new StringContent("whisper-large-v3-turbo"), "model");

        var response = await _client.PostAsync("/v1/audio/translations", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("text", out var textProp));
        Assert.False(string.IsNullOrWhiteSpace(textProp.GetString()));
    }

    [Fact]
    public async Task Transcriptions_ResponseFormat_VerboseJson_ReturnsDetailedStructure()
    {
        using var form = new MultipartFormDataContent();
        var dummyWav = CreateDummyWavBytes();
        var fileContent = new ByteArrayContent(dummyWav);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", "speech.wav");
        form.Add(new StringContent("verbose_json"), "response_format");

        var response = await _client.PostAsync("/v1/audio/transcriptions", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("text", out _));
        Assert.True(json.TryGetProperty("language", out _));
        Assert.True(json.TryGetProperty("duration", out _));
        Assert.True(json.TryGetProperty("segments", out var segmentsProp));
        Assert.Equal(JsonValueKind.Array, segmentsProp.ValueKind);
    }

    [Fact]
    public async Task Transcriptions_ResponseFormat_Text_ReturnsPlainText()
    {
        using var form = new MultipartFormDataContent();
        var dummyWav = CreateDummyWavBytes();
        var fileContent = new ByteArrayContent(dummyWav);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", "speech.wav");
        form.Add(new StringContent("text"), "response_format");

        var response = await _client.PostAsync("/v1/audio/transcriptions", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var text = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(text));
    }

    [Fact]
    public async Task Transcriptions_ResponseFormat_SrtAndVtt_ReturnsSubtitleFormats()
    {
        // SRT format
        using (var srtForm = new MultipartFormDataContent())
        {
            var dummyWav = CreateDummyWavBytes();
            var fileContent = new ByteArrayContent(dummyWav);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            srtForm.Add(fileContent, "file", "speech.wav");
            srtForm.Add(new StringContent("srt"), "response_format");

            var response = await _client.PostAsync("/v1/audio/transcriptions", srtForm);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var srtText = await response.Content.ReadAsStringAsync();
            Assert.Contains("-->", srtText);
        }

        // VTT format
        using (var vttForm = new MultipartFormDataContent())
        {
            var dummyWav = CreateDummyWavBytes();
            var fileContent = new ByteArrayContent(dummyWav);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            vttForm.Add(fileContent, "file", "speech.wav");
            vttForm.Add(new StringContent("vtt"), "response_format");

            var response = await _client.PostAsync("/v1/audio/transcriptions", vttForm);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var vttText = await response.Content.ReadAsStringAsync();
            Assert.Contains("WEBVTT", vttText);
        }
    }

    [Fact]
    public void SetupWhisperSttScript_GeneratesExpectedFiles()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "setup_whisper_stt.ps1");
        Assert.True(File.Exists(scriptPath), $"Script not found at {scriptPath}");

        var tempTargetDir = Path.Combine(Path.GetTempPath(), "WhisperSttTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -TargetDir \"{tempTargetDir}\" -SkipModelDownload",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            Assert.NotNull(process);
            process.WaitForExit(30000);
            Assert.Equal(0, process.ExitCode);

            Assert.True(File.Exists(Path.Combine(tempTargetDir, "transcribe.py")), "transcribe.py was not created");
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "requirements.txt")), "requirements.txt was not created");
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "run.bat")), "run.bat was not created");
            Assert.True(File.Exists(Path.Combine(tempTargetDir, "start.bat")), "start.bat was not created");
            Assert.True(Directory.Exists(Path.Combine(tempTargetDir, "model")), "model directory was not created");
            Assert.True(Directory.Exists(Path.Combine(tempTargetDir, "output")), "output directory was not created");

            var transcribePy = File.ReadAllText(Path.Combine(tempTargetDir, "transcribe.py"));
            Assert.Contains("faster-whisper", transcribePy, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/v1/audio/transcriptions", transcribePy);
            Assert.Contains("/v1/audio/translations", transcribePy);

            var reqs = File.ReadAllText(Path.Combine(tempTargetDir, "requirements.txt"));
            Assert.Contains("faster-whisper", reqs);
            Assert.Contains("fastapi", reqs);
        }
        finally
        {
            if (Directory.Exists(tempTargetDir))
            {
                try { Directory.Delete(tempTargetDir, recursive: true); } catch { }
            }
        }
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "LocalLLMServerManager.sln")) ||
                File.Exists(Path.Combine(current, "LocalLLMServerManager.csproj")))
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return Directory.GetCurrentDirectory();
    }

    private static byte[] CreateDummyWavBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Minimal 44-byte WAV header with 100 zero samples
        writer.Write("RIFF"u8);
        writer.Write(36 + 200);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)1); // mono
        writer.Write(16000);   // Sample rate 16kHz
        writer.Write(32000);   // Byte rate
        writer.Write((short)2); // Block align
        writer.Write((short)16);// Bits per sample
        writer.Write("data"u8);
        writer.Write(200);      // Data size
        writer.Write(new byte[200]);

        return ms.ToArray();
    }
}
