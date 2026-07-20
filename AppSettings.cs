namespace LocalLLMServerManager;

/// <summary>
/// Application settings persisted to settings.json next to the executable.
/// </summary>
public record AppSettings(string ForgeModelsPath = "");
