using System.IO;
using System.Text;
using System.Text.Json;
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

        app.MapGet("/api/video/workflows", (ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();

            string videoWorkflowsDir = "";
            if (!string.IsNullOrWhiteSpace(settings.VideoModelsPath) && Directory.Exists(settings.VideoModelsPath))
            {
                videoWorkflowsDir = settings.VideoModelsPath;
            }
            else if (!string.IsNullOrWhiteSpace(settings.WorkflowsPath) && Directory.Exists(Path.Combine(settings.WorkflowsPath, "Video")))
            {
                videoWorkflowsDir = Path.Combine(settings.WorkflowsPath, "Video");
            }
            else if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Workflows", "Video")))
            {
                videoWorkflowsDir = Path.Combine(AppContext.BaseDirectory, "Workflows", "Video");
            }
            else if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Video")))
            {
                videoWorkflowsDir = Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Video");
            }

            if (string.IsNullOrEmpty(videoWorkflowsDir) || !Directory.Exists(videoWorkflowsDir))
            {
                return Results.Ok(new object[0]);
            }

            var files = Directory.GetFiles(videoWorkflowsDir, "*.json")
                .Select(f => new
                {
                    id = Path.GetFileNameWithoutExtension(f),
                    name = Path.GetFileNameWithoutExtension(f).Replace('_', ' '),
                    filename = Path.GetFileName(f),
                    path = f
                });

            return Results.Ok(files);
        });

        app.MapPost("/api/video/generate", async (VideoGenerateRequest request, ISettingsService settingsService, VramOrchestrator vramOrchestrator, HttpClient httpClient) =>
        {
            await vramOrchestrator.EnsureVramForComfyUiAsync();

            var settings = settingsService.LoadSettings();

            var possibleDirs = new List<string>();
            if (!string.IsNullOrWhiteSpace(settings.VideoModelsPath)) possibleDirs.Add(settings.VideoModelsPath);
            if (!string.IsNullOrWhiteSpace(settings.WorkflowsPath))
            {
                possibleDirs.Add(Path.Combine(settings.WorkflowsPath, "Video"));
                possibleDirs.Add(settings.WorkflowsPath);
            }
            possibleDirs.Add(Path.Combine(AppContext.BaseDirectory, "Workflows", "Video"));
            possibleDirs.Add(Path.Combine(AppContext.BaseDirectory, "Workflows"));
            possibleDirs.Add(Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Video"));
            possibleDirs.Add(Path.Combine(Directory.GetCurrentDirectory(), "Workflows"));

            string? templatePath = null;
            var workflowId = string.IsNullOrWhiteSpace(request.WorkflowId) ? "wan2.2_t2v" : request.WorkflowId;

            foreach (var dir in possibleDirs)
            {
                if (Directory.Exists(dir))
                {
                    var p = Path.Combine(dir, $"{workflowId}.json");
                    if (File.Exists(p))
                    {
                        templatePath = p;
                        break;
                    }
                }
            }

            if (templatePath == null)
            {
                return Results.NotFound(new { message = $"Video workflow '{workflowId}' not found." });
            }

            var jsonStr = await File.ReadAllTextAsync(templatePath);

            long effectiveSeed = request.Seed <= 0
                ? Random.Shared.NextInt64(1, 999999999999999L)
                : request.Seed;

            int width = request.Width > 0 ? request.Width : 832;
            int height = request.Height > 0 ? request.Height : 480;
            int frames = request.Frames > 0 ? request.Frames : 49;
            int fps = request.Fps > 0 ? request.Fps : 16;
            string prompt = request.Prompt ?? "";
            string negativePrompt = request.NegativePrompt ?? "";
            string imageUrl = request.ImageUrl ?? request.Image ?? "";

            jsonStr = jsonStr.Replace("\"{{PROMPT}}\"", JsonSerializer.Serialize(prompt))
                             .Replace("{{PROMPT}}", prompt)
                             .Replace("\"{{NEGATIVE_PROMPT}}\"", JsonSerializer.Serialize(negativePrompt))
                             .Replace("{{NEGATIVE_PROMPT}}", negativePrompt)
                             .Replace("\"{{WIDTH}}\"", width.ToString())
                             .Replace("{{WIDTH}}", width.ToString())
                             .Replace("\"{{HEIGHT}}\"", height.ToString())
                             .Replace("{{HEIGHT}}", height.ToString())
                             .Replace("\"{{FRAMES}}\"", frames.ToString())
                             .Replace("{{FRAMES}}", frames.ToString())
                             .Replace("\"{{FPS}}\"", fps.ToString())
                             .Replace("{{FPS}}", fps.ToString())
                             .Replace("\"{{SEED}}\"", effectiveSeed.ToString())
                             .Replace("{{SEED}}", effectiveSeed.ToString())
                             .Replace("\"{{IMAGE}}\"", JsonSerializer.Serialize(imageUrl))
                             .Replace("{{IMAGE}}", imageUrl);

            JsonNode? workflowNode;
            try
            {
                workflowNode = JsonNode.Parse(jsonStr);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = $"Invalid workflow template JSON: {ex.Message}" });
            }

            var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl;
            var baseUrl = comfyUrl.TrimEnd('/');
            var promptId = Guid.NewGuid().ToString();

            try
            {
                var payload = new JsonObject
                {
                    ["prompt"] = workflowNode
                };
                var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync($"{baseUrl}/prompt", content);

                if (response.IsSuccessStatusCode)
                {
                    var resContent = await response.Content.ReadAsStringAsync();
                    var resJson = JsonNode.Parse(resContent);
                    var idFromComfy = resJson?["prompt_id"]?.ToString();
                    if (!string.IsNullOrEmpty(idFromComfy))
                    {
                        promptId = idFromComfy;
                    }
                }
            }
            catch
            {
                // Fallback promptId used if ComfyUI is offline or in test env
            }

            var wsScheme = baseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            var uriBuilder = new UriBuilder(baseUrl)
            {
                Scheme = wsScheme,
                Path = "/ws"
            };
            var wsUrl = uriBuilder.Uri.ToString();

            return Results.Ok(new
            {
                promptId,
                status = "queued",
                wsUrl
            });
        });

        app.MapGet("/api/video/files", (ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();
            string outputDir = "";

            if (!string.IsNullOrWhiteSpace(settings.VideoOutputPath) && Directory.Exists(settings.VideoOutputPath))
            {
                outputDir = settings.VideoOutputPath;
            }
            else if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "output_video")))
            {
                outputDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "output_video");
            }
            else if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "output_video")))
            {
                outputDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "output_video");
            }

            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                return Results.Ok(new object[0]);
            }

            var files = Directory.GetFiles(outputDir, "*.*")
                .Where(f => f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".webm", StringComparison.OrdinalIgnoreCase))
                .Select(f => new
                {
                    filename = Path.GetFileName(f),
                    url = $"/output_video/{Path.GetFileName(f)}",
                    sizeBytes = new FileInfo(f).Length,
                    createdAt = File.GetCreationTimeUtc(f)
                })
                .OrderByDescending(x => x.createdAt);

            return Results.Ok(files);
        });
    }
}

public record VideoGenerateRequest(
    string? WorkflowId = "wan2.2_t2v",
    string? Prompt = "",
    string? NegativePrompt = "",
    int Width = 832,
    int Height = 480,
    int Frames = 49,
    int Fps = 16,
    long Seed = -1,
    string? ImageUrl = null,
    string? Image = null
);
