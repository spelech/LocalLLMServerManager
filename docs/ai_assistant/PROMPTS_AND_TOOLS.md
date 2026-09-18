# AI Assistant Prompts & Native Tools Reference

The In-App AI Assistant relies on a combination of **Living Prompts** (markdown guidelines on disk) and **Native C# Tools** (executable functions exposed via `Microsoft.Extensions.AI`).

---

## Part 1: Living Prompts System

Prompts are stored as modular Markdown files in the `Prompts/` directory:

| File | Primary Role | Contents |
|---|---|---|
| **[`system-prompt.md`](file:///C:/Users/Alias/repos/LocalLLMServerManager/Prompts/system-prompt.md)** | Core Persona & Identity | Defines the assistant persona, ASD-STE100 technical communication tone, and operational boundaries. |
| **[`capabilities.md`](file:///C:/Users/Alias/repos/LocalLLMServerManager/Prompts/capabilities.md)** | System Capabilities Manual | Hardware requirements, model sizing rules, engine endpoints, and multimodal support matrices. |
| **[`workflows.md`](file:///C:/Users/Alias/repos/LocalLLMServerManager/Prompts/workflows.md)** | Workflow Execution Guides | Procedural steps for text LLM generation, image creation, video DiT rendering, Kokoro TTS, and 3D mesh modeling. |
| **[`app-control.md`](file:///C:/Users/Alias/repos/LocalLLMServerManager/Prompts/app-control.md)** | Natural Language Control Spec | Rules for invoking app tools, inspecting/modifying settings, handling VRAM allocations, and safety confirmation. |

### Prompt Resolution & Reloading
The `IPromptManagementService` resolves the prompts directory according to the following order:
1. `AppSettings.AiAssistantPromptsDirectory` (if configured and valid).
2. `<AppBaseDir>/Prompts/` (built-in application directory).
3. `<CurrentWorkingDir>/Prompts/` (working directory).
4. Development project directory candidates.

#### Hot-Reloading Prompts
Prompts are cached in memory for high-throughput responses. When modifying markdown files:
- Click the **"🔄 Reload Prompts"** button in the AI Assistant toolbar.
- Or call the endpoint: `POST /api/ai/prompts/reload`.
- Memory caches are cleared immediately and new files take effect on the very next chat request without needing to rebuild or restart the application.

---

## Part 2: Native C# Tool Catalog

The `AiAppTools` class defines 12 native functions callable by the external LLM via `Microsoft.Extensions.AI.FunctionInvokingChatClient`.

### 1. `GetVramTelemetry`
- **Description**: Retrieves real-time GPU VRAM allocation, total memory, used memory, free memory, and GPU hardware device name.
- **Parameters**: None.
- **Returns**: `GpuTelemetryResult` (`GpuName`, `TotalVramMb`, `UsedVramMb`, `FreeVramMb`).
- **Example User Request**: *"How much VRAM do I have free right now?"*

### 2. `GetSystemHealth`
- **Description**: Retrieves overall system health and the online/offline status of all AI backends (ComfyUI, SD-WebUI Forge, Ollama, Kokoro TTS).
- **Parameters**: None.
- **Returns**: Dictionary of engine names to health status strings.
- **Example User Request**: *"Are my image generation and TTS engines online?"*

### 3. `ListInstalledModels`
- **Description**: Enumerates all installed models currently downloaded in Ollama with their parameter sizes, quantization tags, and file sizes.
- **Parameters**: None.
- **Returns**: List of `OllamaModelItem`.
- **Example User Request**: *"What LLMs do I currently have installed on this machine?"*

### 4. `StartAiEngine`
- **Description**: Starts a local AI backend engine process (`comfy`, `forge`, or `audio`).
- **Parameters**:
  - `engine` (string, required): One of `"comfy"`, `"forge"`, or `"audio"`.
- **Returns**: Status message with process start confirmation.
- **Example User Request**: *"Start the ComfyUI engine for me."*

### 5. `StopAiEngine`
- **Description**: Shuts down a running local AI backend engine process.
- **Parameters**:
  - `engine` (string, required): One of `"comfy"`, `"forge"`, or `"audio"`.
- **Returns**: Status message confirming termination.
- **Example User Request**: *"Stop Forge to free up GPU memory."*

### 6. `UnloadAllVram`
- **Description**: Ejects and unloads all models currently occupying GPU VRAM across Ollama and other backends.
- **Parameters**: None.
- **Returns**: Confirmation message.
- **Example User Request**: *"Unload all models from VRAM."*

### 7. `GetAppSettings`
- **Description**: Returns current application configuration settings (model paths, engine URLs, ports, default voices).
- **Parameters**: None.
- **Returns**: `AppSettings` object.
- **Example User Request**: *"What are my current ComfyUI and audio URLs?"*

### 8. `UpdateAppSetting`
- **Description**: Updates an existing application setting property and persists it to disk.
- **Parameters**:
  - `propertyName` (string, required): Setting key (e.g. `"ComfyUiUrl"`, `"PreferredAudioVoice"`).
  - `newValue` (string, required): New setting value.
- **Returns**: Confirmation of setting update.
- **Example User Request**: *"Change my preferred audio voice to am_adam."*

### 9. `EvaluateModelHardwareFit`
- **Description**: Calculates whether a specific model (LLM, diffusion, video, audio, or 3D) will fit within available VRAM/RAM, estimating offload layers and speed.
- **Parameters**:
  - `modelName` (string, required): Name of model (e.g. `"Llama 3.3 70B"`, `"Flux.1 Dev"`, `"Wan 2.2 14B"`).
  - `modality` (string, optional, default `"llm"`): `"llm"`, `"diffusion"`, `"video"`, `"audio"`, or `"3d"`.
  - `parametersBillions` (double?, optional): Parameter size in billions (e.g. 70.0).
  - `quantization` (string?, optional): Quantization string (e.g. `"Q4_K_M"`, `"FP8"`).
  - `contextLength` (int?, optional): Context window size (e.g. 8192).
- **Returns**: `LlmFitResult`, `DiffusionFitResult`, `VideoFitResult`, etc.
- **Example User Request**: *"Can my computer run DeepSeek R1 70B with 8k context?"*

### 10. `GenerateImage`
- **Description**: Submits an image generation job with prompt and dimensions to ComfyUI or Forge.
- **Parameters**:
  - `prompt` (string, required): Positive descriptive prompt.
  - `negativePrompt` (string, optional): Negative prompt.
  - `width` (int, optional, default 1024): Width in pixels.
  - `height` (int, optional, default 1024): Height in pixels.
  - `steps` (int, optional, default 20): Sampling steps.
- **Returns**: Status and generated image asset URL or task tracking ID.
- **Example User Request**: *"Generate a futuristic cyberpunk skyline at sunset."*

### 11. `SpeakText`
- **Description**: Synthesizes speech using the local Kokoro text-to-speech engine.
- **Parameters**:
  - `text` (string, required): Text to vocalize.
  - `voice` (string, optional): Target voice ID (e.g. `"af_heart"`, `"am_adam"`).
- **Returns**: Confirmation and audio URL.
- **Example User Request**: *"Speak 'System initialization complete' with voice af_heart."*

### 12. `SearchDocumentation`
- **Description**: Searches the in-app ASD-STE100 user guides and documentation topics.
- **Parameters**:
  - `query` (string, required): Search keyword or question.
- **Returns**: Matching document titles and excerpt snippets.
- **Example User Request**: *"How do I configure reverse proxy remote access?"*
