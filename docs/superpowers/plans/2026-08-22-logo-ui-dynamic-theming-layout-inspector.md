# Logo, Matte UI Theme, Dynamic Theming & Playwright Layout Inspector Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Overhaul LocalLLMServerManager with the official monochromatic L³M² brand identity, clean matte carbon XAML UI design system, runtime dynamic theming engine, and integrated Playwright layout inspection tooling.

**Architecture:** 
1. Vector and multi-resolution raster asset pipeline generating `Assets/app-icon.ico`, `Assets/app-icon.png`, `Assets/logo.svg`, and WASM favicons.
2. Matte carbon/slate design system in `LocalLLMServerManager.Shared/Styles/` with dynamic resource mutation for runtime theme switching (`MatteCarbon`, `OledBlack`, `Light`) across Native Desktop and WASM.
3. Node/TypeScript test suite leveraging `github:spelech/playwright-layout-inspector` to audit DOM layout overflow, touch target ergonomics, and responsive stability.

**Tech Stack:** C# 13 / .NET 9, Avalonia UI 11.2, Avalonia WASM, Playwright, TypeScript, `playwright-layout-inspector`.

## Global Constraints

- Always run linting and typechecking after making code changes: `npm run lint` and `npx tsc --noEmit`.
- Ensure all C# tests pass via `dotnet test`.
- All brand marks and icons use the approved borderless monochromatic `L³M²` squircle design.

---

### Task 1: Generate and Install Monochromatic L³M² Brand Assets

**Files:**
- Create: `Assets/logo.svg`
- Modify: `Assets/app-icon.ico`
- Modify: `Assets/app-icon.png`
- Modify: `Assets/app_tray_icon.jpg`
- Modify: `LocalLLMServerManager.Web/wwwroot/favicon.ico`
- Modify: `LocalLLMServerManager.Web/wwwroot/index.html:10-18`

**Interfaces:**
- Produces: Official L³M² brand icon assets used by Desktop Window, System Tray, and WASM web dashboard.

- [ ] **Step 1: Create scalable SVG asset for L³M²**

```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" width="100%" height="100%">
  <rect width="128" height="128" rx="28" fill="#14171d"/>
  <text x="24" y="86" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif" font-weight="900" font-size="54" fill="#ffffff">L</text>
  <text x="52" y="56" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif" font-weight="800" font-size="25" fill="#ffffff">3</text>
  <text x="64" y="86" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif" font-weight="900" font-size="54" fill="#ffffff">M</text>
  <text x="104" y="56" font-family="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif" font-weight="800" font-size="25" fill="#ffffff">2</text>
</svg>
```

- [ ] **Step 2: Generate multi-resolution PNG and ICO assets**

Render 512x512, 256x256, 128x128, 64x64, 32x32, 24x24, 16x16 bitmaps from SVG and package into `Assets/app-icon.ico`, `Assets/app-icon.png`, `Assets/app_tray_icon.jpg`, and `LocalLLMServerManager.Web/wwwroot/favicon.ico`.

- [ ] **Step 3: Update WASM HTML header and title**

Update `LocalLLMServerManager.Web/wwwroot/index.html` to reference the new favicon and title "L³M² — Local LLM Server Manager".

- [ ] **Step 4: Verify asset loading with C# unit test**

Run: `dotnet test --filter "FullyQualifiedName~StaticFileMimeTypeTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Assets/ LocalLLMServerManager.Web/wwwroot/
git commit -m "feat(branding): install monochromatic L³M² icon and logo assets"
```

---

### Task 2: Overhaul Design Tokens & Matte UI Theme

**Files:**
- Modify: `LocalLLMServerManager.Shared/Styles/DesignTokens.axaml`
- Modify: `LocalLLMServerManager.Shared/Styles/GlassmorphicTheme.axaml` -> rename/refactor to `LocalLLMServerManager.Shared/Styles/MatteTheme.axaml`
- Modify: `App.axaml:9-16`
- Modify: `LocalLLMServerManager.Web/App.axaml:5-15`

