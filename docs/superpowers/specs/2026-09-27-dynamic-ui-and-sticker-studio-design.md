# Dynamic UI Workspace & Sticker Studio Design

## 1. Overview & Goals

The Local LLM Server Manager UI is transitioning from rigid stock Avalonia horizontal tabs into a dynamic, responsive workspace. While both Desktop and Web (WASM) consume the same screen real estate, they have distinct windowing constraints and user interaction models:
- **Desktop**: Benefits from multi-window OS primitives, native file drag-and-drop, and detachable snapped companion windows.
- **Web (WASM)**: Confined to a single browser viewport; requires fluid in-canvas responsive transitions, non-modal slide-out drawers, and browser-safe file selection.

To anchor this dynamic architecture, we are introducing the **Sticker Studio**: an end-to-end creative workflow where users drop in reference images, select or customize sticker art styles, and generate die-cut transparent PNG stickers.

---

## 2. App Shell Architecture & Navigation

### 2.1. Activity Rail (`ActivityRailControl.axaml`)
Replaces the horizontal tab bar with a sleek, vertical navigation rail on the left flank:
- **Collapsed Width**: 56px (icon-only mode with tooltips, maximizing main canvas space).
- **Expanded Width**: 200px (slide-out drawer displaying text labels and keyboard shortcuts).
- **Navigation Groups**:
  - **Primary Domains (Top)**:
    - ⚡ **Studio / Workflows** (`StickerStudio`, Image, Text, Video, 3D Mesh)
    - 📦 **Models & Hubs** (Ollama, Hugging Face, Civitai)
    - 💻 **Hardware Fit** (Can I Run It calculator)
    - ⚙️ **Settings & Daemon** (Caddy, remote access, themes)
  - **Utilities (Bottom)**:
    - 📖 **Documentation**
    - 🤖 **AI Assistant**
    - ◀ **Collapse / Expand Toggle**

### 2.2. Integrated Telemetry Status Ribbon
Replaces the bulky 90px multi-card header with a compact 34px status ribbon:
- **Desktop**: Embedded directly in the custom titlebar (`MainWindow.axaml`).
  - Service status indicator dots: Ollama (🟢), Forge (🟢), ComfyUI (🟡).
  - Compact VRAM badge (e.g., `RTX 4090: 6.2 / 24 GB`) with expandable details flyout.
- **Web (WASM)**: Positioned at the top of `MainView.axaml` with identical status indicators and quick-drawer toggles.

### 2.3. Platform-Specific Side Companion Routing
- **Desktop**: Clicking Documentation or AI Assistant launches or focuses docked native OS companion windows managed via `WindowSnapManager`. The primary canvas retains full screen width.
- **Web (WASM)**: Companion tools slide out as non-modal, in-canvas drawers or split panes without blocking user interaction with the canvas.

---

## 3. Dynamic Dual-Stage Canvas & Responsive Breakpoints

### 3.1. Stage Layout Modes
The workspace to the right of the rail dynamically adapts based on active context:
1. **Dual-Stage Mode (Studio)**:
   - **Column 1 — Input Deck (380px, responsive shrink to 320px)**: Dedicated to image drop zones, art style presets, prompt tuning, and launch controls.
   - **Column 2 — Output Preview Canvas (Flexible `*`)**: Displays the live rendering canvas, alpha checkerboard background, die-cut contour preview, and export controls.
2. **Full-Bleed Stage (Management Modes)**:
   - For Models, Hardware Fit, and Settings, the stage provides a single wide canvas (`*`) for data tables, model cards, and settings forms.

### 3.2. Responsive Breakpoints

| Viewport Width | Desktop Behavior | Web (WASM) Behavior |
| :--- | :--- | :--- |
| **Widescreen (≥ 1200px)** | Dual columns side-by-side (`380px, *`). Companions snap outside to window flanks. | Dual columns side-by-side. Utility drawers compress canvas smoothly to `340px, *` without clipping. |
| **Tablet / Medium (900px – 1199px)** | Dual columns condense to `320px, *`. | Canvas displays segmented toggle pills: `[Input Deck]` / `[Output Canvas]` to prevent squishing. |
| **Compact (< 900px)** | Window minimum width constrained to 768px; rail collapses to icon-only (56px). | Single-column stacked cards; drawers slide up as bottom sheets. |

---

