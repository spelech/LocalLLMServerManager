# Design Specification: Fluid Studio & Docked Copilot UI Redesign

**Date:** 2026-09-29  
**Status:** Approved by User  
**Target:** LocalLLMServerManager Desktop (Avalonia) & Web (WASM)  

---

## 1. Overview & Problem Statement

### 1.1 The Problems
1. **Rigid, Intimidating UI:** The current Generation Studio uses rigid, nested 5-step numbered boxes ("Step 1 Check Engine, Step 2 Model, Step 3 Parameters, Step 4 Form, Step 5 Viewport") that feel like an administrative dashboard rather than an intuitive, creative AI tool.
2. **Cryptic Engine Toggles:** The top status bar uses bare toggle switches that lack status feedback, error context, or model awareness (e.g. failing silently when Ollama or ComfyUI is starting or misconfigured).
3. **Disjointed Drawers:** Documentation slides out from the far left and the AI Assistant slides out from the far right, covering work areas and behaving as disconnected features rather than an integrated companion.

### 1.2 The Solution: Fluid Studio & Docked Copilot
A clean, breathing interface inspired by modern creative AI applications (Cursor, ChatGPT canvas, Midjourney):
- **Informative Interactive Engine Cards** in the header.
- **Center Stage Creative Canvas** with top modality pills and a bottom chat-inspired prompt dock.
- **Unified "Copilot & Knowledge" Sidebar** docking the AI Assistant and Documentation seamlessly on the right.

---

## 2. Component Architecture & UI Layout

```
+---------------------------------------------------------------------------------------------------------+
| [LocalLLM]  [🟢 Ollama: qwen2.5]  [🟢 ComfyUI: SDXL]  [⚪ Forge: ▷]  [🟢 Kokoro: af_heart]  [VRAM 22%]  |
+----+-----------------------------------------------------------------------------+---------------------+
| ⚡ |  [🎨 Image]  [💬 Text]  [🎬 Video]  [🧊 3D Mesh]  [🎙️ Audio]                   | [Assistant]  [Docs] |
| 📦 | +-------------------------------------------------------------------------+ | ------------------- |
| 🌐 | |                                                                         | | 👋 Hi! All 4        |
| 🤗 | |                                                                         | | engines are ready.|
| 🖥️ | |               Generous Interactive Output Viewport                      | |                     |
| ⚙️ | |          (High-Res Canvas / 3D Viewer / Waveform / Video)               | | 💡 Fix blurry out |
|    | |                                                                         | | 📦 Recommend LoRA |
|    | +-------------------------------------------------------------------------+ |                     |
|    | | [🎨 Model: SDXL] [📎 Attach] [⚙️ Parameters]                             | | [Ask Copilot...]  |
|    | | Describe what you want to create...                       [Generate ↵] | |                     |
+----+-----------------------------------------------------------------------------+---------------------+
```

---

## 3. Detailed Specifications

### 3.1 Top Header: Interactive Engine Status Cards
Replaces bare toggle switches with rich, interactive pill cards:
1. **Pill Card Contents:**
   - Status Indicator Dot:
     - 🟢 **Online / Ready** (with soft glow effect)
     - 🟡 **Starting / Initializing** (pulsing amber)
     - ⚪ **Stopped / Offline** (muted slate with explicit `▷ Start` action)
     - 🔴 **Error / Attention** (crimson with tooltip diagnostic)
   - Engine Name & Active Model / Port (e.g. `Ollama: qwen2.5-coder`, `ComfyUI: Port 8188`, `Kokoro: af_heart`).
   - One-Click Action: Start, Stop, or Restart directly on the card.
2. **Click-to-Inspect Flyout:**
   - Clicking an engine card opens a lightweight popover showing:
     - Process ID, listening port, and uptime.
     - Memory / VRAM footprint.
     - Plain-English troubleshooting tips when offline (e.g., *"Port 11434 is available. Click Start to launch Ollama"*).
3. **Hardware Telemetry Meter:**
   - Right-aligned live GPU VRAM bar (`RTX 4090: 5.2 / 24 GB (22%)`) with color-graded stages (Green < 70%, Amber 70-90%, Red > 90%).

---

### 3.2 Center Canvas: The Generation Studio

#### 3.2.1 Modality Selector
- Top-anchored pills: `🎨 Image`, `💬 Text`, `🎬 Video`, `🧊 3D Mesh`, `🎙️ Audio`.
- Seamless switching adapts the viewport and prompt dock controls without reloading the page or losing current inputs.

