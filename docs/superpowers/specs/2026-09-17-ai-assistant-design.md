# In-AI Assistant & Natural Language App Control Design Specification

## 1. Overview & Goals

LocalLLMServerManager is a unified management application and web interface for hosting, orchestrating, and interacting with local AI inference runtimes (Ollama, Stable Diffusion Forge, ComfyUI, Kokoro TTS, etc.).

When running locally, a user's machine may lack the spare GPU VRAM required to concurrently run both heavy generation workflows (like Wan 2.2 video or Flux image diffusion) and a massive local reasoning LLM. Furthermore, users often need conversational guidance on hardware requirements, workflow setup, model recommendations, and troubleshooting.

The **AI Assistant Module** bridges this gap by introducing:
1. **External OpenAI-Compatible Routing**: Connects to external LLMs (e.g., LiteLLM proxy, OpenAI, Vertex AI Gemini Flash models) through a configurable API endpoint and key.
2. **Natural Language App Control (Stretch Goal / Bridge)**: Equips the LLM with native tool calling capabilities to query VRAM telemetry, start/stop engines, switch active settings, trigger image/video/speech workflows, and check hardware compatibility.
3. **Living Prompts & Documentation System**: Decouples prompt engineering from application source code by loading markdown-based system prompts, personas, and workflow manuals dynamically from the repository filesystem (`Prompts/`).
4. **Rich Conversational UI/UX**: Delivers an OpenWebUI/Claude-style chat interface in Avalonia XAML (with 100% desktop and WebAssembly web app parity) featuring user/assistant bubbles, tool call progress cards, suggestion chips, and live status indicators.
5. **Modular Component Architecture & Setup Wizard**: Packaged as an optional module in `ComponentManagerService` with a first-run connection validation wizard (pinging models, verifying credentials, and selecting target models).
6. **Dual Verification Pipeline**: Comprehensive unit tests covering all orchestration and tool bridges, alongside live integration tests against LiteLLM / Vertex Flash endpoints.

---

## 2. Architecture & Tech Stack

```
+-----------------------------------------------------------------------------------+
|                            User Interface Layer                                   |
|  +-------------------------------------+   +------------------------------------+  |
|  |  Desktop App (Avalonia 12 / Fluent) |   |  Web App (Avalonia WebAssembly)    |  |
|  |       AiAssistantTabControl         |   |       AiAssistantTabControl        |  |
|  +-------------------------------------+   +------------------------------------+  |
|                                     |                                             |
|                                     v                                             |
|                          AiAssistantViewModel                                     |
+-----------------------------------------------------------------------------------+
                                      |
                           HTTP API Client (/api/ai/*)
                                      |
+-----------------------------------------------------------------------------------+
|                         ASP.NET Core Server (Port 5246)                           |
|                                                                                   |
|  +-----------------------------------------------------------------------------+  |
|  |                            AiAssistantEndpoints                             |  |
|  |   POST /api/ai/chat        GET /api/ai/validate       GET /api/ai/models    |  |
|  |   GET /api/ai/prompts      POST /api/ai/prompts/reload                      |  |
|  +-----------------------------------------------------------------------------+  |
|                                      |                                             |
|                                      v                                             |
|  +-----------------------------------------------------------------------------+  |
|  |                  Orchestration Service: IAiAssistantService                 |  |
|  |             - Powered by Microsoft.Extensions.AI (net10.0)                  |  |
|  |             - FunctionInvokingChatClient tool loop                          |  |
|  |             - Streaming & Non-streaming SSE response generation            |  |
|  +-----------------------------------------------------------------------------+  |
|                         |                                   |                     |
|                         v                                   v                     |
|  +------------------------------+       +--------------------------------------+  |
|  |    PromptManagementService   |       |             AiAppTools               |  |
|  |   Loads & templates markdown |       |  App Integration Bridge:             |  |
|  |   prompts from Prompts/ dir  |       |  - IGpuTelemetryProvider (VRAM)      |  |
|  +------------------------------+       |  - IAiEngineManager (start/stop)     |  |
|                                         |  - ISettingsService (view/update)    |  |
|                                         |  - ICanIRunItService (hardware fit)  |  |
|                                         |  - WorkflowEndpoints / Http          |  |
|                                         |  - Documentation search              |  |
|                                         +--------------------------------------+  |
|                                                             |                     |
|                                                             v                     |
|                                            Local AI Engines & OS Subsystems       |
|                                            (Ollama, Forge, ComfyUI, Kokoro)       |
+-----------------------------------------------------------------------------------+
                                      |
                                      v
                     +----------------------------------+
                     |   External OpenAI-Compatible API |
                     |      (LiteLLM / Vertex Flash)    |
                     +----------------------------------+
```

### 2.1 Why `Microsoft.Extensions.AI`?
- **Native .NET Standard & .NET 10 Alignment**: `Microsoft.Extensions.AI` and `Microsoft.Extensions.AI.OpenAI` (v10.10.0) are Microsoft's official abstractions for AI models and tool calling in .NET 10.
- **Provider Agnostic**: Targets any OpenAI-compatible HTTP endpoint (LiteLLM, Azure OpenAI, vLLM, Ollama, OpenAI) simply by setting `Endpoint` and `ApiKey`.
- **Automatic Function Invocation**: Built-in `FunctionInvokingChatClient` handles multi-turn tool loops cleanly, allowing the LLM to call multiple tools in sequence before replying.
- **Strong Typing**: Clean integration with dependency injection and cancellation tokens.

