# Glassmorphic Styling Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract legacy web dashboard CSS/JS assets from pre-Avalonia git history and build a unified, glassmorphic XAML design system (`DesignTokens.axaml`, `GlassmorphicTheme.axaml`) shared across Avalonia Desktop (`LocalLLMServerManager`) and WebAssembly (`LocalLLMServerManager.Web`).

**Architecture:** Create central `DesignTokens.axaml` and `GlassmorphicTheme.axaml` in `LocalLLMServerManager.Shared`. Merge these styles in both Desktop and WASM `App.axaml` files on top of `Semi.Avalonia`. Apply glassmorphic control templates (cards, telemetry pills, glowing buttons, linear gradient tabs) to all shared user controls.

**Tech Stack:** C# .NET 10.0, Avalonia 12.1.1, Semi.Avalonia 12.1.0.1, XAML ResourceDictionaries.

## Global Constraints
- Retain 100% test pass rate across `dotnet test`.
- Retain clean build output across `LocalLLMServerManager.csproj` and `LocalLLMServerManager.Web.csproj`.
- Use atomic, descriptive commits per task on branch `feat/glassmorphic-ui-styling`.

---

### Task 1: Extract Legacy Web Dashboard Assets

**Files:**
- Create: `docs/legacy-web-dash/index.html`
- Create: `docs/legacy-web-dash/index.css`
- Create: `docs/legacy-web-dash/app.js`

**Interfaces:**
- Consumes: `git show 452cf3e:wwwroot/...`
- Produces: Unmodified pre-Avalonia web dashboard source files in `docs/legacy-web-dash/` for styling translation.

- [ ] **Step 1: Extract files from git history commit 452cf3e**

Run:
```powershell
New-Item -ItemType Directory -Force -Path docs/legacy-web-dash
git show 452cf3e:wwwroot/index.html | Out-File -Encoding utf8 docs/legacy-web-dash/index.html
git show 452cf3e:wwwroot/index.css | Out-File -Encoding utf8 docs/legacy-web-dash/index.css
git show 452cf3e:wwwroot/app.js | Out-File -Encoding utf8 docs/legacy-web-dash/app.js
```

- [ ] **Step 2: Verify extracted files exist and contain CSS tokens**

Run:
```powershell
Get-Content docs/legacy-web-dash/index.css | Select-Object -First 30
```
Expected: Output showing `:root` variables `--bg-dark`, `--primary`, `--secondary`, etc.

- [ ] **Step 3: Commit extracted legacy web dashboard assets**

Run:
```bash
git add docs/legacy-web-dash/
git commit -m "feat(legacy): extract pre-Avalonia web dashboard styling reference assets"
```

---

### Task 2: Define Design Tokens ResourceDictionary

**Files:**
- Create: `LocalLLMServerManager.Shared/Styles/DesignTokens.axaml`
- Modify: `LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`

**Interfaces:**
- Consumes: CSS color tokens from `docs/legacy-web-dash/index.css`
- Produces: `ResourceDictionary` containing `BgDarkColor`, `PrimaryBrush`, `PrimaryGradientBrush`, `GlassBorderBrush`, `BorderGlowBrush`, `TextMainBrush`, `TextMutedBrush`.

- [ ] **Step 1: Create DesignTokens.axaml**

