using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class PlaywrightWasmE2ETests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public PlaywrightWasmE2ETests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task WebDashboard_BootsCleanlyWithoutConsoleOr404Errors()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--use-gl=angle", "--use-angle=swiftshader", "--enable-webgl", "--ignore-gpu-blocklist", "--no-sandbox" }
        });
        var page = await browser.NewPageAsync();

        var consoleErrors = new ConcurrentBag<string>();
        var network404s = new ConcurrentBag<string>();

        page.Console += (_, msg) =>
        {
            if (msg.Type == "error" 
                && !msg.Text.Contains("ERR_CONNECTION_REFUSED")
                && !msg.Text.Contains("ERR_FAILED")
                && !msg.Text.Contains("Failed to load resource")
                && !msg.Text.Contains("Access to fetch"))
            {
                consoleErrors.Add($"Console Error: {msg.Text}");
            }
        };

        page.PageError += (_, exception) =>
        {
            consoleErrors.Add($"Page Error: {exception}");
        };

        page.Response += (_, response) =>
        {
            if (response.Status == 404 && response.Url.Contains("/_framework/"))
            {
                network404s.Add(response.Url);
            }
        };

        await page.GotoAsync(AppTestServerFixture.TestBaseUrl);
        await page.WaitForTimeoutAsync(5000);

        var outputContainer = await page.QuerySelectorAsync("#out");

        Assert.Empty(network404s);
        Assert.Empty(consoleErrors);
        Assert.NotNull(outputContainer);
    }
}
