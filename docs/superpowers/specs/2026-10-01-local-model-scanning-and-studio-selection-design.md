# Design Specification: Local Model Scanning & Multi-Studio Selection

## Overview
This specification addresses the limitation where the application displays hardcoded model names (such as "SDXL Base 1.0") across the Studio Canvas and restricts the "Downloaded & Manage" tab to Ollama-only models. It introduces dynamic discovery of local diffusion models, LoRAs, video pipelines, audio voices, and 3D assets from disk and user-configured paths (Forge, ComfyUI, etc.), provides a unified multi-modality management UI, and connects scanned models to interactive selectors in the Studio Prompt Dock and Parameters Flyout.

---

## Architecture & Data Flow

### 1. Model Discovery Service: `LocalModelScannerService`
- **Interfaces / Service**: `ILocalModelScannerService` and `LocalModelScannerService` in `LocalLLMServerManager.Shared.Services`.
- **Model Representation**: `LocalModelItem`
  - `Id`: Unique string or normalized path
  - `Name`: Display name / filename without extension
  - `FileName`: Full filename (e.g. `juggernautXL_v9.safetensors`)
  - `FullPath`: Absolute path on disk
  - `Modality`: Modality enum / string (`Image`, `Video`, `Audio`, `3DMesh`, `Lora`)
  - `Architecture`: Detected architecture family (`SDXL`, `SD 1.5`, `Flux`, `Pony`, `Wan`, `LTX`, `Kokoro`, etc.)
  - `SizeBytes`: File length in bytes
  - `FormattedSize`: Human-readable size string (e.g. `6.46 GB`)
  - `SourceLocation`: Origin description (`Local Models`, `Forge Checkpoints`, `ComfyUI Checkpoints`, `LoRAs`, etc.)
  - `CreatedAt`: File creation timestamp
- **Scanning Logic**:
  - Scans `models/checkpoints`, `models/Lora`, `ComfyUI/models/diffusion_models`, `models/3d`, `models/tts`, `Workflows/Video`, `Workflows/Audio`, `Workflows/`.
  - Also inspects paths configured in `AppSettings` (`ForgeModelsPath`, `ComfyModelsPath`, `VideoModelsPath`, `ThreeDModelsPath`, `AudioPath`, `WorkflowsPath`).
  - Supports standard file extensions: `.safetensors`, `.ckpt`, `.json` (for workflows).
  - Architecture detection via heuristic inspection of filename and subfolders.

### 2. Multi-Modality "Downloaded & Manage" Hub (`ModelsTabControl.axaml`)
- Upgrades Subtab 1 of `ModelsTabControl.axaml` to host a unified manager or category sub-navigation:
  - **🦙 Ollama LLMs**: Keeps the existing `OllamaModelsTabControl` with KV Cache calculator and parameter inspectors.
  - **🎨 Image & Diffusion Checkpoints**: Grid/cards of installed `.safetensors`, `.ckpt`, and LoRAs.
    - Displays: Name, Size, Architecture, Directory path.
    - Action: **"🎨 Use in Studio"** (switches to Studio tab and selects this model), **"📂 Reveal"** (opens directory), **"🗑️ Delete"** (deletes via API).
  - **🎬 Video & Workflows**: Grid of video generation workflows and diffusion models with "Use in Studio".
  - **🎙️ Audio & Voice Models**: Installed Kokoro voices and audio synthesis models.
  - **Top Actions**: Global **"🔄 Scan / Refresh"** button to rescan disk immediately.

### 3. Studio Interactive Model Selector
- **EngineStudioViewModel & MainViewModel**:
  - Expose `AvailableImageModels`, `AvailableVideoModels`, `AvailableAudioModels`, `AvailableMeshModels`, `AvailableTextModels`.
  - When models are scanned or updated, these collections refresh automatically.
  - Fallback starter options always available even if no local files are downloaded yet.
- **Creative Prompt Dock Pill (`EngineStudioTabControl.axaml`)**:
  - Replace static `Border` with a clickable `Button` styled as a pill (`Classes="model-selector-pill"`).
  - Clicking opens a `Flyout` with a searchable list or clean dropdown of available models for the current modality.
  - Selecting an item updates `ActiveModelBadge`, `SelectedImageWorkflow` / `ImageModel`, and triggers a toast notification.
- **Fine-Tuning Parameters Flyout**:
  - Add a dedicated **Model / Checkpoint Selector** directly above the parameter sliders.

---

## Verification & Testing
- Unit tests for `LocalModelScannerService` ensuring directory scanning, path safety, and architecture detection.
- ViewModel tests verifying `ActiveModelBadge` updates and collection bindings.
- UI compile verification and typecheck/lint check (`npm run lint` and `npx tsc --noEmit`).
