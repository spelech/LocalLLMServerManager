### Task 4: Avalonia Desktop UI: File/Folder Pickers, Auto-Detect & Status Indicators

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/SettingsViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/SettingsTabControl.axaml.cs`
- Test: `LocalLLMServerManager.Tests/SettingsViewModelCoverageTests.cs`

**Interfaces:**
- Consumes: Avalonia `TopLevel` / `IStorageProvider` for native file/folder pickers, `/api/system/tools/detect` & `/api/system/tools/validate`.
- Produces:
  - `AutoDetectToolsCommand`: Queries `/api/system/tools/detect` via HttpClient or runs discovery, automatically sets discovered paths on the ViewModel for any empty path fields, and refreshes validation statuses.
  - Browse commands:
    - `BrowseComfyExecutableCommand` (OpenFilePickerAsync with `.bat`, `.cmd`, `.exe` filters)
    - `BrowseForgeExecutableCommand` (OpenFilePickerAsync with `.bat`, `.cmd`, `.exe` filters)
    - `BrowseForgeModelsCommand` (OpenFolderPickerAsync)
    - `BrowseThreeDModelsCommand` (OpenFolderPickerAsync)
    - `BrowseWorkflowsCommand` (OpenFolderPickerAsync)
  - Live Status Properties:
    - `ComfyUiExecutableStatus`, `ForgeExecutableStatus`, `ForgeModelsStatus`, `ThreeDModelsStatus`, `WorkflowsStatus`, `OllamaStatus`
  - XAML in `SettingsTabControl.axaml`:
    - "🔍 Auto-Detect Installed Tools" button at top.
    - Each path input has a TextBox, a `📁 Browse...` button, and a status pill badge displaying its verified state (🟢 Found / ⚠️ Missing / 🔍 Auto-Discovered).

**Steps:**
1. Write unit tests in `LocalLLMServerManager.Tests/SettingsViewModelCoverageTests.cs` verifying auto-detection, path status evaluations, and browse callbacks.
2. Run test to verify failure (`dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~SettingsViewModelCoverageTests"`).
3. Implement `SettingsViewModel.cs` properties and commands.
4. Update `SettingsTabControl.axaml` and code-behind `SettingsTabControl.axaml.cs` to bind to browse commands using `TopLevel.GetTopLevel(this)?.StorageProvider`.
5. Run tests and verify they pass (`dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`).
6. Commit changes: `git commit -m "feat: add desktop file/folder pickers, auto-detect command, and status badges"`.
