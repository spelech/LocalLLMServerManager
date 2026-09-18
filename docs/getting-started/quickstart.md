# Quickstart Guide

This tutorial guides you through downloading your first language model and sending your first prompt.

---

## Prerequisites

Verify these prerequisites before you begin:
- Local LLM Server Manager is running and shows a green health indicator.
- The Ollama engine is installed and active on port `11434`.
- Your system has at least 8 GB of available system memory or GPU VRAM.

---

## Step 1: Open the Model Download Tab

Open the model search interface:

1. Open the application window or navigate to `http://localhost:5246` in your web browser.
2. Click the **Find & Download Models** tab on the navigation bar.

---

## Step 2: Download Your First Model

Choose one of two methods to download a language model.

### Option A: Download from the Ollama Library (Fastest)

1. Click the **Ollama Library** sub-tab.
2. Choose one of the recommended starter models:
   - **`llama3.2:latest`** (2.0 GB): Lightweight model for general conversation.
   - **`qwen2.5-coder:7b`** (4.7 GB): High-performance model for coding tasks.
3. Click the **Pull** button on your chosen model card.
4. Observe the progress bar as the system streams the model layers to disk.

> [!TIP]
> The `qwen2.5-coder:7b` model delivers high code generation accuracy while using under 6 GB of VRAM.

---

### Option B: Download from Hugging Face Hub (GGUF)

1. Click the **Hugging Face Hub (GGUF)** sub-tab.
2. Type a repository name in the search box (for example: `Qwen/Qwen2.5-Coder-7B-Instruct-GGUF`).
3. Press `Enter` to search the Hugging Face Hub.
4. Select your preferred quantization file from the repository file list (for example: `qwen2.5-coder-7b-instruct-q4_k_m.gguf`).
5. Click **Pull Selected**.
6. Wait for the download to finish.

> [!NOTE]
> `Q4_K_M` quantization provides an optimal balance between memory usage and generation quality.

---

## Step 3: Inspect Telemetry and Context Size

Inspect your installed model and calculate memory requirements:

1. Click the **My Models** tab on the top navigation bar.
2. Locate your downloaded model in the model collection.
3. Review the model badges (for example: `Coding`, `Chat`, `4.7 GB`).
4. Drag the **Interactive KV Cache Calculator** slider to your target token length (for example: `8192` tokens).
5. Review the estimated total VRAM calculation to ensure your GPU has sufficient capacity.

---

## Step 4: Send Your First Prompt

Test your model with an interactive prompt.

### Method 1: Use the Built-In AI Assistant

1. Click the **AI Assistant** tab in the application.
2. Select your newly downloaded model from the model selector dropdown.
3. Click inside the text input box at the bottom of the window.
4. Type your prompt:
   ```text
   Write a Python function to check whether an integer is prime. Include unit tests.
   ```
5. Click **Send** or press `Enter`.
6. Read the streamed response as the model generates text.
7. Observe the VRAM bar at the top of the window as the GPU allocates memory for inference.

---

### Method 2: Send a Prompt via the REST API

Send a prompt from your terminal using the OpenAI-compatible HTTP endpoint:

```bash
curl http://localhost:5246/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "qwen2.5-coder:7b",
    "messages": [
      {
        "role": "user",
        "content": "Explain binary search in three sentences."
      }
    ],
    "stream": false
  }'
```

The server returns a JSON response containing the generated text completion.

---

## Step 5: Unload Models to Free VRAM

Release GPU memory when you complete your tasks:

1. Look at the top status bar in the application.
2. Click **Unload All VRAM**.

The VRAM Orchestrator sends a release signal to Ollama. The GPU memory allocation bar drops back to zero.

---

## Next Steps

- Consult the [Troubleshooting Guide](./troubleshooting.md) if you encounter errors.
- Visit the [Engines Overview](../engines/index.md) to configure Stable Diffusion Forge and ComfyUI.
- Visit the [Multimodal Studio Guide](../studio/index.md) to generate 3D meshes, videos, and speech.
