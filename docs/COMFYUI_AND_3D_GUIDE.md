# ComfyUI & 3D Mesh Generation Setup Guide

This guide explains how to set up **ComfyUI** with **3D Mesh Generation nodes** (TRELLIS V2 and Hunyuan3D v2) and connect it to **LocalLLMServerManager**.

---

## 🚀 Overview

`LocalLLMServerManager` acts as a central proxy, VRAM orchestrator, and interactive WebGL 3D asset studio. It connects to your local ComfyUI instance running on `http://127.0.0.1:8188`.

### Key Benefits
- **Automated VRAM Offloading**: Automatically unloads Ollama LLM models from GPU memory before launching heavy ComfyUI 3D mesh or FLUX image generation workflows.
- **Interactive 3D WebGL Canvas**: Renders generated `.glb` and `.gltf` 3D meshes natively in the browser with 360° orbital controls, wireframe toggles, and direct downloading.
- **Pre-Configured Workflow Presets**: Ships with API-format workflow JSON templates for TRELLIS V2, Hunyuan3D v2, and FLUX / SDXL image generation.

---

## 🛠 Prerequisites & Installation

### 1. ComfyUI Setup
Ensure ComfyUI is installed on your local machine:
- Download & install [ComfyUI](https://github.com/comfyanonymous/ComfyUI).
- Launch ComfyUI using `run_nvidia_gpu.bat` (or your preferred launch script). ComfyUI defaults to `http://127.0.0.1:8188`.

### 2. Installing 3D Mesh Nodes
To run 3D generation workflows, install the relevant custom nodes via [ComfyUI-Manager](https://github.com/ltdrdata/ComfyUI-Manager):

#### A. TRELLIS V2 Nodes
- Search for and install `ComfyUI-Trellis` or `TRELLIS` custom nodes.
- Place TRELLIS weights inside `ComfyUI/models/trellis/` or `models/checkpoints/`.

#### B. Hunyuan3D v2 Nodes
- Search for and install `ComfyUI-Hunyuan3DWrapper` or `Hunyuan3D-v2`.
- Download Hunyuan3D v2 weights into `ComfyUI/models/hunyuan3d/`.

---

## ⚙️ Connecting to LocalLLMServerManager

1. Open the manager dashboard at `http://localhost:5246`.
2. Navigate to the **3D & ComfyUI Studio** tab.
3. Check the **ComfyUI** health badge in the top-right header:
   - 🟢 **Online**: ComfyUI is running and responding at `127.0.0.1:8188`.
   - 🔴 **Offline**: ComfyUI is not running. Launch ComfyUI or update the endpoint URL in the configuration bar.
4. (Optional) In the configuration bar, set your **Preferred Image & Mesh Engine** to **ComfyUI** or **Stable Diffusion / Forge**.

---

## 🎨 Running 3D Workflows

1. Select your target preset:
   - **TRELLIS V2 (3D Mesh Generator)**
   - **Hunyuan3D v2 (3D Mesh Generator)**
   - **FLUX / SDXL (High-Res Image Generator)**
2. Customize the prompt in the text box (e.g. `a futuristic cyberpunk sports car, 3d asset, white background`).
3. Click **🚀 Queue Generation (Auto-Free LLM VRAM)**.
4. The status box will show progress as the server automatically unloads loaded Ollama LLM models from GPU VRAM and posts the workflow payload to ComfyUI.
5. Once complete, click any generated `.glb` asset in the **Generated 3D Mesh Assets** gallery to load it interactively in the WebGL 3D viewer!

---

## 📤 Custom Workflow Uploads

You can export custom workflows from ComfyUI for use in `LocalLLMServerManager`:
1. In ComfyUI, enable **"Enable Dev mode Options"** in settings.
2. Click **"Save (API Format)"** to export the workflow as a JSON file.
3. Save the JSON file into the `Workflows/` directory inside `LocalLLMServerManager`.
4. The workflow will automatically appear in the preset dropdown menu!
