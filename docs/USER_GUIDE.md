# Local LLM Server Manager — Detailed User Guide

Welcome to the **Local LLM Server Manager**. This guide will walk you through the four main tabs of the dashboard, showing you how to manage your local AI engines (Ollama, Stable Diffusion / Forge, and ComfyUI), configure your settings, and successfully generate text, images, and 3D models.

---

## 1. My Models (Dashboard & VRAM Orchestrator)

The **My Models** tab is your home base for monitoring your system's hardware and active language models.

### Features:
* **VRAM Monitor:** At the top of the screen, you will see a visual representation of your GPU's VRAM. It accurately reads your hardware (e.g. `NVIDIA GeForce RTX 4070 Ti SUPER — 16 GB`) and shows a stacked bar representing free vs. used memory.
* **Model Capabilities Profile:** Installed models are grouped and tagged with capabilities (e.g., *Coding*, *Reasoning*, *Chat*). 
* **KV Cache Estimator:** Click on a model to view its details, where you can use the **Context Length Slider** to estimate how much VRAM a specific context length (up to 32K tokens) will require.
* **VRAM Orchestrator:** If your GPU gets full, the orchestrator will automatically unload idle text models when you try to run heavy image/video generations in the background.

**How to Use:**
To run a model in a frontend chat UI (like Open WebUI or LibreChat), simply select the model there. The Local LLM Server Manager proxy will wake up Ollama on port `11434` and transparently route the generation request while updating your VRAM usage bar live.

---

## 2. Find & Download Models

This tab integrates directly with the **Hugging Face Hub (GGUF)** and the **Ollama Official Library** so you can pull models natively without touching the command line.

### Sub-tabs:
* **Hugging Face Hub (GGUF):** Search for community-quantized models. 
  * *Sample search:* Type `DeepSeek-R1-Distill-GGUF` or `Llama-3.2`. 
  * Click on a model repository to view a list of quantization sizes (e.g., `Q4_K_M` which is a good balance of size and quality, or `Q8_0` for high fidelity).
  * Select your desired `.gguf` file and hit **Pull Selected**. The dashboard will stream the download progress directly to your screen.
* **Ollama Library:** Provides one-click pulls for the most popular models.
  * *Sample models:* Click **Pull 14B (7.9 GB)** under the `phi3` card, or **Pull 7B (4.7 GB)** under `qwen2.5-coder`.

**Changing Settings (Multiple Models):**
Ollama can run multiple models simultaneously. To do this, edit your system environment variables and set `OLLAMA_MAX_LOADED_MODELS=3`. 

---

## 3. Stable Diffusion (CivitAI Integration)

The Stable Diffusion tab allows you to configure your Forge engine and seamlessly download new image generation assets from **CivitAI**.

### Configuring Your Engine:
1. At the top of the tab, look for **Forge / SD Models Directory**.
2. Type in your absolute path (e.g. `C:\AI\SD_Forge\models`).
3. Click **Save Path**. This updates your `settings.json` so downloads go exactly where they need to.
4. You can boot or stop the SD Forge engine directly using the **Boot SD Forge** and **Stop SD Forge** UI controls. The app relies on Win32 Job Objects to manage these background processes cleanly.

### CivitAI Search & Download:
* **Search:** Look up popular models. *Sample text:* `"RealisticVision"`, `"DreamShaper"`, or `"Flux"`.
* **Filters:** Use the dropdowns to search by type (*Checkpoint, LoRA, VAE, Embedding*) or Sort by *Highest Rated*.
* **Download:** Click on a result to view its details and thumbnail. Choose the specific version you want, and click **⬇ Download to Forge**. The file will stream directly to your configured directory.

---

## 4. 3D, Video & ComfyUI Studio

This studio tab provides a powerful frontend for interacting with your local ComfyUI installation, complete with a built-in interactive WebGL 3D canvas viewer.

### Configuring Your Studio:
1. Verify the **ComfyUI Service Endpoint** at the top is correct (defaults to `http://127.0.0.1:8188`).
2. Set your **Preferred Image & Mesh Generator Engine** (toggle between Stable Diffusion/Forge or ComfyUI).
3. Use the **▶ Boot Engine** button to lazily boot up ComfyUI in the background.

### Running a Workflow:
1. **Select a Preset Workflow:** Choose from the dropdown (e.g. `TRELLIS V2 (3D Mesh Generator)`, `Hunyuan3D v2`, or `FLUX / SDXL`).
2. **Generation Prompt:** Enter a detailed prompt describing what you want.
   * *Sample 3D Prompt:* `"A low-poly stylized wooden treasure chest with glowing blue runes and gold trim"`
   * *Sample Image Prompt:* `"A futuristic neon-lit cyberpunk street alleyway in Tokyo, raining, 8k resolution, photorealistic"`
3. Click **🚀 Queue Generation**. 

Behind the scenes, the VRAM orchestrator will automatically free up any LLM models you have loaded if it needs the space. Once ComfyUI finishes the job, the result will automatically populate in your **Recent Media Gallery**. If you generated a `.glb` or `.gltf` 3D mesh, it will load into the **Interactive WebGL 3D Viewer** where you can rotate it 360°, toggle wireframes, and export it!

---

## 5. Cross-Platform Linux & Remote SSH Workflow

Local LLM Server Manager supports native Linux execution and remote SSH viewing.

### Running on Linux Desktop (Native GUI)
Launch the desktop application directly from your Linux terminal or application menu:
```bash
localllmmanager
```
This opens the native Avalonia UI dark desktop window on X11 and Wayland display environments.

### Working Remotely over SSH
To access all features of the manager from a remote machine:
1. Establish an SSH connection with port forwarding for port `5246`:
   ```bash
   ssh -L 5246:localhost:5246 user@linux-host
   ```
2. Start the headless service on the remote Linux host:
   ```bash
   sudo systemctl start localllmmanager
   # or run directly: dotnet run -- --service
   ```
3. Open `http://localhost:5246` in your local client browser. You get 100% of the UI functionality (VRAM telemetry, Hugging Face search, CivitAI downloader, 3D WebGL viewer) at native speed with zero lag over SSH.

