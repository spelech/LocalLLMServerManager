namespace LocalLLMServerManager;

/// <summary>
/// Application settings persisted to settings.json next to the executable.
/// </summary>
public record AppSettings(
    string ForgeModelsPath = "",
    string ComfyUiUrl = "http://127.0.0.1:8188",
    string ThreeDModelsPath = "",
    string PreferredImageEngine = "Forge",
    string ComfyUiExecutablePath = ""
);

