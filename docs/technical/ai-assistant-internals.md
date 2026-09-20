---
title: In-AI Assistant Architecture
description: Technical architecture, component layers, model discovery, and multimodal execution flow.
outline: deep
---

# In-AI Assistant Architecture

The In-AI Assistant provides natural language control and diagnostics for LocalLLMServerManager. Local engines run image, audio, and text generation on your graphics card. These local engines can consume all available video memory. Therefore, the assistant routes requests to an external API endpoint. The assistant uses a LiteLLM proxy connected to Google Cloud Vertex AI Gemini Flash.

::: info External Proxy Design
External routing keeps the assistant operational during heavy local generation tasks. The assistant requires zero megabytes of local GPU memory.
:::

---

## Architectural Overview

```mermaid
flowchart TD
    subgraph UI_Layer["Presentation Layer (Desktop & WASM)"]
        Composer["Composer Bar & Attachment Tray\n(AiAssistantTabControl.axaml)"]
        ModelSelect["Model Selector & Capability Badges\n(👁️ Vision, ⚡ Tools, Context)"]
        VM["AiAssistantViewModel\n(ObservableCollection, Reactive Commands)"]
    end

    subgraph API_Layer["Web & REST Endpoints Layer"]
        Endpoints["AiAssistantEndpoints.cs\n(/api/ai/chat, /validate, /models, /prompts)"]
        ComponentPack["ComponentManagerService\n(ai-assistant component lifecycle)"]
    end

    subgraph Discovery_Layer["Model Discovery & Filtering"]
        Discovery["Capability Discovery Engine\n(/model/info -> fallback /v1/models)"]
        Filter["Local Runtime Exclusion Filter\n(Exclude ollama/local, allow LAN IPs)"]
    end

    subgraph Orchestration_Layer["AI Orchestration Layer"]
        Service["IAiAssistantService / AiAssistantService\n(Microsoft.Extensions.AI Pipeline)"]
        Multimodal["Multimodal Serializer\n(AiChatMessageAttachment -> ImageContent)"]
        Invoker["FunctionInvokingChatClient\n(Automated Tool Calling Loop)"]
        Client["OpenAI.Chat.ChatClient.AsIChatClient()\n(External OpenAI Protocol)"]
    end

    subgraph Prompt_Layer["Living Prompts System"]
        PromptService["IPromptManagementService\n(PromptManagementService)"]
        PromptsDisk["Markdown Documents (Prompts/)\n- system-prompt.md\n- capabilities.md\n- workflows.md\n- app-control.md"]
    end

    subgraph Bridge_Layer["Application Integration Bridge"]
        Tools["IAiAppTools / AiAppTools\n(Native C# App Functions)"]
        Engines["AiEngineManager\n(ComfyUI, Forge, Kokoro)"]
        VRAM["GpuTelemetryProvider / VramOrchestrator"]
        Hardware["CanIRunItService\n(Memory Fit Engine)"]
        Settings["SettingsService\n(AppSettings Persistence)"]
    end

    subgraph Remote_LLM["External LLM Gateway"]
        LiteLLM["LiteLLM Proxy / Vertex AI\n(gemini-2.5-flash / OpenAI API)"]
    end

    Composer --> VM
    ModelSelect --> VM
    VM -->|Direct Call| Service
    VM -->|HTTP Fallback| Endpoints
    Endpoints --> Service
    Endpoints --> PromptService
    Service --> Discovery
    Discovery --> Filter
    Filter --> LiteLLM
    Service --> PromptService
    PromptService --> PromptsDisk
    Service --> Multimodal
    Multimodal --> Invoker
    Service --> Invoker
    Invoker --> Client
    Client -->|HTTPS / SSE| LiteLLM
    Invoker -->|Function Call| Tools
    Tools --> Engines
    Tools --> VRAM
    Tools --> Hardware
    Tools --> Settings
```

---

## Component Layers

### 1. Presentation Layer (UI/UX)

