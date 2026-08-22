# Task 2 Brief: Overhaul Design Tokens & Matte UI Theme

## Objective
Replace the neon violet/cyan glassmorphic theme with a clean, high-contrast, matte carbon/slate design system across all Avalonia XAML styles in Desktop and WASM.

## Target Files
- Modify: `LocalLLMServerManager.Shared/Styles/DesignTokens.axaml`
- Create/Refactor: `LocalLLMServerManager.Shared/Styles/MatteTheme.axaml` (and keep `GlassmorphicTheme.axaml` forwarding/aliased if needed for backward compatibility)
- Modify: `App.axaml`
- Modify: `LocalLLMServerManager.Web/App.axaml`

## Requirements & Exact Values

### 1. DesignTokens.axaml
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
    <SolidColorBrush x:Key="PrimaryGradientBrush" Color="{StaticResource PrimaryColor}" />
</ResourceDictionary>
```

### 2. MatteTheme.axaml
- Card styles (`Border.glass-card` / `Border.matte-card`): Flat solid `#1c2128`, 1px `#30363d` border, 8px corner radius, no heavy glow shadows.
- Buttons (`Button.glass-primary`, `Button.glass-secondary`): Flat solid fill with 1px border, 6px-8px corner radius, no glowing dropshadows.
- Inputs (`TextBox.glass-input`): Solid surface `#161b22`, 1px `#30363d` border, `#388bfd` focus border.
- Progress bars (`ProgressBar.glass-progress`): Clean background `#161b22`, foreground `#388bfd`.
- TabControl & TabItems: Clean segmented pill tabs with `#21262d` active indicator and 8px border radius.

### 3. App.axaml & LocalLLMServerManager.Web/App.axaml
- Update StyleIncludes to reference `avares://LocalLLMServerManager.Shared/Styles/MatteTheme.axaml` (or maintain `GlassmorphicTheme.axaml`).

## Verification
- Run `dotnet build LocalLLMServerManager.sln` — must compile with 0 errors.
- Run `dotnet test` — all tests must pass.
- Commit with message: `feat(ui): implement clean matte design system and remove neon glassmorphism`.
