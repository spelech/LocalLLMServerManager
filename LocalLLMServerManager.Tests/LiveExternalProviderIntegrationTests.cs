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
}