**Interfaces:**
- Produces: Matte carbon/slate XAML resources (`BgDarkBrush`, `BgSurfaceBrush`, `BgCardBrush`, `BorderColorBrush`, `TextMainBrush`, `TextMutedBrush`, `PrimaryBrush`, `AccentBrush`) for Desktop and WASM.

- [ ] **Step 1: Update DesignTokens.axaml with Matte Carbon palette**

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Color Definitions -->
    <Color x:Key="BgDarkColor">#0d1117</Color>
    <Color x:Key="BgSurfaceColor">#161b22</Color>
    <Color x:Key="BgCardColor">#1c2128</Color>
    <Color x:Key="BorderColor">#30363d</Color>
    <Color x:Key="PrimaryColor">#388bfd</Color>
    <Color x:Key="SecondaryColor">#58a6ff</Color>
    <Color x:Key="AccentColor">#79c0ff</Color>
    <Color x:Key="TextMainColor">#f0f6fc</Color>
    <Color x:Key="TextMutedColor">#8b949e</Color>
    <Color x:Key="OnlineColor">#238636</Color>
    <Color x:Key="OfflineColor">#da3633</Color>
    <Color x:Key="WarningColor">#d29922</Color>

    <!-- Solid Brushes -->
    <SolidColorBrush x:Key="BgDarkBrush" Color="{StaticResource BgDarkColor}" />
    <SolidColorBrush x:Key="BgSurfaceBrush" Color="{StaticResource BgSurfaceColor}" />
    <SolidColorBrush x:Key="BgCardBrush" Color="{StaticResource BgCardColor}" />
    <SolidColorBrush x:Key="GlassBorderBrush" Color="{StaticResource BorderColor}" />
    <SolidColorBrush x:Key="BorderGlowBrush" Color="#388bfd" Opacity="0.4" />
    <SolidColorBrush x:Key="PrimaryBrush" Color="{StaticResource PrimaryColor}" />
    <SolidColorBrush x:Key="SecondaryBrush" Color="{StaticResource SecondaryColor}" />
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}" />
    <SolidColorBrush x:Key="TextMainBrush" Color="{StaticResource TextMainColor}" />
    <SolidColorBrush x:Key="TextMutedBrush" Color="{StaticResource TextMutedColor}" />
    <SolidColorBrush x:Key="OnlineBrush" Color="{StaticResource OnlineColor}" />
    <SolidColorBrush x:Key="OfflineBrush" Color="{StaticResource OfflineColor}" />
    <SolidColorBrush x:Key="WarningBrush" Color="{StaticResource WarningColor}" />

    <!-- Solid Base Background -->
    <SolidColorBrush x:Key="GlassBackgroundBrush" Color="{StaticResource BgDarkColor}" />
</ResourceDictionary>
```

- [ ] **Step 2: Update MatteTheme.axaml with flat modern controls**

Replace glowing button and card box shadows with flat matte borders (1px `#30363d`), clean rounded corners (6px-8px), and modern segmented tab bar styling.

- [ ] **Step 3: Update App.axaml references**

Ensure `App.axaml` and `LocalLLMServerManager.Web/App.axaml` reference `avares://LocalLLMServerManager.Shared/Styles/MatteTheme.axaml`.

- [ ] **Step 4: Build and test .NET compilation**

Run: `dotnet build`
Expected: 0 errors

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Styles/ App.axaml LocalLLMServerManager.Web/App.axaml
git commit -m "feat(ui): implement clean matte design system and remove neon glassmorphism"
```

---

### Task 3: Refactor XAML Views & Modernize Control Layouts

**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/OllamaModelsTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/CivitaiTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/EngineStudioTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`

**Interfaces:**
- Produces: Modernized XAML controls with compact horizontal telemetry chips, segmented pill tab headers, and clean model cards.

- [ ] **Step 1: Modernize TelemetryHeaderControl layout**

Refactor telemetry metrics into sleek, horizontal badge chips with compact progress indicators for CPU, RAM, and GPU VRAM.

- [ ] **Step 2: Refactor MainView tab bar and layout**

Update `MainView.axaml` with segmented tab styling, clear section margins, and clean status footer.

- [ ] **Step 3: Update model and engine tab controls**

