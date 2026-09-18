# Application Capabilities Reference

LocalLLMServerManager provides unified orchestration and management across several core AI engines and subsystems:

## 1. LLM Engine (Ollama)
- **Port / Protocol**: `http://127.0.0.1:11434`
- **Features**:
  - Download and run GGUF quantized models (e.g. Llama 3, Qwen 2.5, DeepSeek, Mistral, Gemma).
  - List installed models, active running models (`/api/ps`), and tags (`/api/tags`).
  - One-click model pull from Ollama library or Hugging Face.
  - VRAM release (`keep_alive: 0`) to dynamically free GPU memory.

## 2. Image Generation Engines
- **Stable Diffusion Forge**:
  - Default URL: `http://127.0.0.1:7860`
  - High-performance web UI and API for SD1.5, SDXL, and Flux diffusion models.
  - Native support for ControlNet, LoRAs, and VAEs.
- **ComfyUI**:
  - Default URL: `http://127.0.0.1:8188`
  - Node-based modular execution graph for complex image and video workflows.
  - Supports custom workflow JSON files stored in `Workflows/`.

## 3. Video Generation (ComfyUI DiT)
- **Workflows**: Wan 2.2 (`wan2.2_t2v`), LTX-Video 2.5 (`ltx2.5_t2v`), and HunyuanVideo 1.5.
- **Hardware Requirement**: 8 GB to 24 GB VRAM depending on resolution and frame count.

## 4. Audio & Speech Engines
- **Kokoro TTS**:
  - Fast, lightweight text-to-speech engine on port `http://127.0.0.1:8880` or `7851`.
  - Quality voices such as `af_heart`, `am_adam`, `bf_alice`.
- **Music & SFX**:
  - Stable Audio Open 1.0 SFX and MusicGen melody synthesis via ComfyUI.

## 5. Hardware Fit Calculator ("Can I Run It")
- Accurately models model weight footprint + KV cache size across context lengths (2K, 4K, 8K, 32K, 128K).
- Generates fit badges:
  - `PERFECT FIT` (green): Completely fits in GPU VRAM with full layer offloading.
  - `TIGHT FIT` (amber): Fits with minimal VRAM headroom; lower context recommended.
  - `RAM OFFLOAD` (yellow): Partial GPU offload; remaining layers execute in system RAM.
  - `WILL NOT FIT` (red): Exceeds combined VRAM and system memory.

## 6. Remote LAN & Tunnel Access
- Allows sharing local inference over local LAN (`http://<host-ip>:5246`) or secure reverse proxies with CORS pre-configured.
