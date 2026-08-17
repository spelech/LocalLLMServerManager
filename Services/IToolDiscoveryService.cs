using System.Threading.Tasks;

namespace LocalLLMServerManager.Services;

public interface IToolDiscoveryService
{
    Task<DiscoveredToolsResult> DetectAllToolsAsync();
    DiscoveredToolInfo DetectOllama();
    DiscoveredToolInfo DetectComfyUi();
    DiscoveredToolInfo DetectForge();
    PathValidationResult ValidatePath(string? path, PathTargetType targetType);
}

public record DiscoveredToolInfo(
    bool IsInstalled,
    string? ExecutablePath,
    string? RootDirectory,
    string? ModelsDirectory,
    string? WorkflowsDirectory,
    string StatusMessage
);

public record DiscoveredToolsResult(
    DiscoveredToolInfo Ollama,
    DiscoveredToolInfo ComfyUi,
    DiscoveredToolInfo Forge,
    string SuggestedThreeDPath,
    string SuggestedWorkflowsPath
);

public enum PathTargetType
{
    Executable,
    Directory
}

public record PathValidationResult(bool Exists, bool IsValid, string? ErrorMessage);

public record PathValidationItem(string? Path, PathTargetType TargetType = PathTargetType.Directory, string? Key = null);

public record ValidatePathsRequest(
    List<PathValidationItem>? Items = null,
    Dictionary<string, PathTargetType>? Paths = null,
    string? ForgeModelsPath = null,
    string? ThreeDModelsPath = null,
    string? WorkflowsPath = null,
    string? ComfyModelsPath = null,
    string? ComfyUiExecutablePath = null,
    string? ForgeExecutablePath = null,
    string? OllamaExecutablePath = null
);

public record ValidatePathsResponse(
    Dictionary<string, PathValidationResult> Results,
    bool AllValid
);

