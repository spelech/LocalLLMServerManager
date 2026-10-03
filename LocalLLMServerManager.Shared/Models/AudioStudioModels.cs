using System;
using System.Net.Http;

namespace LocalLLMServerManager.Shared.ViewModels;

public record AudioWorkflowItem(
    string Id,
    string Name,
    string Filename,
    string Path,
    string Type,
    string Description
);

public record AudioFileItem(
    string Filename,
    string Url,
    long SizeBytes,
    DateTime CreatedAt
);

public record ParamContext(string ApiBase, HttpClient Http);
