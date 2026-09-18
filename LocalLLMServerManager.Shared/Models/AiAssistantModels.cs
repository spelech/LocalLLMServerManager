using System;
using System.Collections.Generic;

namespace LocalLLMServerManager.Shared.Models;

public record AiToolExecutionItem(
    string ToolName,
    string Arguments = "",
    string Result = "",
    bool IsSuccess = true,
    long ExecutionTimeMs = 0,
    bool IsRunning = false
);

public class AiChatMessageAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "image/png";
    public string Base64Data { get; set; } = "";
    public byte[]? RawBytes { get; set; }
}

public record AiModelCapabilityInfo(
    string Id,
    string DisplayName,
    string Provider,
    string Mode = "chat",
    bool SupportsVision = false,
    bool SupportsFunctionCalling = true,
    bool SupportsAudio = false,
    int? MaxInputTokens = null,
    int? MaxOutputTokens = null,
    bool IsLocal = false
)
{
    public string SummaryBadge
    {
        get
        {
            var parts = new List<string>();
            if (SupportsVision) parts.Add("👁️");
            if (SupportsFunctionCalling) parts.Add("⚡");
            if (MaxInputTokens.HasValue && MaxInputTokens.Value > 0) parts.Add(FormatTokenCount(MaxInputTokens.Value));
            var badges = parts.Count > 0 ? $"{string.Join(" ", parts)} " : "";
            return $"{badges}[{Provider}]";
        }
    }

    private static string FormatTokenCount(int tokens) =>
        tokens >= 1_000_000 ? $"{tokens / 1_000_000.0:0.#}M" : (tokens >= 1_000 ? $"{tokens / 1_000}k" : $"{tokens}");
}

public class AiChatMessageItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Role { get; set; } = "user"; // "user", "assistant", "system", "tool"
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsError { get; set; } = false;
    public bool IsLoading { get; set; } = false;
    public string? StatusText { get; set; }
    public List<AiToolExecutionItem> ToolCalls { get; set; } = new();
    public List<AiChatMessageAttachment> Attachments { get; set; } = new();

    public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;
    public bool HasAttachments => Attachments != null && Attachments.Count > 0;
    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.OrdinalIgnoreCase);
    public bool IsSystem => string.Equals(Role, "system", StringComparison.OrdinalIgnoreCase);
}

public record AiValidationResult(
    bool Success,
    string Message,
    List<string> AvailableModels,
    long LatencyMs = 0
);

public record AiChatRequest(
    List<AiChatMessageItem> Messages,
    bool Stream = false,
    string? Model = null,
    double? Temperature = null
);

public record AiChatResponse(
    bool Success,
    AiChatMessageItem? Message = null,
    string? Error = null,
    int? TotalTokens = null
);

public record AiChatChunk(
    string DeltaText = "",
    AiToolExecutionItem? ToolCall = null,
    bool IsDone = false,
    string? Error = null
);
