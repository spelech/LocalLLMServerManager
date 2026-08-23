using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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

            var filePath = Directory.GetFiles(workflowsDir, $"{id}.json", SearchOption.AllDirectories).FirstOrDefault()
                ?? Path.Combine(workflowsDir, $"{id}.json");

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

        // Audio Generation Workflows & Files Endpoints
        app.MapGet("/api/audio/workflows", async (ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();
            var workflowsDir = string.IsNullOrWhiteSpace(settings.WorkflowsPath)
                ? Path.Combine(AppContext.BaseDirectory, "Workflows")
                : settings.WorkflowsPath;

            var audioWorkflowsDir = Path.Combine(workflowsDir, "Audio");
            var searchDirs = new[] { audioWorkflowsDir, workflowsDir }.Where(Directory.Exists).Distinct();

            var list = new System.Collections.Generic.List<object>();
            var seenIds = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                        // Fallback if parsing fails
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

            var audioWorkflowPath = Path.Combine(workflowsDir, "Audio", $"{request.WorkflowId}.json");
            if (!File.Exists(audioWorkflowPath))
            {
                audioWorkflowPath = Path.Combine(workflowsDir, $"{request.WorkflowId}.json");
            }

            if (!File.Exists(audioWorkflowPath))
            {
                return Results.NotFound(new { message = $"Audio workflow '{request.WorkflowId}' not found." });
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
                promptId = promptId,
                status = "queued",
                wsUrl = wsUrl
            });
        });

        app.MapGet("/api/audio/files", (ISettingsService settingsService) =>
        {
            var settings = settingsService.LoadSettings();
            var outputDir = string.IsNullOrWhiteSpace(settings.AudioPath)
                ? Path.Combine(AppContext.BaseDirectory, "wwwroot", "output_audio")
                : settings.AudioPath;

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
