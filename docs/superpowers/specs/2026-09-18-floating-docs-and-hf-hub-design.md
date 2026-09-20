# Floating Documentation Window & Hugging Face Hub Overhaul Design Specification

## 1. Overview & Objective

This design addresses two key user experience improvements in **Local LLM Server Manager**:
1. **Floating / Standalone Documentation Window**: Allow users to keep procedural guides visible in a floating, movable window or overlay while simultaneously operating other tabs (Workflows, Models, Settings) without switching back and forth, including smart step-level tab navigation.
2. **Hugging Face Hub Overhaul**: Fix the initial empty state by automatically loading curated trending/starter models on open, implement clear, high-contrast active/inactive visual styling for modality and hardware filter toggles, and verify filter tag mappings.

---

## 2. Component 1: Floating In-App Documentation System

### 2.1 Problem
Currently, in-app documentation is confined to a full-screen tab (`📖 Documentation`). When following instructions to generate video, download models, or configure settings, the user must constantly toggle between the documentation tab and the target workspace tab.

### 2.2 Architecture & Hybrid Layout
* **Desktop Native Window (`DocumentationWindow.axaml`)**:
  * Can be popped out into a standalone OS window that can be dragged to a secondary monitor.
  * Includes a **Pin / Always-on-Top** toggle (`Topmost="True"`).
  * Uses the application's dark theme palette and compact dimensions (420x620).
* **In-App Draggable Floating HUD (`DocumentationFloatingOverlay.axaml`)**:
  * Lives inside `MainView.axaml` on an overlay layer (`ZIndex="100"`).
  * Can be dragged anywhere across the main window canvas via pointer drag on its titlebar.
  * Supports a **Minimize to Pill** mode (collapses to a compact 36px floating badge showing current step title and quick Next/Prev buttons).
  * Works identically across both Desktop and WebAssembly (WASM).
* **Smart Step Navigation**:
  * `DocStep` is extended with an optional `TargetTab` property (`Models = 0`, `Workflows = 1`, `HardwareFit = 2`, `AI Assist = 3`, `Documentation = 4`, `Settings = 5`).
  * Each step card in the floating guide features a **"Jump to Tab"** button (`🚀`). Clicking it updates `MainViewModel.SelectedTabIndex` so the target workspace immediately appears behind the guide.

---

## 3. Component 2: Hugging Face Hub Overhaul

### 3.1 Initial Model Loading
* **Problem**: Opening the Hugging Face Hub currently shows an empty list with a "No models found" message because search is never invoked on startup.
* **Solution**:
  * `HuggingFaceSearchViewModel` triggers an initial automated query on first load if results are empty.
  * The initial query fetches top curated trending and high-utility starter models (`Qwen/Qwen2.5-Coder-7B-Instruct-GGUF`, `meta-llama/Llama-3.2-3B-Instruct-GGUF`, `deepseek-ai/DeepSeek-R1-Distill-Qwen-7B-GGUF`, `Wan-AI/Wan2.1-T2V-14B`, `hexgrad/Kokoro-82M`).
  * Live Can I Run It hardware fit badges are computed immediately against the user's GPU VRAM and system RAM.

### 3.2 Toggle Button Styling & Visual Clarity
* **Problem**: Modality buttons (`Text`, `Image`, `Audio`, `Video`, `3D`) and Hardware Fit buttons (`🟢 Full VRAM`, `🟡 Partial Offload`, etc.) are plain flat buttons with zero visual indicator showing whether they are currently active/included or disabled.
* **Solution**:
  * Implement distinct visual states:
    * **Active Modality State**: Solid vibrant background (`#2563EB` for inputs, `#8B5CF6` for outputs), bright white text, bold font, subtle glow border, and an active checkmark (`✓`).
    * **Inactive Modality State**: Dark muted background (`#1E293B`), dim text (`#64748B`), 50% opacity, standard border.
    * **Active Hardware Fit State**: Full color badge with solid pill background and border.
    * **Inactive Hardware Fit State**: 35% opacity, desaturated muted background, and a strikethrough or dim icon.

### 3.3 Filter Logic Verification
* Ensure `ToggleInputModality`, `ToggleOutputModality`, and `ToggleFitVerdict` immediately trigger search and local filtering without getting out of sync.
* Ensure pipeline tags (`image-to-video`, `text-to-video`, `text-to-image`, `text-generation`, `automatic-speech-recognition`, `text-to-speech`) map accurately to Hugging Face API filters.

---

## 4. Verification & Testing

* **Unit Tests**:
  * `DocumentationViewModelTests`: Verify step navigation, pop-out state toggle, and `TargetTab` properties.
  * `HuggingFaceSearchViewModelTests`: Verify initial model loading, modality toggle synchronization, and hardware filter states.
* **Tooling Checks**:
  * `npm run lint`: 0 ESLint errors.
  * `npx tsc --noEmit`: 0 TypeScript errors.
  * `dotnet test`: 100% pass across all unit and integration tests.
