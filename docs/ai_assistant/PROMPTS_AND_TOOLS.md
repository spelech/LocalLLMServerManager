---
title: AI Assistant Prompts, Capabilities & Tools Reference
description: Specification for living prompts, rich model capabilities schema, native tools, and multimodal attachments.
outline: deep
---

# AI Assistant Prompts, Capabilities & Tools Reference

The In-App AI Assistant combines living markdown prompts, a rich model capability catalog, and executable C# tools.

---

## Part 1: Living Prompts System

The assistant reads system instructions from modular markdown documents in the `Prompts/` directory.

| File | Primary Purpose | Document Contents |
|---|---|---|
| `system-prompt.md` | Persona & Identity | Defines assistant tone, ASD-STE100 guidelines, and operational boundaries. |
| `capabilities.md` | Capabilities Manual | Defines hardware limits, model sizing rules, and engine API endpoints. |
| `workflows.md` | Workflow Guides | Defines procedural steps for text, image, video, and audio pipelines. |
| `app-control.md` | Tool Control Spec | Defines rules for invoking tools, editing settings, and memory safety. |

### Directory Resolution Order

The `PromptManagementService` searches for the prompts directory in this sequence:
1. Directory path configured in `AppSettings.AiAssistantPromptsDirectory`.
2. Built-in application folder at `<AppBaseDir>/Prompts/`.
3. Current working directory folder at `<CurrentWorkingDir>/Prompts/`.
4. Development repository candidate folders.

### Prompt Caching and Invalidation

The service caches prompt files in memory for fast response times.

::: tip Hot-Reloading Prompts
Update markdown prompt files on disk at any time.
Click **Reload Prompts** in the AI Assistant toolbar, or send a request to `POST /api/ai/prompts/reload`.
The service clears the cache immediately. The next request uses the new prompt content without an application restart.
:::

---

## Part 2: Rich Model Capabilities Schema

The assistant queries remote gateways for model capability metadata. The system stores metadata in the `AiModelCapabilityInfo` record.

### Schema Fields

| Field Name | Type | Description |
|---|---|---|
| `Id` | `string` | Unique model identifier string (e.g. `vertex_ai/gemini-2.5-flash`). |
| `DisplayName` | `string` | Clean human-readable label for user interfaces. |
| `Provider` | `string` | Hosting provider name (e.g. `vertex_ai`, `openai`, `anthropic`). |
| `Mode` | `string` | Operation mode (defaults to `"chat"`). |
| `SupportsVision` | `bool` | True when the model accepts image inputs. |
| `SupportsFunctionCalling` | `bool` | True when the model executes tool calls. |
| `SupportsAudio` | `bool` | True when the model accepts audio inputs. |
| `MaxInputTokens` | `int?` | Maximum supported input context window tokens. |
| `MaxOutputTokens` | `int?` | Maximum completion output tokens. |
| `IsLocal` | `bool` | True when the model runs on local engines (e.g. Ollama). |
| `SummaryBadge` | `string` | Computed badge string displaying capability icons and provider. |

### Summary Badge Formatting Rules

The `SummaryBadge` property formats capability indicators into a compact label:
- **Vision Support**: Prepends `👁️` if `SupportsVision` is true.
- **Tool Calling**: Prepends `⚡` if `SupportsFunctionCalling` is true.
- **Context Size**: Formats token counts as `1M` (≥1,000,000 tokens) or `128k` (≥1,000 tokens).
- **Provider Tag**: Appends the provider name in square brackets (e.g. `[vertex_ai]`).

::: info Badge Examples
- `👁️ ⚡ 1M [vertex_ai]` (Gemini 2.5 Flash: vision, function calling, 1,000,000 tokens).
- `👁️ ⚡ 128k [openai]` (GPT-4o: vision, function calling, 128,000 tokens).
- `⚡ 128k [anthropic]` (Claude 3.5 Haiku: text function calling, 128,000 tokens).
:::

---

## Part 3: Native C# Tool Catalog

The assistant exposes 12 native C# tools. The language model invokes these tools autonomously through `FunctionInvokingChatClient`.

### 1. `GetGpuVramTelemetryAsync`
- **Purpose**: Reads live GPU memory usage, total memory, used memory, and GPU name.
- **Parameters**: None.
- **Returns**: Formatted JSON string containing memory metrics.
- **Example User Request**: *"How much VRAM is currently free?"*

### 2. `CheckServicesHealthAsync`
- **Purpose**: Checks the online status of Ollama, Forge, ComfyUI, and Kokoro TTS.
- **Parameters**: None.
- **Returns**: Status map showing engine connection state and port numbers.
- **Example User Request**: *"Are my image generation and TTS services running?"*

