using System;

namespace LocalLLMServerManager.Shared.Models;

public enum LocalModelCategory
{
    Ollama,
    ImageCheckpoint,
    ImageLora,
    Video,
    Audio,
    ThreeD
}

public record LocalModelItem(
    string Id,
    string Name,
    string FileName,
    string FullPath,
    LocalModelCategory Category,
    string Architecture,
    long SizeBytes,
    string FormattedSize,
    string SourceLocation,
    DateTime CreatedAt
);
