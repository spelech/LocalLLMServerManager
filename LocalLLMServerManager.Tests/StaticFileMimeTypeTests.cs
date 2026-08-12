using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StaticFileMimeTypeTests : IClassFixture<AppTestServerFixture>
{
    private readonly HttpClient _client;

    public StaticFileMimeTypeTests(AppTestServerFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task IcudtEFIGSDat_Returns200_WithOctetStreamContentType()
    {
        var response = await _client.GetAsync("/_framework/icudt_EFIGS.dat");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType.MediaType);
    }

    [Fact]
    public async Task DotnetNativeJsSymbols_Returns200_WithOctetStreamContentType()
    {
        var response = await _client.GetAsync("/_framework/dotnet.native.js.symbols");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType.MediaType);
    }
}
