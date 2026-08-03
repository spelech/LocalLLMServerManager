# Task 1 Report: AppSettings Record & Environment Path Resolution in Program.cs

## Summary
Updated `AppSettings` default values to use `%APPDATA%\AI\...` paths, added `WorkflowsPath` and `OllamaExecutablePath` properties, implemented `Program.ResolvePath(string? rawPath, string fallbackRelativePath)` for environment variable path expansion, replaced hardcoded `D:\AI` path references in `Program.cs`, and verified unit tests.

## Changes Made
1. **`AppSettings.cs`**:
   - `ForgeModelsPath` default set to `"%APPDATA%\\AI\\SD_Forge\\models"`
   - `ThreeDModelsPath` default set to `"%APPDATA%\\AI\\3d_outputs"`
   - `WorkflowsPath` added with default `"%APPDATA%\\AI\\Workflows"`
   - `ComfyUiExecutablePath` default set to `"%APPDATA%\\AI\\ComfyUI\\run_nvidia_gpu.bat"`
   - `ForgeExecutablePath` default set to `"%APPDATA%\\AI\\SD_Forge\\webui-user.bat"`
   - `OllamaExecutablePath` added with default `"ollama"`

2. **`Program.cs`**:
   - Added `public static string ResolvePath(string? rawPath, string fallbackRelativePath)` method which expands environment variables via `Environment.ExpandEnvironmentVariables` and returns `Path.GetFullPath(expanded)`.
   - Replaced hardcoded `D:\AI\...` literals in `ComfyUI` start endpoint (`/api/comfy/start`) and `SD Forge` start endpoint (`/api/forge/start`) with `Program.ResolvePath(...)`.
   - Updated `CivitAI` download endpoint and 3D files listing endpoint to resolve configured model paths using `ResolvePath`.

3. **`LocalLLMServerManager.Tests/AppSettingsTests.cs`**:
   - Added unit test `AppSettings_DefaultValues_UseAppDataAiPaths` asserting all tool default paths contain `%APPDATA%`.
   - Added unit test `ResolvePath_ExpandsEnvironmentVariables_Correctly` verifying path expansion without leftover `%APPDATA%` environment tokens.
   - Added unit test `ResolvePath_WithNullOrEmpty_UsesFallback` verifying fallback handling.

## Verification
- Verified initial test failure prior to `AppSettings.cs` & `Program.cs` changes.
- Executed `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`.
- Result: **59 passed**, 0 failed.
- Staged and committed changes to git with commit message: `"feat(settings): update AppSettings defaults to %APPDATA%/AI and add environment path resolver"`.