---

## 3. Subsystem Breakdown

### 3.1 Living Prompts System (`Prompts/`)
All prompt text, persona instructions, and tool usage documentation are maintained as distinct markdown documents in `Prompts/`:
1. `Prompts/system-prompt.md`: Core system instructions, persona (helpful local AI orchestrator and assistant), safety rules, and operating principles.
2. `Prompts/capabilities.md`: Detailed descriptions of what the host application supports (Ollama LLMs, SD Forge image generation, ComfyUI workflows, Kokoro TTS, hardware fit calculator).
3. `Prompts/workflows.md`: Instructions on how to guide users through image, video, audio, and text workflows.
4. `Prompts/app-control.md`: Guidelines and schemas for when and how the assistant should call tools to control the application.

`IPromptManagementService` reads these files at startup and provides a live reload mechanism (`ReloadPromptsAsync`) so prompt engineers can tweak prompts without rebuilding or restarting the app.

### 3.2 App Integration Bridge (`AiAppTools`)
The LLM can execute actions in the app via registered functions:
- `get_gpu_vram_telemetry()`: Live VRAM capacity, used bytes, free bytes, GPU model.
- `check_services_health()`: Online/offline status of Ollama, SD Forge, ComfyUI, Kokoro.
- `list_installed_models()`: Query local Ollama and HuggingFace/Civitai downloaded models.
- `start_ai_engine(engine)` / `stop_ai_engine(engine)`: Process control for `forge`, `comfyui`, `ollama`.
- `get_app_settings()` / `update_app_setting(key, value)`: Inspect and update application settings.
- `calculate_hardware_fit(modelName, sizeGb, quant)`: Run hardware fit checks before downloading models.
- `generate_image(prompt, negativePrompt, engine, width, height)`: Trigger text-to-image on Forge or ComfyUI.
- `synthesize_speech(text, voice)`: Call Kokoro TTS to generate voice audio.
- `query_app_documentation(query)`: Search built-in ASD-STE100 user documentation.

### 3.3 Optional Module Registration & Setup Wizard
- In `ComponentManagerService`, the `ai-assistant` component pack is registered alongside video and audio packs.
- The module includes a **Setup Wizard** in the UI:
  - Base URL field (default: `http://127.0.0.1:4000/v1` for LiteLLM).
  - API Key field (masked).
  - Model Name field (default: `google/gemini-2.5-flash` or configurable).
  - "Validate Connection" button which sends a lightweight test ping to the endpoint.
  - Success badge with available model list upon successful validation.

### 3.4 Conversational UI / UX
- **Theme Consistency**: Uses Semi.Avalonia / Fluent dark tokens (`BgDarkBrush`, `BgSurfaceBrush`, `PrimaryBrush`, `BorderBrush`).
- **Chat Bubbles**:
  - User messages: Right-aligned styled bubble.
  - Assistant messages: Left-aligned markdown bubble with copy button.
  - Tool Invocation Cards: Specialized expandable cards showing "Invoking [Tool Name]...", tool arguments, and results with status indicators (Running, Success, Error).
- **Quick Action Chips**:
  - "📊 Check GPU VRAM"
  - "🔍 What models can I run?"
  - "⚡ Check backend services"
  - "🎨 Generate an image"
- **Controls**:
  - Text input with Shift+Enter multi-line support.
  - Send button and Stop Generation button.
  - Clear conversation button.
  - Model and connection status pill in header.

---

## 4. Error Handling & Edge Cases
- **External Endpoint Down / Timeout**: Clear error message in chat bubble with actionable troubleshooting steps.
- **Invalid API Key**: Prompt the user to open the Setup Wizard and test credentials.
- **Tool Execution Failure**: Tool errors are safely caught, wrapped into JSON error objects, and returned to the LLM so it can explain the error gracefully to the user.
- **Prompts Missing**: `PromptManagementService` falls back to robust built-in default prompts if the `Prompts/` directory is missing.
- **Concurrent Generation**: The UI disables the Send button and displays an active generation indicator while awaiting stream completion.

---

## 5. Testing & Verification Strategy
- **Unit Tests (`LocalLLMServerManager.Tests`)**:
  - `PromptManagementServiceTests`: Test directory loading, missing file fallback, prompt composition, reload.
  - `AiAssistantServiceTests`: Test client initialization, connection validation, tool execution routing, error formatting.
  - `AiAppToolsTests`: Test all app tool functions with mocked providers.
  - `AiAssistantEndpointsTests`: Test REST API endpoints (`/api/ai/chat`, `/api/ai/validate`, `/api/ai/prompts`).
  - `AiAssistantViewModelTests`: Test ViewModel state machine, message list updates, tool card formatting, validation command.
- **Live LLM Integration Tests**:
  - `AiAssistantLiveLlmTests`: Interactive test with LiteLLM / Vertex Flash endpoint (with environment variable override `LITELLM_API_KEY` / `LITELLM_ENDPOINT`).
- **Regression & Quality Checks**:
  - `dotnet test LocalLLMServerManager.sln`
  - `npm run lint`
  - `npx tsc --noEmit`
