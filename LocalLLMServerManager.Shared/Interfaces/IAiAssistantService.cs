using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Interfaces;

/// <summary>
/// Orchestration service interface for OpenAI-compatible LLM routing, tool execution loop,
/// and streaming chat completion.
/// </summary>
public interface IAiAssistantService
{
    /// <summary>
    /// Validates connectivity, authentication, and model availability against the target OpenAI-compatible API endpoint.
    /// </summary>
    Task<AiValidationResult> ValidateConnectionAsync(
        string? endpoint = null,
        string? apiKey = null,
        string? model = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerate available model IDs from the remote endpoint (via /v1/models).
    /// </summary>
    Task<List<string>> GetAvailableModelsAsync(
        string? endpoint = null,
        string? apiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerate available model capabilities from the remote LiteLLM or OpenAI-compatible endpoint.
    /// </summary>
    Task<List<AiModelCapabilityInfo>> GetModelCapabilitiesAsync(
        string? endpoint = null,
        string? apiKey = null,
        bool includeLocal = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a full non-streaming multi-turn chat completion including automated tool calling.
    /// </summary>
    Task<AiChatResponse> SendChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams tokens and tool execution events using async enumeration.
    /// </summary>
    IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}
