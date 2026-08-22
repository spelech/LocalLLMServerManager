using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class PlaywrightScreenshotGenerator : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public PlaywrightScreenshotGenerator(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Skip = "Run manually when generating doc screenshots")]
    public async Task GenerateRealDocScreenshots()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "LocalLLMServerManager.slnx")) && !File.Exists(Path.Combine(dir.FullName, "LocalLLMServerManager.sln")))
        {
            dir = dir.Parent;
        }
        string repoRoot = dir?.FullName ?? Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        string outputDir = Path.Combine(repoRoot, "docs", "images");
        Directory.CreateDirectory(outputDir);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--use-gl=angle", "--use-angle=swiftshader", "--enable-webgl", "--ignore-gpu-blocklist", "--no-sandbox" }
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        });

        var page = await context.NewPageAsync();
        await page.GotoAsync(AppTestServerFixture.TestBaseUrl);
        await page.WaitForTimeoutAsync(5000);

        // 1. Overview Dashboard (default Tab 1 view)
        string desktopPath = Path.Combine(outputDir, "dashboard_desktop.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = desktopPath, FullPage = false });
        Assert.True(File.Exists(desktopPath) && new FileInfo(desktopPath).Length > 0, "dashboard_desktop.png should exist and be non-empty");

        // 2. Ollama Installed Models tab (Tab 1)
        string ollamaPath = Path.Combine(outputDir, "dashboard_ollama.png");
        await page.Mouse.ClickAsync(100, 170);
        await page.WaitForTimeoutAsync(1000);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = ollamaPath, FullPage = false });
        Assert.True(File.Exists(ollamaPath) && new FileInfo(ollamaPath).Length > 0, "dashboard_ollama.png should exist and be non-empty");

        // 3. Hugging Face Search tab (Tab 2)
        string hfPath = Path.Combine(outputDir, "dashboard_huggingface.png");
        await page.Mouse.ClickAsync(270, 170);
        await page.WaitForTimeoutAsync(1000);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = hfPath, FullPage = false });
        Assert.True(File.Exists(hfPath) && new FileInfo(hfPath).Length > 0, "dashboard_huggingface.png should exist and be non-empty");

        // 4. CivitAI Search tab (Tab 3)
        string civitaiPath = Path.Combine(outputDir, "dashboard_civitai.png");
        await page.Mouse.ClickAsync(450, 170);
        await page.WaitForTimeoutAsync(1000);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = civitaiPath, FullPage = false });
        Assert.True(File.Exists(civitaiPath) && new FileInfo(civitaiPath).Length > 0, "dashboard_civitai.png should exist and be non-empty");

        // 5. 3D & ComfyUI Studio tab (Tab 4)
        string studio3dPath = Path.Combine(outputDir, "dashboard_3d_studio.png");
        await page.Mouse.ClickAsync(580, 170);
        await page.WaitForTimeoutAsync(1000);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = studio3dPath, FullPage = false });
        Assert.True(File.Exists(studio3dPath) && new FileInfo(studio3dPath).Length > 0, "dashboard_3d_studio.png should exist and be non-empty");

        // 6. Settings tab (Tab 5)
        string settingsPath = Path.Combine(outputDir, "dashboard_settings.png");
        await page.Mouse.ClickAsync(730, 170);
        await page.WaitForTimeoutAsync(1000);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = settingsPath, FullPage = false });
        Assert.True(File.Exists(settingsPath) && new FileInfo(settingsPath).Length > 0, "dashboard_settings.png should exist and be non-empty");

        // Assert that screenshots for different tabs are visually distinct
        byte[] bytesDesktop = File.ReadAllBytes(desktopPath);
        byte[] bytesHf = File.ReadAllBytes(hfPath);
        byte[] bytesCivitai = File.ReadAllBytes(civitaiPath);
        byte[] bytes3d = File.ReadAllBytes(studio3dPath);
        byte[] bytesSettings = File.ReadAllBytes(settingsPath);

        Assert.False(bytesDesktop.AsSpan().SequenceEqual(bytesHf), "dashboard_huggingface.png should differ from desktop");
        Assert.False(bytesDesktop.AsSpan().SequenceEqual(bytesCivitai), "dashboard_civitai.png should differ from desktop");
        Assert.False(bytesDesktop.AsSpan().SequenceEqual(bytes3d), "dashboard_3d_studio.png should differ from desktop");
        Assert.False(bytesDesktop.AsSpan().SequenceEqual(bytesSettings), "dashboard_settings.png should differ from desktop");
        Assert.False(bytes3d.AsSpan().SequenceEqual(bytesSettings), "dashboard_settings.png should differ from dashboard_3d_studio.png");
        Assert.False(bytesHf.AsSpan().SequenceEqual(bytesCivitai), "dashboard_civitai.png should differ from dashboard_huggingface.png");
    }
}
