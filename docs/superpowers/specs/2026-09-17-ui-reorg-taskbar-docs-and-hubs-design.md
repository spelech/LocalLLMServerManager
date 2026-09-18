# Window Taskbar, Tab Reorganization, Hub Enhancements & ASD-STE100 In-App Documentation Design Specification

**Date:** 2026-09-17  
**Branch:** `feat/ui-reorg-taskbar-docs-and-hubs`

## 1. Overview & Problem Statement
The user has identified several core usability and navigation deficiencies in the LocalLLMServerManager desktop application:
1. **Window Movement / Dragging:** The desktop window cannot be dragged or moved across screens because `ExtendClientAreaToDecorationsHint="True"` removed the OS window title bar without providing a custom draggable taskbar/titlebar with window controls (minimize, maximize/restore, close).
2. **Hugging Face Hub Search Feedback:** Hugging Face model search lacks a loading indicator, creating uncertainty about whether a query is processing or results have updated.
3. **CivitAI Initial State:** CivitAI model search initializes as a completely blank view with no starter suggestions or initial models, leaving users unsure what queries to run.
4. **Overcrowded 3D & ComfyUI Tab:** The Studio tab is overcrowded with stacked controls. The layout must be reorganized into a clean two-level hierarchy:
   - **Models:** Downloaded & Manage (Ollama), Hugging Face Hub, CivitAI Hub.
   - **Workflows:** Step-by-step pathways for Image, Text, Video, 3D, and Audio synthesis.
   - **Can I Run It:** Hardware capability and VRAM budgeting calculator.
   - **Documentation:** Dedicated in-app guide written in ASD-STE100.
   - **Settings:** Engine configuration, remote access, feature packs, dynamic theming.
5. **In-App ASD-STE100 Documentation:** The application lacks structured, clear, sequential user guidance explaining how to operate each tab and execute generation pipelines.

---

## 2. Technical Architecture & Component Design

### 2.1 Window Titlebar / Taskbar (`Views/MainWindow.axaml` & `MainWindow.axaml.cs`)
* **Visual Structure:** A 36px high glassmorphic header at the top of the window containing:
  - App Icon (18x18 icon).
  - Application Title: `Local LLM Server Manager` with a subtle version badge (`v3.13.1`).
  - Spacer region marked for window drag.
  - Window control button cluster:
    - **Minimize (`—`):** Sets `WindowState = WindowState.Minimized`.
    - **Maximize / Restore (`🗖` / `❐`):** Toggles between `WindowState.Maximized` and `WindowState.Normal`.
    - **Close (`✕`):** Calls `Close()`, which triggers `OnClosing` (minimizing to tray per existing behavior).
* **Pointer Interaction:** 
  - `PointerPressed` on the title bar background initiates `BeginMoveDrag(e)`.
  - Double click on the title bar toggles maximize / restore.

### 2.2 Hugging Face Hub Search Feedback (`HuggingFaceSearchViewModel.cs` & `HuggingFaceTabControl.axaml`)
* **State Management:**
  - Add `[ObservableProperty] private bool _isLoading = false;`
  - In `SearchHuggingFaceAsync(string apiBase, HttpClient http)`:
    Wrap the query execution in `try { IsLoading = true; ... } finally { IsLoading = false; }`.
* **View Indicators:**
  - An indeterminate `ProgressBar` is displayed immediately below the search bar when `IsLoading` is true.
  - Search button displays a busy state (`⏳ Searching...`) and disables duplicate clicks while `IsLoading` is active.
  - Empty-state helper text is displayed when results are empty and `!IsLoading`.

