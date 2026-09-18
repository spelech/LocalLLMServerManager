---
title: AI Assistant Setup & Configuration Guide
description: Step-by-step setup guide for LiteLLM proxy, model discovery, multimodal chat, and copilot controls.
outline: deep
---

# AI Assistant Setup & Configuration Guide

This guide describes how to connect the In-App AI Assistant to an external OpenAI-compatible API endpoint. The recommended configuration uses **LiteLLM** proxying to **Google Cloud Vertex AI Gemini 2.5 Flash**.

---

## Architecture Context

LocalLLMServerManager runs image, video, audio, and text models on your local graphics card. Running a large assistant model locally consumes video memory needed by your generative workflows.

Connecting the assistant to an external LiteLLM gateway provides three major advantages:
1. **Always Available**: The assistant responds even if local engines crash or stop.
2. **Zero VRAM Footprint**: Generative workflows retain 100% of local GPU memory.
3. **Large Context and Vision**: Gemini 2.5 Flash offers a 1,000,000 token context window with native image understanding.

---

## Step 1: Set Up the LiteLLM Proxy

### 1.1 Install the LiteLLM Proxy
Install the LiteLLM proxy package using Python:
```bash
pip install 'litellm[proxy]'
```

### 1.2 Authenticate with Google Cloud
Authenticate your system to Google Cloud:
```bash
gcloud auth application-default login
```
Alternatively, set the service account environment variable:
```bash
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/key.json"
```

### 1.3 Create the Configuration File
Create a file named `config.yaml`:
```yaml
model_list:
  - model_name: vertex_ai/gemini-2.5-flash
    litellm_params:
      model: vertex_ai/gemini-2.5-flash
      vertex_project: "YOUR_GCP_PROJECT_ID"
      vertex_location: "us-central1"

litellm_settings:
  drop_params: true
```

### 1.4 Start the LiteLLM Proxy
Start the proxy on port 4000:
```bash
litellm --config config.yaml --port 4000
```
LiteLLM now listens at `http://127.0.0.1:4000/v1` with OpenAI API compatibility.

---

## Step 2: Configure LocalLLMServerManager

### Method A: Use the In-App Setup Wizard (Recommended)
1. Open **LocalLLMServerManager**.
2. Select the **Copilot** tab.
3. Click **Setup & Endpoint** in the toolbar to expand settings.
4. Enter the connection settings:
   - **OpenAI-Compatible Endpoint URL**: `http://127.0.0.1:4000/v1`
   - **API Key**: Optional for local LiteLLM proxies.
   - **Model Identifier**: `vertex_ai/gemini-2.5-flash`
5. Click **Test Connection**.
   - The app verifies connectivity and runs capability discovery.
   - The status bar displays discovered models and connection latency.
6. Click **Save Settings**.

::: info Automated Capability Discovery
Testing the connection calls `GET /model/info`. If `/model/info` is not supported, the app queries `GET /v1/models`.
The discovery process populates model token limits, vision flags, and tool capabilities.
:::

::: warning Local Model Exclusion Filter
LiteLLM proxies may list local Ollama models.
The assistant automatically filters out local models (`ollama`, `local`, `llama.cpp`) to prevent local GPU memory use.
Hosting LiteLLM on local addresses (`127.0.0.1`) or LAN IP addresses remains fully supported.
:::

### Method B: Configure via `settings.json`
Edit `settings.json` in the application root folder:
```json
{
  "AiAssistantEnabled": true,
  "AiAssistantEndpoint": "http://127.0.0.1:4000/v1",
  "AiAssistantApiKey": "",
  "AiAssistantModel": "vertex_ai/gemini-2.5-flash",
  "AiAssistantPromptsDirectory": null
}
```

---

## Step 3: Interactive Composer Bar & Multimodal Chat

The chat interface includes an interactive composer bar with real-time capability controls.

### Dynamic Model Switching
1. Open the model dropdown in the composer bar.
2. Review the capability badges next to each model name:
   - `👁️`: Supports multimodal image inputs.
   - `⚡`: Supports autonomous tool and function execution.
   - `1M` / `128k`: Displays input context window capacity.
   - `[provider]`: Shows the hosting backend provider.
3. Select any model to switch targets for the next chat message.

### Attaching Images (Multimodal Chat)
You can attach images using two convenient methods:

1. **File Picker Button (📎)**:
   - Click the attachment button (`📎`) next to the text input.
   - Select one or more images (`.png`, `.jpg`, `.jpeg`, `.webp`, `.gif`).
2. **Clipboard Paste (`Ctrl+V` / `Cmd+V`)**:
   - Copy an image from a browser, paint tool, or file manager.
   - Focus the chat prompt input box.
   - Press `Ctrl+V` (or `Cmd+V` on macOS) to paste the image directly.

Staged images appear in the preview tray above the prompt box.
Click the remove button (`✕`) on any image thumbnail to remove it before sending.

::: tip Multimodal Query Examples
- Attach a desktop screenshot: *"Explain this error message and suggest a solution."*
- Attach a generated image: *"Critique the lighting and composition of this image."*
:::

---

## Step 4: Verify Copilot Control Tools

Submit test queries to verify native C# tool execution:

1. **Hardware Memory Telemetry**:
   > *"What is my current VRAM allocation and free memory?"*
   - The assistant calls `GetGpuVramTelemetryAsync` and reports your GPU memory status.

2. **Model Hardware Fit Evaluation**:
   > *"Can my system run DeepSeek R1 70B with 8k context?"*
   - The assistant calls `CalculateHardwareFitAsync` and evaluates offload layers.

3. **Backend Service Health**:
   > *"Are ComfyUI and Kokoro running?"*
   - The assistant calls `CheckServicesHealthAsync` and summarizes engine status.

4. **Speech Synthesis**:
   > *"Speak 'Local server manager is ready' using voice af_heart."*
   - The assistant calls `SynthesizeSpeechAsync` and returns audio playback controls.

---

## Alternative Providers

| Provider | Endpoint URL | Example Model | Notes |
|---|---|---|---|
| **LiteLLM Gateway** | `http://127.0.0.1:4000/v1` | `vertex_ai/gemini-2.5-flash` | Recommended. 0 MB VRAM, 1M context, vision, tools. |
| **OpenAI Direct** | `https://api.openai.com/v1` | `gpt-4o` | Requires standard OpenAI API key (`sk-...`). |
| **Groq Cloud** | `https://api.groq.com/openai/v1` | `llama-3.3-70b-versatile` | Ultra-fast token generation with tool support. |
| **Anthropic via LiteLLM** | `http://127.0.0.1:4000/v1` | `claude-3-5-sonnet-20241022` | Advanced coding and vision capabilities. |

---

## Troubleshooting

::: warning Connection Refused
If connection fails on port 4000:
1. Verify LiteLLM is running: `curl http://127.0.0.1:4000/health`.
2. When running in containers or virtual machines, use the host IP instead of `127.0.0.1`.
:::

::: warning HTTP 401 Unauthorized
If LiteLLM responds with status 401:
1. Check if LiteLLM requires a master key in `config.yaml`.
2. Enter the configured master key into the **API Key** field in the setup wizard.
:::

::: tip Vision Model Selection
If image attachment fails or returns an error:
Make sure your selected model displays the `👁️` vision badge in the composer bar.
Models without vision capability cannot process image attachments.
:::