### 3. `ListInstalledModelsAsync`
- **Purpose**: Lists all installed Ollama language models and their quantization details.
- **Parameters**: None.
- **Returns**: Array of model records with size and parameter counts.
- **Example User Request**: *"List my downloaded local language models."*

### 4. `StartAiEngineAsync`
- **Purpose**: Starts a local engine process (`"forge"`, `"comfyui"`, or `"ollama"`).
- **Parameters**:
  - `engine` (`string`, required): Name of target engine.
- **Returns**: Status confirmation message.
- **Example User Request**: *"Start the ComfyUI engine."*

### 5. `StopAiEngineAsync`
- **Purpose**: Shuts down a running backend engine process.
- **Parameters**:
  - `engine` (`string`, required): Name of engine to stop.
- **Returns**: Process termination status.
- **Example User Request**: *"Stop Forge to free up GPU memory."*

### 6. `UnloadVramAsync`
- **Purpose**: Ejects all active models from GPU video memory across engines.
- **Parameters**: None.
- **Returns**: Memory unload confirmation.
- **Example User Request**: *"Unload all models from VRAM."*

### 7. `GetAppSettingsAsync`
- **Purpose**: Reads current configuration values from `settings.json`.
- **Parameters**: None.
- **Returns**: Current application settings JSON.
- **Example User Request**: *"Show my current engine URLs."*

### 8. `UpdateAppSettingAsync`
- **Purpose**: Updates and persists a configuration setting.
- **Parameters**:
  - `key` (`string`, required): Setting property name.
  - `value` (`string`, required): New setting value.
- **Returns**: Update confirmation.
- **Example User Request**: *"Set PreferredAudioVoice to am_adam."*

### 9. `CalculateHardwareFitAsync`
- **Purpose**: Evaluates whether a model fits into available GPU VRAM and system RAM.
- **Parameters**:
  - `modelName` (`string`, required): Model name or architecture.
  - `parametersBillions` (`double?`, optional): Model parameter count in billions.
  - `quantization` (`string?`, optional): Quantization format (e.g. `"Q4_K_M"`, `"FP8"`).
  - `modality` (`string?`, optional): Modality type (`"llm"`, `"diffusion"`, `"video"`).
- **Returns**: Offload layer estimation and fit verdict.
- **Example User Request**: *"Can my system run DeepSeek R1 70B?"*

### 10. `GenerateImageAsync`
- **Purpose**: Submits an image generation job to Forge or ComfyUI.
- **Parameters**:
  - `prompt` (`string`, required): Image description prompt.
  - `negativePrompt` (`string?`, optional): Negative prompt text.
  - `engine` (`string?`, optional): Engine target (`"forge"` or `"comfyui"`).
  - `width` (`int`, optional, default `1024`): Image width in pixels.
  - `height` (`int`, optional, default `1024`): Image height in pixels.
- **Returns**: Status message with asset URL or tracking ID.
- **Example User Request**: *"Generate an image of a red sports car in the rain."*

### 11. `SynthesizeSpeechAsync`
- **Purpose**: Synthesizes spoken audio from text using local Kokoro TTS.
- **Parameters**:
  - `text` (`string`, required): Text content to speak.
  - `voice` (`string`, optional, default `"af_heart"`): Target voice identifier.
  - `format` (`string`, optional, default `"mp3"`): Output audio format.
- **Returns**: Confirmation message and audio playback URI.
- **Example User Request**: *"Speak 'System initialization complete' using voice af_heart."*

### 12. `QueryAppDocumentationAsync`
- **Purpose**: Performs keyword searches across in-app documentation guides.
- **Parameters**:
  - `query` (`string`, required): Search keywords or topic.
- **Returns**: Matching guide titles and relevant excerpt text.
- **Example User Request**: *"How do I configure remote access?"*

---

## Part 4: Multimodal Message Attachments

The assistant supports image inputs alongside text prompts.

### Data Structures

#### `AiChatMessageAttachment`
```csharp
public class AiChatMessageAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "image/png";
    public string Base64Data { get; set; } = "";
    public byte[]? RawBytes { get; set; }
}
```

#### `AiChatMessageItem`
Each chat message contains an attachment list:
```csharp
public class AiChatMessageItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public List<AiChatMessageAttachment> Attachments { get; set; } = new();
    public bool HasAttachments => Attachments.Count > 0;
}
```

### Pipeline Serialization

The orchestration layer maps attachments into `Microsoft.Extensions.AI.ImageContent`:
1. If `RawBytes` exists, the service creates `new ImageContent(RawBytes, ContentType)`.
2. If `Base64Data` exists, the service constructs a data URI (`data:image/png;base64,...`).
3. The service packages the text content and all images into a unified `ChatMessage`.
4. LiteLLM forwards the multimodal request to the external vision model.
