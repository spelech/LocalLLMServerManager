# LiteLLM Model Capabilities, Local Model Exclusion & Multimodal Chat Design Specification

## 1. Overview & Objectives

In `LocalLLMServerManager`, the In-AI Assistant connects to external OpenAI-compatible endpoints—primarily a network-hosted LiteLLM proxy instance—to guide the user, evaluate hardware fit, adjust app settings, and trigger workflows.

This specification defines the architecture, data structures, UI/UX, and testing strategy to:
1. **Discover LiteLLM Models & Rich Capabilities**: Dynamically query LiteLLM's `/model/info` (or `/v1/model_info` and `/v1/models`) catalog to extract model capabilities (modality, vision support, function calling, context window, max output tokens).
2. **Exclude Local Models**: Filter out local models (Ollama, local engines, llama.cpp) while preserving cloud models (Vertex AI, Gemini, OpenAI, Anthropic, Groq, Bedrock, etc.), without erroneously filtering LiteLLM itself when hosted on a LAN/network IP.
3. **Dynamic Model Switching**: Expose a compact, capability-aware model selector inside the chat composer bar next to the send button, allowing rapid on-the-fly switching between models.
4. **Multimodal Chat Support**: Enable image input (both file attachment 📎 and direct clipboard paste `Ctrl+V` with thumbnail preview chips) for vision-capable models, integrated with Microsoft.Extensions.AI and tool execution.
5. **Comprehensive Testing**: Unit tests, Web API integration tests, and live LiteLLM E2E tests for capability parsing, local exclusion, and multimodal chat.

---

## 2. Architecture & Data Flow

```
+-----------------------------------------------------------------------------------------------+
|                                      User Interface Layer                                     |
|  +-----------------------------------------------------------------------------------------+  |
|  |                             AiAssistantTabControl.axaml                                 |  |
|  |   - Message List (renders text, tool cards, and image thumbnails)                       |  |
|  |   - Composer Bar:                                                                       |  |
|  |     [ 📎 Attach ] [ Prompt TextBox (Ctrl+V Paste) ] [ Model Selector (👁️⚡) ] [ 🚀 Send ] |  |
|  |     [ Staged Image Thumbnails Bar (with ✕ dismiss buttons)                            ] |  |
|  +-----------------------------------------------------------------------------------------+  |
|                                              |                                                |
|                                              v                                                |
|                                     AiAssistantViewModel                                      |
+-----------------------------------------------------------------------------------------------+
                                               |
                   Direct / In-process or HTTP Client (/api/ai/models, /api/ai/chat)
                                               |
+-----------------------------------------------------------------------------------------------+
|                               ASP.NET Core Server / Services Layer                            |
|                                                                                               |
|  +-----------------------------------------------------------------------------------------+  |
|  |                                  AiAssistantEndpoints                                   |  |
|  |   - GET /api/ai/models?includeLocal=false&refresh=false                                 |  |
|  |   - POST /api/ai/chat (multimodal attachments + streaming SSE)                          |  |
|  +-----------------------------------------------------------------------------------------+  |
|                                              |                                                |
|                                              v                                                |
|  +-----------------------------------------------------------------------------------------+  |
|  |                                   AiAssistantService                                    |  |
|  |   1. GetModelCapabilitiesAsync():                                                       |  |
|  |      - Fetch /model/info (fallback to /v1/models) from LiteLLM proxy                    |  |
|  |      - Filter out local providers (Ollama, local, llama.cpp)                            |  |
|  |      - Parse mode, supports_vision, supports_function_calling, max_tokens               |  |
|  |   2. SendChatAsync() / StreamChatAsync():                                               |  |
|  |      - Build ChatMessage with TextContent & ImageContent (base64/data URI)              |  |
|  |      - Wrap ChatClient in FunctionInvokingChatClient for native tool calling             |  |
|  +-----------------------------------------------------------------------------------------+  |
+-----------------------------------------------------------------------------------------------+
                                               |
                                     HTTP / OpenAI Protocol
                                               v
+-----------------------------------------------------------------------------------------------+
|                            Network-Hosted LiteLLM Proxy / Upstream LLMs                       |
|   Endpoints:                                                                                  |
|   - GET /model/info (model metadata, capabilities, context limits)                            |
|   - GET /v1/models  (OpenAI-compatible models catalog)                                        |
|   - POST /v1/chat/completions (text + vision multimodal execution)                            |
|                                                                                               |
|   Providers:                                                                                  |
|   - Cloud: Vertex AI (gemini-2.5-flash), OpenAI (gpt-4o), Anthropic (claude-3-5), Groq        |
|   - Local (Filtered Out): Ollama, llama.cpp, local/                                           |
+-----------------------------------------------------------------------------------------------+
```

