# AI Assistant Setup & Configuration Guide

This guide walks through connecting the In-App AI Assistant to an external OpenAI-compatible API endpoint, specifically focusing on **LiteLLM proxying to Google Cloud Vertex AI Gemini 2.5 Flash**.

---

## Architecture Context

LocalLLMServerManager operates locally on your machine to manage Ollama, ComfyUI, Kokoro TTS, and SD-WebUI Forge. However, running a large copilot LLM locally would consume GPU VRAM needed by your image and text models.

Connecting the AI Assistant to an external **LiteLLM proxy** provides:
1. **Always-available Copilot**: Functions even if all local engines are offline or crashed.
2. **0 MB Local VRAM Usage**: Leaves 100% of your GPU VRAM free for your local generative workflows.
3. **Massive Context & Speed**: Vertex AI Gemini 2.5 Flash offers 1M+ context window with sub-second token latency.

---

## Step 1: Set Up LiteLLM with Google Cloud Vertex AI

### 1.1 Install LiteLLM Proxy
In your Python environment or terminal:
```bash
pip install 'litellm[proxy]'
```

### 1.2 Authenticate with Google Cloud
Ensure your machine is authenticated to your Google Cloud project:
```bash
gcloud auth application-default login
# or set the service account key environment variable:
# export GOOGLE_APPLICATION_CREDENTIALS="/path/to/key.json"
```

### 1.3 Create `config.yaml`
Create a `config.yaml` configuration file for LiteLLM:
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

### 1.4 Start LiteLLM
Run the proxy server on port 4000:
```bash
litellm --config config.yaml --port 4000
```
LiteLLM is now listening at `http://127.0.0.1:4000/v1` with OpenAI protocol compatibility.

---

## Step 2: Configure LocalLLMServerManager

### Method A: Using the In-App Setup Wizard (Recommended)
1. Launch **LocalLLMServerManager** (Desktop GUI or Web UI).
2. Select the **`[🤖 Copilot]`** tab in the main tab navigation.
3. In the toolbar, click **`⚙️ Setup & Endpoint`** to expand the configuration panel.
4. Fill in the connection settings:
   - **OpenAI-Compatible Endpoint URL**: `http://127.0.0.1:4000/v1`
   - **API Key**: Optional for local LiteLLM (or enter your master key if configured).
   - **Model Identifier**: `vertex_ai/gemini-2.5-flash`
5. Click **`⚡ Test Connection`**.
   - You should see: `🟢 Connected successfully to 127.0.0.1 (Latency: ~25 ms, 1 models found)`.
6. Click **`💾 Save Settings`**.

### Method B: Via `settings.json`
Alternatively, edit `settings.json` in your application root:
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

## Step 3: Verifying Copilot Natural Language Control

Once configured, verify app control by entering test queries in the copilot chat box:

1. **Hardware Telemetry**:
   > *"What is my current VRAM allocation and free memory?"*
   - *Expected*: Assistant executes the `GetVramTelemetry` tool, displays the execution card, and reports your GPU model and free memory.

2. **Hardware Fit Calculator**:
   > *"Can my system run Llama 3.3 70B with 8k context?"*
   - *Expected*: Assistant executes `EvaluateModelHardwareFit` and explains layer offloading and VRAM fit.

3. **Engine Health Check**:
   > *"Are ComfyUI and Kokoro running?"*
   - *Expected*: Assistant calls `GetSystemHealth` and summarizes engine status.

4. **Speech Synthesis**:
   > *"Speak 'Local LLM Server Manager is ready' using Kokoro."*
   - *Expected*: Assistant calls `SpeakText` and presents the synthesized audio output.

---

## Alternative Providers

| Provider | Endpoint URL | Model Identifier | Notes |
|---|---|---|---|
| **Local Ollama** | `http://127.0.0.1:11434/v1` | `llama3.1:8b` or `qwen2.5:7b` | Uses local GPU VRAM. Requires Ollama running. |
| **OpenAI Direct** | `https://api.openai.com/v1` | `gpt-4o-mini` or `gpt-4o` | Requires standard OpenAI API key (`sk-...`). |
| **Groq Cloud** | `https://api.groq.com/openai/v1` | `llama-3.3-70b-versatile` | Ultra-fast token generation (~500 tok/s). |
| **vLLM / Local AI** | `http://127.0.0.1:8000/v1` | `<custom-model-id>` | Self-hosted OpenAI-compatible inference. |

---

## Troubleshooting

### Error: `Connection refused on port 4000`
- Ensure LiteLLM is actively running: `curl http://127.0.0.1:4000/health`.
- If running LocalLLMServerManager in WSL or a container, use `http://host.docker.internal:4000/v1` or your LAN IP instead of `127.0.0.1`.

### Error: `Endpoint responded with status 401 Unauthorized`
- Your LiteLLM proxy requires an API key. Set `litellm_settings: master_key: ...` or provide the matching key in the Copilot Setup Wizard.

### Error: `Function calling failed / tool arguments invalid`
- Ensure your target model supports OpenAI-compatible function calling (Gemini 2.5 Flash, GPT-4o, and Claude 3.5 Sonnet support this natively).
- Check the console logs for detailed JSON schema inspection.
