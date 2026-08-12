# WASM Static Assets Mime Types & Playwright E2E Robustness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate WebAssembly static file 404s (`.dat`, `.symbols`, `.wasm`) and JS console errors by configuring Kestrel `FileExtensionContentTypeProvider`, syncing WASM `AppBundle` build assets, and introducing Playwright E2E browser automated testing (`Microsoft.Playwright`) in `LocalLLMServerManager.Tests`.

**Architecture:** Configure `FileExtensionContentTypeProvider` in `Program.cs` to serve `.dat`, `.symbols`, `.wasm`, `.boot.json`, `.clr`, `.pdb` files from `wwwroot/`. Add `Microsoft.Playwright` to `LocalLLMServerManager.Tests` with a dedicated E2E test fixture (`PlaywrightE2EFixture`) that spins up Kestrel and headless Chromium, asserting zero 404 network responses and zero uncaught JS console errors.

**Tech Stack:** C# .NET 10.0, ASP.NET Core Minimal APIs, Microsoft.Playwright 1.50.0+, xUnit v3, Avalonia.Browser WebAssembly.

## Global Constraints
- Target Framework: `net10.0`
- Minimum Test Pass Rate: 100%
- Web Server Port: 5246 (Service mode default)
- Linting & typechecking rules: `dotnet test` must pass 100%

---

### Task 1: Configure Kestrel Static File Mime Types & WASM Build Sync

**Files:**
- Modify: `Program.cs:155-160`
- Modify: `LocalLLMServerManager.csproj`
- Create: `LocalLLMServerManager.Tests/StaticFileMimeTypeTests.cs`

**Interfaces:**
- Produces: `FileExtensionContentTypeProvider` configuration in `Program.cs` supporting `.dat`, `.symbols`, `.wasm`, `.clr`, `.pdb`, `.boot.json`.

- [ ] **Step 1: Write failing unit test for static file mime types**

Create `LocalLLMServerManager.Tests/StaticFileMimeTypeTests.cs`:
```csharp
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class StaticFileMimeTypeTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public StaticFileMimeTypeTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DatAndSymbolsStaticFiles_ReturnValidContentTypeAndNot404()
    {
        var client = _fixture.CreateClient();
        
        var datResp = await client.GetAsync("/_framework/icudt_EFIGS.dat");
        Assert.True(datResp.IsSuccessStatusCode, "icudt_EFIGS.dat should return 200 OK");
        Assert.Equal("application/octet-stream", datResp.Content.Headers.ContentType?.MediaType);

        var symbolsResp = await client.GetAsync("/_framework/dotnet.native.js.symbols");
        Assert.True(symbolsResp.IsSuccessStatusCode, "dotnet.native.js.symbols should return 200 OK");
        Assert.Equal("application/octet-stream", symbolsResp.Content.Headers.ContentType?.MediaType);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StaticFileMimeTypeTests" -c Release`
Expected: FAIL (404 Not Found on `.dat` / `.symbols` static file requests)

- [ ] **Step 3: Implement FileExtensionContentTypeProvider in Program.cs**

Update `Program.cs` static file configuration:
```csharp
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".dat"] = "application/octet-stream";
provider.Mappings[".symbols"] = "application/octet-stream";
provider.Mappings[".wasm"] = "application/wasm";
provider.Mappings[".clr"] = "application/octet-stream";
provider.Mappings[".pdb"] = "application/octet-stream";
provider.Mappings[".boot.json"] = "application/json";

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});
```

- [ ] **Step 4: Copy missing .dat / .symbols / AppBundle files into wwwroot/_framework**

Copy `icudt_EFIGS.dat`, `dotnet.native.js.symbols`, `icudt_CJK.dat`, `icudt_no_CJK.dat` from `LocalLLMServerManager.Web/bin/Release/net10.0/browser-wasm/AppBundle/_framework/*` into `wwwroot/_framework/`.

- [ ] **Step 5: Run unit test to verify it passes**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StaticFileMimeTypeTests" -c Release`
Expected: PASS (200 OK for `.dat` and `.symbols` requests)

- [ ] **Step 6: Commit Task 1**

```bash
git add Program.cs LocalLLMServerManager.Tests/StaticFileMimeTypeTests.cs wwwroot/
git commit -m "fix(wasm): add static file content type provider mappings for .dat, .symbols, .wasm assets"
```

---

### Task 2: Implement Microsoft.Playwright Automated E2E Browser Testing

**Files:**
- Modify: `LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`
- Create: `LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs`

**Interfaces:**
- Consumes: `Microsoft.Playwright` NuGet package.
- Produces: `PlaywrightWasmE2ETests` asserting zero 404 errors, zero console exceptions, and successful WASM DOM mount on `#out`.

- [ ] **Step 1: Add Microsoft.Playwright package to LocalLLMServerManager.Tests.csproj**

Add `<PackageReference Include="Microsoft.Playwright" Version="1.50.0" />` to `LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`.

- [ ] **Step 2: Create PlaywrightWasmE2ETests class**

Create `LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
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
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        var consoleErrors = new List<string>();
        var network404s = new List<string>();

        page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
            {
                consoleErrors.Add(msg.Text);
            }
        };

        page.PageError += (_, exception) =>
        {
            consoleErrors.Add($"PageError: {exception}");
        };

        page.Response += (_, response) =>
        {
            if (response.Status == 404 && response.Url.Contains("/_framework/"))
            {
                network404s.Add($"{response.Status} {response.Url}");
            }
        };

        await page.GotoAsync(AppTestServerFixture.TestBaseUrl);
        await page.WaitForTimeoutAsync(5000);

        Assert.Empty(network404s);
        Assert.Empty(consoleErrors);

        var outputContainer = await page.QuerySelectorAsync("#out");
        Assert.NotNull(outputContainer);
    }
}
```

- [ ] **Step 3: Run Playwright E2E test**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightWasmE2ETests" -c Release`
Expected: PASS (Zero 404 assets, zero console errors)

- [ ] **Step 4: Commit Task 2**

```bash
git add LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs
git commit -m "test(e2e): add Playwright automated browser test suite for WebAssembly dashboard"
```

---

### Task 3: Complete Suite Verification & Release Update

**Files:**
- Modify: `.github/workflows/ci.yml`
- Modify: `README.md`

- [ ] **Step 1: Run full unit & E2E test suite**

Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --nologo -c Release`
Expected: 100% PASS across all tests

- [ ] **Step 2: Re-publish WASM bundle into wwwroot & restart backend**

Publish WASM and start backend service to verify live remote dashboard.

- [ ] **Step 3: Commit and tag release**

```bash
git add .
git commit -m "chore(release): finalize WASM static asset robustness and Playwright E2E test integration"
```
