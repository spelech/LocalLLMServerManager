# Application Workflows Guide

This guide details recommended multi-step workflows in LocalLLMServerManager:

## Workflow 1: Pulling and Running an LLM
1. Verify system VRAM with `get_gpu_vram_telemetry`.
2. Check model fit using `calculate_hardware_fit`.
3. Check if Ollama is running via `check_services_health`; if stopped, start it via `start_ai_engine("ollama")`.
4. Run `pull_model(modelName)` or guide user to the Models tab to download from Hugging Face or Ollama Library.

## Workflow 2: Generating an Image
1. Inspect available VRAM. If an LLM is occupying VRAM and memory is low, call `unload_vram()` to free GPU memory.
2. Ensure Forge (`start_ai_engine("forge")`) or ComfyUI (`start_ai_engine("comfyui")`) is running.
3. Execute `generate_image(prompt, negativePrompt, engine, width, height)`.
4. Provide the generated media preview path or link to the user.

## Workflow 3: Video Generation with Wan 2.2
1. Video generation requires at least 8 GB VRAM.
2. Confirm ComfyUI is online.
3. Invoke `generate_video(prompt, workflow="wan2.2_t2v", width=832, height=480, frames=49)`.
4. Direct the user to the Workflows -> Video sub-tab to view progress.

## Workflow 4: Kokoro Speech Synthesis
1. Call `synthesize_speech(text, voice="af_heart")`.
2. The generated audio file will be saved under the application's audio output directory.

## Workflow 5: VRAM Orchestration & Engine Switching
- When switching from LLM chat to heavy diffusion (SDXL, Flux, Wan 2.2):
  1. Call `unload_vram()`.
  2. Start the target engine (Forge or ComfyUI).
  3. Run the generation workflow.
- When finished with image/video tasks, user can resume LLM inference immediately.
