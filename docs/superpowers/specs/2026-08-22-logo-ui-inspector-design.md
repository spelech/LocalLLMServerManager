# Local LLM Server Manager — Brand, UI Design, Dynamic Theming & Layout Inspector Design Spec

- **Date:** 2026-08-22
- **Topic:** Logo & Brand Identity, Matte UI Theme Overhaul, Runtime Dynamic Theming, and Playwright Layout Inspector Integration
- **Status:** Approved

---

## 1. Executive Summary

This design specification establishes a unified modernization of **Local LLM Server Manager (v3.5.0)** across:
1. **Brand Identity & Iconography**: Introducing the official borderless monochromatic **`L³M²`** (Local LLM Model Manager) squircle brand mark.
2. **UI & Theme Design System**: Replacing the neon purple/cyan glassmorphic theme with a high-contrast, matte carbon/slate design system across all Avalonia Desktop and WASM views.
3. **Dynamic / Live Theming Architecture**: Implementing a runtime theme switching engine (`ThemeService`) that dynamically updates Avalonia resource dictionaries across Native Desktop and WASM without page reload or application restart.
4. **Layout Inspector Tooling**: Integrating [`spelech/playwright-layout-inspector`](https://github.com/spelech/playwright-layout-inspector) to automatically audit DOM geometry, canvas bleeding, touch target ergonomics, and responsive layout across mobile and desktop viewport profiles.

---

## 2. Brand Identity & Logo Specification

### 2.1 Logo Mark & Typography
- **Core Mark**: `L³M²` (Local LLM Model Manager).
- **Container**: Rich obsidian / matte carbon squircle tile (`#14171d`), borderless with smooth radius (`rx=28` at 128px scale).
- **Typography**: Solid high-contrast white (`#ffffff`) bold sans-serif lettering with superscript exponents `³` and `²` for `L³` and `M²`.
- **Styling**: Monochromatic, high-contrast, zero neon bloom/glow, optimized for high legibility at extreme small scale (16px–24px OS tray) and large scale (512px app store/installer).

### 2.2 Asset Generation & Destinations
- `Assets/app-icon.ico` — Multi-resolution Windows/Desktop application icon (16x16, 24x24, 32x32, 48x48, 64x64, 128x128, 256x256).
- `Assets/app-icon.png` — High-resolution 512x512 PNG app tile.
- `Assets/app_tray_icon.jpg` / `Assets/app_tray_icon.png` — High-contrast 128px tray asset.
- `Assets/logo.svg` — Pure scalable vector source asset.
- `LocalLLMServerManager.Web/wwwroot/favicon.ico` — WebAssembly browser tab icon.

---

## 3. UI Theme & XAML Design System Overhaul

### 3.1 Design Tokens (`DesignTokens.axaml`)
Replacing neon violet/cyan gradients and heavy glow box-shadows with clean matte tokens:

| Token Name | Default Matte Carbon | OLED Pure Black | Clean Light |
| :--- | :--- | :--- | :--- |
| `BgDarkColor` | `#0d1117` | `#000000` | `#ffffff` |
| `BgSurfaceColor` | `#161b22` | `#121212` | `#f6f8fa` |
| `BgCardColor` | `#1c2128` | `#18181b` | `#ffffff` |
| `BorderColor` | `#30363d` | `#27272a` | `#d0d7de` |
| `TextMainColor` | `#f0f6fc` | `#f4f4f5` | `#1f2328` |
| `TextMutedColor` | `#8b949e` | `#71717a` | `#656d76` |
| `OnlineColor` | `#238636` | `#22c55e` | `#1a7f37` |
| `OfflineColor` | `#da3633` | `#ef4444` | `#cf222e` |
| `WarningColor` | `#d29922` | `#f59e0b` | `#9a6700` |
| `AccentColor` | `#388bfd` | `#3b82f6` | `#0969da` |

### 3.2 Component Modernization
- **Theme Stylesheet**: Refactor `GlassmorphicTheme.axaml` into `MatteTheme.axaml`.
  - Buttons (`Button.matte-primary`, `Button.matte-secondary`): Flat solid backgrounds with 1px border, 6px corner radius, no glowing dropshadows.
  - Cards (`Border.matte-card`): Solid background `#1c2128`, 1px `#30363d` border, 8px corner radius.
  - Inputs (`TextBox.matte-input`): Subtle surface background with `#388bfd` focus border.
- **Telemetry Header (`TelemetryHeaderControl.axaml`)**:
  - Streamlined horizontal chip layout (CPU, RAM, GPU VRAM) with compact numeric telemetry and mini progress bars.
- **Tab Bar Navigation**:
  - Modern segmented pill tabs with `#21262d` active indicator and 8px border radius.
- **Model Cards (Ollama, HuggingFace, Civitai, Engine Studio)**:
  - Clean metadata chips, concise action buttons (Start, Stop, Download, Delete), and crisp status badges.

---

## 4. Dynamic Runtime Theming Architecture

### 4.1 Theme Service Interface & Implementation
- Located in `LocalLLMServerManager.Shared/Services/ThemeService.cs`:
  ```csharp
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
  ```

### 4.2 Dynamic Resource Mutation Mechanism
- Modifies `Application.Current.Resources` directly at runtime.
- Keys updated: `BgDarkBrush`, `BgSurfaceBrush`, `BgCardBrush`, `GlassBorderBrush`, `BorderColorBrush`, `TextMainBrush`, `TextMutedBrush`, `PrimaryBrush`, `AccentBrush`.
- Fully supported in Avalonia WASM and Desktop without reloading the webpage or restarting the process.
- Persisted to application configuration (`appsettings.json` / WASM local storage).

---

## 5. Playwright Layout Inspector Tooling

### 5.1 Package & Tooling Setup
- Integrated dependency: `github:spelech/playwright-layout-inspector` and `@playwright/test` in `package.json`.
- Configuration file: `playwright.config.ts`.
- Test suite: `tests/layout-inspector/layout-audit.spec.ts`.

### 5.2 Test Scenarios & Device Viewports
1. **Desktop Baseline (1920x1080 & 1440x900)**:
   - Audit overall page stability.
   - Assert zero horizontal scrollbar or canvas container overflow (`expect(page).toHaveNoLayoutOverflow()`).
2. **Tablet Viewport (iPad / 768x1024)**:
   - Verify card wrapping and navigation bar scaling.
3. **Mobile Viewports (Samsung Galaxy S25+ & iPhone 16 Pro)**:
   - Assert mobile fit (`expect(page).toHaveMobileFit()`).
   - Audit touch target ergonomics ($\ge 24\text{px}$ standard via `expect(page).toHaveTouchFriendlyTargets({ minSize: 24 })`).
   - Measure layout shift stability during tab transitions.

### 5.3 CI & Local Execution
- Script command: `npm run test:layout`.
- Runs against `http://localhost:5000` (or the configured WASM server port).

---

## 6. Verification & Quality Gates

- **Linting & Typechecking**: `npm run lint` and `npx tsc --noEmit` as required by user rules.
- **.NET Build & Tests**: `dotnet build` and `dotnet test` across all projects (`LocalLLMServerManager`, `LocalLLMServerManager.Shared`, `LocalLLMServerManager.Web`, `LocalLLMServerManager.Tests`).
- **Visual & Layout Inspection**: Pass all `playwright-layout-inspector` audits on Desktop, Tablet, and Mobile viewports with zero horizontal overflow violations.
