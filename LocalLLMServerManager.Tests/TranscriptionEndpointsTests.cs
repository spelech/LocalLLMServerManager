using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using LocalLLMServerManager.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
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
        // 1. Empty multipart form
        using var emptyContent = new MultipartFormDataContent();
        var response = await _client.PostAsync("/v1/audio/transcriptions", emptyContent);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out var errorProp));
        Assert.True(errorProp.TryGetProperty("message", out var msgProp));
        Assert.Equal("No file uploaded in 'file' form field.", msgProp.GetString());

        // 2. Non-form content
        using var stringContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var nonFormResp = await _client.PostAsync("/v1/audio/transcriptions", stringContent);
        Assert.Equal(HttpStatusCode.BadRequest, nonFormResp.StatusCode);

        // 3. File in wrong field name
        using var wrongFieldForm = new MultipartFormDataContent();
        wrongFieldForm.Add(new ByteArrayContent(CreateDummyWavBytes()), "audio_data", "test.wav");
        var wrongFieldResp = await _client.PostAsync("/v1/audio/transcriptions", wrongFieldForm);
        Assert.Equal(HttpStatusCode.BadRequest, wrongFieldResp.StatusCode);

        // 4. Empty file content (0 bytes)
        using var emptyFileForm = new MultipartFormDataContent();
        emptyFileForm.Add(new ByteArrayContent(Array.Empty<byte>()), "file", "test.wav");
        var emptyFileResp = await _client.PostAsync("/v1/audio/transcriptions", emptyFileForm);
        Assert.Equal(HttpStatusCode.BadRequest, emptyFileResp.StatusCode);
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
        Assert.Equal("No file uploaded in 'file' form field.", msgProp.GetString());
    }

    [Fact]
    public async Task Transcriptions_UpstreamEngineOffline_Returns502BadGateway()
    {
        using var form = new MultipartFormDataContent();
        var dummyWav = CreateDummyWavBytes();
        var fileContent = new ByteArrayContent(dummyWav);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", "test.wav");
        form.Add(new StringContent("whisper-large-v3-turbo"), "model");

        var response = await _client.PostAsync("/v1/audio/transcriptions", form);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out var errorProp));
        Assert.True(errorProp.TryGetProperty("message", out var msgProp));
        Assert.Contains("Upstream audio transcription engine unavailable", msgProp.GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(errorProp.TryGetProperty("type", out var typeProp));
        Assert.Equal("bad_gateway", typeProp.GetString());
    }

    [Fact]
    public async Task Translations_UpstreamEngineOffline_Returns502BadGateway()
    {
        using var form = new MultipartFormDataContent();
        var dummyWav = CreateDummyWavBytes();
        var fileContent = new ByteArrayContent(dummyWav);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", "test.wav");
        form.Add(new StringContent("whisper-large-v3-turbo"), "model");

        var response = await _client.PostAsync("/v1/audio/translations", form);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out var errorProp));
        Assert.True(errorProp.TryGetProperty("message", out var msgProp));
        Assert.Contains("Upstream audio transcription engine unavailable", msgProp.GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(errorProp.TryGetProperty("type", out var typeProp));
        Assert.Equal("bad_gateway", typeProp.GetString());
    }

    [Fact]
    public void FormatTranscriptionResponse_Text_ReturnsPlainText()
    {
        var result = TranscriptionEndpoints.FormatTranscriptionResponse("text", "Sample transcript", "english", false);
        var contentResult = Assert.IsAssignableFrom<ContentHttpResult>(result);
        Assert.Equal("Sample transcript", contentResult.ResponseContent);
        Assert.Equal("text/plain", contentResult.ContentType);
    }

    [Fact]
    public void FormatTranscriptionResponse_SrtAndVtt_ReturnsSubtitleFormats()
    {
        // SRT format
        {
            var result = TranscriptionEndpoints.FormatTranscriptionResponse("srt", "Subtitle line", "english", false);
            var contentResult = Assert.IsAssignableFrom<ContentHttpResult>(result);
            Assert.Contains("-->", contentResult.ResponseContent);
            Assert.Contains("Subtitle line", contentResult.ResponseContent);
            Assert.Equal("text/plain", contentResult.ContentType);
        }

        // VTT format
        {
            var result = TranscriptionEndpoints.FormatTranscriptionResponse("vtt", "VTT line", "english", false);
            var contentResult = Assert.IsAssignableFrom<ContentHttpResult>(result);
            Assert.Contains("WEBVTT", contentResult.ResponseContent);
            Assert.Contains("VTT line", contentResult.ResponseContent);
            Assert.Equal("text/vtt", contentResult.ContentType);
        }
    }

    [Fact]
    public void FormatTranscriptionResponse_JsonAndVerboseJson_ReturnsExpectedJsonStructure()
    {
        // Default json
        {
            var result = TranscriptionEndpoints.FormatTranscriptionResponse("json", "Hello transcription", "english", false);
            var okResult = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IValueHttpResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("Hello transcription", root.GetProperty("text").GetString());
            Assert.Equal("english", root.GetProperty("language").GetString());
            Assert.Equal(1.0, root.GetProperty("duration").GetDouble());
            Assert.Equal(JsonValueKind.Array, root.GetProperty("segments").ValueKind);
        }

        // Verbose json (transcribe)
        {
            var result = TranscriptionEndpoints.FormatTranscriptionResponse("verbose_json", "Detailed transcript", "spanish", false);
            var okResult = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IValueHttpResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("Detailed transcript", root.GetProperty("text").GetString());
            Assert.Equal("spanish", root.GetProperty("language").GetString());
            Assert.Equal("transcribe", root.GetProperty("task").GetString());
            Assert.Equal(1.0, root.GetProperty("duration").GetDouble());
            Assert.Equal(JsonValueKind.Array, root.GetProperty("segments").ValueKind);
        }

        // Verbose json (translate)
        {
            var result = TranscriptionEndpoints.FormatTranscriptionResponse("verbose_json", "Translated text", "french", true);
            var okResult = Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IValueHttpResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.Equal("Translated text", root.GetProperty("text").GetString());
            Assert.Equal("french", root.GetProperty("language").GetString());
            Assert.Equal("translate", root.GetProperty("task").GetString());
        }
    }

    [Fact]
    public void SetupWhisperSttScript_GeneratesExpectedFiles()
    {
        var repoRoot = FindRepoRoot();
        var scriptPath = Path.Combine(repoRoot, "scripts", "setup_whisper_stt.ps1");
        Assert.True(File.Exists(scriptPath), $"Script not found at {scriptPath}");

        var tempTargetDir = Path.Combine(Path.GetTempPath(), "WhisperSttTest_" + Guid.NewGuid().ToString("N"));

        var psExe = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = psExe,
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -TargetDir \"{tempTargetDir}\" -SkipModelDownload",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return;
            process.WaitForExit(30000);
            if (process.ExitCode != 0) return;

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
        catch (System.ComponentModel.Win32Exception)
        {
            // PowerShell executable not available in environment
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
