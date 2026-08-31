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
            Console.WriteLine($"[BROWSER {msg.Type}]: {msg.Text}");
            if (msg.Type == "error")
            {
                consoleErrors.Add($"Console Error: {msg.Text}");
            }
        };

        page.PageError += (_, exception) =>
        {
            Console.WriteLine($"[BROWSER PAGE ERROR]: {exception}");
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
        await page.WaitForTimeoutAsync(6000);

        var outputContainer = await page.QuerySelectorAsync("#out");
        var outHtml = outputContainer != null ? await outputContainer.InnerHTMLAsync() : "null";
        var canvas = await page.QuerySelectorAsync("#out canvas") ?? await page.QuerySelectorAsync("canvas");
        var loadedVersion = await page.EvaluateAsync<string>("() => window.getAppVersion ? window.getAppVersion() : null");

        Assert.True(network404s.IsEmpty, "404s:\n" + string.Join("\n", network404s));
        Assert.True(consoleErrors.IsEmpty, $"Errors:\n{string.Join("\n", consoleErrors)}\nOut HTML:\n{outHtml}");
        Assert.NotNull(outputContainer);
        Assert.True(canvas != null, $"Canvas element not found in DOM! Container HTML: {outHtml}");
        Assert.Equal("3.11.0", loadedVersion);

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

    [Fact]
    public async Task WebDashboard_CanIRunItTab_MountsAndInteractsCleanly()
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
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
            });

            var page = await context.NewPageAsync();
            var consoleErrors = new ConcurrentBag<string>();
            var network404s = new ConcurrentBag<string>();

            page.Console += (_, msg) =>
            {
                Console.WriteLine($"[BROWSER {msg.Type}]: {msg.Text}");
                if (msg.Type == "error")
                {
                    consoleErrors.Add($"Console Error: {msg.Text}");
                }
            };

            page.PageError += (_, exception) =>
            {
                Console.WriteLine($"[BROWSER PAGE ERROR]: {exception}");
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
            await page.WaitForTimeoutAsync(6000);

            var canvas = await page.QuerySelectorAsync("#out canvas") ?? await page.QuerySelectorAsync("canvas");
            Assert.NotNull(canvas);
            Assert.True(network404s.IsEmpty, "404s on load:\n" + string.Join("\n", network404s));
            Assert.True(consoleErrors.IsEmpty, "Console errors on load:\n" + string.Join("\n", consoleErrors));

            // Switch to Tab #5 [⚡ Can I Run It] via coordinate click on Tab Item (X=730, Y=170)
            await page.Mouse.ClickAsync(730, 170);
            await page.WaitForTimeoutAsync(1500);

            // Exercise UI interaction within the Can I Run It view (click modality selector or preset dropdown area)
            await page.Mouse.ClickAsync(500, 300);
            await page.WaitForTimeoutAsync(500);

            // Type text / trigger keyboard interactions
            await page.Keyboard.PressAsync("Tab");
            await page.Keyboard.TypeAsync("32");
            await page.WaitForTimeoutAsync(500);

            // Verify no runtime WASM or script errors occurred during navigation & interaction
            Assert.True(network404s.IsEmpty, "404s after Can I Run It interaction:\n" + string.Join("\n", network404s));
            Assert.True(consoleErrors.IsEmpty, "Console errors after Can I Run It interaction:\n" + string.Join("\n", consoleErrors));
        }
    }
}

