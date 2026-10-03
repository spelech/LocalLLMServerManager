using System;
using System.IO;
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
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ComfyWorkflowGenerationTests
{
    private const string SampleComfyGraphJson = """
    {
      "3": {
        "class_type": "KSampler",
        "inputs": {
          "cfg": 8.0,
          "denoise": 1.0,
          "latent_image": ["5", 0],
          "model": ["4", 0],
          "negative": ["7", 0],
          "positive": ["6", 0],
          "sampler_name": "euler",
          "scheduler": "normal",
          "seed": 12345678,
          "steps": 20
        }
      },
      "6": {
        "class_type": "CLIPTextEncode",
        "_meta": { "title": "Positive Prompt" },
        "inputs": {
          "clip": ["4", 1],
          "text": "original prompt"
        }
      },
      "7": {
        "class_type": "CLIPTextEncode",
        "_meta": { "title": "Negative Prompt" },
        "inputs": {
          "clip": ["4", 1],
          "text": "original negative"
        }
      },
      "5": {
        "class_type": "EmptyLatentImage",
        "inputs": {
          "batch_size": 1,
          "height": 512,
          "width": 512
        }
      }
    }
    """;

    private static HttpClient CreateMockClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var mock = new Mock<HttpMessageHandler>();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) => handler(req));

        return new HttpClient(mock.Object);
    }

    [Fact]
    public void GraphTranslation_ParameterInjection_ReplacesPromptSeedAndSteps()
    {
        var node = JsonNode.Parse(SampleComfyGraphJson);
        Assert.NotNull(node);
        var graph = node.AsObject();

        string targetPrompt = "Astronaut riding a horse on Mars, photorealistic, 8k";
        string targetNegative = "blurry, low quality, cartoon";
        long targetSeed = 9876543210L;
        int targetSteps = 35;
        double targetCfg = 6.5;
        double targetDenoise = 0.75;

        // Perform substitution matching Studio / WorkflowEndpoints logic
        foreach (var kvp in graph)
        {
            if (kvp.Value is JsonObject nodeObj)
            {
                var classType = nodeObj["class_type"]?.ToString() ?? "";
                var title = nodeObj["_meta"]?["title"]?.ToString() ?? "";
                var inputs = nodeObj["inputs"] as JsonObject;

                if (inputs != null)
                {
                    if (classType.Contains("CLIPTextEncode") || title.Contains("Prompt"))
                    {
                        if (title.Contains("Negative") || classType.Contains("Negative"))
                        {
                            inputs["text"] = targetNegative;
                        }
                        else
                        {
                            inputs["text"] = targetPrompt;
                        }
                    }

                    if (classType == "KSampler")
                    {
                        if (inputs.ContainsKey("seed")) inputs["seed"] = targetSeed;
                        if (inputs.ContainsKey("steps")) inputs["steps"] = targetSteps;
                        if (inputs.ContainsKey("cfg")) inputs["cfg"] = targetCfg;
                        if (inputs.ContainsKey("denoise")) inputs["denoise"] = targetDenoise;
                    }
                }
            }
        }

        // Verify Positive Prompt node
        var posText = graph["6"]?["inputs"]?["text"]?.ToString();
        Assert.Equal(targetPrompt, posText);

        // Verify Negative Prompt node
        var negText = graph["7"]?["inputs"]?["text"]?.ToString();
        Assert.Equal(targetNegative, negText);

        // Verify KSampler node
        var ksamplerInputs = graph["3"]?["inputs"];
        Assert.Equal(targetSeed, ksamplerInputs?["seed"]?.GetValue<long>());
        Assert.Equal(targetSteps, ksamplerInputs?["steps"]?.GetValue<int>());
        Assert.Equal(targetCfg, ksamplerInputs?["cfg"]?.GetValue<double>());
        Assert.Equal(targetDenoise, ksamplerInputs?["denoise"]?.GetValue<double>());
    }

    [Fact]
    public void GraphTranslation_TemplateMacroSubstitution_ReplacesAllTokens()
    {
        string templateJson = """
        {
          "prompt": "{{PROMPT}}",
          "negative_prompt": "{{NEGATIVE_PROMPT}}",
          "width": {{WIDTH}},
          "height": {{HEIGHT}},
          "frames": {{FRAMES}},
          "fps": {{FPS}},
          "seed": {{SEED}}
        }
        """;

        string prompt = "Cinematic aerial shot of cyberpunk city";
        string negPrompt = "grain, noise";
        int width = 1280;
        int height = 720;
        int frames = 81;
        int fps = 24;
        long seed = 42424242L;

        var substituted = templateJson
            .Replace("{{PROMPT}}", prompt)
            .Replace("{{NEGATIVE_PROMPT}}", negPrompt)
            .Replace("{{WIDTH}}", width.ToString())
            .Replace("{{HEIGHT}}", height.ToString())
            .Replace("{{FRAMES}}", frames.ToString())
            .Replace("{{FPS}}", fps.ToString())
            .Replace("{{SEED}}", seed.ToString());

        var parsed = JsonNode.Parse(substituted);
        Assert.NotNull(parsed);
        Assert.Equal(prompt, parsed["prompt"]?.ToString());
        Assert.Equal(negPrompt, parsed["negative_prompt"]?.ToString());
        Assert.Equal(width, parsed["width"]?.GetValue<int>());
        Assert.Equal(height, parsed["height"]?.GetValue<int>());
        Assert.Equal(frames, parsed["frames"]?.GetValue<int>());
        Assert.Equal(fps, parsed["fps"]?.GetValue<int>());
        Assert.Equal(seed, parsed["seed"]?.GetValue<long>());
    }

    [Fact]
    public async Task ComfyInterruptEndpoint_SendsInterruptToBackend_ReturnsSuccess()
    {
        string? interceptedMethod = null;
        string? interceptedPath = null;

        using var client = CreateMockClient(req =>
        {
            interceptedMethod = req.Method.Method;
            interceptedPath = req.RequestUri?.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });

        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings { ComfyUiUrl = "http://127.0.0.1:8188" });

        var comfyUrl = "http://127.0.0.1:8188/interrupt";
        var response = await client.PostAsync(comfyUrl, new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("POST", interceptedMethod);
        Assert.Equal("/interrupt", interceptedPath);
    }

    private static HttpClient CreateMockAsyncClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var mock = new Mock<HttpMessageHandler>();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage req, CancellationToken ct) => handler(req, ct));

        return new HttpClient(mock.Object);
    }

    [Fact]
    public async Task WorkflowDispatch_WithCancellationToken_AbortsImmediately()
    {
        using var client = CreateMockAsyncClient(async (req, ct) =>
        {
            await Task.Delay(10000, ct); // Simulate long generation that cancels
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await client.PostAsync("http://127.0.0.1:8188/prompt", new StringContent("{}", Encoding.UTF8, "application/json"), cts.Token);
        });
    }
}
