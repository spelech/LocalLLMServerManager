using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;

namespace LocalLLMServerManager.Shared.Services;

public class PromptManagementService : IPromptManagementService
{
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public const string SystemPromptFileName = "system-prompt.md";
    public const string CapabilitiesFileName = "capabilities.md";
    public const string WorkflowsFileName = "workflows.md";
    public const string AppControlFileName = "app-control.md";

    public async Task<string> GetSystemPromptAsync(string? customPromptDir = null, CancellationToken cancellationToken = default)
    {
        return await LoadPromptDocumentAsync(SystemPromptFileName, FallbackSystemPrompt, customPromptDir, cancellationToken);
    }

    public async Task<string> GetCapabilitiesPromptAsync(string? customPromptDir = null, CancellationToken cancellationToken = default)
    {
        return await LoadPromptDocumentAsync(CapabilitiesFileName, FallbackCapabilitiesPrompt, customPromptDir, cancellationToken);
    }

    public async Task<string> GetWorkflowsPromptAsync(string? customPromptDir = null, CancellationToken cancellationToken = default)
    {
        return await LoadPromptDocumentAsync(WorkflowsFileName, FallbackWorkflowsPrompt, customPromptDir, cancellationToken);
    }

    public async Task<string> GetAppControlPromptAsync(string? customPromptDir = null, CancellationToken cancellationToken = default)
    {
        return await LoadPromptDocumentAsync(AppControlFileName, FallbackAppControlPrompt, customPromptDir, cancellationToken);
    }

    public async Task<string> BuildFullSystemPromptAsync(string? customPromptDir = null, CancellationToken cancellationToken = default)
    {
        var sys = await GetSystemPromptAsync(customPromptDir, cancellationToken);
        var cap = await GetCapabilitiesPromptAsync(customPromptDir, cancellationToken);
        var work = await GetWorkflowsPromptAsync(customPromptDir, cancellationToken);
        var ctrl = await GetAppControlPromptAsync(customPromptDir, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine(sys.Trim());
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(cap.Trim());
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(work.Trim());
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(ctrl.Trim());

        return sb.ToString();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllPromptsAsync(string? customPromptDir = null, CancellationToken cancellationToken = default)
    {
        var sys = await GetSystemPromptAsync(customPromptDir, cancellationToken);
        var cap = await GetCapabilitiesPromptAsync(customPromptDir, cancellationToken);
        var work = await GetWorkflowsPromptAsync(customPromptDir, cancellationToken);
        var ctrl = await GetAppControlPromptAsync(customPromptDir, cancellationToken);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [SystemPromptFileName] = sys,
            [CapabilitiesFileName] = cap,
            [WorkflowsFileName] = work,
            [AppControlFileName] = ctrl,
            ["system-prompt"] = sys,
            ["capabilities"] = cap,
            ["workflows"] = work,
            ["app-control"] = ctrl
        };
        return dict;
    }

    public void InvalidateCache()
    {
        _cache.Clear();
    }

    public static string? ResolvePromptDirectory(string? customPromptDir = null)
    {
        if (!string.IsNullOrWhiteSpace(customPromptDir) && Directory.Exists(customPromptDir))
        {
            return Path.GetFullPath(customPromptDir);
        }

        var candidateDirs = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Prompts"),
            Path.Combine(Directory.GetCurrentDirectory(), "Prompts"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Prompts"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Prompts")
        };

        foreach (var dir in candidateDirs)
        {
            try
            {
                if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, SystemPromptFileName)))
                {
                    return Path.GetFullPath(dir);
                }
            }
            catch { }
        }

        return null;
    }

    private async Task<string> LoadPromptDocumentAsync(string fileName, string fallbackContent, string? customPromptDir, CancellationToken cancellationToken)
    {
        var resolvedDir = ResolvePromptDirectory(customPromptDir);
        var cacheKey = $"{resolvedDir ?? "fallback"}::{fileName}";

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        if (resolvedDir != null)
        {
            var filePath = Path.Combine(resolvedDir, fileName);
            if (File.Exists(filePath))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(filePath, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        _cache[cacheKey] = content;
                        return content;
                    }
                }
                catch { }
            }
        }

        _cache[cacheKey] = fallbackContent;
        return fallbackContent;
    }

    private const string FallbackSystemPrompt = @"# LocalLLMServerManager AI Copilot — System Prompt
You are the LocalLLMServerManager Copilot, an expert AI engineer integrated directly into the application.
Guide users on models, workflows, and settings, and proactively use your tools to perform actions in the app.";

    private const string FallbackCapabilitiesPrompt = @"# Application Capabilities Reference
LocalLLMServerManager supports Ollama LLMs, Stable Diffusion Forge, ComfyUI workflows, Kokoro TTS, and the Can I Run It hardware calculator.";

    private const string FallbackWorkflowsPrompt = @"# Application Workflows Guide
Workflows include LLM downloading and execution, image generation with Forge/ComfyUI, video rendering, and Kokoro speech synthesis.";

    private const string FallbackAppControlPrompt = @"# In-App Natural Language Control Guidelines
Use your registered tools to inspect VRAM, check health, start/stop engines, update settings, and trigger generation workflows.";
}
