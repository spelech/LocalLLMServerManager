using System.Collections.Generic;

namespace LocalLLMServerManager.Shared.ViewModels;

public record DocStep(
    int StepNumber,
    string Title,
    string Action,
    string ExpectedResult,
    int? TargetTab = null
);

public record DocSection(
    string Id,
    string Title,
    string Category,
    string ReadingTime,
    string Icon,
    string Summary,
    string Prerequisite,
    List<DocStep> Steps,
    List<string>? Notes = null,
    List<string>? Warnings = null
);
