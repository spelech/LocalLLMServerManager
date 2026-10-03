using System;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class LiveExternalProviderIntegrationTests
{
    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(2) };
    private const string LocalServerUrl = "http://127.0.0.1:5246";
    private const string OllamaUrl = "http://127.0.0.1:11434";
    private const string ForgeUrl = "http://127.0.0.1:7860";
    private const string KokoroUrl = "http://127.0.0.1:8880";
    private const string ComfyUiUrl = "http://127.0.0.1:8188";

    private static bool IsLiveTestingEnabled() =>
        Environment.GetEnvironmentVariable("LIVE_EXTERNAL_TESTS") == "1";

    private static async Task<bool> IsUrlReachableAsync(string url)
    {
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            var resp = await _client.GetAsync(url, cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task Live_ServerHealth_ReturnsHealthyStatus()
    {
        if (!IsLiveTestingEnabled() || !await IsUrlReachableAsync($"{LocalServerUrl}/health"))
        {
            Assert.Skip("Live server is not running or LIVE_EXTERNAL_TESTS is not enabled.");
        }

        var response = await _client.GetAsync($"{LocalServerUrl}/health");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(content);

        Assert.NotNull(doc);
        Assert.Equal("Healthy", doc?["status"]?.ToString());
        Assert.NotNull(doc?["version"]?.ToString());
    }

    [Fact]
    public async Task Live_GpuVramEndpoint_ReturnsHardwareMetrics()
    {
        if (!IsLiveTestingEnabled() || !await IsUrlReachableAsync($"{LocalServerUrl}/health"))
        {
            Assert.Skip("Live server is not running or LIVE_EXTERNAL_TESTS is not enabled.");
        }

        var response = await _client.GetAsync($"{LocalServerUrl}/api/gpu/vram");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(content);

        Assert.NotNull(doc);
        Assert.NotNull(doc?["gpuName"]?.ToString());
        Assert.True(doc?["vramBytes"]?.GetValue<long>() > 0);
    }

    [Fact]
    public async Task Live_McpEndpoint_ReturnsValidResponse()
    {
        if (!IsLiveTestingEnabled() || !await IsUrlReachableAsync($"{LocalServerUrl}/health"))
        {
            Assert.Skip("Live server is not running or LIVE_EXTERNAL_TESTS is not enabled.");
        }

        var postContent = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}", System.Text.Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"{LocalServerUrl}/mcp", postContent);
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Live_HuggingFaceHub_SearchReturnsGgufRepositories()
    {
        if (!IsLiveTestingEnabled())
        {
            Assert.Skip("External provider integration tests are skipped unless LIVE_EXTERNAL_TESTS=1 is set.");
        }

        string url = "https://huggingface.co/api/models?search=llama-3.3&filter=gguf&limit=5";
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var arr = JsonNode.Parse(content)?.AsArray();

        Assert.NotNull(arr);
        Assert.True(arr.Count > 0);
        Assert.NotNull(arr[0]?["id"]?.ToString());
    }

    [Fact]
    public async Task Live_CivitaiApi_SearchReturnsModelCheckpoints()
    {
        if (!IsLiveTestingEnabled())
        {
            Assert.Skip("External provider integration tests are skipped unless LIVE_EXTERNAL_TESTS=1 is set.");
        }

        string url = "https://civitai.com/api/v1/models?query=cyberpunk&types=Checkpoint&limit=5";
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(content);

        Assert.NotNull(doc);
        var items = doc?["items"]?.AsArray();
        Assert.NotNull(items);
        Assert.True(items.Count > 0);
        Assert.NotNull(items[0]?["name"]?.ToString());
    }

    [Fact]
    public async Task Live_OllamaTags_ReturnsInstalledModels()
    {
        if (!IsLiveTestingEnabled() || !await IsUrlReachableAsync($"{OllamaUrl}/api/version"))
        {
            Assert.Skip("Ollama daemon is not running or LIVE_EXTERNAL_TESTS is not enabled.");
        }

        var response = await _client.GetAsync($"{OllamaUrl}/api/tags");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(content);

        Assert.NotNull(doc);
        Assert.NotNull(doc?["models"]);
    }

    [Fact]
    public async Task Live_OllamaPs_ReturnsRunningProcessList()
    {
        if (!IsLiveTestingEnabled() || !await IsUrlReachableAsync($"{OllamaUrl}/api/version"))
        {
            Assert.Skip("Ollama daemon is not running or LIVE_EXTERNAL_TESTS is not enabled.");
        }

        var response = await _client.GetAsync($"{OllamaUrl}/api/ps");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(content);

        Assert.NotNull(doc);
        Assert.NotNull(doc?["models"]);
    }

    [Fact]
    public async Task Live_OllamaVersion_ReturnsDaemonVersion()
    {
        if (!IsLiveTestingEnabled() || !await IsUrlReachableAsync($"{OllamaUrl}/api/version"))
        {
            Assert.Skip("Ollama daemon is not running or LIVE_EXTERNAL_TESTS is not enabled.");
        }

        var response = await _client.GetAsync($"{OllamaUrl}/api/version");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(content);

        Assert.NotNull(doc);
        Assert.NotNull(doc?["version"]?.ToString());
    }

    [Fact]
    public async Task Live_Ollama_GenerateText_ProducesTokensAsync()
    {
        if (!IsLiveTestingEnabled() || !await IsUrlReachableAsync($"{OllamaUrl}/api/version"))
        {
            Assert.Skip("Ollama daemon is not running or LIVE_EXTERNAL_TESTS is not enabled.");
        }

        // Fetch installed model list
        var tagsResp = await _client.GetAsync($"{OllamaUrl}/api/tags");
        tagsResp.EnsureSuccessStatusCode();
        var tagsDoc = JsonNode.Parse(await tagsResp.Content.ReadAsStringAsync());
        var firstModel = tagsDoc?["models"]?[0]?["name"]?.ToString();

        if (string.IsNullOrEmpty(firstModel))
        {
            Assert.Skip("No Ollama models installed locally to probe text generation.");
        }

        var generatePayload = new
        {
            model = firstModel,
            prompt = "Respond with one word: OK",
            stream = false
        };

        var postContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(generatePayload), System.Text.Encoding.UTF8, "application/json");
        var genResp = await _client.PostAsync($"{OllamaUrl}/api/generate", postContent);
        genResp.EnsureSuccessStatusCode();

        var genDoc = JsonNode.Parse(await genResp.Content.ReadAsStringAsync());
        Assert.NotNull(genDoc);
        var responseText = genDoc?["response"]?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(responseText), "Ollama should return non-empty text response.");
    }

    [Fact]
    public async Task Live_Forge_Txt2Img_GeneratesImageAsync()
    {
        if (!IsLiveTestingEnabled() || !await IsUrlReachableAsync($"{ForgeUrl}/sdapi/v1/sd-models"))
        {
            Assert.Skip("Stable Diffusion Forge is not running on port 7860 or LIVE_EXTERNAL_TESTS is not enabled.");
        }

        var payload = new
        {
            prompt = "a solid blue square",
            negative_prompt = "",
            steps = 1,
            width = 64,
            height = 64
        };

        var postContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
        var resp = await _client.PostAsync($"{ForgeUrl}/sdapi/v1/txt2img", postContent, cts.Token);
        resp.EnsureSuccessStatusCode();

        var doc = JsonNode.Parse(await resp.Content.ReadAsStringAsync(cts.Token));
        var images = doc?["images"]?.AsArray();
        Assert.NotNull(images);
        Assert.True(images.Count > 0, "Forge should return at least one generated image.");

        var b64 = images[0]?.ToString();
        Assert.False(string.IsNullOrEmpty(b64));
        var bytes = Convert.FromBase64String(b64!);
        Assert.True(bytes.Length > 8);
        // Verify PNG magic header: 0x89, 0x50, 0x4E, 0x47
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal(0x50, bytes[1]);
        Assert.Equal(0x4E, bytes[2]);
        Assert.Equal(0x47, bytes[3]);
    }

    [Fact]
    public async Task Live_Kokoro_SynthesizeSpeech_ProducesAudioStreamAsync()
    {
        if (!IsLiveTestingEnabled() || !await IsUrlReachableAsync($"{KokoroUrl}/api/tts"))
        {
            Assert.Skip("Kokoro TTS engine is not running on port 8880 or LIVE_EXTERNAL_TESTS is not enabled.");
        }

        var payload = new
        {
            text = "Testing local Kokoro speech engine.",
            voice = "af_heart",
            format = "wav"
        };

        var postContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
        var resp = await _client.PostAsync($"{KokoroUrl}/api/tts", postContent, cts.Token);
        resp.EnsureSuccessStatusCode();

        var audioBytes = await resp.Content.ReadAsByteArrayAsync(cts.Token);
        Assert.NotNull(audioBytes);
        Assert.True(audioBytes.Length > 12, "Kokoro audio stream should return valid audio data.");
        // Verify RIFF / WAVE header if WAV format
        Assert.Equal((byte)'R', audioBytes[0]);
        Assert.Equal((byte)'I', audioBytes[1]);
        Assert.Equal((byte)'F', audioBytes[2]);
        Assert.Equal((byte)'F', audioBytes[3]);
    }

    [Fact]
    public async Task Live_ComfyUi_SystemStatsAndQueue_OperationalAsync()
    {
        if (!IsLiveTestingEnabled() || !await IsUrlReachableAsync($"{ComfyUiUrl}/system_stats"))
        {
            Assert.Skip("ComfyUI engine is not running on port 8188 or LIVE_EXTERNAL_TESTS is not enabled.");
        }

        var statsResp = await _client.GetAsync($"{ComfyUiUrl}/system_stats");
        statsResp.EnsureSuccessStatusCode();
        var statsDoc = JsonNode.Parse(await statsResp.Content.ReadAsStringAsync());
        Assert.NotNull(statsDoc);
        Assert.NotNull(statsDoc?["devices"]);

        var queueResp = await _client.GetAsync($"{ComfyUiUrl}/queue");
        queueResp.EnsureSuccessStatusCode();
        var queueDoc = JsonNode.Parse(await queueResp.Content.ReadAsStringAsync());
        Assert.NotNull(queueDoc);
        Assert.NotNull(queueDoc?["queue_running"]);
        Assert.NotNull(queueDoc?["queue_pending"]);
    }
}
