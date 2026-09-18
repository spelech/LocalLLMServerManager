# AI Assistant Technology Stack & Decision Rationale

This document details the software libraries, frameworks, architectural decisions, and provider targets selected for the In-App AI Assistant in [LocalLLMServerManager](file:///C:/Users/Alias/repos/LocalLLMServerManager).

---

## Technology Stack Summary

| Layer | Technology | Version | Purpose & Rationale |
|---|---|---|---|
| **AI Abstractions** | `Microsoft.Extensions.AI` | `10.10.0` | Official Microsoft unified AI interface (`IChatClient`, middleware, function invocation) |
| **OpenAI Protocol** | `Microsoft.Extensions.AI.OpenAI` / `OpenAI` | `10.10.0` / `2.1.0` | High-performance OpenAI v1/v2 protocol implementation, SSE streaming, Bearer auth |
| **Target Gateway** | **LiteLLM** | Latest | Lightweight OpenAI-compatible proxy gateway with load balancing and routing |
| **Recommended Model** | **Vertex AI Gemini 2.5 Flash** | `vertex_ai/gemini-2.5-flash` | Ultra-low latency (~200ms TTFT), 1M context, cheap token pricing, native tool calling |
| **Living Prompts** | Markdown Documents | Filesystem | Dynamic, version-controlled system prompt guidelines editable without recompilation |
| **UI Framework** | Avalonia UI | `11.2.5` | Cross-platform desktop (Windows/Linux/macOS) and WebAssembly (browser) UI |
| **MVVM Architecture** | `CommunityToolkit.Mvvm` | `8.4.0` | High-performance source-generated observable properties and relay commands |
| **Testing** | `xUnit.v3`, `Moq`, `Avalonia.Headless.XUnit` | `3.2.2` / `4.20.72` / `12.1.2` | Headless visual tree tests, mock unit tests, live LLM integration tests |

---

## Architectural Decision Records (ADRs)

### ADR 1: Why Microsoft.Extensions.AI instead of LangChain.NET or Raw HttpClient?
- **Standardization**: `Microsoft.Extensions.AI` is the official Microsoft standard released with .NET 9 and .NET 10. It establishes standard abstractions across OpenAI, Azure OpenAI, Ollama, Anthropic, and custom models.
- **Pipeline Middleware Architecture**: Provides composable wrappers like `FunctionInvokingChatClient`, `LoggingChatClient`, and `OpenTelemetryChatClient` that chain seamlessly.
- **Automatic Function Calling**: Generates JSON Schema tool declarations directly from standard C# method signatures, parameter types, and `[Description]` attributes using reflection—eliminating fragile manual schema writing.
- **Lightweight & Native**: Zero Python runtime dependencies, no sidecar processes, and near-zero memory footprint.

### ADR 2: Why Target LiteLLM + Google Cloud Vertex AI Gemini Flash?
- **VRAM Conservation**: Local models loaded in Ollama, ComfyUI, or Forge consume precious GPU VRAM (4GB–24GB). Running an external LLM for assistant tasks guarantees the assistant is **always available**, even when local engines are busy, crashing, or allocating 100% of VRAM for 4K video/image generation.
- **Gemini 2.5 Flash (`vertex_ai/gemini-2.5-flash`)**:
  - Extremely fast time-to-first-token (~200–400ms).
  - High accuracy on multi-turn tool calling and schema compliance.
  - Very large context window (1,000,000+ tokens) allowing full inclusion of app documentation, capabilities, and system logs without truncation.
  - Minimal cost (~$0.075 per 1M tokens), making continuous copilot assistance cost-effective.
- **LiteLLM Proxy**:
  - Exposes standard OpenAI `/v1/chat/completions` and `/v1/models` endpoints.
  - Handles Google Cloud IAM / Service Account authentication transparently, eliminating the need for complex GCP SDK dependencies in this application.
  - Allows seamless switching between Gemini Flash, Claude 3.5 Sonnet, GPT-4o, or local Ollama endpoints simply by changing the model string.

### ADR 3: Why Living Prompts in Markdown?
- Prompts stored directly in source code strings become rigid, difficult to read, and impossible to adjust without re-building and re-deploying the binary.
- Storing prompts as clean Markdown files in `Prompts/` (`system-prompt.md`, `capabilities.md`, `workflows.md`, `app-control.md`):
  - Enables version control via Git alongside code.
  - Enables users and developers to edit prompts in any markdown editor.
  - Supports runtime hot-reloading (`POST /api/ai/prompts/reload` or UI button) with immediate cache invalidation.
  - Allows directory overriding via `AppSettings.AiAssistantPromptsDirectory`.

### ADR 4: Why Dual Desktop & WASM UI Support?
- `LocalLLMServerManager` runs in two primary modes:
  1. **Native Desktop Application**: Standalone GUI with direct in-process access to OS APIs, GPUs, and services.
  2. **Browser Client / LAN Access**: Hosted WebAssembly client connecting over the network to the server.
- `AiAssistantViewModel` supports both modes:
  - If direct `IAiAssistantService` is injected (Desktop mode), it streams directly in-process via C# async streams.
  - If only `HttpClient` is available (WASM / Remote Browser mode), it communicates with `/api/ai/chat` via SSE streaming (`text/event-stream`).