## 4. Sticker Studio: Image-to-Sticker Workflow

### 4.1. Input Deck (`StickerStudioControl.axaml`)
1. **Drop Zone (`ImageDropZoneControl.axaml`)**:
   - Supports native drag & drop (`DragDrop.DropHandler`) on Desktop and file picker / HTML5 drop on Web.
   - Accepts PNG, JPG, WebP images up to 25MB.
   - Displays a preview thumbnail with replace and clear buttons.
2. **Art Style Preset Chips**:
   - Fast 1-click starter chips injecting curated positive and negative tokens:
     - 🏷️ **Die-Cut Vinyl**: `bold white die-cut border, vector sticker, clean lineart, glossy finish, solid white background`
     - 🌈 **Holographic**: `holographic foil sticker, iridescent rainbow sheen, metallic edge, prismatic reflections`
     - 👾 **Chibi Anime**: `chibi kawaii sticker, oversized head, bold clean lines, cel shaded, sticker cutout`
     - 📼 **80s Retro**: `retro 80s synthwave sticker, neon cyan and magenta, halftone dots, badge contour`
     - 🎨 **Pop Art**: `pop art comic sticker, bold ink outlines, vibrant flat colors, dot pattern`
     - 🖌️ **Watercolor**: `watercolor illustration sticker, soft pigment bleeding, crisp white border`
3. **Contour & Parameter Controls**:
   - **Auto-Cutout Toggle**: Enables automatic subject segmentation and background removal.
   - **Die-Cut Border Slider**: Adjustable white contour border thickness (0px – 24px).
   - **Custom Style Prompt**: Text input allowing freeform style additions.

### 4.2. Output Preview Canvas
1. **Alpha Checkerboard Viewer**:
   - Renders the generated sticker over an alternating dark/light checkerboard grid, highlighting transparency and die-cut borders.
2. **Generation Pipeline Status**:
   - Shows active stage: `[1. Diffusion Generation] ➔ [2. Subject Isolation] ➔ [3. Contour Application] ➔ [4. Ready]`.
3. **Export Actions**:
   - 📋 **Copy PNG**: Copies 32-bit RGBA PNG with alpha directly to OS clipboard.
   - 💾 **Save File**: Exports transparent PNG to `outputs/stickers/` or triggers browser download.

---

## 5. ViewModels, Services & Component Interfaces

### 5.1. ViewModels
- **`NavigationRailViewModel`**: Manages selected domain (`Studio`, `Models`, `HardwareFit`, `Settings`), rail collapsed/expanded state, and companion triggers.
- **`TelemetryRibbonViewModel`**: Drives status indicator dots and compact VRAM telemetry metrics.
- **`StickerStudioViewModel`**:
  - `InputImagePath`, `InputBitmap`
  - `SelectedStylePreset`, `CustomPrompt`, `NegativePrompt`
  - `BorderWidth`, `IsAutoCutoutEnabled`
  - `GeneratedStickerBitmap`, `IsGenerating`, `CurrentStage`
  - Commands: `DropImageCommand`, `SelectStylePresetCommand`, `GenerateStickerCommand`, `CopyStickerCommand`, `SaveStickerCommand`.

### 5.2. Service Abstractions
- **`IStickerGenerationService`**: Dispatches requests to local diffusion engines (WebUI Forge / SDXL / ComfyUI) with automatic contouring, background removal, and offline engine detection.
- **`IClipboardService`**: Cross-platform RGBA PNG clipboard integration.

---

## 6. Testing & Layout Verification

1. **`Avalonia.LayoutInspector` Audits**:
   - Tests `ActivityRailControl`, `DynamicStageContainerControl`, and `StickerStudioControl` across:
     - Desktop widescreen: 1440 × 900
     - Tablet landscape: 1024 × 768
     - Web drawer active: 1280 × 800 with 320px drawer open
   - Asserts:
     - `CheckBoundaryOverflow = true` (0 elements clipped outside container bounds)
     - `CheckSiblingCollisions = true` (0 overlapping peer controls)
     - `CheckTextClipping = true` (0 truncated labels without ellipsis)
2. **Unit Tests**:
   - `StickerStudioViewModelTests`: Verifies style preset token injection, border parameter clamping, and engine offline fallback state.
   - `NavigationRailViewModelTests`: Verifies rail expansion toggle and active domain switching.
