using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AppTestServerFixture : IAsyncLifetime
{
    public static string TestBaseUrl = "http://127.0.0.1:5299";
    private WebApplication? _app;

    public async ValueTask InitializeAsync()
    {
        for (int port = 5299; port <= 5310; port++)
        {
            try
            {
                TestBaseUrl = $"http://127.0.0.1:{port}";
                _app = Program.CreateWebApplication(Array.Empty<string>(), isServiceMode: false, url: TestBaseUrl);
                await _app.StartAsync();
                return;
            }
            catch (System.IO.IOException)
            {
                if (_app != null) { try { await _app.DisposeAsync(); } catch { } }
                if (port == 5310) throw;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    public HttpClient CreateClient() => new HttpClient { BaseAddress = new Uri(TestBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
    public HttpClient Client => CreateClient();
}
