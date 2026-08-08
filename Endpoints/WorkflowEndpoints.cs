using System.IO;
using System.Text.Json.Nodes;
using LocalLLMServerManager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        app.MapGet("/api/comfy/workflows", (ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();
            var workflowsDir = string.IsNullOrWhiteSpace(settings.WorkflowsPath)
                ? Path.Combine(AppContext.BaseDirectory, "Workflows")
                : settings.WorkflowsPath;

            if (!Directory.Exists(workflowsDir))
            {
                return Results.Ok(new object[0]);
            }

            var files = Directory.GetFiles(workflowsDir, "*.json")
                .Select(f => new
                {
                    id = Path.GetFileNameWithoutExtension(f),
                    name = Path.GetFileNameWithoutExtension(f).Replace('_', ' '),
                    filename = Path.GetFileName(f),
                    path = f
                });

            return Results.Ok(files);
        });

        app.MapGet("/api/comfy/workflows/{id}", async (string id, ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();
            var workflowsDir = string.IsNullOrWhiteSpace(settings.WorkflowsPath)
                ? Path.Combine(AppContext.BaseDirectory, "Workflows")
                : settings.WorkflowsPath;

            var filePath = Path.Combine(workflowsDir, $"{id}.json");
            if (!File.Exists(filePath))
            {
                return Results.NotFound(new { message = $"Workflow '{id}' not found." });
            }

            var jsonStr = await File.ReadAllTextAsync(filePath);
            return Results.Content(jsonStr, "application/json");
        });

        app.MapGet("/api/3d/files", (ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();
            var outputDir = string.IsNullOrWhiteSpace(settings.ThreeDModelsPath)
                ? Path.Combine(AppContext.BaseDirectory, "wwwroot", "output_3d")
                : settings.ThreeDModelsPath;

            if (!Directory.Exists(outputDir))
            {
                return Results.Ok(new object[0]);
            }

            var files = Directory.GetFiles(outputDir, "*.glb")
                .Select(f => new
                {
                    filename = Path.GetFileName(f),
                    url = $"/output_3d/{Path.GetFileName(f)}",
                    sizeBytes = new FileInfo(f).Length,
                    createdAt = File.GetCreationTimeUtc(f)
                })
                .OrderByDescending(x => x.createdAt);

            return Results.Ok(files);
        });
    }
}
