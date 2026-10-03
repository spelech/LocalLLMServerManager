# Automated Provider, ComfyUI Workflow, and Expanded MCP Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide end-to-end automated test coverage for Hugging Face Hub, Civitai, ComfyUI/Studio workflows, and expand the embedded MCP server with full programmatic control over all providers.

**Architecture:**
- Create `HuggingFaceProviderTests.cs` with authentic Hugging Face JSON payloads, covering repository search, GGUF sibling quantization parsing, multi-shard resolution, and Avalonia headless UI interaction.
- Create `CivitaiProviderTests.cs` with realistic Civitai V1 API mocks, covering model querying, version and safetensors file parsing, filter toggles, and Avalonia headless UI inspection.
- Create `ComfyWorkflowGenerationTests.cs` testing prompt-to-graph translation, parameter injection (steps, CFG, denoise, seed, dimensions), and responsive cancellation handling.
- Expand `LocalLlmMcpTools.cs` with `search_huggingface`, `search_civitai`, and `run_studio_workflow` MCP tools, enabling full AI agent programmatic control over every AI provider.

**Tech Stack:** .NET 10.0, Avalonia 11.2 (Headless), xUnit, Moq, System.Text.Json, ModelContextProtocol.AspNetCore.

---

### Task 1: Hugging Face Provider & UI Interaction Test Suite (`HuggingFaceProviderTests.cs`)
- Implement full API contract fixtures matching Hugging Face Hub (`/api/models`, `api/hf/search`, `api/hf/model`).
- Test multi-shard GGUF identification and sorting.
- Test headless UI interaction: search execution, repository selection, quantization drawer opening, and hardware fit calculation.

### Task 2: Civitai Provider & UI Interaction Test Suite (`CivitaiProviderTests.cs`)
- Implement realistic Civitai V1 API response fixtures with checkpoints, LoRAs, and multi-version files.
- Test type filtering, sort criteria, and download URL extraction.
- Test headless UI interaction: search typing, chip filtering, starter model inspection, and `OnInspectModelRequested` invocation.

### Task 3: ComfyUI / Studio Workflow Graph Translation & Cancellation Test Suite (`ComfyWorkflowGenerationTests.cs`)
- Test template JSON parsing and prompt graph node substitution.
- Test parameter injection: steps, CFG scale, denoise strength, random seed, width/height, and frame count.
- Test workflow cancellation handling and `/interrupt` endpoint dispatch.

### Task 4: Expand Embedded MCP Server (`LocalLlmMcpTools.cs`)
- Add `search_huggingface` MCP tool to search HF models with quantization breakdowns.
- Add `search_civitai` MCP tool to query Civitai models, versions, and safetensors URLs.
- Add `run_studio_workflow` MCP tool to programmatically generate text, images, audio, or video across all providers.
- Add tests in `McpServerIntegrationTests.cs` verifying tool attributes, schema, and invocation.

### Task 5: Full Suite Verification & Integration
- Run `dotnet test` across the entire solution.
- Run `npm run lint` and `npx tsc --noEmit`.
- Merge worktree changes into `feature/richUIChanges`.
