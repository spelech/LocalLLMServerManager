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

    public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;
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
