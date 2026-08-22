# Task 4 Report: Runtime Dynamic Theming Engine (`ThemeService`)

## Overview
Successfully implemented runtime dynamic theme switching (`MatteCarbon`, `OledBlack`, and `Light`) across Desktop and WASM by dynamically mutating Avalonia `Application.Current.Resources` brush and color resource dictionaries and synchronizing `RequestedThemeVariant`. Integrated dynamic theme selection into `SettingsViewModel` and `SettingsTabControl.axaml`.

## Deliverables & Key Changes
1. **`LocalLLMServerManager.Shared/Services/IThemeService.cs`**:
   - Defined `AppTheme` enum (`MatteCarbon`, `OledBlack`, `Light`).
   - Defined `IThemeService` interface specifying `CurrentTheme`, `SetTheme(AppTheme theme)`, and `ThemeChanged` event.

2. **`LocalLLMServerManager.Shared/Services/ThemeService.cs`**:
   - Implemented `IThemeService` with singleton accessor `Instance` and dictionary injection support.
   - Dynamic palette application for `MatteCarbon`, `OledBlack`, and `Light`.
   - In-place mutation of existing `SolidColorBrush` instances and resource dictionary updates for:
     - `BgDarkBrush`, `BgSurfaceBrush`, `BgCardBrush`
     - `GlassBorderBrush`, `BorderColorBrush`, `BorderGlowBrush`
     - `TextMainBrush`, `TextMutedBrush`
     - `PrimaryBrush`, `SecondaryBrush`, `AccentBrush`
     - `GlassBackgroundBrush`, `PrimaryGradientBrush`
     - Corresponding color tokens (`BgDarkColor`, `BgSurfaceColor`, `BgCardColor`, `BorderColor`, `TextMainColor`, `TextMutedColor`, `PrimaryColor`, `SecondaryColor`, `AccentColor`).
   - Updates `Application.Current.RequestedThemeVariant` to `ThemeVariant.Light` / `ThemeVariant.Dark`.

3. **`LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs` & `MainViewModel.cs`**:
   - Injected `IThemeService` in `SettingsViewModel` (defaulting to `ThemeService.Instance`).
   - Added `AvailableThemes` list (`Matte Carbon (Default)`, `OLED Pure Black`, `Clean Light`).
   - Added reactive `SelectedTheme` property triggering `_themeService.SetTheme(...)` on value changes.
   - Added mapping methods `MapThemeToString` and `MapStringToTheme`.
   - Forwarded `SelectedTheme` and `AvailableThemes` on `MainViewModel`.

4. **`LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`**:
   - Added Theme Palette `ComboBox` bound to `AvailableThemes` and `SelectedTheme`.

5. **`LocalLLMServerManager.Tests/ThemeServiceTests.cs` & `SettingsViewModelTests.cs`**:
   - Comprehensive unit test coverage for theme switching (`OledBlack`, `Light`, `MatteCarbon`).
   - Verification of `ThemeChanged` event invocation, brush mutation, in-place color modification, and headless `RequestedThemeVariant` synchronization.
   - Unit tests for `SettingsViewModel` theme integration and mapping helpers.

## Verification & Test Results
- **Targeted Test Suite**:
  - `dotnet test --filter "FullyQualifiedName~ThemeServiceTests"`: **8 Passed, 0 Failed**.
- **Full Solution Test Suite**:
  - `dotnet test LocalLLMServerManager.sln`: **180 Passed, 0 Failed, 1 Skipped**.

## Git Commit
- **Commit Hash**: `6fcb752b7be78249491a1a7b773460e13ad1d19a`
- **Commit Message**: `feat(theming): implement live dynamic theme switching across Desktop and WASM`
