### Task 3: Discovery & Path Validation REST Endpoints

**Files:**
- Create: `Endpoints/DiscoveryEndpoints.cs`
- Modify: `Program.cs`
- Test: `LocalLLMServerManager.Tests/DiscoveryEndpointsTests.cs`

**Interfaces:**
- Consumes: `IToolDiscoveryService`, `ISettingsService`
- Produces:
  - `GET /api/system/tools/detect`: Calls `IToolDiscoveryService.DetectAllToolsAsync()` and returns `DiscoveredToolsResult`.
  - `POST /api/system/tools/apply-detected`: Automatically updates any unset/empty properties in `settings.json` with discovered tool paths from `IToolDiscoveryService` without overriding explicitly set paths, saves settings via `ISettingsService`, and returns updated `AppSettings`.
  - `POST /api/system/tools/validate`: Accepts a batch of paths to validate (`ValidatePathsRequest`), runs `IToolDiscoveryService.ValidatePath`, and returns validation results.
  - Extension method: `app.MapDiscoveryEndpoints()`.

**Steps:**
1. Write unit / integration tests in `LocalLLMServerManager.Tests/DiscoveryEndpointsTests.cs` testing `GET /api/system/tools/detect`, `POST /api/system/tools/apply-detected`, and `POST /api/system/tools/validate`.
2. Run test to verify failure (`dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~DiscoveryEndpointsTests"`).
3. Create `Endpoints/DiscoveryEndpoints.cs` and register `app.MapDiscoveryEndpoints()` in `Program.cs`.
4. Run tests and verify they pass.
5. Commit changes: `git commit -m "feat: add REST endpoints for tool discovery and path validation"`.