---

## 3. Detailed Data Models

### 3.1 `AiModelCapabilityInfo` (`LocalLLMServerManager.Shared/Models/AiAssistantModels.cs`)
```csharp
public record AiModelCapabilityInfo(
    string Id,
    string DisplayName,
    string Provider,                     // e.g. "vertex_ai", "openai", "anthropic", "groq"
    string Mode = "chat",                // "chat", "completion", "image_generation", etc.
    bool SupportsVision = false,         // 👁️ Multimodal image input
    bool SupportsFunctionCalling = true, // ⚡ Tool / function calling
    bool SupportsAudio = false,          // 🎙️ Audio input/output
    int? MaxInputTokens = null,          // Context window (e.g. 1,000,000 for Gemini Flash)
    int? MaxOutputTokens = null,         // Output token limit
    bool IsLocal = false                 // True if hosted on Ollama / local runtime
)
{
    public string SummaryBadge =>
        $"{(SupportsVision ? "👁️ " : "")}{(SupportsFunctionCalling ? "⚡ " : "")}{(MaxInputTokens > 0 ? $"{FormatTokenCount(MaxInputTokens.Value)} " : "")}[{Provider}]";

    private static string FormatTokenCount(int tokens) =>
        tokens >= 1_000_000 ? $"{tokens / 1_000_000.0:0.#}M" : (tokens >= 1_000 ? $"{tokens / 1_000}k" : $"{tokens}");
}
```

### 3.2 Multimodal Attachment Model
```csharp
public class AiChatMessageAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "image/png"; // "image/jpeg", "image/png", "image/webp"
    public string Base64Data { get; set; } = "";           // Raw base64 or data URI
    public byte[]? RawBytes { get; set; }
}

// In AiChatMessageItem:
public List<AiChatMessageAttachment> Attachments { get; set; } = new();
public bool HasAttachments => Attachments != null && Attachments.Count > 0;
```

---

## 4. Local Model Exclusion Rules

LiteLLM itself is often hosted on the local network (e.g., `http://192.168.1.100:4000/v1` or `http://10.0.0.5:4000/v1`). Therefore, local filtering **must not** filter based on IP addresses.

A model is classified as **Local** (`IsLocal = true`) and excluded from the chat list when:
1. `litellm_provider` equals `"ollama"`, `"local"`, `"llama.cpp"`, or `"vllm_local"`.
2. Model ID or name begins with `"ollama/"`, `"local/"`, `"llama/"`, or `"ollama_chat/"`.
3. LiteLLM explicitly reports `mode: "local"` or upstream configuration denotes an embedded local runner.

All cloud-backed providers (`vertex_ai`, `openai`, `anthropic`, `gemini`, `groq`, `azure`, `bedrock`, `mistral`, `deepseek`, `cohere`, etc.) are retained.

---

## 5. Service & API Layer

### 5.1 `IAiAssistantService` Interface Updates
```csharp
public interface IAiAssistantService
{
    Task<AiValidationResult> ValidateConnectionAsync(
        string? endpoint = null,
        string? apiKey = null,
        string? model = null,
        CancellationToken cancellationToken = default);

    Task<List<AiModelCapabilityInfo>> GetModelCapabilitiesAsync(
        string? endpoint = null,
        string? apiKey = null,
        bool includeLocal = false,
        CancellationToken cancellationToken = default);

    Task<List<string>> GetAvailableModelsAsync(
        string? endpoint = null,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    Task<AiChatResponse> SendChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}
```

### 5.2 Discovery & Parsing Pipeline (`AiAssistantService.cs`)
1. **Endpoint Resolution**:
   - Primary: `GET /model/info` (or `/v1/model_info`).
   - Fallback: `GET /v1/models` (or `/models`).
2. **Parsing**:
   - Extract `id`, `model_info` object (`mode`, `litellm_provider`, `supports_vision`, `supports_function_calling`, `max_tokens`, `max_input_tokens`, `max_output_tokens`).
   - Heuristic fallback: If `model_info` is sparse or omitted by a generic proxy, infer capabilities from well-known model ID patterns (e.g., `gemini-2.5-flash` or `gpt-4o` have `SupportsVision = true`, `SupportsFunctionCalling = true`).
3. **Filtering**:
   - Apply `!IsLocalModel(id, provider)` when `includeLocal` is false.
4. **Caching**:
   - Cache results in-memory with a 5-minute TTL, invalidable on validation or explicit refresh.

