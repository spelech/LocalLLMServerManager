namespace LocalLLMServerManager;

/// <summary>
/// Application settings persisted to settings.json next to the executable.
/// </summary>
public record AppSettings(
    string ForgeModelsPath = "",
    string ComfyUiUrl = "http://127.0.0.1:8188",
    string ThreeDModelsPath = "",
    string WorkflowsPath = "",
    string PreferredImageEngine = "comfy",
    string ComfyUiExecutablePath = "",
    string ForgeExecutablePath = "",
    string OllamaExecutablePath = "ollama",
    string ServiceName = "LocalLLMServerManager",
    string PublishOutputPath = "C:\\LocalLLMServerManager",
    string ComfyModelsPath = "",
    string LanAccessUrl = "http://127.0.0.1:5246",
    string SelectedThemeStyle = "semi"
);

