# Task 3 Report: Discovery & Path Validation REST Endpoints

## Work Completed
1. **Implemented Discovery Endpoints (`Endpoints/DiscoveryEndpoints.cs`)**:
   - `GET /api/system/tools/detect`: Injects `IToolDiscoveryService`, invokes `DetectAllToolsAsync()`, and returns `DiscoveredToolsResult`.
   - `POST /api/system/tools/apply-detected`: Automatically detects installed tools via `IToolDiscoveryService`, checks existing `AppSettings` from `ISettingsService`, and populates any unset or whitespace tool paths (`ForgeModelsPath`, `ThreeDModelsPath`, `WorkflowsPath`, `ComfyUiExecutablePath`, `ForgeExecutablePath`, `OllamaExecutablePath`, `ComfyModelsPath`) without overwriting explicitly set user paths, persists the updated settings, and returns the updated `AppSettings`.
   - `POST /api/system/tools/validate`: Accepts a batch validation request (`ValidatePathsRequest`) containing item lists, dictionary paths, or named properties (`ForgeModelsPath`, `ThreeDModelsPath`, `WorkflowsPath`, `ComfyModelsPath`, `ComfyUiExecutablePath`, `ForgeExecutablePath`, `OllamaExecutablePath`), validates each with `IToolDiscoveryService.ValidatePath`, and returns `ValidatePathsResponse` with per-path results and an `AllValid` summary boolean.
   - `MapDiscoveryEndpoints` extension method on `WebApplication`.

2. **Registered Endpoints in `Program.cs`**:
   - Added `app.MapDiscoveryEndpoints()` inside `CreateWebApplication`.

3. **Added Validation Request / Response Models (`Services/IToolDiscoveryService.cs`)**:
   - `PathValidationItem(string? Path, PathTargetType TargetType = PathTargetType.Directory, string? Key = null)`
   - `ValidatePathsRequest(List<PathValidationItem>? Items, Dictionary<string, PathTargetType>? Paths, ...)`
   - `ValidatePathsResponse(Dictionary<string, PathValidationResult> Results, bool AllValid)`

4. **Added Comprehensive Integration & Unit Tests (`LocalLLMServerManager.Tests/DiscoveryEndpointsTests.cs`)**:
   - `DetectToolsEndpoint_ReturnsDiscoveredToolsResult`: Validates HTTP 200 and schema for `GET /api/system/tools/detect`.
   - `ApplyDetectedToolsEndpoint_UpdatesEmptySettingsWithoutOverridingExisting`: Validates that empty path settings are updated while custom configured paths are strictly preserved across memory and persistence.
   - `ApplyDetectedTools_WhenAllPropertiesSet_DoesNotOverwriteAny`: Confirms that when all properties are explicitly configured, none are modified.
   - `ValidatePathsEndpoint_ValidatesBatchOfPaths`: Verifies mixed valid and missing files/directories and assertions on `AllValid == false`.
   - `ValidatePathsEndpoint_AllValid_ReturnsAllValidTrue`: Tests batch where all paths exist, verifying `AllValid == true`.
   - `ValidatePathsEndpoint_WithNamedProperties_ValidatesSuccessfully`: Tests validation with direct named properties.
   - `ValidatePathsEndpoint_WithPathsDictionary_ValidatesSuccessfully`: Tests validation via dictionary map.
   - `ValidatePathsEndpoint_WithAllPropertiesNull_ReturnsEmptyResultsAndAllValidTrue`: Tests empty/null payload safety.
   - Updated `EndpointRegistrationCoverageTests.cs` to test registration coverage.

## Verification
- Target test suite: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~DiscoveryEndpointsTests|FullyQualifiedName~ToolDiscoveryServiceTests|FullyQualifiedName~AppSettingsTests|FullyQualifiedName~EndpointRegistrationCoverageTests"` -> **27 passed, 0 failed** (395 ms).
- Solution build: `dotnet build LocalLLMServerManager.sln` -> **0 errors**.

## Commits Created
- `3a83667`: `feat: add REST endpoints for tool discovery and path validation`