Write `LocalLLMServerManager.Shared/Styles/DesignTokens.axaml` with content:
```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Color Definitions -->
    <Color x:Key="BgDarkColor">#0b0e14</Color>
    <Color x:Key="BgSurfaceColor">#161b26</Color>
    <Color x:Key="BgCardColor">#1e2536</Color>
    <Color x:Key="PrimaryColor">#8b5cf6</Color>
    <Color x:Key="SecondaryColor">#06b6d4</Color>
    <Color x:Key="AccentColor">#c084fc</Color>

    <!-- Solid Brushes -->
    <SolidColorBrush x:Key="BgDarkBrush" Color="{StaticResource BgDarkColor}" />
    <SolidColorBrush x:Key="BgSurfaceBrush" Color="{StaticResource BgSurfaceColor}" Opacity="0.45" />
    <SolidColorBrush x:Key="BgCardBrush" Color="{StaticResource BgCardColor}" Opacity="0.65" />
    <SolidColorBrush x:Key="GlassBorderBrush" Color="#4b5563" Opacity="0.35" />
    <SolidColorBrush x:Key="BorderGlowBrush" Color="#a78bfa" Opacity="0.25" />
    <SolidColorBrush x:Key="PrimaryBrush" Color="{StaticResource PrimaryColor}" />
    <SolidColorBrush x:Key="SecondaryBrush" Color="{StaticResource SecondaryColor}" />
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}" />
    <SolidColorBrush x:Key="TextMainBrush" Color="#f3f4f6" />
    <SolidColorBrush x:Key="TextMutedBrush" Color="#9ca3af" />
    <SolidColorBrush x:Key="OnlineBrush" Color="#22c55e" />
    <SolidColorBrush x:Key="OfflineBrush" Color="#ef4444" />

    <!-- Gradient Brushes -->
    <LinearGradientBrush x:Key="PrimaryGradientBrush" StartPoint="0%,50%" EndPoint="100%,50%">
        <GradientStop Color="#8b5cf6" Offset="0.0" />
        <GradientStop Color="#06b6d4" Offset="1.0" />
    </LinearGradientBrush>

    <RadialGradientBrush x:Key="GlassBackgroundBrush" Center="50%,50%" GradientOrigin="50%,50%" Radius="0.8">
        <GradientStop Color="#1e1b4b" Offset="0.0" />
        <GradientStop Color="#0b0e14" Offset="1.0" />
    </RadialGradientBrush>
</ResourceDictionary>
```

- [ ] **Step 2: Update LocalLLMServerManager.Shared.csproj to include AvaloniaResource**

Modify `LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj` to include:
```xml
  <ItemGroup>
    <AvaloniaResource Include="Styles/**" />
  </ItemGroup>
```

- [ ] **Step 3: Run build to verify resource compilation**

Run: `dotnet build LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`
Expected: Build succeeds with 0 errors.

- [ ] **Step 4: Commit DesignTokens**

Run:
```bash
git add LocalLLMServerManager.Shared/Styles/DesignTokens.axaml LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj
git commit -m "feat(ui): add DesignTokens ResourceDictionary with glassmorphic color palette"
```

---

### Task 3: Create GlassmorphicTheme ResourceDictionary

**Files:**
- Create: `LocalLLMServerManager.Shared/Styles/GlassmorphicTheme.axaml`

**Interfaces:**
- Consumes: Brushes and colors from `DesignTokens.axaml`
- Produces: Styles for `Border.glass-card`, `Border.telemetry-pill`, `Button.glass-primary`, `Button.glass-secondary`, `TabControl.glass-tabs`.

- [ ] **Step 1: Create GlassmorphicTheme.axaml**

Write `LocalLLMServerManager.Shared/Styles/GlassmorphicTheme.axaml` with content:
```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Glass Card Style -->
    <Style Selector="Border.glass-card">
        <Setter Property="Background" Value="{StaticResource BgCardBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource GlassBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="12" />
        <Setter Property="Padding" Value="16" />
        <Setter Property="BoxShadow" Value="0 8 32 0 #20000000" />
    </Style>

    <!-- Telemetry Pill Style -->
    <Style Selector="Border.telemetry-pill">
        <Setter Property="Background" Value="{StaticResource BgSurfaceBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource GlassBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="16" />
        <Setter Property="Padding" Value="10,6" />
    </Style>

    <!-- Primary Glowing Button Style -->
    <Style Selector="Button.glass-primary">
        <Setter Property="Background" Value="{StaticResource PrimaryGradientBrush}" />
        <Setter Property="Foreground" Value="#ffffff" />
        <Setter Property="FontWeight" Value="Bold" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="Padding" Value="16,10" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="BoxShadow" Value="0 4 15 0 #408b5cf6" />
    </Style>

    <!-- Secondary Glass Button Style -->
    <Style Selector="Button.glass-secondary">
        <Setter Property="Background" Value="{StaticResource BgSurfaceBrush}" />
        <Setter Property="Foreground" Value="{StaticResource TextMainBrush}" />
        <Setter Property="BorderBrush" Value="{StaticResource GlassBorderBrush}" />
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="Padding" Value="14,8" />
    </Style>
</ResourceDictionary>
```

- [ ] **Step 2: Build project to verify theme dictionary**

Run: `dotnet build LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`
Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Commit GlassmorphicTheme**

Run:
```bash
git add LocalLLMServerManager.Shared/Styles/GlassmorphicTheme.axaml
git commit -m "feat(ui): create GlassmorphicTheme control templates and styles"
```

