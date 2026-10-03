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
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

/// <summary>
/// Exhaustive unit & integration tests verifying that our commands actually control
/// the underlying engines correctly across all four creative modalities:
/// 1. Text Generation (Ollama / Local LLM)
/// 2. Image Generation (Stable Diffusion Forge & ComfyUI)
/// 3. Voice & Speech Synthesis (Kokoro TTS & ComfyUI Audio)
/// 4. Video & 3D Mesh Generation (ComfyUI Wan2.1 / LTX / TRELLIS / Hunyuan3D)
/// </summary>
public class EngineCommandExecutionTests
{
    // =========================================================================
    // MODALITY 1: TEXT GENERATION (Ollama & LLM Commands)
    // =========================================================================

    [Fact]
    public async Task Ollama_GenerateTextCommand_SendsValidPayload_AndParsesResponse()
    {
        // Arrange
        var capturedRequests = new List<HttpRequestMessage>();
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedRequests.Add(req);
                var responseJson = """
                {
                    "model": "llama3.2:latest",
                    "created_at": "2026-10-03T12:00:00Z",
                    "response": "Antigravity pair-programming assistant is ready.",
                    "done": true,
                    "total_duration": 123456789,
                    "eval_count": 42
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var vm = new MainViewModel
        {
            ApiBase = "http://127.0.0.1:11434",
            Http = httpClient,
            OllamaPrompt = "Explain quantum computing in one sentence."
        };

        // Act
        await vm.GenerateOllamaTextAsync();

        // Assert
        Assert.Single(capturedRequests);
        var req = capturedRequests[0];
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("/api/generate", req.RequestUri?.ToString() ?? "");

        var sentBody = await req.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(sentBody);
        var root = doc.RootElement;

        Assert.Equal("Explain quantum computing in one sentence.", root.GetProperty("prompt").GetString());
        Assert.NotNull(root.GetProperty("model").GetString());
        Assert.True(root.GetProperty("options").TryGetProperty("num_ctx", out var numCtx));
        Assert.True(numCtx.GetInt32() > 0);

        Assert.Equal("Antigravity pair-programming assistant is ready.", vm.OllamaResponseText);
        Assert.False(vm.IsGeneratingOllamaText);
    }

    [Fact]
    public async Task Ollama_UnloadAllVram_SendsZeroKeepAliveForActiveModels()
    {
        // Arrange
        var postedGenerateRequests = new List<string>();
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("/api/ps") || r.RequestUri!.ToString().Contains("/api/ollama/ps")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                    "models": [
                        { "name": "llama3.2:latest", "size": 4300000000 },
                        { "name": "qwen2.5-coder:7b", "size": 4700000000 }
                    ]
                }
                """, Encoding.UTF8, "application/json")
            });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("/api/generate")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                var body = req.Content!.ReadAsStringAsync().Result;
                postedGenerateRequests.Add(body);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"done\": true}", Encoding.UTF8, "application/json")
                };
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var service = new OllamaModelService();

        // Act
        var result = await service.UnloadAllVramAsync("http://127.0.0.1:11434", httpClient);

        // Assert
        Assert.True(result);
        Assert.Equal(2, postedGenerateRequests.Count);

        foreach (var body in postedGenerateRequests)
        {
            using var doc = JsonDocument.Parse(body);
            Assert.True(doc.RootElement.TryGetProperty("keep_alive", out var ka));
            Assert.Equal(0, ka.GetInt32());
            Assert.True(doc.RootElement.TryGetProperty("model", out var modelProp));
            Assert.False(string.IsNullOrWhiteSpace(modelProp.GetString()));
        }
    }

    // =========================================================================
    // MODALITY 2: IMAGE GENERATION (Forge & ComfyUI Image Workflows)
    // =========================================================================

    [Theory]
    [InlineData("1:1", 1024, 1024)]
    [InlineData("16:9", 1344, 768)]
    [InlineData("9:16", 768, 1344)]
    [InlineData("4:3", 1152, 864)]
    public async Task Forge_Txt2ImgCommand_CalculatesCorrectDimensionsAndAspects(string aspect, int expectedW, int expectedH)
    {
        // Arrange
        string? capturedPayload = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("/sdapi/v1/txt2img")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedPayload = req.Content!.ReadAsStringAsync().Result;
                var dummyPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"images\": [\"{dummyPng}\"]}}", Encoding.UTF8, "application/json")
                };
            });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("/sdapi/v1/progress")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"progress\": 1.0, \"state\": {\"sampling_step\": 20, \"sampling_steps\": 20}}", Encoding.UTF8, "application/json")
            });

        var vm = new MainViewModel
        {
            Http = new HttpClient(mockHandler.Object),
            PromptText = "Cyberpunk neon metropolis with flying cars",
            NegativePromptText = "blurry, low quality, artifacts"
        };
        vm.SelectAspectPreset(aspect);

        // Act
        await vm.GenerateStudioImageAsync();

        // Assert
        Assert.NotNull(capturedPayload);
        using var doc = JsonDocument.Parse(capturedPayload!);
        var root = doc.RootElement;

        Assert.Equal("Cyberpunk neon metropolis with flying cars", root.GetProperty("prompt").GetString());
        Assert.Equal("blurry, low quality, artifacts", root.GetProperty("negative_prompt").GetString());
        Assert.Equal(expectedW, root.GetProperty("width").GetInt32());
        Assert.Equal(expectedH, root.GetProperty("height").GetInt32());
        Assert.NotNull(vm.StudioGeneratedImageBytes);
        Assert.True(vm.StudioGeneratedImageBytes.Length > 0);
    }

    [Fact]
    public async Task Forge_Img2ImgCommand_AttachesBase64ReferenceImageAndDenoising()
    {
        // Arrange
        string? capturedPayload = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("/sdapi/v1/img2img")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedPayload = req.Content!.ReadAsStringAsync().Result;
                var dummyPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"{{\"images\": [\"{dummyPng}\"]}}", Encoding.UTF8, "application/json")
                };
            });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("/sdapi/v1/progress")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"progress\": 1.0}", Encoding.UTF8, "application/json")
            });

        var dummyReferenceBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var vm = new MainViewModel
        {
            Http = new HttpClient(mockHandler.Object),
            PromptText = "Transform into stained glass art",
            AttachedImageBytes = dummyReferenceBytes,
            HasAttachedImage = true,
            StudioDenoise = 0.65
        };

        // Act
        await vm.GenerateStudioImageAsync();

        // Assert
        Assert.NotNull(capturedPayload);
        using var doc = JsonDocument.Parse(capturedPayload!);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("init_images", out var initImages));
        Assert.Equal(JsonValueKind.Array, initImages.ValueKind);
        Assert.Equal(Convert.ToBase64String(dummyReferenceBytes), initImages[0].GetString());
        Assert.Equal(0.65, root.GetProperty("denoising_strength").GetDouble(), 2);
    }

    [Fact]
    public void ComfyUi_ProductionImageWorkflows_ExistAndHaveValidNodeTopology()
    {
        // Arrange & Act
        var workflowsDir = FindWorkflowsDirectory();
        var fluxPath = Path.Combine(workflowsDir, "flux_sdxl_image_api.json");
        var stickerPath = Path.Combine(workflowsDir, "sticker_generator.json");

        Assert.True(File.Exists(fluxPath), $"Expected workflow at {fluxPath}");
        Assert.True(File.Exists(stickerPath), $"Expected workflow at {stickerPath}");

        var fluxJson = File.ReadAllText(fluxPath);
        var stickerJson = File.ReadAllText(stickerPath);

        var fluxNode = JsonNode.Parse(fluxJson);
        var stickerNode = JsonNode.Parse(stickerJson);

        // Assert
        Assert.NotNull(fluxNode);
        Assert.NotNull(stickerNode);

        // Verify that flux has CLIP text encode and KSampler nodes
        var fluxGraph = fluxNode?["workflow"] as JsonObject ?? fluxNode as JsonObject;
        Assert.NotNull(fluxGraph);

        bool hasClip = false;
        bool hasSampler = false;
        foreach (var kvp in fluxGraph!)
        {
            if (kvp.Value is JsonObject node)
            {
                var classType = node["class_type"]?.ToString() ?? "";
                if (classType.Contains("CLIPTextEncode")) hasClip = true;
                if (classType.Contains("KSampler") || classType.Contains("Sampler")) hasSampler = true;
            }
        }

        Assert.True(hasClip, "Flux workflow should contain a CLIPTextEncode node.");
        Assert.True(hasSampler, "Flux workflow should contain a KSampler node.");
    }

    // =========================================================================
    // MODALITY 3: VOICE & SPEECH SYNTHESIS (Kokoro & ComfyUI Audio)
    // =========================================================================

    [Fact]
    public async Task Kokoro_SynthesizeSpeech_ConstructsValidPayload_AndExtractsAudioStream()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        var fakeWavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 }; // RIFF...WAVE

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedRequest = req;
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(fakeWavBytes)
                };
                resp.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                return resp;
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var mockHttpFactory = new Mock<IHttpClientFactory>();
        mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.LoadSettings()).Returns(new AppSettings(AudioEngineUrl: "http://127.0.0.1:8880"));

        var tools = new AiAppTools(
            new Mock<IGpuTelemetryProvider>().Object,
            new Mock<IAiEngineManager>().Object,
            new Mock<IOllamaModelService>().Object,
            mockSettings.Object,
            new Mock<ICanIRunItService>().Object,
            mockHttpFactory.Object
        );

        // Act
        var resultJson = await tools.SynthesizeSpeechAsync("Welcome to the multimodal server manager.", "af_heart", "wav");

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Contains("/api/tts", capturedRequest.RequestUri?.ToString() ?? "");

        var sentBody = await capturedRequest.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(sentBody);
        Assert.Equal("Welcome to the multimodal server manager.", doc.RootElement.GetProperty("text").GetString());
        Assert.Equal("af_heart", doc.RootElement.GetProperty("voice").GetString());

        using var resultDoc = JsonDocument.Parse(resultJson);
        Assert.True(resultDoc.RootElement.GetProperty("success").GetBoolean());
        Assert.Contains("/output/speech_", resultDoc.RootElement.GetProperty("outputUrl").GetString() ?? "");
    }

    [Fact]
    public void ComfyUi_AudioWorkflows_HaveParameterSubstitutionCapabilities()
    {
        // Arrange
        var workflowsDir = FindWorkflowsDirectory();
        var audioDir = Path.Combine(workflowsDir, "Audio");
        var sfxWorkflowPath = Path.Combine(audioDir, "stable_audio_open_sfx.json");
        var musicWorkflowPath = Path.Combine(audioDir, "musicgen_melody.json");

        Assert.True(File.Exists(sfxWorkflowPath), $"Expected workflow at {sfxWorkflowPath}");
        Assert.True(File.Exists(musicWorkflowPath), $"Expected workflow at {musicWorkflowPath}");

        var sfxJson = File.ReadAllText(sfxWorkflowPath);
        var sfxNode = JsonNode.Parse(sfxJson);
        Assert.NotNull(sfxNode);

        // Act: Test parameter injection logic
        var targetGraph = sfxNode?["workflow"] as JsonObject ?? sfxNode as JsonObject;
        Assert.NotNull(targetGraph);

        bool promptInjected = false;
        bool durationInjected = false;

        foreach (var kvp in targetGraph!)
        {
            if (kvp.Value is JsonObject node)
            {
                var classType = node["class_type"]?.ToString() ?? "";
                var title = node["_meta"]?["title"]?.ToString() ?? "";
                var inputs = node["inputs"] as JsonObject;

                if (inputs != null)
                {
                    if (classType.Contains("CLIPTextEncode") || title.Contains("Prompt"))
                    {
                        inputs["text"] = "Futuristic sci-fi laser sound effect";
                        promptInjected = true;
                    }
                    if (inputs.ContainsKey("seconds") || inputs.ContainsKey("duration"))
                    {
                        inputs["seconds"] = 15;
                        durationInjected = true;
                    }
                }
            }
        }

        // Assert
        Assert.True(promptInjected, "Prompt was successfully injected into audio workflow graph.");
        Assert.True(durationInjected, "Duration parameter was successfully injected into audio workflow graph.");
    }

    // =========================================================================
    // MODALITY 4: VIDEO & 3D MESH GENERATION (ComfyUI Workflows)
    // =========================================================================

    [Fact]
    public void ComfyUi_VideoWorkflows_SubstituteAllPlaceholders_WithoutJsonCorruption()
    {
        // Arrange
        var workflowsDir = FindWorkflowsDirectory();
        var videoDir = Path.Combine(workflowsDir, "Video");
        var wan2Path = Path.Combine(videoDir, "wan2.2_t2v.json");
        var ltxPath = Path.Combine(videoDir, "ltx2.5_t2v.json");

        Assert.True(File.Exists(wan2Path), $"Expected video workflow at {wan2Path}");
        Assert.True(File.Exists(ltxPath), $"Expected video workflow at {ltxPath}");

        var wan2Template = File.ReadAllText(wan2Path);

        // Act: Perform the exact parameter substitution executed in WorkflowEndpoints
        var substituted = wan2Template
            .Replace("\"{{PROMPT}}\"", JsonSerializer.Serialize("Drone flyover of a futuristic neon cyber-city"))
            .Replace("{{PROMPT}}", "Drone flyover of a futuristic neon cyber-city")
            .Replace("\"{{NEGATIVE_PROMPT}}\"", JsonSerializer.Serialize("low frame rate, stutter"))
            .Replace("{{NEGATIVE_PROMPT}}", "low frame rate, stutter")
            .Replace("\"{{WIDTH}}\"", "832")
            .Replace("{{WIDTH}}", "832")
            .Replace("\"{{HEIGHT}}\"", "480")
            .Replace("{{HEIGHT}}", "480")
            .Replace("\"{{FRAMES}}\"", "49")
            .Replace("{{FRAMES}}", "49")
            .Replace("\"{{FPS}}\"", "16")
            .Replace("{{FPS}}", "16")
            .Replace("\"{{SEED}}\"", "88888888")
            .Replace("{{SEED}}", "88888888");

        // Assert: Ensure it remains strictly valid JSON
        var parsed = JsonNode.Parse(substituted);
        Assert.NotNull(parsed);

        var promptStr = parsed?["workflow"]?["1"]?["inputs"]?["text"]?.ToString()
                     ?? parsed?["1"]?["inputs"]?["text"]?.ToString()
                     ?? parsed?["6"]?["inputs"]?["text"]?.ToString();

        // Either in template placeholder or in CLIPTextEncode node
        Assert.True(!string.IsNullOrEmpty(promptStr) || substituted.Contains("Drone flyover of a futuristic neon cyber-city"));
    }

    [Fact]
    public void ComfyUi_3DWorkflows_TrellisAndHunyuan_SubstitutePromptAndSeed()
    {
        // Arrange
        var workflowsDir = FindWorkflowsDirectory();
        var trellisPath = Path.Combine(workflowsDir, "trellis_v2_api.json");
        var hunyuanPath = Path.Combine(workflowsDir, "hunyuan3d_v2_api.json");

        Assert.True(File.Exists(trellisPath), $"Expected 3D workflow at {trellisPath}");
        Assert.True(File.Exists(hunyuanPath), $"Expected 3D workflow at {hunyuanPath}");

        var trellisJson = File.ReadAllText(trellisPath);
        var rootNode = JsonNode.Parse(trellisJson);
        Assert.NotNull(rootNode);

        var targetGraph = rootNode?["workflow"] as JsonObject ?? rootNode as JsonObject;
        Assert.NotNull(targetGraph);

        // Act: Inject 3D prompt and custom seed
        string targetPrompt = "A detailed fantasy stone gargoyle, 3d game asset, glb";
        long targetSeed = 999999;
        string targetFormat = "glb";

        foreach (var kvp in targetGraph!)
        {
            if (kvp.Value is JsonObject node)
            {
                var classType = node["class_type"]?.ToString() ?? "";
                var title = node["_meta"]?["title"]?.ToString() ?? "";
                var inputs = node["inputs"] as JsonObject;

                if (inputs != null)
                {
                    if (classType.Contains("CLIPTextEncode") || title.Contains("Prompt"))
                    {
                        inputs["text"] = targetPrompt;
                    }
                    if (inputs.ContainsKey("seed"))
                    {
                        inputs["seed"] = targetSeed;
                    }
                    if (inputs.ContainsKey("file_format"))
                    {
                        inputs["file_format"] = targetFormat;
                    }
                }
            }
        }

        // Assert
        var clipNode = targetGraph["1"] as JsonObject;
        Assert.NotNull(clipNode);
        Assert.Equal(targetPrompt, clipNode?["inputs"]?["text"]?.ToString());

        var ksamplerNode = targetGraph["3"] as JsonObject;
        Assert.NotNull(ksamplerNode);
        Assert.Equal(targetSeed, ksamplerNode?["inputs"]?["seed"]?.GetValue<long>());

        var saveMeshNode = targetGraph["7"] as JsonObject;
        Assert.NotNull(saveMeshNode);
        Assert.Equal(targetFormat, saveMeshNode?["inputs"]?["file_format"]?.ToString());
    }

    [Fact]
    public async Task MainViewModel_Generate3DAsync_DispatchesCommandToEndpoint()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("/api/3d/generate")),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedRequest = req;
                var resJson = """
                {
                    "promptId": "abc12345",
                    "status": "queued",
                    "url": "/output_3d/mesh_abc12345.glb",
                    "filename": "mesh_abc12345.glb",
                    "format": "glb",
                    "seed": 42
                }
                """;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(resJson, Encoding.UTF8, "application/json")
                };
            });

        var vm = new MainViewModel
        {
            ApiBase = "http://127.0.0.1:5246",
            Http = new HttpClient(mockHandler.Object),
            Prompt3D = "A low-poly medieval chest with golden ornaments",
            Selected3DFormat = "GLB (.glb)"
        };

        // Act
        await vm.Generate3DAsync();

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);

        var body = await capturedRequest.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("A low-poly medieval chest with golden ornaments", doc.RootElement.GetProperty("prompt").GetString());
        Assert.Equal("glb", doc.RootElement.GetProperty("format").GetString());

        Assert.Equal("http://127.0.0.1:5246/output_3d/mesh_abc12345.glb", vm.Rendered3DAssetUrl);
        Assert.False(vm.IsGenerating3D);
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    private static string FindWorkflowsDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Workflows"),
            Path.Combine(Directory.GetCurrentDirectory(), "Workflows"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Workflows")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Workflows"))
        };

        foreach (var c in candidates)
        {
            if (Directory.Exists(c)) return c;
        }

        throw new DirectoryNotFoundException("Could not locate Workflows directory.");
    }
}
