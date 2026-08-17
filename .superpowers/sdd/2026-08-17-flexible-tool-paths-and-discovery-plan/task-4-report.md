# Task 4 Report: Avalonia Desktop UI: File/Folder Pickers, Auto-Detect & Status Indicators

## Work Completed

1. **Enhanced `SettingsViewModel.cs` (`LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs`)**:
   - **Auto-Detection Command**: Implemented `AutoDetectToolsCommand` (`AutoDetectToolsAsync`) which queries `GET /api/system/tools/detect`, parses discovered tool paths for Ollama, ComfyUI, and SD Forge, updates empty path fields on the ViewModel, updates status badges to `🔍 Auto-Discovered`, and triggers toast notifications.
   - **Native Browse Commands**:
     - `BrowseComfyExecutableCommand` (`BrowseComfyExecutableAsync`): Opens file picker with `.bat`, `.cmd`, `.exe` filters for ComfyUI.
     - `BrowseForgeExecutableCommand` (`BrowseForgeExecutableAsync`): Opens file picker with `.bat`, `.cmd`, `.exe` filters for SD Forge.
     - `BrowseOllamaExecutableCommand` (`BrowseOllamaExecutableAsync`): Opens file picker with `.exe`, `.cmd`, `.bat` filters for Ollama.
     - `BrowseForgeModelsCommand` (`BrowseForgeModelsAsync`): Opens directory picker for Forge models.
     - `BrowseComfyModelsCommand` (`BrowseComfyModelsAsync`): Opens directory picker for ComfyUI models.
     - `BrowseThreeDModelsCommand` (`BrowseThreeDModelsAsync`): Opens directory picker for 3D models output.
     - `BrowseWorkflowsCommand` (`BrowseWorkflowsAsync`): Opens directory picker for Workflows directory.
   - **Live Real-Time Status Properties**:
     - Added `ComfyUiExecutableStatus`, `ForgeExecutableStatus`, `OllamaStatus` (with `OllamaExecutableStatus` alias), `ForgeModelsStatus`, `ComfyModelsStatus`, `ThreeDModelsStatus`, and `WorkflowsStatus`.
     - Dynamically evaluates paths with `EvaluateExecutableStatus` (checking `File.Exists`, PATH expansion, and environment variables) and `EvaluateDirectoryStatus` (`Directory.Exists`).
     - Added `StorageProvider` (`IStorageProvider?`) observable property for Avalonia top-level binding and unit test mock injection.

2. **Updated Avalonia XAML View (`LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`)**:
   - Added `"🔍 Auto-Detect Installed Tools"` glass-primary button card at the top of the Settings view.
   - For every path configuration (ComfyUI Executable, Forge Executable, Ollama Executable, Forge Models, ComfyUI Models, 3D Models, Workflows):
     - Added a status indicator pill badge (`telemetry-pill`) next to each label showing real-time verified state (`🟢 Verified` / `⚠️ Missing` / `🔍 Auto-Discovered`).
     - Added a `📁 Browse...` glass button adjacent to each path input field bound to its corresponding browse command.

3. **Updated Code-Behind (`LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml.cs`)**:
   - Implemented `OnDataContextChanged` and `OnAttachedToVisualTree` to automatically attach `TopLevel.GetTopLevel(this)?.StorageProvider` to `vm.StorageProvider`.

4. **Created Comprehensive Unit Tests (`LocalLLMServerManager.Tests/SettingsViewModelCoverageTests.cs`)**:
   - `DefaultState_HasExpectedPropertiesAndMissingStatus`: Verifies initial status evaluation.
   - `PathChanges_UpdateStatusIndicatorsDynamically`: Tests real-time reactive badge transitions between Verified/Found and Missing states for files and directories.
   - `AutoDetectToolsAsync_PopulatesEmptyPathsAndDiscoveredStatus`: Validates auto-detection HTTP response parsing, path population, and status updating.
   - `AutoDetectToolsAsync_DoesNotOverwriteExistingConfiguredPaths`: Ensures user-customized paths are never overwritten by auto-discovery.
   - `AutoDetectToolsAsync_HandlesNetworkFailureGracefully`: Tests exception resilience and graceful UI error reporting.
   - `BrowseFileCommands_WithStorageProvider_SetsSelectedPath`: Tests mock `IStorageProvider.OpenFilePickerAsync` for executable commands.
   - `BrowseFolderCommands_WithStorageProvider_SetsSelectedPath`: Tests mock `IStorageProvider.OpenFolderPickerAsync` for directory commands.
   - `BrowseCommands_WhenPickerCancelled_KeepsOriginalPath`: Verifies cancelled picker dialogs preserve existing paths.
   - `LoadAndSaveSettings_IncludesAllToolPaths`: Tests end-to-end serialization and deserialization against settings API.
   - `SwitchThemeStyle_UpdatesSelectedThemeStyle`: Validates theme switching command.

## Verification
- **Settings ViewModel Tests**: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelCoverageTests"` -> **10 passed, 0 failed** (478 ms).
- **Comprehensive Test Suite**: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName!~LiveExternalProvider&FullyQualifiedName!~Playwright"` -> **163 passed, 0 failed** (47 s).
- **Solution Build**: `dotnet build LocalLLMServerManager.sln` -> **0 errors**.

## Commits Created
- `9f417d7`: `feat: add desktop file/folder pickers, auto-detect command, and status badges`
