# Task 2 Report: MainViewModel Tool Paths & Settings Persistence Commands

## Summary
Added tool path and engine settings observable properties to `MainViewModel`, implemented `LoadSettingsAsync` and `SaveSettingsAsync` RelayCommands, created comprehensive unit tests in `MainViewModelCoverageTests.cs`, verified test failure before implementation, and confirmed all tests pass post-implementation.

## Changes Made

1. **`LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`**:
   - Added observable properties with defaults:
     - `_comfyUiExecutablePath = "%APPDATA%\\AI\\ComfyUI\\run_nvidia_gpu.bat"`
     - `_forgeExecutablePath = "%APPDATA%\\AI\\SD_Forge\\webui-user.bat"`
     - `_forgeModelsPath = "%APPDATA%\\AI\\SD_Forge\\models"`
     - `_threeDModelsPath = "%APPDATA%\\AI\\3d_outputs"`
     - `_workflowsPath = "%APPDATA%\\AI\\Workflows"`
     - `_comfyUiUrl = "http://127.0.0.1:8188"`
     - `_preferredImageEngine = "Forge"`
   - Implemented `[RelayCommand] public async Task LoadSettingsAsync()` to fetch settings from GET `/api/settings` and populate ViewModel properties.
   - Implemented `[RelayCommand] public async Task SaveSettingsAsync()` to post an `AppSettings` record payload to POST `/api/settings` endpoint.

2. **`LocalLLMServerManager.Tests/MainViewModelCoverageTests.cs`**:
   - Added `MainViewModel_HasSettingsObservableProperties_DefaultsAreSet` test asserting default property values.
   - Added `LoadSettingsAsync_PopulatesPropertiesFromHttpResponse` test asserting property population from HTTP response.
   - Added `SaveSettingsAsync_PostsAppSettingsToEndpoint` test verifying `AppSettings` payload is sent to `/api/settings`.

3. **`LocalLLMServerManager.Shared/AppSettings.cs`**:
   - Moved `AppSettings.cs` into `LocalLLMServerManager.Shared` project so it is shared across viewmodels, server endpoints, and tests.

## Verification
- Verified initial unit test compilation/failure prior to `MainViewModel` changes.
- Ran unit tests targeting `MainViewModelCoverageTests` (10 passed, 0 failed).
- Ran full test suite across the solution (57 passed, 0 failed).
- Committed changes to git with commit message: `"feat(viewmodel): add tool path observable properties and SaveSettingsAsync command"`.