---

### Task 4: Integrate Glassmorphic Styles into App Entrypoints & MainView

**Files:**
- Modify: `App.axaml:1-10`
- Modify: `LocalLLMServerManager.Web/App.axaml:1-9`
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml:1-30`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml:1-40`

**Interfaces:**
- Consumes: `DesignTokens.axaml` and `GlassmorphicTheme.axaml`
- Produces: Visual glassmorphic backdrop, glowing header pills, and gradient tabs across Desktop and Web apps.

- [ ] **Step 1: Merge ResourceDictionaries in App.axaml**

Update `App.axaml` line 6-8:
```xml
    <Application.Styles>
        <semi:SemiTheme Locale="en-US" />
        <StyleInclude Source="avares://LocalLLMServerManager.Shared/Styles/DesignTokens.axaml" />
        <StyleInclude Source="avares://LocalLLMServerManager.Shared/Styles/GlassmorphicTheme.axaml" />
    </Application.Styles>
```

- [ ] **Step 2: Merge ResourceDictionaries in Web App.axaml**

Update `LocalLLMServerManager.Web/App.axaml`:
```xml
<Application xmlns="https://github.com/avaloniaui"
              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
              xmlns:semi="using:Semi.Avalonia"
              x:Class="LocalLLMServerManager.App"
              RequestedThemeVariant="Dark">
    <Application.Styles>
        <semi:SemiTheme Locale="en-US" />
        <StyleInclude Source="avares://LocalLLMServerManager.Shared/Styles/DesignTokens.axaml" />
        <StyleInclude Source="avares://LocalLLMServerManager.Shared/Styles/GlassmorphicTheme.axaml" />
    </Application.Styles>
</Application>
```

- [ ] **Step 3: Apply Glass Background and Layout to MainView.axaml**

Set background of `MainView.axaml` root grid to `{StaticResource GlassBackgroundBrush}`.

- [ ] **Step 4: Update TelemetryHeaderControl.axaml to use telemetry-pill style**

Wrap telemetry metrics (RAM, VRAM, CPU) in `Border` controls with `Classes="telemetry-pill"`.

- [ ] **Step 5: Run tests and build to verify integration**

Run: `dotnet test`
Expected: 100% test pass rate across all projects.

- [ ] **Step 6: Commit integration changes**

Run:
```bash
git add App.axaml LocalLLMServerManager.Web/App.axaml LocalLLMServerManager.Shared/Views/MainView.axaml LocalLLMServerManager.Shared/Views/Controls/TelemetryHeaderControl.axaml
git commit -m "feat(ui): integrate glassmorphic theme into Desktop & WASM App entrypoints"
```

---

### Task 5: Refine View Controls with Glassmorphic Cards & Buttons

**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/Controls/CivitaiTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/EngineStudioTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/OllamaModelsTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`

**Interfaces:**
- Consumes: `glass-card`, `glass-primary`, `glass-secondary` classes
- Produces: Polished, uniform glassmorphic card layouts and button states across all sub-tabs.

- [ ] **Step 1: Apply glass-card classes to tab panels**

Update outer borders in `CivitaiTabControl.axaml`, `EngineStudioTabControl.axaml`, `HuggingFaceTabControl.axaml`, `OllamaModelsTabControl.axaml`, and `SettingsTabControl.axaml` to use `Classes="glass-card"`.

- [ ] **Step 2: Apply glass-primary and glass-secondary button styles**

Update main action buttons (Search, Download, Save, Trigger) to use `Classes="glass-primary"` and secondary buttons to use `Classes="glass-secondary"`.

- [ ] **Step 3: Run full verification suite**

Run: `dotnet test`
Run: `npx tsc --noEmit` (if any TS files present)
Expected: 100% pass rate.

- [ ] **Step 4: Commit control styling updates**

Run:
```bash
git add LocalLLMServerManager.Shared/Views/Controls/*.axaml
git commit -m "feat(ui): apply glassmorphic card & button templates across all view controls"
```

---

## Verification Plan

### Automated Tests
- Run `dotnet test` across all solution test projects to ensure zero regressions.
- Run `dotnet build LocalLLMServerManager.csproj` and `dotnet build LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj`.

### Manual Verification
- Launch `LocalLLMServerManager` desktop app and check glass backdrop glow, card styling, telemetry header pills, and tab switching visuals.