#### 3.2.2 Uncluttered Viewport
- **Image Mode:** Pan/zoom high-resolution canvas with upscale, download, and "Send to Video/3D" actions.
- **Text Mode:** Conversational stream with markdown rendering and one-click code copying.
- **Video Mode:** Video player with frame scrubbing, playback speed, and loop toggle.
- **3D Mesh Mode:** Interactive WebGL/3D viewer with mouse orbit/pan/zoom for OBJ, GLB, and Gaussian Splat files.
- **Audio Mode:** Audio waveform visualizer with playback scrubber, volume control, and voice selector.
- **Zero-State Starters:** Friendly chips when idle (*"✨ Photorealistic Portrait"*, *"🎬 Animate an Image"*, *"🧊 3D Sci-Fi Asset"*).

#### 3.2.3 Creative Prompt Dock (Bottom)
- **Fluid Input Area:** Autosizing multi-line textarea replacing rigid textboxes. Supports `Ctrl+Enter` to generate.
- **Pill Bar:**
  - `Model Selector Pill`: Shows currently selected model with a fast dropdown switcher.
  - `Attachment Pill (`📎`)`: Drag-and-drop or file picker for image conditioning (Image-to-Image, Image-to-Video, Image-to-3D).
  - `Parameters Popover (`⚙️`)`: Clean slide-up/popover panel containing advanced sliders (Steps, CFG, Seed, Denoise, Aspect Ratio) so novices aren't overwhelmed by default.
  - `Generate Button`: High-contrast, prominent action button with loading spinner state.

---

### 3.3 Right Sidebar: Unified Copilot & Knowledge

Replaces the separate left (`DocumentationDrawer`) and right (`AiAssistantDrawer`) slide-out drawers with a docked, collapsible right pane:

#### 3.3.1 Tab 1: AI Assistant (The Copilot)
- **Live LLM Chat:** Powered by Ollama / LiteLLM with streaming token responses.
- **Context Awareness:** Automatically knows the active studio modality, loaded models, and engine health.
- **Novice Guidance Chips:**
  - *"Why is ComfyUI offline?"* &rarr; Diagnoses and provides a 1-click start.
  - *"Enhance my prompt"* &rarr; Suggests improved prompt and offers an "Insert into Dock" button.
  - *"Explain Steps & CFG simply"* &rarr; Plain-English explanation.
- **Tool Calling Badges:** Clean visual indicators when the assistant queries VRAM or controls engines.

#### 3.3.2 Tab 2: Documentation & Guides (Knowledge)
- **Markdown Reader:** Formatted guides with clear typography, callouts, and code blocks.
- **Instant Search:** Fuzzy search bar filtering guides and topics.
- **Copilot Bridge:** Every guide features an **`Ask Assistant about this`** button that sends the guide summary into the Assistant tab for interactive Q&A.

#### 3.3.3 Collapsibility & Pop-Out
- Can be collapsed with a toggle button or `Ctrl+B` shortcut.
- Retains native `Pop Out` window support for multi-monitor desktop users.

---

## 4. ViewModel & Service Integration

1. **`MainViewModel.cs`**:
   - Manages top-level layout state, active view, and right sidebar collapse (`IsCopilotOpen`).
2. **`TelemetryViewModel.cs`**:
   - Updates `EngineStatusCard` models for Ollama, ComfyUI, Forge, and Kokoro with live health check responses, active model names, and process IDs.
3. **`EngineStudioViewModel.cs`**:
   - Manages modality switching, prompt dock state, parameter popover visibility, and execution dispatch to `/api/generate/*` and `/api/workflows/*`.
4. **`AiAssistantViewModel.cs` & `DocumentationViewModel.cs`**:
   - Back the two tabs in the right sidebar. Includes bridging logic where documentation topics can be forwarded to the assistant chat.

---

## 5. Non-Functional Requirements & Verification

1. **Clean Code & Quality:**
   - Pass `npm run lint` and `npx tsc --noEmit` with 0 errors.
   - Pass existing unit & integration tests (`dotnet test LocalLLMServerManager.Tests`).
2. **Performance:**
   - Smooth 60fps UI transitions in Avalonia Desktop and WASM.
   - No blocking I/O on the UI thread during engine status polling.
3. **Novice Usability:**
   - Every technical term (CFG, VAE, LoRA, KSampler) provides tooltip or copilot explanations.
   - All engines clearly communicate their running/stopped/starting status.
