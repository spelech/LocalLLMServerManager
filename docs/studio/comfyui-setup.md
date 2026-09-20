---
title: ComfyUI & 3D Nodes Setup
description: Step-by-step setup guide for connecting ComfyUI, installing 3D nodes (TRELLIS V2 and Hunyuan3D v2), and exporting API workflow presets.
outline: deep
---

# ComfyUI & 3D Nodes Setup Guide

This guide describes how to configure **ComfyUI** and install 3D mesh reconstruction nodes for use with Local LLM Server Manager.

---

## Prerequisites

Verify these requirements before installation:
- NVIDIA GPU with at least 12 GB VRAM for 3D mesh generation.
- Python 3.10 or 3.11 installed.
- Git command-line tool installed.

---

## 1. Install ComfyUI

1. Download the official standalone package from the [ComfyUI repository](https://github.com/comfyanonymous/ComfyUI), or clone the source repository:
   ```bash
   git clone https://github.com/comfyanonymous/ComfyUI.git
   cd ComfyUI
   ```
2. Install Python dependencies:
   ```bash
   pip install -r requirements.txt
   ```
3. Start ComfyUI:
   - On Windows: Double-click `run_nvidia_gpu.bat`.
   - On Linux: Run `python main.py --listen 127.0.0.1 --port 8188`.
4. Verify that ComfyUI opens at `http://127.0.0.1:8188`.

---

## 2. Install 3D Mesh Generation Nodes

Install custom nodes for **TRELLIS V2** and **Hunyuan3D v2**:

### Option A: Use ComfyUI-Manager (Recommended)
1. Install [ComfyUI-Manager](https://github.com/ltdrdata/ComfyUI-Manager) if ComfyUI-Manager is not yet installed.
2. In the ComfyUI web interface, click **Manager** in the side panel.
3. Click **Custom Nodes Manager**.
4. Search for and install:
   - `ComfyUI-Trellis` (TRELLIS V2 Image-to-3D node)
   - `ComfyUI-Hunyuan3DWrapper` (Hunyuan3D v2 Text/Image-to-3D node)
5. Restart ComfyUI.

### Option B: Manual Git Clone
1. Navigate to the `custom_nodes/` directory in ComfyUI:
   ```bash
   cd custom_nodes
   git clone https://github.com/JeffreyXiang/ComfyUI-TRELLIS.git
   git clone https://github.com/Tencent/Hunyuan3D-2.git
   ```
2. Install requirements for each custom node package.
3. Restart ComfyUI.

---

## 3. Connect ComfyUI to Local LLM Server Manager

1. Open **Local LLM Server Manager**.
2. Select the **Settings** tab.
3. In the **Engine Paths & URLs** panel:
   - Set **ComfyUI URL** to `http://127.0.0.1:8188`.
   - Set **ComfyUI Launch Script** to your `run_nvidia_gpu.bat` path.
   - Set **ComfyUI Models Directory** to your `ComfyUI/models` folder.
4. Click **Save Settings**.
5. Check the **ComfyUI** health indicator in the header. Confirm that the status shows 🟢 **Online**.

```mermaid
flowchart LR
    Manager["Local LLM Server Manager (:5246)"] -->|Unload LLM VRAM| Ollama["Ollama (:11434)"]
    Manager -->|POST /prompt (API JSON)| ComfyUI["ComfyUI (:8188)"]
    ComfyUI -->|Generate .glb Mesh| Storage["Output Folder"]
    Storage -->|Serve WebGL Stream| Canvas["Interactive 3D Canvas"]
```

---

## 4. Export Custom Workflow Presets

You can export any ComfyUI node graph as a preset for the manager:

1. Open ComfyUI in your web browser (`http://127.0.0.1:8188`).
2. Open the ComfyUI settings dialog (gear icon).
3. Enable **Enable Dev mode Options**.
4. Construct your generation workflow.
5. Click **Save (API Format)**. ComfyUI downloads an API-format JSON file.
6. Copy the JSON file into the `Workflows/` directory in Local LLM Server Manager.
7. The preset appears in the Studio workflow dropdown menu immediately.

---

## Related Documentation

- [3D Mesh Reconstruction Studio](./3d-mesh.md)
- [Video Generation Studio](./video-generation.md)
- [Multimodal Studio Overview](./index.md)
