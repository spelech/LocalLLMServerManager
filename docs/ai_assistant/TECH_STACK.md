---
title: AI Assistant Technology Stack & Decision Rationale
description: Libraries, frameworks, protocols, and architectural decision records for the AI Assistant.
outline: deep
---

# AI Assistant Technology Stack & Decision Rationale

This document details the software libraries, frameworks, architectural decisions, and provider targets for the In-AI Assistant.

---

## Technology Stack Summary

| Layer | Technology | Version | Purpose and Rationale |
|---|---|---|---|
| **AI Abstractions** | `Microsoft.Extensions.AI` | `10.10.0` | Official Microsoft AI interfaces (`IChatClient`, `ImageContent`, function calling). |
| **OpenAI Protocol** | `Microsoft.Extensions.AI.OpenAI` / `OpenAI` | `10.10.0` / `2.1.0` | OpenAI REST client, SSE streaming, and Bearer token authentication. |
| **Target Gateway** | **LiteLLM** | Latest | Proxy gateway providing model routing and `/model/info` capability discovery. |
| **Recommended Model** | **Vertex AI Gemini 2.5 Flash** | `vertex_ai/gemini-2.5-flash` | Low latency (~200ms), 1M token context, multimodal vision, native tools. |
| **Living Prompts** | Markdown Documents | Filesystem | Modular, version-controlled system prompts with runtime hot-reloading. |
| **Model Metadata** | `AiModelCapabilityInfo` | Internal Record | Rich capability schema with token limits, vision flags, and badge formatting. |
| **UI Framework** | Avalonia UI | `11.2.5` | Cross-platform desktop and WebAssembly chat interface with image attachment. |
| **MVVM Architecture** | `CommunityToolkit.Mvvm` | `8.4.0` | Observable collections, property generation, and async relay commands. |
| **Testing** | `xUnit.v3`, `Moq`, `Avalonia.Headless.XUnit` | `3.2.2` / `4.20.72` / `12.1.2` | Headless visual tree tests, mock unit tests, live LLM integration tests. |

---

## Architectural Decision Records (ADRs)

### ADR 1: Unified AI Abstraction and Multimodal Serialization

- **Context**: The assistant requires tool calling, streaming responses, and image input support.
- **Decision**: Use `Microsoft.Extensions.AI` as the core abstraction.
- **Rationale**:
  - `Microsoft.Extensions.AI` is the official Microsoft standard for modern .NET applications.
  - Middleware wrappers like `FunctionInvokingChatClient` automate tool execution loops.
  - The framework generates JSON Schemas directly from C# method signatures and `[Description]` attributes.
  - Unified `ChatMessage` objects accept both text and `ImageContent` seamlessly.
  - Zero Python runtime dependencies exist in the host application.

### ADR 2: LiteLLM Gateway and Capability Discovery Protocol

- **Context**: Standard OpenAI `/v1/models` endpoints return only model identifiers. They omit context limits, vision support, and tool calling metadata.
- **Decision**: Implement a two-tiered discovery protocol using LiteLLM `/model/info` with `/v1/models` fallback.
- **Rationale**:
  - LiteLLM `/model/info` exposes rich metadata: `max_input_tokens`, `supports_vision`, and `supports_function_calling`.
  - The discovery engine converts this metadata into `AiModelCapabilityInfo` records.
  - The fallback to standard `/v1/models` maintains compatibility with non-LiteLLM OpenAI endpoints.
  - Discovered capabilities populate composer badges (`👁️`, `⚡`, `1M`), giving immediate user feedback.

::: info Discovery Fallback Flow
1. Query `GET {baseUrl}/model/info`.
2. If successful, parse model properties and capability flags.
3. If `/model/info` returns 404 or fails, query `GET {baseUrl}/v1/models`.
4. Fallback parsing infers capabilities from model name patterns.
:::

### ADR 3: Local Model Exclusion Rules

- **Context**: LiteLLM can register local Ollama instances alongside cloud models. Running assistant queries against local models exhausts GPU video memory needed for generative tasks.
- **Decision**: Filter out local models by runtime provider and model identifier prefix. Do not filter by IP address.
- **Rationale**:
  - LiteLLM itself frequently runs on `127.0.0.1` or LAN IP addresses (`192.168.x.x`). Filtering by IP address would block valid cloud proxies.
  - Filtering by provider (`ollama`, `local`, `llama.cpp`) reliably identifies local instances.
  - Filtering by ID prefix (`ollama/`, `local/`) catches unbadged local model entries.
  - Cloud models routed through LiteLLM consume zero local GPU memory.

### ADR 4: Living Prompts in Modular Markdown

- **Context**: System prompts hardcoded into C# source code require compilation and deployment for minor adjustments.
- **Decision**: Store system prompts as clean Markdown files in `Prompts/` on disk.
- **Rationale**:
  - Markdown files reside in version control alongside source code.
  - Developers and users can edit prompts in any text editor.
  - `PromptManagementService` provides runtime cache invalidation via `POST /api/ai/prompts/reload` and UI actions.
  - Custom prompt folders can be specified via `AppSettings.AiAssistantPromptsDirectory`.

### ADR 5: Dual Desktop & WebAssembly UI Support

- **Context**: LocalLLMServerManager runs both as a native desktop application and as a WebAssembly browser app.
- **Decision**: Provide dual execution paths in `AiAssistantViewModel`.
- **Rationale**:
  - When running in desktop mode, the ViewModel calls `IAiAssistantService` directly in-process.
  - When running in WebAssembly mode, the ViewModel connects to `/api/ai/chat` via Server-Sent Events (SSE).
  - The composer bar supports dynamic model selection and image attachments across both execution modes.
