# Design Specification: Carbon & Indigo Design System & Sidebar Settings

## Overview
Elevate the application's visual system from flat, drab default dark greys to a modern **Carbon & Indigo** workstation design language. Simultaneously restructure the monolithic, scrolling Settings view into a premier native **Sidebar Sub-Navigation** experience with dedicated detail panes.

---

## 1. Global Design System & Token Elevation

### 1.1 Color Tokens (`LocalLLMServerManager.Shared/Styles/DesignTokens.axaml`)
- **Base Background**: Deep Obsidian Carbon `#090A0F`
- **Surface Level 1**: Graphite Surface `#12141C`
- **Surface Level 2 (Cards/Popovers)**: Elevated Charcoal `#181B26`
- **Recessed Insets / Canvases**: Deep Void `#0D0E15`
- **Borders**: `#222634` resting, `#4F46E5` / `#6366F1` active glow
- **Primary / Brand Accent**: Indigo `#6366F1`
- **Secondary Accent**: Electric Indigo `#4F46E5`
- **Tertiary Accent**: Violet `#8B5CF6`
- **Semantic Status**:
  - Online / Success: `#10B981` (Emerald)
  - Warning / In Progress: `#F59E0B` (Amber)
  - Error / Offline: `#EF4444` (Rose)

### 1.2 Component Styles (`LocalLLMServerManager.Shared/Styles/MatteTheme.axaml`)
- **Cards (`Border.matte-card`, `Border.glass-card`)**:
  - `CornerRadius="12"`, `Padding="18"`
  - `BoxShadow="0 4 24 0 #40000000"`
  - Background `#12141C`, Border `#222634`
- **Pills (`Border.matte-pill`)**:
  - `CornerRadius="14"`, Background `#1A1D2A`, Border `#2A2F42`
- **Buttons (`Button.matte-primary`)**:
  - Background `#4F46E5`, Border `#6366F1`, `CornerRadius="8"`, Font SemiBold
  - Hover: Background `#6366F1`, Border `#818CF8`
- **Buttons (`Button.matte-secondary`)**:
  - Background `#181B26`, Border `#262B3B`, Foreground `#E2E8F0`, `CornerRadius="8"`
  - Hover: Background `#222634`, Border `#6366F1`
- **Inputs (`TextBox.matte-input`)**:
  - Background `#0D0E15`, Border `#222634`, `CornerRadius="8"`
  - Focus: Border `#6366F1`
- **Progress Bars (`ProgressBar.matte-progress`)**:
  - Background `#181B26`, Foreground `#6366F1`, `CornerRadius="4"`

---

## 2. Settings Sidebar Navigation Architecture

### 2.1 ViewModel (`LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs`)
- Add `[ObservableProperty] private string _selectedSettingsCategory = "Appearance";`
- Add Category booleans:
  - `IsAppearanceCategoryActive => SelectedSettingsCategory == "Appearance";`
  - `IsEnginesCategoryActive => SelectedSettingsCategory == "Engines";`
  - `IsFeaturePacksCategoryActive => SelectedSettingsCategory == "FeaturePacks";`
  - `IsNetworkCategoryActive => SelectedSettingsCategory == "Network";`
- Add RelayCommand `SelectCategory(string category)` to switch views smoothly.

### 2.2 View (`LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`)
- **Left Column: Sidebar Navigation (width 220px)**:
  - Header: `⚙️ Settings` title with subtitle
  - Vertical list of category pill buttons:
    - 🎨 `Appearance`: Theme Palettes & Framework Themes
    - 🔍 `Ecosystem & Engines`: Auto-Discovery & path scanner
    - 📦 `Feature Packs`: Video Pack, Audio Pack, Disk Usage
    - 🌐 `Network & Remote`: LAN Access URL & Connectivity
- **Right Column: Detail Pane (flex/scroll)**:
  - Render clean, focused elevated cards corresponding to the active category.
  - Zero dead whitespace, high-contrast typography, interactive action buttons.

---

## 3. Verification & Compliance
- **Code Quality**: Run `npm run lint` and `npx tsc --noEmit` after code edits.
- **Fast Build**: Run `pwsh scripts/fast_update.ps1 -NoLaunch`.
- **Playwright Screenshots**: Rerun screenshot suite to capture refreshed `dashboard_settings.png`.
