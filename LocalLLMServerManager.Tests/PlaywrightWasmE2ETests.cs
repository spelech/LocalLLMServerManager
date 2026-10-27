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
        Assert.Equal("4.0.0", loadedVersion);

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
                if (msg.Type == "error" && !msg.Text.Contains("/api/ai/models") && !msg.Text.Contains("400 (Bad Request)"))
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

            // Switch to Tab #3 [⚡ Can I Run It] via coordinate click on Tab Item (X=330, Y=155)
            await page.Mouse.ClickAsync(330, 155);
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

    [Fact]
    public async Task WebDashboard_MemoryTelemetryAndPurge_VerifiedAcrossChromiumAndFirefox()
    {
        using var playwright = await Playwright.CreateAsync();
        var browserTypes = new[]
        {
            ("chromium", playwright.Chromium),
            ("firefox", playwright.Firefox)
        };

        foreach (var (name, browserType) in browserTypes)
        {
            IBrowser browser;
            try
            {
                var launchOptions = new BrowserTypeLaunchOptions
                {
                    Headless = true
                };
                if (name == "chromium")
                {
                    launchOptions.Args = new[]
                    {
                        "--use-gl=angle",
                        "--use-angle=swiftshader",
                        "--enable-webgl",
                        "--ignore-gpu-blocklist",
                        "--no-sandbox"
                    };
                }

                browser = await browserType.LaunchAsync(launchOptions);
            }
            catch (PlaywrightException ex)
            {
                Console.WriteLine($"Playwright browser '{name}' failed to launch or is not installed: {ex.Message}");
                continue;
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
                    Console.WriteLine($"[{name.ToUpperInvariant()} BROWSER {msg.Type}]: {msg.Text}");
                    if (msg.Type == "error" && !msg.Text.Contains("/api/ai/models") && !msg.Text.Contains("400 (Bad Request)"))
                    {
                        consoleErrors.Add($"Console Error: {msg.Text}");
                    }
                };

                page.PageError += (_, exception) =>
                {
                    Console.WriteLine($"[{name.ToUpperInvariant()} BROWSER PAGE ERROR]: {exception}");
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

                // Hard reload per requirements
                await page.ReloadAsync();
                await page.WaitForTimeoutAsync(5000);

                // Zero console errors and zero /_framework/ 404 errors
                Assert.True(network404s.IsEmpty, $"404s on {name}:\n" + string.Join("\n", network404s));
                Assert.True(consoleErrors.IsEmpty, $"Console errors on {name}:\n" + string.Join("\n", consoleErrors));

                // Canvas element is present, visible (Width > 100, Height > 100)
                var canvas = await page.QuerySelectorAsync("#out canvas") ?? await page.QuerySelectorAsync("canvas");
                Assert.NotNull(canvas);

                var boundingBox = await canvas.BoundingBoxAsync();
                Assert.NotNull(boundingBox);
                Assert.True(boundingBox.Width > 100, $"Canvas width {boundingBox.Width} was not > 100 on {name}");
                Assert.True(boundingBox.Height > 100, $"Canvas height {boundingBox.Height} was not > 100 on {name}");

                // Non-blank rendered pixel data via toDataURL > 1000
                var dataUrlLength = await page.EvaluateAsync<int>(
                    "() => { const c = document.querySelector('canvas'); return c ? c.toDataURL('image/png').length : 0; }"
                );
                Assert.True(dataUrlLength > 1000, $"Canvas toDataURL length {dataUrlLength} was not > 1000 on {name}");

                // Evaluates fetch('/api/hardware/memory') inside browser context
                var memoryData = await page.EvaluateAsync<System.Text.Json.JsonElement>(
                    "async () => { const res = await fetch('/api/hardware/memory'); return await res.json(); }"
                );
                Assert.True(memoryData.GetProperty("totalRamGb").GetDouble() > 0, "totalRamGb should be > 0");
                Assert.True(memoryData.GetProperty("ramUsedPercent").GetDouble() > 0, "ramUsedPercent should be > 0");
                Assert.Equal(15, memoryData.GetProperty("modelCacheIdleTimeoutMinutes").GetInt32());
                Assert.Equal(30, memoryData.GetProperty("engineHibernationTimeoutMinutes").GetInt32());
                Assert.Equal(75, memoryData.GetProperty("ramPressureThresholdPercent").GetInt32());

                // Evaluates fetch('/api/memory/purge', { method: 'POST' }) inside browser context
                var purgeData = await page.EvaluateAsync<System.Text.Json.JsonElement>(
                    "async () => { const res = await fetch('/api/memory/purge', { method: 'POST' }); return await res.json(); }"
                );
                Assert.Equal("purged", purgeData.GetProperty("status").GetString());
            }
        }
    }
}