### 2.3 CivitAI Hub Starter Content & Suggestions (`CivitaiSearchViewModel.cs` & `CivitaiTabControl.axaml`)
* **State & Initial Data:**
  - Add `[ObservableProperty] private bool _isLoading = false;`
  - Add `StarterCategories` collection containing curated high-value categories:
    `"🌟 Popular Checkpoints"`, `"📸 Photorealistic"`, `"🎨 Anime & Illustration"`, `"⚡ SDXL / Turbo"`, `"🎭 Style LoRAs"`, `"🔮 Flux Checkpoints"`.
  - Provide fallback / starter models on initialization (e.g. `Juggernaut XL`, `Realistic Vision V6.0`, `DreamShaper XL`, `Animagine XL V3`) so the grid is never blank on first view.
  - Provide `ApplyStarterKeywordCommand` to execute instant searches when a chip is clicked.
* **View Indicators:**
  - Filter chip row for starter suggestions.
  - Indeterminate `ProgressBar` and loading badge during network queries.

### 2.4 Tab Reorganization & Multi-Step Workflow Layout
Reorganize the top-level `TabControl` into 5 clear domains:
1. **📦 Models**
   - Sub-tab A: **Downloaded & Manage** (`OllamaModelsTabControl` with installed models, KV cache calculator, model deletion).
   - Sub-tab B: **Hugging Face Hub** (`HuggingFaceTabControl` with loading states and quantization browser).
   - Sub-tab C: **CivitAI Hub** (`CivitaiTabControl` with starter suggestions and download manager).
2. **⚡ Workflows (Studio)**
   Organized with a clean sub-tab or selector bar into 5 distinct modalities:
   - **🎨 Image:** Stable Diffusion / Forge + ComfyUI image presets. Step-by-step pathway: Step 1 Engine & Model ➔ Step 2 Prompt & Preset ➔ Step 3 Generate ➔ Step 4 Output Gallery.
   - **💬 Text:** Ollama text generation & prompts. Step 1 Model ➔ Step 2 Prompt ➔ Step 3 Response.
   - **🎬 Video:** Wan 2.2, LTX-2.5, HunyuanVideo. Step 1 Workflow & Resolution ➔ Step 2 Prompt & Frame Count ➔ Step 3 Generate ➔ Step 4 Video Player Preview.
   - **📦 3D:** Trellis V2 & Hunyuan3D V2 API. Step 1 Pipeline ➔ Step 2 Seed & Prompt ➔ Step 3 Render ➔ Step 4 3D Mesh Output.
   - **🎵 Audio:** Kokoro TTS, AllTalk XTTS, Stable Audio Open. Step 1 Voice/Workflow ➔ Step 2 Prompt/Text ➔ Step 3 Synthesize ➔ Step 4 Waveform & Playback.
3. **⚡ Can I Run It:** Pre-flight hardware compatibility calculator.
4. **📖 Documentation:** In-app ASD-STE100 guide.
5. **⚙️ Settings:** Configuration, tool discovery, feature packs, dynamic theming.

### 2.5 In-App ASD-STE100 Documentation (`DocumentationTabControl.axaml` & `DocumentationViewModel.cs`)
ASD-STE100 (Simplified Technical English) requires:
- Restricting sentence length (< 20 words for instructions).
- Using active imperative verbs (`Select`, `Click`, `Enter`, `Start`, `Stop`).
- Exactly one instruction per sentence.
- Sequential numbered steps (`Step 1: ...`, `Step 2: ...`).
- Clear prerequisite statements and explicit result descriptions.

The in-app documentation module includes 7 comprehensive topics:
1. **Quick Start Guide:** System overview, server startup, navigation.
2. **Image Generation Flow:** How to start Forge/ComfyUI, select presets, enter prompts, and save images.
3. **Text / LLM Workflow:** How to download Ollama models, allocate VRAM, and generate text responses.
4. **Video Generation Flow:** How to configure resolution, frame counts, seeds, and preview video renders.
5. **Audio & Speech Synthesis:** How to generate speech with Kokoro TTS and sound effects with Stable Audio.
6. **3D Mesh Generation Flow:** How to render 3D GLB assets using Trellis and Hunyuan3D.
7. **Model Management & Hardware Fit:** How to check VRAM compatibility, download from Hubs, and delete models safely.