- **AiAssistantViewModel**:
  - Manages reactive state with `CommunityToolkit.Mvvm`.
  - Maintains `AvailableModelCapabilities` (`ObservableCollection<AiModelCapabilityInfo>`).
  - Synchronizes `SelectedModelCapability` with the active `SelectedModel`.
  - Manages `StagedAttachments` (`ObservableCollection<AiChatMessageAttachment>`).
  - Supports image paste actions from the system clipboard.
  - Supports dual execution: direct C# service calls in desktop mode and REST/SSE fallback in web mode.

- **AiAssistantTabControl.axaml**:
  - Implements a responsive chat interface styled with `DesignTokens.axaml`.
  - Features an interactive composer bar with:
    - **Model Selector Dropdown**: Displays models with compact capability badges (`👁️`, `⚡`, `1M`).
    - **Attachment Button (📎)**: Opens a file picker for images (`PNG`, `JPG`, `WEBP`, `GIF`).
    - **Clipboard Paste**: Captures `Ctrl+V` key events on the text input to stage images directly.
    - **Staged Image Tray**: Shows image preview thumbnails with removal buttons (`✕`).
    - **Status Indicators**: Shows tool execution cards and streaming token text.

### 2. Model Discovery and Exclusion Layer

The discovery engine queries remote endpoints to identify model capabilities.

```mermaid
flowchart TD
    Start["Call GetModelCapabilitiesAsync()"] --> TryInfo["GET {baseUrl}/model/info"]
    TryInfo -->|Success 200| ParseInfo["Parse LiteLLM model_info metadata"]
    TryInfo -->|Failed / 404| TryModels["GET {baseUrl}/v1/models"]
    TryModels -->|Success 200| ParseModels["Parse OpenAI standard model list"]
    TryModels -->|Failed| ReturnEmpty["Return empty capability list"]
    ParseInfo --> FilterLocal{"Is includeLocal true?"}
    ParseModels --> FilterLocal
    FilterLocal -->|Yes| KeepAll["Keep all models"]
    FilterLocal -->|No| ApplyExclusion["Filter out local runtimes\n(ollama, local, llama.cpp)"]
    KeepAll --> BuildCaps["Build AiModelCapabilityInfo records"]
    ApplyExclusion --> BuildCaps
    BuildCaps --> Finish["Return capability collection"]
```

#### LiteLLM Discovery Protocol
1. The service requests `GET {baseUrl}/model/info`.
2. LiteLLM returns rich capability metadata:
   - `max_input_tokens` and `max_output_tokens`.
   - `supports_vision` and `supports_function_calling`.
   - `litellm_provider` and deployment mode.
3. If `/model/info` fails, the service falls back to `GET {baseUrl}/v1/models`.
4. The fallback inspects model identifiers to infer vision and tool capabilities.

#### Local Model Exclusion Rules
LiteLLM can route requests to local engines. However, assistant requests must not use local GPU memory.

::: warning Exclusion Rule Logic
Do not filter models by IP address. LiteLLM frequently runs on local addresses (`127.0.0.1`) or local area network IPs.
Filter models strictly by provider name or model identifier prefix.
:::

- **Excluded Providers**:
  - `ollama`
  - `local`
  - `llama.cpp`
  - `vllm_local`
- **Excluded ID Prefixes**:
  - `ollama/`
  - `local/`
  - `llama/`
  - `ollama_chat/`
- **Exclusion Toggle**: Set query parameter `includeLocal=true` on `GET /api/ai/models` to include local models.

### 3. Web API Endpoints Layer

- **AiAssistantEndpoints.cs**:
  - `GET /api/ai/status`: Reports assistant installation status and active endpoint.
  - `POST /api/ai/validate`: Validates credentials with round-trip latency checks.
  - `GET /api/ai/models`: Returns `List<AiModelCapabilityInfo>`. Accepts `endpoint`, `apiKey`, and `includeLocal` parameters.
  - `POST /api/ai/chat`: Executes chat completions. Accepts multimodal message attachments.
  - `GET /api/ai/prompts`: Returns loaded system prompt sections.
  - `POST /api/ai/prompts/reload`: Flushes prompt memory caches immediately.

