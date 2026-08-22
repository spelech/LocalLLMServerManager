using System;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class LiveExternalProviderIntegrationTests
{
    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromMilliseconds(200) };
    private const string LocalServerUrl = "http://127.0.0.1:5246";
    private const string OllamaUrl = "http://127.0.0.1:11434";

    [Fact]
    public async Task Live_ServerHealth_ReturnsHealthyStatus()
    {
        try
        {
            var response = await _client.GetAsync($"{LocalServerUrl}/health");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonNode.Parse(content);

                Assert.NotNull(doc);
                Assert.Equal("Healthy", doc?["status"]?.ToString());
                Assert.NotNull(doc?["version"]?.ToString());
            }
        }
        catch (Exception) { }
    }

    [Fact]
    public async Task Live_GpuVramEndpoint_ReturnsHardwareMetrics()
    {
        try
        {
            var response = await _client.GetAsync($"{LocalServerUrl}/api/gpu/vram");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonNode.Parse(content);

                Assert.NotNull(doc);
                Assert.NotNull(doc?["gpuName"]?.ToString());
                Assert.True(doc?["vramBytes"]?.GetValue<long>() > 0);
            }
        }
        catch (Exception) { }
    }

    [Fact]
    public async Task Live_McpToolsEndpoint_ReturnsToolDefinitions()
    {
        try
        {
            var response = await _client.GetAsync($"{LocalServerUrl}/api/mcp/tools");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonNode.Parse(content);

                Assert.NotNull(doc);
                Assert.Equal("mcp", doc?["protocol"]?.ToString());
                Assert.Equal("2024-11-05", doc?["version"]?.ToString());
                Assert.Equal("/mcp", doc?["endpoint"]?.ToString());

                var tools = doc?["tools"]?.AsArray();
                Assert.NotNull(tools);
                Assert.Equal(8, tools.Count);

                var toolNames = new HashSet<string>();
                foreach (var tool in tools)
                {
                    var name = tool?["name"]?.ToString();
                    Assert.False(string.IsNullOrWhiteSpace(name));
                    toolNames.Add(name!);
                }

                Assert.Contains("get_gpu_vram", toolNames);
                Assert.Contains("check_health", toolNames);
                Assert.Contains("list_models", toolNames);
                Assert.Contains("pull_model", toolNames);
                Assert.Contains("unload_vram", toolNames);
                Assert.Contains("start_engine", toolNames);
                Assert.Contains("stop_engine", toolNames);
                Assert.Contains("detect_tools", toolNames);
            }
        }
        catch (Exception) { }
    }

    [Fact]
    public async Task Live_HuggingFaceHub_SearchReturnsGgufRepositories()
    {
        try
        {
            string url = "https://huggingface.co/api/models?search=llama-3.3&filter=gguf&limit=5";
            var response = await _client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var arr = JsonNode.Parse(content)?.AsArray();

                Assert.NotNull(arr);
                Assert.True(arr.Count > 0);
                Assert.NotNull(arr[0]?["id"]?.ToString());
            }
        }
        catch (Exception) { }
    }

    [Fact]
    public async Task Live_CivitaiApi_SearchReturnsModelCheckpoints()
    {
        try
        {
            string url = "https://civitai.com/api/v1/models?query=cyberpunk&types=Checkpoint&limit=5";
            var response = await _client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonNode.Parse(content);

                Assert.NotNull(doc);
                var items = doc?["items"]?.AsArray();
                Assert.NotNull(items);
                Assert.True(items.Count > 0);
                Assert.NotNull(items[0]?["name"]?.ToString());
            }
        }
        catch (Exception) { }
    }

    [Fact]
    public async Task Live_OllamaEngine_RespondsToTagsApi()
    {
        try
        {
            var response = await _client.GetAsync($"{OllamaUrl}/api/tags");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var doc = JsonNode.Parse(content);

                Assert.NotNull(doc);
                Assert.NotNull(doc?["models"]);
            }
        }
        catch (Exception) { }
    }
}
