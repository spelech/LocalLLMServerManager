# Studio Presets, Test Flight & Rich Stage Feedback Design Specification

## Overview & Goals
The goal of this feature set is to demystify local AI generation (specifically Video, Image, and Audio/TTS synthesis) and eliminate user friction and fear of complicated parameters, Out-Of-Memory (OOM) errors, or broken workflows. 

This is accomplished by introducing:
1. **Curated & Custom Studio Presets:** Pre-tuned one-click configurations for common standard resolutions, frame rates, and voice profiles across Video, Image, and Audio/TTS, with complete custom preset creation/editing both inline and in the Settings tab.
2. **"Can I Run It" Pre-Flight & Live Hardware Fit Indicator:** Real-time VRAM verification badge directly on generation tabs before the user clicks Generate, complete with automated safety guardrails (VRAM orchestrator LLM paging).
3. **Interactive Test Flight & Starter Prompt Chips:** 1-click starter chips and a dedicated step-by-step diagnostic test flight wizard to guarantee a successful "Hello World" generation out of the box.
4. **Rich Stage Feedback & Live Pipeline Tracker:** A 4-stage visual progress pipeline (`VRAM & Model Load` ➔ `Denoising/Sampling` ➔ `Encoding & Assembly` ➔ `Ready`) with elapsed timers, live memory monitoring, and collapsible real-time engine logs.

---

## 1. Data Models & Service Architecture

### 1.1 `StudioPreset` Model (`LocalLLMServerManager.Shared/Models/StudioPresetModels.cs`)
```csharp
namespace LocalLLMServerManager.Shared.Models;

public enum StudioModality
{
    Image,
    Video,
    Audio
}

public record StudioPreset
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public StudioModality Modality { get; init; } = StudioModality.Video;
    public string WorkflowOrEngine { get; init; } = "";
    public int Width { get; init; } = 832;
    public int Height { get; init; } = 480;
    public int FrameCount { get; init; } = 48;
    public int Fps { get; init; } = 16;
    public int DurationSeconds { get; init; } = 3;
    public string VoiceProfile { get; init; } = "";
    public string SamplePrompt { get; init; } = "";
    public string NegativePrompt { get; init; } = "";
    public bool IsBuiltIn { get; init; } = false;
}
```

### 1.2 Default Built-in Presets
* **Video Generation:**
  * `quick_480p`: "⚡ Quick 480p Preview" (832x480, 32 frames, 16 fps, ~2s duration)
  * `cinematic_720p`: "🎬 Cinematic HD (720p)" (1280x720, 48 frames, 16 fps, ~3s duration)
  * `vertical_reel_9_16`: "📱 Vertical Reel (9:16)" (480x832, 48 frames, 16 fps)
  * `high_fps_master`: "🌟 High-Fidelity Master" (1024x576, 64 frames, 24 fps)
* **Image Generation:**
  * `image_square_1024`: "🖼️ Standard Square (1024x1024)"
  * `image_landscape_wide`: "🌄 Landscape Wallpaper (1344x768)"
  * `image_portrait_photo`: "📸 Portrait Photo (768x1152)"
* **Audio / TTS Generation:**
  * `tts_narrator`: "🎙️ Natural Storyteller" (`af_heart`, clean speech)
  * `tts_radio`: "📻 Energetic Broadcaster" (`am_michael`, vibrant inflection)
  * `audio_sfx_ambient`: "🌧️ Ambient Soundscape" (Stable Audio Open, 30-sec loop)
  * `audio_song_yue`: "🎸 Full Song Generator" (YuE lyrics-to-music)

### 1.3 `StudioPresetService` (`LocalLLMServerManager.Shared/Services/StudioPresetService.cs`)
* Manages the active list of presets (merging default built-ins with user custom presets stored in `AppSettings.CustomPresets`).
* Provides CRUD operations: `GetPresets(modality)`, `SaveCustomPreset(preset)`, `DeleteCustomPreset(id)`, `DuplicatePreset(id)`, `ExportPresetsJson()`, `ImportPresetsJson(json)`, and `ResetToDefaults()`.
* Fully available on both Desktop and WASM builds.

---

## 2. Pre-Flight Hardware Fit & "Can I Run It" Integration

### 2.1 Live VRAM Estimation
* The Studio view subscribes to parameter changes (resolution, frame count, workflow).
* Calls `CanIRunItService.CalculateRequiredVram(modality, resolution, frameCount)` to compute estimated VRAM load.
* Queries `TelemetryService` for total and free VRAM.

