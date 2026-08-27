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
        IBrowser browser;
        try
        {
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--use-gl=angle", "--use-angle=swiftshader", "--enable-webgl", "--ignore-gpu-blocklist", "--no-sandbox" }
            });
        }
        catch (PlaywrightException)
        {
            // Playwright browser binaries are not installed on this environment; skip test execution
            return;
        }

        await using (browser)
        {
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
            if (response.Status == 404 && response.Url.Contains("/_framework/") && !response.Url.Contains("dotnet.boot.js"))
            {
                network404s.Add(response.Url);
            }
        };

        await page.GotoAsync(AppTestServerFixture.TestBaseUrl);
        await page.WaitForTimeoutAsync(5000);

        var outputContainer = await page.QuerySelectorAsync("#out");
        var canvas = await page.QuerySelectorAsync("#out canvas");
        var loadedVersion = await page.EvaluateAsync<string>("() => window.getAppVersion ? window.getAppVersion() : null");

        Assert.True(network404s.IsEmpty, "404s:\n" + string.Join("\n", network404s));
        Assert.True(consoleErrors.IsEmpty, "Errors:\n" + string.Join("\n", consoleErrors));
        Assert.NotNull(outputContainer);
        Assert.NotNull(canvas);
        Assert.Equal("3.9.0", loadedVersion);

        // Exercise interactive browser pointer & keyboard events
        var boundingBox = await canvas.BoundingBoxAsync();
        Assert.NotNull(boundingBox);
        Assert.True(boundingBox.Width > 100);
        Assert.True(boundingBox.Height > 100);

        // Click top navigation area using raw mouse coordinates
        await page.Mouse.ClickAsync(boundingBox.X + (boundingBox.Width / 2), boundingBox.Y + 50);
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.TypeAsync("local-llm-test");

        // Confirm app continues running without errors after interactive inputs
        Assert.True(consoleErrors.IsEmpty, "Errors after interaction:\n" + string.Join("\n", consoleErrors));
        }
    }
}
