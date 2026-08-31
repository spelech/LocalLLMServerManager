# Task 4: CanIRunItView.axaml Avalonia UI & Main Navigation Integration

## Description
Create the Avalonia UserControl `CanIRunItView.axaml` and code-behind `CanIRunItView.axaml.cs` in `LocalLLMServerManager.Shared/Views/Controls/` (or `LocalLLMServerManager.Shared/Views/`), integrate `CanIRunItViewModel` into `MainViewModel.cs`, register Tab #5 in `MainView.axaml`, and update Avalonia headless interaction tests.

## Requirements
1. **View Structure (`CanIRunItView.axaml`)**:
   - Styled with Matte Carbon dark design system (`#0F172A`, `#1E293B`, `#334155`, `#38BDF8`, `#22C55E`, `#E2E8F0`).
   - **Hardware Telemetry Banner**:
     - GPU Name text badge, Total & Free VRAM readout, Total System RAM readout, and Refresh button.
   - **Modality Segmented Control / Button Bar**:
     - `[🦙 Text LLMs]`, `[🎨 Image Generation]`, `[🎬 Video Generation]`, `[🎙️ Audio & Speech]`, `[🧊 3D Generation]`.
     - Active tab highlighted with accent background.
   - **Configuration Column / Controls**:
     - Model Preset dropdown (`ComboBox`).
     - **LLM Section**:
       - Parameter Count slider (`0.5B` to `405B`) with numeric readout TextBlock.
       - Quantization level dropdown (`ComboBox` with `Q2_K`..`FP16`).
       - Context Window slider (`2,048` to `131,072` tokens) with formatted token readout TextBlock.
       - KV Cache Precision picker (`ComboBox` with `FP16`, `Q8_0`, `Q4_0`).
     - **Image Section**: Preset combo, Resolution combo (`512`, `768`, `1024`, `1536`), Batch size slider.
     - **Video Section**: Preset combo, Frame count slider (`49`..`129`), Resolution combo (`480p`, `720p`).
     - **Audio & 3D Section**: Preset combo with model descriptions.
   - **Interactive Results & Memory Bar**:
     - Fit Verdict Badge: Colored card with status icon (`🟢`, `🟡`, `🟠`, `🔴`), `VerdictSummaryText`, `OffloadSummaryText`, `RecommendationText`, `SpeedEstimationText`.
     - **Visual Stacked Memory Allocation Bar**:
       - Segment 1: Model Weights (Blue `#38BDF8` or `#2563EB`)
       - Segment 2: KV Cache / Latent Buffer (Purple `#A855F7`)
       - Segment 3: CUDA Overhead (Slate `#64748B`)
       - Segment 4: Free Headroom (Dark `#1E293B`)
       - Legend labels indicating MB/GB for each segment.
2. **Main Navigation Integration**:
   - In `MainViewModel.cs`:
     - Add `public CanIRunItViewModel HardwareFit { get; }` property.
     - Initialize `HardwareFit = new CanIRunItViewModel(httpClient, telemetryService, new CanIRunItService());` in constructor.
     - In telemetry polling, call `HardwareFit.UpdateHardwareTelemetry(telemetry);`.
     - Add `NavigateToCanIRunIt(string modelName, string modality = "LLM")` helper method that sets active tab to Tab 5 (Index 4) and calls `HardwareFit.InspectModel(...)`.
   - In `LocalLLMServerManager.Shared/Views/MainView.axaml`:
     - Add Tab #5 header `[⚡ Can I Run It]` in the top tab navigation bar.
     - Add TabItem / content panel hosting `<controls:CanIRunItView DataContext="{Binding HardwareFit}" />`.
3. **Headless Avalonia Tests (`AvaloniaHeadlessInteractionTests.cs`)**:
   - Verify `CanIRunItView` renders with DataContext.
   - Verify clicking Tab #5 switches active tab and renders sliders and verdict card.
   - Verify slider manipulation updates text blocks.

## Constraints & TDD
- Write failing headless UI tests in `LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs`.
- Implement `CanIRunItView.axaml`, `CanIRunItView.axaml.cs`, `MainViewModel.cs`, and `MainView.axaml`.
- Run `dotnet test --filter "FullyQualifiedName~AvaloniaHeadlessInteractionTests"`.
- Run `npm run lint` and `npx tsc --noEmit`.
- Commit: `git commit -m "feat(ui): add CanIRunItView Avalonia control and Tab 5 navigation integration"`
- Write report to `.superpowers/sdd/2026-08-30-can-i-run-it\task-4-report.md`.
