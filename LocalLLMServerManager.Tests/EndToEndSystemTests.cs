using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class EndToEndSystemTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public EndToEndSystemTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void WasmStaticAssets_IndexHtml_PointsToAvaloniaJs()
    {
        string indexHtmlPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
        Assert.True(File.Exists(indexHtmlPath), "wwwroot/index.html should exist");

        string content = File.ReadAllText(indexHtmlPath);
        Assert.Contains("avalonia.js", content);
        Assert.DoesNotContain("main.js", content);
    }

    [Fact]
    public async Task OllamaModelsEndpoint_ReturnsJsonPayload()
    {
        var client = _fixture.CreateClient();
        var response = await client.GetAsync("/api/models");

        Assert.True(response.IsSuccessStatusCode, "GET /api/models should return 200 OK");
        string json = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task MainViewModel_LoadInstalledModels_PopulatesCollection()
    {
        var client = _fixture.CreateClient();
        var vm = new MainViewModel(client)
        {
            ApiBase = AppTestServerFixture.TestBaseUrl
        };

        await vm.LoadInstalledModelsAsync();
        Assert.NotNull(vm.InstalledModels);
    }
}
