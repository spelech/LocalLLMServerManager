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
    string AudioPath = "",
    string LanAccessUrl = "http://127.0.0.1:5246",
    string AudioEngineExecutablePath = "",
    string AudioEngineUrl = "http://127.0.0.1:8880",
    string PreferredAudioVoice = "af_heart",
    string VideoModelsPath = "",
    string VideoOutputPath = ""
);

