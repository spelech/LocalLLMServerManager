# Task 4 Brief: Implement Runtime Dynamic Theming Engine (`ThemeService`)

## Objective
Implement runtime theme switching (`MatteCarbon`, `OledBlack`, `Light`) by dynamically updating Avalonia `Application.Current.Resources` dictionaries across Desktop and WASM, and expose it in `SettingsViewModel` and `SettingsTabControl.axaml`.

## Target Files
- Create: `LocalLLMServerManager.Shared/Services/IThemeService.cs`
- Create: `LocalLLMServerManager.Shared/Services/ThemeService.cs`
- Create: `LocalLLMServerManager.Tests/ThemeServiceTests.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`

## Requirements

### 1. IThemeService.cs & ThemeService.cs
```csharp
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
```

Implementation logic:
- `ThemeService.SetTheme(AppTheme theme)`:
  - Updates `CurrentTheme` and invokes `ThemeChanged`.
  - Mutates `Application.Current.Resources` brushes:
    - `BgDarkBrush`
    - `BgSurfaceBrush`
    - `BgCardBrush`
    - `GlassBorderBrush`
    - `BorderGlowBrush`
    - `TextMainBrush`
    - `TextMutedBrush`
    - `PrimaryBrush`
    - `SecondaryBrush`
    - `AccentBrush`
    - `GlassBackgroundBrush`
  - Color palettes:
    - `MatteCarbon`: BgDark: `#0d1117`, BgSurface: `#161b22`, BgCard: `#1c2128`, Border: `#30363d`, TextMain: `#f0f6fc`, TextMuted: `#8b949e`, Primary: `#388bfd`, Accent: `#79c0ff`
    - `OledBlack`: BgDark: `#000000`, BgSurface: `#121212`, BgCard: `#18181b`, Border: `#27272a`, TextMain: `#f4f4f5`, TextMuted: `#71717a`, Primary: `#3b82f6`, Accent: `#60a5fa`
    - `Light`: BgDark: `#ffffff`, BgSurface: `#f6f8fa`, BgCard: `#ffffff`, Border: `#d0d7de`, TextMain: `#1f2328`, TextMuted: `#656d76`, Primary: `#0969da`, Accent: `#218bff`

### 2. SettingsViewModel.cs & SettingsTabControl.axaml
- Inject or create `IThemeService` in `SettingsViewModel`.
- Add `AvailableThemes` (`Matte Carbon (Default)`, `OLED Pure Black`, `Clean Light`) and `SelectedTheme` property in `SettingsViewModel` with reactive property change triggering `themeService.SetTheme(...)`.
- Add a Theme Selection `ComboBox` in `SettingsTabControl.axaml`.

### 3. ThemeServiceTests.cs
- Unit tests asserting `SetTheme(AppTheme.OledBlack)` and `SetTheme(AppTheme.Light)` update `CurrentTheme`, fire `ThemeChanged`, and mutate application resources.

## Verification
- Run `dotnet test --filter "FullyQualifiedName~ThemeServiceTests"` — must PASS.
- Run `dotnet test LocalLLMServerManager.sln` — all tests must PASS.
- Commit with message: `feat(theming): implement live dynamic theme switching across Desktop and WASM`.
