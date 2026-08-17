### Task 2: Modernize `AppSettings.cs` & Dynamic Fallback Resolution

**Files:**
- Modify: `LocalLLMServerManager.Shared/Models/AppSettings.cs`
- Modify: `Program.cs`
- Modify: `Services/SettingsService.cs`
- Modify: `LocalLLMServerManager.Tests/AppSettingsTests.cs`

**Interfaces:**
- Consumes: `IToolDiscoveryService`
- Produces: Modernized `AppSettings` record without hardcoded path assumptions, with dynamic resolution via `IToolDiscoveryService` or `Program.ResolvePath`.
  ```csharp
  namespace LocalLLMServerManager;

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
  ```

**Steps:**
1. Update `LocalLLMServerManager.Tests/AppSettingsTests.cs` to test the new `AppSettings` defaults, serialization, and dynamic path resolution without hardcoded `D:\AI` or `%APPDATA%\AI` defaults.
2. Run tests to observe failure (`dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~AppSettingsTests"`).
3. Update `LocalLLMServerManager.Shared/Models/AppSettings.cs`, `Program.cs` (register `IToolDiscoveryService` in DI, ensure `ResolvePath` handles empty/null paths with discovery/user fallback), and `Services/SettingsService.cs`.
4. Ensure any existing tests referencing `AppSettings` defaults pass or are updated to match the new dynamic defaults.
5. Run full test suite: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`.
6. Commit changes: `git commit -m "feat: modernize AppSettings with dynamic discovery fallbacks"`.