### 2.2 Status Badging
* 🟢 **Optimal (`Ready`):** Estimated VRAM fits comfortably in free GPU memory.
* 🟡 **Tight (`LLM Auto-Unload`):** Estimated VRAM fits in total GPU memory, but requires unloading active Ollama models. Handled safely by `VramOrchestrator`.
* 🔴 **Exceeds GPU Limit:** Estimated VRAM exceeds total physical GPU VRAM. Provides a 1-click suggestion button (e.g., `⚡ Switch to Quick 480p Preview`).

---

## 3. Test Flight Experience & Starter Prompt Chips

### 3.1 Inline Starter Prompt Chips
* Placed above prompt boxes for rapid, verified 1-click loading.
* Video Chips:
  * `[🐕 Golden Retriever Beach]` ➔ Fills prompt + auto-selects `Quick 480p Preview` preset.
  * `[🌆 Cyberpunk Rain 720p]` ➔ Fills prompt + auto-selects `Cinematic HD` preset.
  * `[☕ Cozy Cafe Steam]` ➔ Fills slow atmospheric motion prompt.
  * `[🚀 Space Nebula Flyby]` ➔ Fills dynamic sci-fi motion prompt.
* Audio Chips:
  * `[🎙️ Natural Storyteller Sample]` ➔ Fills sample script + selects `af_heart` voice.
  * `[🌧️ Rainy Cyberpunk Ambience]` ➔ Fills ambient SFX prompt.
  * `[🎵 Retro Synthwave Melody]` ➔ Fills electronic synth song prompt.

### 3.2 "🚀 Run System Test Flight" Modal Dialog
* Accessible from the Studio header via `<Button Content="🚀 Run Test Flight" />`.
* **Step 1:** Modality selection (Video / Image / Audio) + prompt picker.
* **Step 2:** Pre-flight sanity checks (`✓ Engine Active`, `✓ VRAM Clearance`).
* **Step 3:** 1-Click test run execution.
* **Step 4:** Live stage progress tracking.
* **Step 5:** Preview output display with confirmation badge: `🎉 Test Flight Succeeded! Your system is verified and ready.`

---

## 4. Rich Stage Feedback & Live Pipeline Tracker

### 4.1 4-Stage Visual Progress Pipeline
* **Stage 1 (VRAM & Weights):** Models loading into GPU, LLM memory unallocated.
* **Stage 2 (Sampling / Denoising):** Iterative generation and frame progress percentage.
* **Stage 3 (Encoding & Assembly):** VAE decode and container export (MP4/WAV).
* **Stage 4 (Ready & Complete):** Result ready for preview and instant playback.

### 4.2 Live Metrics & Drawer
* **Timer:** Live elapsed execution time and estimated completion duration (`⏱️ 0:14s elapsed • Est. ~0:25s total`).
* **Toggleable Live Engine Log:** Collapsible drawer showing real-time WebSocket events and engine logs for troubleshooting.
* **Cancel / Abort Action:** Safely cancels queued or executing generation jobs.

---

## 5. UI Controls & Layout

### 5.1 Reusable UI Controls
* `LocalLLMServerManager.Shared/Views/Controls/StudioPresetBarControl.axaml`: Preset dropdown + Save/Edit/Delete buttons + Starter chips.
* `LocalLLMServerManager.Shared/Views/Controls/GenerationStageTrackerControl.axaml`: 4-stage pipeline stepper + elapsed timer + expandable log.
* `LocalLLMServerManager.Shared/Views/Controls/TestFlightModalControl.axaml`: Multi-step diagnostic test flight modal.

### 5.2 Settings View Enhancement
* In `SettingsTabControl.axaml`: Add the **"🎨 Studio Presets Manager"** card containing the filterable preset table, edit dialogs, JSON export/import, and factory reset actions.

---

## 6. Verification & Quality Gates
1. **Unit & Logic Tests:**
   * Test `StudioPresetService` CRUD operations, default seeds, and serialization.
   * Test VRAM pre-flight calculation against different resolutions and presets.
2. **Avalonia UI & Compilation:**
   * Ensure clean compile of both desktop and WASM projects (`dotnet build`).
   * Run typecheck and linter: `npm run lint` and `npx tsc --noEmit`.
3. **Minor Version Bump:**
   * Update version in `.csproj` files / `MainViewModel.cs` to next minor version.
