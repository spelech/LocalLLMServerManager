namespace LocalLLMServerManager;

/// <summary>
/// Application settings persisted to settings.json next to the executable.
/// </summary>
public record AppSettings(
    string ForgeModelsPath = "%APPDATA%\\AI\\SD_Forge\\models",
    string ComfyUiUrl = "http://127.0.0.1:8188",
    string ThreeDModelsPath = "%APPDATA%\\AI\\3d_outputs",
    string WorkflowsPath = "%APPDATA%\\AI\\Workflows",
    string PreferredImageEngine = "Forge",
    string ComfyUiExecutablePath = "%APPDATA%\\AI\\ComfyUI\\run_nvidia_gpu.bat",
    string ForgeExecutablePath = "%APPDATA%\\AI\\SD_Forge\\webui-user.bat",
    string OllamaExecutablePath = "ollama"
);
