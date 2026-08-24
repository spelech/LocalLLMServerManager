using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocalLLMServerManager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public record AudioGenerateRequest(
    string WorkflowId = "stable_audio_open_sfx",
    string Prompt = "",
    string? NegativePrompt = null,
    int DurationSeconds = 30,
    long Seed = -1
);

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

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        // ------------------ General ComfyUI Workflows ------------------
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

            var files = Directory.GetFiles(workflowsDir, "*.json", SearchOption.AllDirectories)
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

            var safeId = Path.GetFileNameWithoutExtension(id);
            var filePath = Directory.GetFiles(workflowsDir, $"{safeId}.json", SearchOption.AllDirectories).FirstOrDefault()
                ?? Path.Combine(workflowsDir, $"{safeId}.json");

            if (!File.Exists(filePath))
            {
                return Results.NotFound(new { message = $"Workflow '{safeId}' not found." });
            }

            var jsonStr = await File.ReadAllTextAsync(filePath);
            return Results.Content(jsonStr, "application/json");
        });

        // ------------------ 3D Mesh Outputs ------------------
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

        // ------------------ Video Generation Endpoints ------------------
        app.MapGet("/api/video/workflows", (ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();

            string videoWorkflowsDir = "";
            if (!string.IsNullOrWhiteSpace(settings.WorkflowsPath) && Directory.Exists(Path.Combine(settings.WorkflowsPath, "Video")))
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
            else if (!string.IsNullOrWhiteSpace(settings.VideoModelsPath) && Directory.Exists(settings.VideoModelsPath))
            {
                videoWorkflowsDir = settings.VideoModelsPath;
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
            if (!string.IsNullOrWhiteSpace(settings.WorkflowsPath))
            {
                possibleDirs.Add(Path.Combine(settings.WorkflowsPath, "Video"));
                possibleDirs.Add(settings.WorkflowsPath);
            }
            possibleDirs.Add(Path.Combine(AppContext.BaseDirectory, "Workflows", "Video"));
            possibleDirs.Add(Path.Combine(AppContext.BaseDirectory, "Workflows"));
            possibleDirs.Add(Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Video"));
            possibleDirs.Add(Path.Combine(Directory.GetCurrentDirectory(), "Workflows"));
            if (!string.IsNullOrWhiteSpace(settings.VideoModelsPath)) possibleDirs.Add(settings.VideoModelsPath);

            string? templatePath = null;
            var rawId = string.IsNullOrWhiteSpace(request.WorkflowId) ? "wan2.2_t2v" : request.WorkflowId;
            var workflowId = Path.GetFileNameWithoutExtension(rawId);

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

        // ------------------ Audio Generation Workflows & Files ------------------
        app.MapGet("/api/audio/workflows", async (ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();
            var workflowsDir = string.IsNullOrWhiteSpace(settings.WorkflowsPath)
                ? Path.Combine(AppContext.BaseDirectory, "Workflows")
                : settings.WorkflowsPath;

            var audioWorkflowsDir = Path.Combine(workflowsDir, "Audio");
            var searchDirs = new[] { audioWorkflowsDir, workflowsDir, Path.Combine(AppContext.BaseDirectory, "Workflows", "Audio") }
                .Where(Directory.Exists).Distinct();

            var list = new List<object>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in searchDirs)
            {
                foreach (var f in Directory.GetFiles(dir, "*.json"))
                {
                    var id = Path.GetFileNameWithoutExtension(f);
                    if (seenIds.Contains(id)) continue;

                    try
                    {
                        var jsonStr = await File.ReadAllTextAsync(f);
                        var node = JsonNode.Parse(jsonStr);
                        var name = node?["name"]?.ToString() ?? id.Replace('_', ' ');
                        var type = node?["type"]?.ToString() ?? "audio";
                        var description = node?["description"]?.ToString() ?? "";

                        if (dir == audioWorkflowsDir || type.Equals("audio", StringComparison.OrdinalIgnoreCase))
                        {
                            seenIds.Add(id);
                            list.Add(new
                            {
                                id,
                                name,
                                filename = Path.GetFileName(f),
                                path = f,
                                type,
                                description
                            });
                        }
                    }
                    catch
                    {
                        if (seenIds.Add(id))
                        {
                            list.Add(new
                            {
                                id,
                                name = id.Replace('_', ' '),
                                filename = Path.GetFileName(f),
                                path = f,
                                type = "audio",
                                description = ""
                            });
                        }
                    }
                }
            }

            return Results.Ok(list);
        });

        app.MapPost("/api/audio/generate", async (AudioGenerateRequest request, ISettingsService settingsService, IHttpClientFactory clientFactory) =>
        {
            var settings = settingsService.LoadSettings();
            var workflowsDir = string.IsNullOrWhiteSpace(settings.WorkflowsPath)
                ? Path.Combine(AppContext.BaseDirectory, "Workflows")
                : settings.WorkflowsPath;

            var safeWorkflowId = Path.GetFileNameWithoutExtension(string.IsNullOrWhiteSpace(request.WorkflowId) ? "stable_audio_open_sfx" : request.WorkflowId);
            var audioWorkflowPath = Path.Combine(workflowsDir, "Audio", $"{safeWorkflowId}.json");
            if (!File.Exists(audioWorkflowPath))
            {
                audioWorkflowPath = Path.Combine(workflowsDir, $"{safeWorkflowId}.json");
            }
            if (!File.Exists(audioWorkflowPath))
            {
                audioWorkflowPath = Path.Combine(AppContext.BaseDirectory, "Workflows", "Audio", $"{safeWorkflowId}.json");
            }

            if (!File.Exists(audioWorkflowPath))
            {
                return Results.NotFound(new { message = $"Audio workflow '{safeWorkflowId}' not found." });
            }

            var jsonContent = await File.ReadAllTextAsync(audioWorkflowPath);
            var rootNode = JsonNode.Parse(jsonContent);

            JsonNode targetGraph = rootNode?["workflow"] ?? rootNode ?? new JsonObject();

            // Perform parameter substitutions
            if (targetGraph is JsonObject graphObject)
            {
                foreach (var kvp in graphObject)
                {
                    if (kvp.Value is JsonObject nodeObj)
                    {
                        var classType = nodeObj["class_type"]?.ToString() ?? "";
                        var title = nodeObj["_meta"]?["title"]?.ToString() ?? "";
                        var inputs = nodeObj["inputs"] as JsonObject;

                        if (inputs != null)
                        {
                            // Prompt substitution
                            if (classType.Contains("CLIPTextEncode") || title.Contains("Prompt") || title.Contains("Lyrics"))
                            {
                                if (title.Contains("Negative") || classType.Contains("Negative"))
                                {
                                    if (request.NegativePrompt != null)
                                    {
                                        inputs["text"] = request.NegativePrompt;
                                    }
                                }
                                else if (!string.IsNullOrWhiteSpace(request.Prompt))
                                {
                                    inputs["text"] = request.Prompt;
                                }
                            }

                            // Seed substitution
                            if (inputs.ContainsKey("seed"))
                            {
                                var seedVal = request.Seed == -1 ? Random.Shared.Next(1, int.MaxValue) : request.Seed;
                                inputs["seed"] = seedVal;
                            }
                            else if (inputs.ContainsKey("noise_seed"))
                            {
                                var seedVal = request.Seed == -1 ? Random.Shared.Next(1, int.MaxValue) : request.Seed;
                                inputs["noise_seed"] = seedVal;
                            }

                            // Duration substitution
                            if (inputs.ContainsKey("seconds"))
                            {
                                inputs["seconds"] = request.DurationSeconds;
                            }
                            else if (inputs.ContainsKey("duration"))
                            {
                                inputs["duration"] = request.DurationSeconds;
                            }
                        }
                    }
                }
            }

            var comfyUrl = string.IsNullOrWhiteSpace(settings.ComfyUiUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiUrl;
            string promptId = Guid.NewGuid().ToString();

            try
            {
                var http = clientFactory.CreateClient();
                var comfyEndpoint = $"{comfyUrl.TrimEnd('/')}/prompt";
                var payload = new { prompt = targetGraph };

                var response = await http.PostAsJsonAsync(comfyEndpoint, payload);
                if (response.IsSuccessStatusCode)
                {
                    var respJson = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (respJson.TryGetProperty("prompt_id", out var pidProp) && pidProp.ValueKind == JsonValueKind.String)
                    {
                        promptId = pidProp.GetString() ?? promptId;
                    }
                }
            }
            catch
            {
                // Fallback promptId if ComfyUI instance is offline
            }

            var wsUrl = comfyUrl.Replace("http://", "ws://").Replace("https://", "wss://").TrimEnd('/') + "/ws";

            return Results.Ok(new
            {
                promptId,
                status = "queued",
                wsUrl
            });
        });

        app.MapGet("/api/audio/files", (ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();
            var outputDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "output_audio");

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var validExts = new[] { ".wav", ".flac", ".mp3" };
            var files = Directory.GetFiles(outputDir)
                .Where(f => validExts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(f => new
                {
                    filename = Path.GetFileName(f),
                    url = $"/output_audio/{Path.GetFileName(f)}",
                    sizeBytes = new FileInfo(f).Length,
                    createdAt = File.GetCreationTimeUtc(f)
                })
                .OrderByDescending(x => x.createdAt);

            return Results.Ok(files);
        });
    }
}
