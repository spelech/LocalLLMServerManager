using System;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.ViewModels;

public record OllamaModelItem(
    string Name,
    string FormatSize,
    string CapabilityTag,
    string CapabilityColor,
    bool IsLoaded,
    QuickFitBadge? FitBadge = null,
    long SizeBytes = 0,
    bool IsSelected = false
);

public record HuggingFaceRepoItem(
    string Id,
    string Author,
    int Likes,
    string Downloads,
    string PipelineTag = "",
    QuickFitBadge? FitBadge = null
);

public record HfFileQuantItem(
    string Filename,
    string Quantization,
    string FormatSize,
    long SizeBytes,
    QuickFitBadge? FitBadge = null
)
{
    public bool IsGguf => (Filename ?? "").EndsWith(".gguf", StringComparison.OrdinalIgnoreCase);
}

public record CivitaiModelItem(
    int Id,
    string Name,
    string Type,
    string ThumbnailUrl,
    string DownloadUrl,
    string FileName,
    double Rating,
    int DownloadCount,
    QuickFitBadge? FitBadge = null,
    long SizeBytes = 0
);

public record VideoAssetItem(
    string Filename,
    string Url,
    string Duration,
    string Resolution,
    int Fps,
    long Seed,
    long SizeBytes,
    DateTime CreatedAt
);
