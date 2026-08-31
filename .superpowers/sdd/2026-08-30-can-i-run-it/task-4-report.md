# Task 4 Report: CanIRunItView Avalonia UI & Main Navigation Integration

**Date:** 2026-08-30  
**Status:** DONE  
**Commit:** `2e59bca feat(ui): add CanIRunItView Avalonia control and Tab 5 navigation integration`  
**Branch:** `feature/canIrunIt`

---

## 1. Overview
Task 4 successfully built and integrated the native Avalonia UI control `CanIRunItView.axaml` and its code-behind `CanIRunItView.axaml.cs` into `LocalLLMServerManager`. The view is styled using the Matte Carbon design system (`#0F172A`, `#1E293B`, `#334155`, `#38BDF8`, `#22C55E`, `#E2E8F0`) and is fully integrated as Tab #5 (`[⚡ Can I Run It]`) in `MainView.axaml`.

---

## 2. Key Changes & Components

### 2.1 Avalonia UserControl (`CanIRunItView.axaml` & `CanIRunItView.axaml.cs`)
- **Live Hardware Telemetry Banner**: Displays GPU Name, VRAM capacity/free readout, System RAM capacity/available readout, and a "🔄 Refresh Telemetry" button bound to `RefreshTelemetryCommand`.
- **Modality Segmented Button Bar**: Quick buttons for `[🦙 Text LLMs]`, `[🎨 Image Generation]`, `[🎬 Video Generation]`, `[🎙️ Audio & Speech]`, and `[🧊 3D Generation]`.
- **Configuration Panels**:
  - **LLM**: Model Preset dropdown, Parameter Count slider (0.5B to 405B) with dynamic readout, GGUF Quantization level picker (`Q2_K`..`FP16`), Context Window slider (2,048 to 131,072 tokens), and KV Cache precision picker (`FP16`, `Q8_0`, `Q4_0`).
  - **Image / Diffusion**: Preset selector, Resolution picker (512 to 1536), Quantization (`FP8`, `FP16`, `Q4`), Batch size slider.
  - **Video (DiT)**: Preset selector (Wan 2.2, LTX, Hunyuan), Resolution picker, Quantization, Frame count slider (49 to 129 frames).
  - **Audio & 3D**: Model preset pickers with architectural details.
- **Compatibility Verdict & Memory Bar**:
  - Fit verdict badge card showing status icon/badge (`🟢`, `🟡`, `🟠`, `🔴`), `VerdictSummaryText`, `OffloadSummaryText`, `RecommendationText`, and estimated throughput/latency.
  - Dynamic stacked VRAM allocation bar using 4 proportional `ColumnDefinition` star widths (`ModelWeightsColumnWidth`, `KvCacheColumnWidth`, `OverheadColumnWidth`, `FreeVramColumnWidth`) and color-coded swatches.

### 2.2 ViewModel Integration (`CanIRunItViewModel.cs` & `MainViewModel.cs`)
- `CanIRunItViewModel.cs`:
  - Added boolean flags (`IsLlmModality`, `IsImageModality`, `IsVideoModality`, `IsAudioModality`, `IsThreeDModality`) for clean declarative XAML visibility.
  - Added `GridLength` width properties for stacked bar rendering and segment formatted text readouts (`ModelWeightsDetailsText`, etc.).
  - Added `[RelayCommand] RefreshTelemetryAsync` overload.
  - Enhanced `InspectModel` to handle model name variations with spaces, hyphens, and underscores.
- `MainViewModel.cs`:
  - Added `public CanIRunItViewModel HardwareFit { get; }` property.
  - Added `[ObservableProperty] private int _selectedTabIndex = 0;` bound to `TabControl.SelectedIndex`.
  - Initialized `HardwareFit` in constructor with telemetry and HTTP client.
  - Added live telemetry synchronization in `RefreshStatusAsync()`.
  - Added `NavigateToCanIRunIt(string modelName, string modality = "LLM")` helper to switch active tab to index 4 and inspect the model.
- `MainView.axaml`:
  - Added Tab #5 `[⚡ Can I Run It]` containing `<controls:CanIRunItView DataContext="{Binding HardwareFit}" />`.
  - Bound `TabControl.SelectedIndex` to `SelectedTabIndex`.

---

## 3. Test & Verification Results

### 3.1 Headless Avalonia UI Tests
- Updated and added tests in `LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs`:
  - `MainView_TabNavigation_SwitchesActiveTabsCleanly`: Verifies 6 tabs and switching between tabs 1 through 6.
  - `CanIRunItView_RendersVisualTree_AndBindsHardwareTelemetry`: Verifies UI tree rendering, GPU name badge, VRAM text, and verdict summary.
  - `CanIRunItView_SliderAndPresetInteraction_UpdatesCalculations`: Verifies slider manipulation (context length) and modality switching buttons.
  - `MainView_Tab5_CanIRunItNavigation_SwitchesTabAndRendersCanIRunItView`: Verifies `NavigateToCanIRunIt` switches tab to Tab 5 and renders `CanIRunItView`.
- **Headless UI Tests Result**: 9 / 9 passed (`0 failed, 0 skipped`).

### 3.2 Full Test Suite
- `dotnet test LocalLLMServerManager.sln`: **387 passed, 0 failed, 1 skipped (388 total)**.
- `npm run lint`: **0 errors**.
- `npx tsc --noEmit`: **0 errors**.

---

## 4. Summary of Files Changed
- `LocalLLMServerManager.Shared/Views/Controls/CanIRunItView.axaml` (Created)
- `LocalLLMServerManager.Shared/Views/Controls/CanIRunItView.axaml.cs` (Created)
- `LocalLLMServerManager.Shared/ViewModels/CanIRunItViewModel.cs` (Updated)
- `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs` (Updated)
- `LocalLLMServerManager.Shared/Views/MainView.axaml` (Updated)
- `LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs` (Updated)
