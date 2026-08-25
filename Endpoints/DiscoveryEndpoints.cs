using System.Collections.Generic;
using System.Linq;
using LocalLLMServerManager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class DiscoveryEndpoints
{
    public static void MapDiscoveryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/system/tools/detect", async (IToolDiscoveryService discoveryService) =>
        {
            var result = await discoveryService.DetectAllToolsAsync();
            return Results.Ok(result);
        });

        app.MapPost("/api/system/tools/apply-detected", async (IToolDiscoveryService discoveryService, ISettingsService settingsService) =>
        {
            var detected = await discoveryService.DetectAllToolsAsync();
            var currentSettings = settingsService.LoadSettings();

            var updatedSettings = currentSettings with
            {
                ForgeModelsPath = string.IsNullOrWhiteSpace(currentSettings.ForgeModelsPath) && !string.IsNullOrWhiteSpace(detected.Forge.ModelsDirectory)
                    ? detected.Forge.ModelsDirectory
                    : currentSettings.ForgeModelsPath,

                ThreeDModelsPath = string.IsNullOrWhiteSpace(currentSettings.ThreeDModelsPath) && !string.IsNullOrWhiteSpace(detected.SuggestedThreeDPath)
                    ? detected.SuggestedThreeDPath
                    : currentSettings.ThreeDModelsPath,

                WorkflowsPath = string.IsNullOrWhiteSpace(currentSettings.WorkflowsPath) && !string.IsNullOrWhiteSpace(detected.SuggestedWorkflowsPath)
                    ? detected.SuggestedWorkflowsPath
                    : currentSettings.WorkflowsPath,

                ComfyUiExecutablePath = string.IsNullOrWhiteSpace(currentSettings.ComfyUiExecutablePath) && !string.IsNullOrWhiteSpace(detected.ComfyUi.ExecutablePath)
                    ? detected.ComfyUi.ExecutablePath
                    : currentSettings.ComfyUiExecutablePath,

                ForgeExecutablePath = string.IsNullOrWhiteSpace(currentSettings.ForgeExecutablePath) && !string.IsNullOrWhiteSpace(detected.Forge.ExecutablePath)
                    ? detected.Forge.ExecutablePath
                    : currentSettings.ForgeExecutablePath,

                OllamaExecutablePath = string.IsNullOrWhiteSpace(currentSettings.OllamaExecutablePath) && !string.IsNullOrWhiteSpace(detected.Ollama.ExecutablePath)
                    ? detected.Ollama.ExecutablePath
                    : currentSettings.OllamaExecutablePath,

                ComfyModelsPath = string.IsNullOrWhiteSpace(currentSettings.ComfyModelsPath) && !string.IsNullOrWhiteSpace(detected.ComfyUi.ModelsDirectory)
                    ? detected.ComfyUi.ModelsDirectory
                    : currentSettings.ComfyModelsPath,

                AudioEngineExecutablePath = string.IsNullOrWhiteSpace(currentSettings.AudioEngineExecutablePath) && !string.IsNullOrWhiteSpace(detected.AudioEngine?.ExecutablePath)
                    ? detected.AudioEngine.ExecutablePath
                    : currentSettings.AudioEngineExecutablePath
            };

            settingsService.SaveSettings(updatedSettings);
            return Results.Ok(updatedSettings);
        });

        app.MapPost("/api/system/tools/validate", (ValidatePathsRequest? request, IToolDiscoveryService discoveryService) =>
        {
            var results = new Dictionary<string, PathValidationResult>();

            if (request == null)
            {
                return Results.Ok(new ValidatePathsResponse(results, true));
            }

            if (request.Items != null)
            {
                for (int i = 0; i < request.Items.Count; i++)
                {
                    var item = request.Items[i];
                    var key = !string.IsNullOrWhiteSpace(item.Key)
                        ? item.Key
                        : (!string.IsNullOrWhiteSpace(item.Path) ? item.Path : $"item_{i}");

                    results[key] = discoveryService.ValidatePath(item.Path, item.TargetType);
                }
            }

            if (request.Paths != null)
            {
                foreach (var kvp in request.Paths)
                {
                    results[kvp.Key] = discoveryService.ValidatePath(kvp.Key, kvp.Value);
                }
            }

            if (request.ForgeModelsPath != null)
            {
                results[nameof(request.ForgeModelsPath)] = discoveryService.ValidatePath(request.ForgeModelsPath, PathTargetType.Directory);
            }

            if (request.ThreeDModelsPath != null)
            {
                results[nameof(request.ThreeDModelsPath)] = discoveryService.ValidatePath(request.ThreeDModelsPath, PathTargetType.Directory);
            }

            if (request.WorkflowsPath != null)
            {
                results[nameof(request.WorkflowsPath)] = discoveryService.ValidatePath(request.WorkflowsPath, PathTargetType.Directory);
            }

            if (request.ComfyModelsPath != null)
            {
                results[nameof(request.ComfyModelsPath)] = discoveryService.ValidatePath(request.ComfyModelsPath, PathTargetType.Directory);
            }

            if (request.ComfyUiExecutablePath != null)
            {
                results[nameof(request.ComfyUiExecutablePath)] = discoveryService.ValidatePath(request.ComfyUiExecutablePath, PathTargetType.Executable);
            }

            if (request.ForgeExecutablePath != null)
            {
                results[nameof(request.ForgeExecutablePath)] = discoveryService.ValidatePath(request.ForgeExecutablePath, PathTargetType.Executable);
            }

            if (request.OllamaExecutablePath != null)
            {
                results[nameof(request.OllamaExecutablePath)] = discoveryService.ValidatePath(request.OllamaExecutablePath, PathTargetType.Executable);
            }

            if (request.AudioEngineExecutablePath != null)
            {
                results[nameof(request.AudioEngineExecutablePath)] = discoveryService.ValidatePath(request.AudioEngineExecutablePath, PathTargetType.Executable);
            }

            if (request.FFmpegExecutablePath != null)
            {
                results[nameof(request.FFmpegExecutablePath)] = discoveryService.ValidatePath(request.FFmpegExecutablePath, PathTargetType.Executable);
            }

            if (request.PythonExecutablePath != null)
            {
                results[nameof(request.PythonExecutablePath)] = discoveryService.ValidatePath(request.PythonExecutablePath, PathTargetType.Executable);
            }

            var allValid = results.Values.All(r => r.IsValid);
            return Results.Ok(new ValidatePathsResponse(results, allValid));
        });
    }
}
