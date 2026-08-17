### Task 1: `IToolDiscoveryService` and `ToolDiscoveryService` Implementation & Unit Tests

**Files:**
- Create: `Services/IToolDiscoveryService.cs`
- Create: `Services/ToolDiscoveryService.cs`
- Test: `LocalLLMServerManager.Tests/ToolDiscoveryServiceTests.cs`

**Interfaces:**
- Produces:
  ```csharp
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
  ```

**Steps:**
1. Write unit tests for `ToolDiscoveryService` in `LocalLLMServerManager.Tests/ToolDiscoveryServiceTests.cs`.
2. Run test to verify it fails (`dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~ToolDiscoveryServiceTests"`).
3. Implement `Services/IToolDiscoveryService.cs` and `Services/ToolDiscoveryService.cs`:
   - Inspect `PATH`, `%LOCALAPPDATA%\Programs\Ollama`, `%USERPROFILE%`, drive roots (`C:\`, `D:\`, `E:\`), and running processes for Ollama, ComfyUI, and SD Forge.
   - For ComfyUI, check standard runner batch files (`run_nvidia_gpu.bat`, `run_cpu.bat`, `main.py`), models folder, workflows folder.
   - For SD Forge, check `webui-user.bat`, `webui.bat`, `models\Stable-diffusion`.
   - Implement `ValidatePath(string? path, PathTargetType targetType)` verifying file/directory existence, environment variable expansion, and safety.
4. Run tests and verify they pass. Ensure project builds and tests pass cleanly.
5. Commit changes with message `feat: add IToolDiscoveryService and discovery unit tests`.
