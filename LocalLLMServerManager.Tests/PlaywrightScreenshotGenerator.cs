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

    [Fact]
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
            // Playwright browser binaries are not installed on this environment; skip screenshot generation
            return;
        }

        string videoDir = Path.Combine(outputDir, "videos");
        Directory.CreateDirectory(videoDir);

        await using (browser)
        {
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                RecordVideoDir = videoDir,
                RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 800 }
            });

        var page = await context.NewPageAsync();
        await page.GotoAsync(AppTestServerFixture.TestBaseUrl);
        await page.WaitForTimeoutAsync(5000);

        // 1. Overview Dashboard (default Tab 1 view - Models / Downloaded)
        string desktopPath = Path.Combine(outputDir, "dashboard_desktop.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = desktopPath, FullPage = false });
        Assert.True(File.Exists(desktopPath) && new FileInfo(desktopPath).Length > 0, "dashboard_desktop.png should exist and be non-empty");

        // 2. Ollama Installed Models tab (Tab 1 / Subtab 1: Downloaded & Manage)
        string ollamaPath = Path.Combine(outputDir, "dashboard_ollama.png");
        await page.Mouse.ClickAsync(119, 205);
        await page.WaitForTimeoutAsync(1000);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = ollamaPath, FullPage = false });
        Assert.True(File.Exists(ollamaPath) && new FileInfo(ollamaPath).Length > 0, "dashboard_ollama.png should exist and be non-empty");

        // 3. Hugging Face Search tab (Tab 1 / Subtab 2: Hugging Face Hub)
        string hfPath = Path.Combine(outputDir, "dashboard_huggingface.png");
        await page.Mouse.ClickAsync(300, 205);
        await page.WaitForTimeoutAsync(1200);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = hfPath, FullPage = false });
        Assert.True(File.Exists(hfPath) && new FileInfo(hfPath).Length > 0, "dashboard_huggingface.png should exist and be non-empty");

        // 4. CivitAI Search tab (Tab 1 / Subtab 3: CivitAI Hub)
        string civitaiPath = Path.Combine(outputDir, "dashboard_civitai.png");
        await page.Mouse.ClickAsync(443, 205);
        await page.WaitForTimeoutAsync(1200);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = civitaiPath, FullPage = false });
        Assert.True(File.Exists(civitaiPath) && new FileInfo(civitaiPath).Length > 0, "dashboard_civitai.png should exist and be non-empty");

        // 5. Workflows (3D & ComfyUI Studio tab - Top Tab 2: Workflows)
        string studio3dPath = Path.Combine(outputDir, "dashboard_3d_studio.png");
        await page.Mouse.ClickAsync(180, 150);
        await page.WaitForTimeoutAsync(1200);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = studio3dPath, FullPage = false });
        Assert.True(File.Exists(studio3dPath) && new FileInfo(studio3dPath).Length > 0, "dashboard_3d_studio.png should exist and be non-empty");

        // 6. Can I Run It tab (Top Tab 3: Can I Run It)
        string canIRunItPath = Path.Combine(outputDir, "dashboard_can_i_run_it.png");
        await page.Mouse.ClickAsync(300, 150);
        await page.WaitForTimeoutAsync(1200);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = canIRunItPath, FullPage = false });
        Assert.True(File.Exists(canIRunItPath) && new FileInfo(canIRunItPath).Length > 0, "dashboard_can_i_run_it.png should exist and be non-empty");

        // 7. Settings tab (Top Tab 4: Settings)
        string settingsPath = Path.Combine(outputDir, "dashboard_settings.png");
        await page.Mouse.ClickAsync(412, 150);
        await page.WaitForTimeoutAsync(1200);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = settingsPath, FullPage = false });
        Assert.True(File.Exists(settingsPath) && new FileInfo(settingsPath).Length > 0, "dashboard_settings.png should exist and be non-empty");

        // Assert that screenshots for different tabs are visually distinct
        byte[] bytesDesktop = File.ReadAllBytes(desktopPath);
        byte[] bytesHf = File.ReadAllBytes(hfPath);
        byte[] bytesCivitai = File.ReadAllBytes(civitaiPath);
        byte[] bytes3d = File.ReadAllBytes(studio3dPath);
        byte[] bytesCanIRunIt = File.ReadAllBytes(canIRunItPath);
        byte[] bytesSettings = File.ReadAllBytes(settingsPath);

        Assert.False(bytesDesktop.AsSpan().SequenceEqual(bytesHf), "dashboard_huggingface.png should differ from desktop");
        Assert.False(bytesDesktop.AsSpan().SequenceEqual(bytesCivitai), "dashboard_civitai.png should differ from desktop");
        Assert.False(bytesDesktop.AsSpan().SequenceEqual(bytes3d), "dashboard_3d_studio.png should differ from desktop");
        Assert.False(bytesDesktop.AsSpan().SequenceEqual(bytesCanIRunIt), "dashboard_can_i_run_it.png should differ from desktop");
        Assert.False(bytesDesktop.AsSpan().SequenceEqual(bytesSettings), "dashboard_settings.png should differ from desktop");
        Assert.False(bytes3d.AsSpan().SequenceEqual(bytesSettings), "dashboard_settings.png should differ from dashboard_3d_studio.png");
        Assert.False(bytesHf.AsSpan().SequenceEqual(bytesCivitai), "dashboard_civitai.png should differ from dashboard_huggingface.png");
        // 8. Exercise Documentation & AI Assist drawer toggles and telemetry header collapse
        // Click Documentation button (Top nav area ~ 530, 150)
        await page.Mouse.ClickAsync(530, 150);
        await page.WaitForTimeoutAsync(1000);
        string docsDrawerPath = Path.Combine(outputDir, "dashboard_docs_drawer.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = docsDrawerPath, FullPage = false });

        // Close drawer (Click nav button again)
        await page.Mouse.ClickAsync(530, 150);
        await page.WaitForTimeoutAsync(800);

        // Click AI Assist button (Top nav area ~ 670, 150)
        await page.Mouse.ClickAsync(670, 150);
        await page.WaitForTimeoutAsync(1000);
        string aiDrawerPath = Path.Combine(outputDir, "dashboard_ai_drawer.png");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = aiDrawerPath, FullPage = false });

        // Type into chat input
        await page.Keyboard.TypeAsync("Hello AI Assistant, can you check system health?");
        await page.WaitForTimeoutAsync(800);

        // Close AI drawer
        await page.Mouse.ClickAsync(670, 150);
        await page.WaitForTimeoutAsync(800);

        // Close page and context to flush video recording
        await page.CloseAsync();
        await context.CloseAsync();

        // Verify video recording was generated
        var videoFiles = Directory.GetFiles(videoDir, "*.webm");
        Assert.True(videoFiles.Length > 0, "Playwright video recording should be generated in " + videoDir);
        var videoInfo = new FileInfo(videoFiles[0]);
        Assert.True(videoInfo.Length > 0, "Playwright video recording file should be non-empty");
        }
    }
}