### 5.3 Web Endpoints (`AiAssistantEndpoints.cs`)
- `GET /api/ai/models`:
  - Supports query parameters `?endpoint=...`, `?apiKey=...`, `?includeLocal=false`, and `?refresh=true`.
  - Returns JSON array of `AiModelCapabilityInfo`.

---

## 6. Multimodal Orchestration with `Microsoft.Extensions.AI`

When building messages for `OpenAIClient` and `FunctionInvokingChatClient`:
- Messages without attachments: `new ChatMessage(ChatRole.User, msg.Content)`
- Messages with attachments:
  ```csharp
  var contents = new List<AIContent>
  {
      new TextContent(msg.Content)
  };
  foreach (var att in msg.Attachments)
  {
      if (att.RawBytes != null && att.RawBytes.Length > 0)
      {
          contents.Add(new ImageContent(att.RawBytes, att.ContentType));
      }
      else if (!string.IsNullOrWhiteSpace(att.Base64Data))
      {
          var uri = att.Base64Data.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
              ? new Uri(att.Base64Data)
              : new Uri($"data:{att.ContentType};base64,{att.Base64Data}");
          contents.Add(new ImageContent(uri, att.ContentType));
      }
  }
  messages.Add(new ChatMessage(ChatRole.User, contents));
  ```
- Tool calling remains fully operational with multimodal input.

---

## 7. UI/UX Design

### 7.1 Composer Bar (`AiAssistantTabControl.axaml`)
- **Staged Attachments Tray**: Horizontal scroll bar above input text displaying thumbnail preview chips with file name and `✕` removal button.
- **Input Bar**:
  - `📎 Attach Button`: Launches Avalonia `StorageProvider.OpenFilePickerAsync` filtered to image extensions (`*.png`, `*.jpg`, `*.jpeg`, `*.webp`, `*.gif`).
  - `Prompt TextBox`: Multi-line text editor with clipboard paste handling for copied screenshots (`Ctrl+V`).
  - `Model Selector ComboBox`: Compact styled dropdown placed directly in the composer bar:
    - Displays: `SelectedModelInfo.DisplayName` or ID, `SelectedModelInfo.Provider` badge, and icons `👁️` (Vision) and `⚡` (Tools).
  - `🚀 Send / ⏹️ Stop Button`: Executes chat completion or cancels streaming.

### 7.2 Chat History Bubble
- User bubble renders image preview thumbnails when attachments exist.
- Assistant bubble displays streaming tokens, markdown content, and tool execution status cards.

---

## 8. Error Handling & Guard Rails

1. **Non-Vision Warning**: If the user stages an image while a text-only model is selected, the UI prompts the user to switch to a vision model (e.g. `vertex_ai/gemini-2.5-flash`).
2. **File Size Limit**: Image uploads are capped at 10MB; oversized files show an error notification.
3. **Proxy Fallback**: If LiteLLM `/model/info` returns 404 or fails, fallback to standard `/v1/models`.
4. **Resilient Defaults**: If connection is lost or no models are returned, preserve the active model selection and show a non-intrusive status warning.

---

## 9. Testing & Verification Strategy

### 9.1 Unit Tests (`LocalLLMServerManager.Tests`)
- `AiAssistantServiceTests.cs`:
  - Test parsing LiteLLM `/model/info` response with multiple providers.
  - Assert local models (`ollama/llama3.2`, `local/mistral`) are excluded.
  - Assert cloud models (`vertex_ai/gemini-2.5-flash`, `openai/gpt-4o`) are present with accurate capabilities.
  - Test fallback parsing when `/model/info` is not found.
  - Test multimodal `ChatMessage` construction with `ImageContent`.
- `AiAssistantViewModelTests.cs`:
  - Test staging and removing attachments.
  - Test model switching from capability collection.
- `AiAssistantEndpointsTests.cs`:
  - Test `GET /api/ai/models` endpoint returning `AiModelCapabilityInfo` list.

### 9.2 Live Integration & E2E Tests (`AiAssistantLiveLlmTests.cs`)
- `LiveModelDiscovery_ExcludesLocal_AndLoadsCapabilities`: Queries real LiteLLM endpoint, verifies local exclusion and capability flags on cloud models.
- `LiveMultimodalChat_SendsImageAndReceivesResponse`: Transmits a test image payload to a vision model and verifies response.
- `LiveMultimodalToolCall_ExecutesSuccessfully`: Executes a tool-calling workflow with image input.

### 9.3 Quality Standards
- `dotnet test LocalLLMServerManager.sln`
- `npm run lint`
- `npx tsc --noEmit`