Standardize card containers across Ollama, HuggingFace, CivitAI, and Engine Studio tabs to use `Border.glass-card` / matte cards with consistent padding and spacing.

- [ ] **Step 4: Run Avalonia UI tests**

Run: `dotnet test --filter "FullyQualifiedName~AvaloniaUiTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Views/
git commit -m "feat(ui): streamline telemetry header, tab navigation, and card layouts"
```

---

### Task 4: Implement Runtime Dynamic Theming Engine (`ThemeService`)

**Files:**
- Create: `LocalLLMServerManager.Shared/Services/IThemeService.cs`
- Create: `LocalLLMServerManager.Shared/Services/ThemeService.cs`
- Create: `LocalLLMServerManager.Tests/ThemeServiceTests.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`

**Interfaces:**
- Produces: `IThemeService` interface and `ThemeService` implementation supporting `MatteCarbon`, `OledBlack`, and `Light` modes at runtime.

- [ ] **Step 1: Write failing unit test for ThemeService**

```csharp
using Avalonia;
using Avalonia.Media;
using LocalLLMServerManager.Shared.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ThemeServiceTests
{
    [Fact]
    public void SetTheme_UpdatesResourceBrushesAndFiresEvent()
    {
        var app = Application.Current ?? new Application();
        var themeService = new ThemeService();
        bool eventFired = false;
        themeService.ThemeChanged += (_, theme) =>
        {
            if (theme == AppTheme.OledBlack) eventFired = true;
        };

        themeService.SetTheme(AppTheme.OledBlack);

        Assert.Equal(AppTheme.OledBlack, themeService.CurrentTheme);
        Assert.True(eventFired);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ThemeServiceTests"`
Expected: FAIL (types not found)

- [ ] **Step 3: Implement IThemeService and ThemeService**

```csharp
using System;
using Avalonia;
using Avalonia.Media;

namespace LocalLLMServerManager.Shared.Services;

public enum AppTheme
{
    MatteCarbon,
    OledBlack,
    Light
}

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    void SetTheme(AppTheme theme);
    event EventHandler<AppTheme>? ThemeChanged;
}

public class ThemeService : IThemeService
{
    public AppTheme CurrentTheme { get; private set; } = AppTheme.MatteCarbon;
    public event EventHandler<AppTheme>? ThemeChanged;

    public void SetTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        ApplyThemeTokens(theme);
        ThemeChanged?.Invoke(this, theme);
    }

    private void ApplyThemeTokens(AppTheme theme)
    {
        if (Application.Current == null) return;

        var (bgDark, bgSurface, bgCard, border, textMain, textMuted) = theme switch
        {
            AppTheme.OledBlack => ("#000000", "#121212", "#18181b", "#27272a", "#f4f4f5", "#71717a"),
            AppTheme.Light => ("#ffffff", "#f6f8fa", "#ffffff", "#d0d7de", "#1f2328", "#656d76"),
            _ => ("#0d1117", "#161b22", "#1c2128", "#30363d", "#f0f6fc", "#8b949e")
        };

        Application.Current.Resources["BgDarkBrush"] = new SolidColorBrush(Color.Parse(bgDark));
        Application.Current.Resources["BgSurfaceBrush"] = new SolidColorBrush(Color.Parse(bgSurface));
        Application.Current.Resources["BgCardBrush"] = new SolidColorBrush(Color.Parse(bgCard));
        Application.Current.Resources["GlassBorderBrush"] = new SolidColorBrush(Color.Parse(border));
        Application.Current.Resources["TextMainBrush"] = new SolidColorBrush(Color.Parse(textMain));
        Application.Current.Resources["TextMutedBrush"] = new SolidColorBrush(Color.Parse(textMuted));
        Application.Current.Resources["GlassBackgroundBrush"] = new SolidColorBrush(Color.Parse(bgDark));
    }
}
```

- [ ] **Step 4: Bind ThemeService in SettingsViewModel & SettingsTabControl**