### 4. Orchestration and Multimodal Pipeline

- **IAiAssistantService & AiAssistantService**:
  - Built on `Microsoft.Extensions.AI` abstractions.
  - Serializes `AiChatMessageAttachment` instances into `Microsoft.Extensions.AI.ImageContent`.
  - Supports binary byte buffers and base64 data URIs.
  - Wraps `OpenAI.Chat.ChatClient` with `FunctionInvokingChatClient`.
  - Automatically executes native C# tools and returns results to the language model.
  - Streams tokens asynchronously via `StreamChatAsync`.

### 5. Living Prompts System

- **IPromptManagementService & PromptManagementService**:
  - Loads markdown files from the `Prompts/` directory:
    - `system-prompt.md`: Defines tone and operational boundaries.
    - `capabilities.md`: Defines hardware sizing and multimodal capabilities.
    - `workflows.md`: Defines execution steps for image, video, and audio tasks.
    - `app-control.md`: Defines safety rules for app tool calls.
  - Caches prompt content in memory.
  - Provides immediate cache invalidation through `InvalidateCache()`.

### 6. Application Integration Bridge (App Control)

- **IAiAppTools & AiAppTools**:
  - Exposes 12 native C# tools for autonomous LLM function calling:
    - `GetGpuVramTelemetryAsync`: Reads live GPU memory usage.
    - `CheckServicesHealthAsync`: Checks backend engine health.
    - `ListInstalledModelsAsync`: Lists installed local Ollama models.
    - `StartAiEngineAsync`: Starts ComfyUI, Forge, or Ollama.
    - `StopAiEngineAsync`: Stops running backend engines.
    - `UnloadVramAsync`: Flushes models from video memory.
    - `GetAppSettingsAsync`: Reads application settings.
    - `UpdateAppSettingAsync`: Updates configuration values.
    - `CalculateHardwareFitAsync`: Calculates model memory fit.
    - `GenerateImageAsync`: Triggers local image generation workflows.
    - `SynthesizeSpeechAsync`: Generates speech with Kokoro TTS.
    - `QueryAppDocumentationAsync`: Searches local documentation guides.

---

## Multimodal Execution Sequence

```mermaid
sequenceDiagram
    autonumber
    actor User as User
    participant UI as AiAssistantTabControl
    participant VM as AiAssistantViewModel
    participant Service as AiAssistantService
    participant LLM as External LiteLLM Gateway
    participant Tools as AiAppTools

    User->>UI: Selects model "vertex_ai/gemini-2.5-flash"
    UI->>VM: Update SelectedModelCapability
    User->>UI: Attaches screenshot via 📎 or Ctrl+V
    UI->>VM: AddStagedAttachment(AiChatMessageAttachment)
    User->>UI: Types "Explain this error and check my VRAM"
    User->>UI: Clicks Send
    VM->>Service: StreamChatAsync(AiChatRequest with ImageContent)
    Service->>Service: Build multimodal ChatMessage list
    Service->>LLM: Send Chat completion request with ImageContent + Tools
    LLM-->>Service: Function call: get_gpu_vram_telemetry()
    Service->>VM: Yield AiChatChunk(ToolCall: get_gpu_vram_telemetry)
    VM-->>UI: Display tool execution card
    Service->>Tools: GetGpuVramTelemetryAsync()
    Tools-->>Service: Return memory metrics JSON
    Service->>LLM: Send tool result back to model
    LLM-->>Service: Stream text response explaining image and VRAM state
    loop Token Streaming
        Service->>VM: Yield AiChatChunk(DeltaText)
        VM-->>UI: Append tokens to chat bubble
    end
    Service->>VM: Yield AiChatChunk(IsDone: true)
    VM-->>UI: Finalize message card
```
