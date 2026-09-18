using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LocalLLMServerManager.Shared.Interfaces;

/// <summary>
/// Service responsible for managing, loading, and assembling living prompt documents from the filesystem.
/// </summary>
public interface IPromptManagementService
{
    /// <summary>
    /// Gets the core system prompt including persona and tone.
    /// </summary>
    Task<string> GetSystemPromptAsync(string? customPromptDir = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the application capabilities reference prompt.
    /// </summary>
    Task<string> GetCapabilitiesPromptAsync(string? customPromptDir = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the workflows guide prompt.
    /// </summary>
    Task<string> GetWorkflowsPromptAsync(string? customPromptDir = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the natural language app-control guidelines prompt.
    /// </summary>
    Task<string> GetAppControlPromptAsync(string? customPromptDir = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assembles the complete consolidated system prompt containing system instructions, capabilities, workflows, and tool calling guidance.
    /// </summary>
    Task<string> BuildFullSystemPromptAsync(string? customPromptDir = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all living prompts as a key-value dictionary of document names to contents.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetAllPromptsAsync(string? customPromptDir = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears any cached prompts so modifications on disk take effect immediately.
    /// </summary>
    void InvalidateCache();
}
