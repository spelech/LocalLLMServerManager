# Documentation & Playwright Real Screenshot Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement an automated Playwright screenshot generator test in `LocalLLMServerManager.Tests` to capture real 1280x800 PNG browser screenshots of the Avalonia WebAssembly UI, and update all repository documentation (`README.md`, `docs/ARCHITECTURE.md`, `docs/DEVELOPMENT_GUIDE.md`, `docs/USER_GUIDE.md`) to `v3.4.0`.

**Architecture:** Create `PlaywrightScreenshotGenerator.cs` using `AppTestServerFixture` to boot Kestrel in-memory, launch headless Chromium, navigate across UI tabs, wait for WASM rendering, and write PNG screenshots to `docs/images/`. Update `README.md` and `docs/` files with real embedded images, Docker setup, and Playwright E2E details.

**Tech Stack:** C# .NET 10.0, Microsoft.Playwright 1.50.0, xUnit v3, Markdown, Mermaid.js.

## Global Constraints
- Target Framework: `net10.0`
- Screenshot Viewport: 1280x800 (headless Chromium)
- Image Format: PNG, stored in `docs/images/`
- Documentation Version: v3.4.0

---

### Task 1: Create Playwright Automated Screenshot Generator

**Files:**
- Create: `LocalLLMServerManager.Tests/PlaywrightScreenshotGenerator.cs`

**Interfaces:**
- Consumes: `AppTestServerFixture`
- Produces: PNG screenshots in `docs/images/`:
  - `docs/images/dashboard_desktop.png`
  - `docs/images/dashboard_ollama.png`
  - `docs/images/dashboard_huggingface.png`
  - `docs/images/dashboard_civitai.png`
  - `docs/images/dashboard_3d_studio.png`
  - `docs/images/dashboard_settings.png`

- [ ] **Step 1: Create PlaywrightScreenshotGenerator test class**

Create `LocalLLMServerManager.Tests/PlaywrightScreenshotGenerator.cs`:
```csharp
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
        var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "docs", "images");
        Directory.CreateDirectory(outputDir);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--use-gl=angle", "--use-angle=swiftshader", "--enable-webgl" }
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        });

        var page = await context.NewPageAsync();
        await page.GotoAsync(AppTestServerFixture.TestBaseUrl);
        await page.WaitForTimeoutAsync(5000);

        // Screenshot 1: Overview Dashboard
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(outputDir, "dashboard_desktop.png"),
            FullPage = false
        });

        Assert.True(File.Exists(Path.Combine(outputDir, "dashboard_desktop.png")), "dashboard_desktop.png should exist");
    }
}
```

- [ ] **Step 2: Execute screenshot generator test**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" -c Release`
Expected: PASS and `docs/images/dashboard_desktop.png` generated.

- [ ] **Step 3: Commit Task 1**

```bash
git add LocalLLMServerManager.Tests/PlaywrightScreenshotGenerator.cs docs/images/
git commit -m "test(e2e): add Playwright automated screenshot generator for documentation"
```

---

### Task 2: Modernize README.md & Embed Real Screenshots

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: PNG screenshots from `docs/images/`
- Produces: Updated `README.md` at version `v3.4.0`.

- [ ] **Step 1: Update README.md version headers, feature list, and screenshots**

Update `README.md`:
- Bump version badge and title to `v3.4.0`.
- Embed real screenshots for Overview, Models, Hugging Face, CivitAI, 3D Studio.
- Add Docker & Docker Compose setup section (`Dockerfile`, `docker-compose.yml`).
- Add Playwright E2E browser testing section.
- Add WASM static asset MIME mappings section.

- [ ] **Step 2: Commit Task 2**

```bash
git add README.md
git commit -m "docs: update README.md to v3.4.0 with real Playwright screenshots and Docker/WASM details"
```

---

### Task 3: Update Architecture, Development, and User Guides

**Files:**
- Modify: `docs/ARCHITECTURE.md`
- Modify: `docs/DEVELOPMENT_GUIDE.md`
- Modify: `docs/USER_GUIDE.md`

- [ ] **Step 1: Update docs/ARCHITECTURE.md**

Update `docs/ARCHITECTURE.md` Mermaid diagrams to include Playwright E2E test layer, WebAssembly AppBundle static asset pipeline, and Docker multi-stage container build.

- [ ] **Step 2: Update docs/DEVELOPMENT_GUIDE.md and docs/USER_GUIDE.md**

Update `docs/DEVELOPMENT_GUIDE.md` with Playwright browser setup (`pwsh LocalLLMServerManager.Tests/bin/Release/net10.0/playwright.ps1 install chromium`) and test commands. Update `docs/USER_GUIDE.md` with embedded screenshots.

- [ ] **Step 3: Commit Task 3**

```bash
git add docs/ARCHITECTURE.md docs/DEVELOPMENT_GUIDE.md docs/USER_GUIDE.md
git commit -m "docs: update architecture, development, and user guides for v3.4.0 release"
```
