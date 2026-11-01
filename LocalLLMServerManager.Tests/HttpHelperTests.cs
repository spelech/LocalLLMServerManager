using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class HttpHelperTests
{
    [Fact]
    public void FormatEndpoint_FormatsRelativeAndAbsoluteUrlsCorrectly()
    {
        Assert.Equal("/health", HttpHelper.FormatEndpoint("", "health"));
        Assert.Equal("/health", HttpHelper.FormatEndpoint("", "/health"));
        Assert.Equal("http://127.0.0.1:5246/health", HttpHelper.FormatEndpoint("http://127.0.0.1:5246", "health"));
        Assert.Equal("http://127.0.0.1:5246/health", HttpHelper.FormatEndpoint("http://127.0.0.1:5246/", "/health"));
    }

    [Fact]
    public void CreateClient_WithAbsoluteBase_ConfiguresBaseAddress()
    {
        using var client = HttpHelper.CreateClient("https://localllms.wileyriley.com");
        Assert.NotNull(client.BaseAddress);
        Assert.Equal("https://localllms.wileyriley.com/", client.BaseAddress.ToString());
    }

    [Fact]
    public async Task CreateClient_DisposingEarlierClients_DoesNotDisposeSharedHandler()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        string baseUrl = $"http://127.0.0.1:{port}";

        var ct = TestContext.Current.CancellationToken;
        var serverTask = Task.Run(async () =>
        {
            for (int i = 0; i < 2; i++)
            {
                using var tcpClient = await listener.AcceptTcpClientAsync(ct);
                await using var stream = tcpClient.GetStream();
                var buffer = new byte[1024];
                _ = await stream.ReadAsync(buffer, ct);
                var responseBytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK");
                await stream.WriteAsync(responseBytes, ct);
            }
        }, ct);

        using (var client1 = HttpHelper.CreateClient(baseUrl))
        {
            using var resp1 = await client1.GetAsync("/test1", ct);
            Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        }

        // Subsequent client created after client1 is disposed must still work without ObjectDisposedException
        using (var client2 = HttpHelper.CreateClient(baseUrl))
        {
            using var resp2 = await client2.GetAsync("/test2", ct);
            Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        }

        await serverTask;
        listener.Stop();
    }

    [Fact]
    public void MainViewModel_PropagatesApiBaseToAllSubViewModels()
    {
        var customHttp = new HttpClient { BaseAddress = new Uri("http://10.0.0.21:5246") };
        var vm = new MainViewModel(customHttp);

        Assert.Equal("http://10.0.0.21:5246", vm.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.Telemetry.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.Ollama.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.HuggingFace.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.Civitai.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.Settings.ApiBase);
        Assert.Equal("http://10.0.0.21:5246", vm.Audio.ApiBase);
    }
}
