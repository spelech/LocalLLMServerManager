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

        app.MapGet("/api/video/files", (ISettingsService settingsService) =>
        {
            var outputDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "output_video");
            if (!Directory.Exists(outputDir))
            {
                return Results.Ok(new object[0]);
            }

            var allowedExtensions = new[] { ".mp4", ".webm" };
            var files = Directory.GetFiles(outputDir)
                .Where(f => allowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(f =>
                {
                    var fileInfo = new FileInfo(f);
                    return new
                    {
                        filename = fileInfo.Name,
                        url = $"/output_video/{fileInfo.Name}",
                        duration = "3.0s",
                        resolution = "832x480",
                        fps = 16,
                        seed = 42890L,
                        sizeBytes = fileInfo.Length,
                        createdAt = fileInfo.CreationTimeUtc
                    };
                })
                .OrderByDescending(x => x.createdAt);

            return Results.Ok(files);
        });

        app.MapPost("/api/video/generate", async (VideoGenerateRequest req) =>
        {
            var outputDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "output_video");
            Directory.CreateDirectory(outputDir);

            var filename = $"video_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp4";
            var filePath = Path.Combine(outputDir, filename);

            if (!File.Exists(filePath))
            {
                var sampleHeader = new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32 };
                await File.WriteAllBytesAsync(filePath, sampleHeader);
            }

            var seed = req.Seed > 0 ? req.Seed : Random.Shared.Next(10000, 99999);
            var resolution = string.IsNullOrWhiteSpace(req.Resolution) ? "832x480" : req.Resolution;
            var frameCount = req.FrameCount > 0 ? req.FrameCount : 48;
            var fps = 16;
            var durationSec = (double)frameCount / fps;

            return Results.Ok(new
            {
                filename = filename,
                url = $"/output_video/{filename}",
                duration = $"{durationSec:F1}s",
                resolution = resolution,
                fps = fps,
                seed = seed,
                sizeBytes = new FileInfo(filePath).Length,
                createdAt = DateTime.UtcNow
            });
        });
    }
}

public record VideoGenerateRequest(
    string? Prompt,
    string? NegativePrompt,
    string? Workflow,
    string? Resolution,
    int FrameCount,
    long Seed
);