Expose theme selection options (`Matte Carbon (Default)`, `OLED Pure Black`, `Clean Light`) in `SettingsViewModel` and bind a ComboBox in `SettingsTabControl.axaml`.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ThemeServiceTests"`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add LocalLLMServerManager.Shared/Services/ LocalLLMServerManager.Shared/ViewModels/ LocalLLMServerManager.Shared/Views/ LocalLLMServerManager.Tests/
git commit -m "feat(theming): implement live dynamic theme switching across Desktop and WASM"
```

---

### Task 5: Setup Node/TS Tooling, Playwright, and `spelech/playwright-layout-inspector`

**Files:**
- Create: `package.json`
- Create: `tsconfig.json`
- Create: `eslint.config.mjs`
- Create: `playwright.config.ts`
- Create: `tests/layout-inspector/layout-audit.spec.ts`

**Interfaces:**
- Consumes: WASM web dashboard served locally.
- Produces: Automated Playwright layout and design violation audit suite.

- [ ] **Step 1: Create package.json and install dependencies**

```json
{
  "name": "localllm-server-manager-tests",
  "version": "1.0.0",
  "private": true,
  "scripts": {
    "lint": "eslint . --ext .ts",
    "typecheck": "tsc --noEmit",
    "test:layout": "playwright test --config=playwright.config.ts"
  },
  "devDependencies": {
    "@eslint/js": "^9.0.0",
    "@playwright/test": "^1.50.0",
    "@types/node": "^22.0.0",
    "eslint": "^9.0.0",
    "playwright-layout-inspector": "github:spelech/playwright-layout-inspector",
    "typescript": "^5.7.0",
    "typescript-eslint": "^8.0.0"
  }
}
```

- [ ] **Step 2: Create tsconfig.json and eslint.config.mjs**

Configure strict TypeScript checking and ESLint rules.

- [ ] **Step 3: Create playwright.config.ts**

```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/layout-inspector',
  timeout: 30000,
  use: {
    baseURL: process.env.TEST_BASE_URL || 'http://localhost:5000',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'Desktop 1080p',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 } },
    },
    {
      name: 'Desktop 1440p',
      use: { ...devices['Desktop Chrome'], viewport: { width: 2560, height: 1440 } },
    },
    {
      name: 'Tablet iPad',
      use: { ...devices['iPad Pro 11'] },
    },
    {
      name: 'Mobile Galaxy S25+',
      use: { ...devices['Galaxy S9+'], viewport: { width: 412, height: 915 } },
    },
  ],
});
```

- [ ] **Step 4: Create layout-audit.spec.ts test suite**

```typescript
import { test, expect } from '@playwright/test';
import 'playwright-layout-inspector/matchers';

test.describe('L³M² Web Dashboard Layout & UX Audit', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.waitForSelector('#out', { timeout: 15000 });
  });

  test('assert zero horizontal viewport overflow and canvas bleeding', async ({ page }) => {
    await expect(page).toHaveNoLayoutOverflow();
  });

  test('assert viewport and mobile fit standards', async ({ page }) => {
    await expect(page).toHaveMobileFit();
  });

  test('assert interactive touch targets meet ergonomics standards', async ({ page }) => {
    await expect(page).toHaveTouchFriendlyTargets({ minSize: 24 });
  });
});
```

- [ ] **Step 5: Run linting and typechecking**

Run: `npm run lint` and `npx tsc --noEmit`
Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add package.json tsconfig.json eslint.config.mjs playwright.config.ts tests/layout-inspector/
git commit -m "feat(tooling): integrate playwright-layout-inspector audit suite for WASM dashboard"
```

---

### Task 6: Full Verification & Quality Gates

**Files:**
- All created and modified files across repository.

- [ ] **Step 1: Run typechecking and linting**
Run: `npm run lint` and `npx tsc --noEmit`
Expected: PASS

- [ ] **Step 2: Run all C# backend and UI unit tests**
Run: `dotnet test`
Expected: All tests PASS

- [ ] **Step 3: Run Playwright layout inspector suite**
Run: `npm run test:layout`
Expected: All layout audits PASS with zero overflow violations.

- [ ] **Step 4: Final commit and cleanup**
```bash
git add .
git commit -m "chore: complete brand, UI, dynamic theming, and layout inspector implementation"
```
