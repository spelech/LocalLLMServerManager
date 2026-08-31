using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocalLLMServerManager.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class ModelProxyEndpoints
{
    public static void MapModelProxyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/models", async (IHttpClientFactory clientFactory) =>
        {
            try
            {
                var http = clientFactory.CreateClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var response = await http.GetAsync("http://127.0.0.1:11434/api/tags", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(content, "application/json");
                }
            }
            catch { }
            return Results.Ok(new { models = new object[0] });
        });

        app.MapGet("/api/ollama/ps", async (IHttpClientFactory clientFactory) =>
        {
            try
            {
                var http = clientFactory.CreateClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var response = await http.GetAsync("http://127.0.0.1:11434/api/ps", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(content, "application/json");
                }
            }
            catch { }
            return Results.Ok(new { models = new object[0] });
        });

        async Task<IResult> HandleDeleteModel(HttpContext context, IHttpClientFactory clientFactory)
        {
            try
            {
                string? target = context.Request.Query["model"].FirstOrDefault()
                              ?? context.Request.Query["model_name"].FirstOrDefault()
                              ?? context.Request.Query["name"].FirstOrDefault()
                              ?? context.Request.Query["target"].FirstOrDefault()
                              ?? context.Request.Query["file_path"].FirstOrDefault()
                              ?? context.Request.Query["filePath"].FirstOrDefault();
                string type = context.Request.Query["type"].FirstOrDefault() ?? "ollama";

                if (string.IsNullOrWhiteSpace(target))
                {
                    try
                    {
                        using var reader = new StreamReader(context.Request.Body);
                        var bodyStr = await reader.ReadToEndAsync();
                        if (!string.IsNullOrWhiteSpace(bodyStr))
                        {
                            using var doc = JsonDocument.Parse(bodyStr);
                            var root = doc.RootElement;
                            if (root.TryGetProperty("model", out var mp)) target = mp.GetString();
                            else if (root.TryGetProperty("model_name", out var mnp)) target = mnp.GetString();
                            else if (root.TryGetProperty("name", out var np)) target = np.GetString();
                            else if (root.TryGetProperty("target", out var tp)) target = tp.GetString();
                            else if (root.TryGetProperty("file_path", out var fp)) target = fp.GetString();
                            else if (root.TryGetProperty("filePath", out var fpp)) target = fpp.GetString();

                            if (root.TryGetProperty("type", out var typeProp)) type = typeProp.GetString() ?? type;
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrWhiteSpace(target))
                {
                    return Results.BadRequest(new { error = "Model name or target path is required." });
                }

                if (string.Equals(type, "ollama", StringComparison.OrdinalIgnoreCase))
                {
                    var http = clientFactory.CreateClient();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

                    // 1. Unload model from Ollama VRAM first to release Windows memory-mapped file locks
                    try
                    {
                        var unloadPayload = new StringContent(
                            JsonSerializer.Serialize(new { model = target, keep_alive = 0 }),
                            System.Text.Encoding.UTF8,
                            "application/json"
                        );
                        await http.PostAsync("http://127.0.0.1:11434/api/generate", unloadPayload, cts.Token);
                        await Task.Delay(200, cts.Token);
                    }
                    catch { }

                    // 2. Send DELETE request to Ollama daemon
                    var ollamaDeletePayload = new StringContent(
                        JsonSerializer.Serialize(new { model = target, name = target }),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    using var req = new HttpRequestMessage(HttpMethod.Delete, "http://127.0.0.1:11434/api/delete") { Content = ollamaDeletePayload };
                    var response = await http.SendAsync(req, cts.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        return Results.Ok(new { status = "success", model = target, target, message = "Ollama model deleted successfully." });
                    }

                    // 3. Fallback: try alternate tag name (e.g. append or strip :latest)
                    var alternateTarget = target.EndsWith(":latest", StringComparison.OrdinalIgnoreCase)
                        ? target.Substring(0, target.Length - 7)
                        : $"{target}:latest";

                    var altDeletePayload = new StringContent(
                        JsonSerializer.Serialize(new { model = alternateTarget, name = alternateTarget }),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );
                    using var altReq = new HttpRequestMessage(HttpMethod.Delete, "http://127.0.0.1:11434/api/delete") { Content = altDeletePayload };
                    var altResponse = await http.SendAsync(altReq, cts.Token);
                    if (altResponse.IsSuccessStatusCode)
                    {
                        return Results.Ok(new { status = "success", model = alternateTarget, target = alternateTarget, message = "Ollama model deleted successfully." });
                    }

                    var errBody = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Json(new { status = "error", error = string.IsNullOrWhiteSpace(errBody) ? $"Ollama delete returned status {(int)response.StatusCode}" : errBody }, statusCode: (int)response.StatusCode);
                }
                else
                {
                    if (!Program.IsSafePath(target))
                    {
                        return Results.BadRequest(new { error = "Target path is invalid or unsafe." });
                    }

                    var fullPath = Path.IsPathRooted(target)
                        ? Path.GetFullPath(target)
                        : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, target));

                    if (!Program.IsSafePath(fullPath))
                    {
                        return Results.BadRequest(new { error = "Target path is invalid or unsafe." });
                    }

                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        return Results.Ok(new { status = "success", target = fullPath, message = "Model file deleted successfully." });
                    }
                    else if (Directory.Exists(fullPath))
                    {
                        Directory.Delete(fullPath, recursive: true);
                        return Results.Ok(new { status = "success", target = fullPath, message = "Model directory deleted successfully." });
                    }

                    return Results.NotFound(new { error = $"Model file not found: {target}" });
                }
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        }

        app.MapDelete("/api/models/delete", HandleDeleteModel);
        app.MapPost("/api/models/delete", HandleDeleteModel);
        app.MapDelete("/api/models", HandleDeleteModel);
        app.MapPost("/api/models/remove", HandleDeleteModel);

        app.MapGet("/api/hf/search", async (string? q, string? pipeline_tag, string? pipeline_tags, HttpClient httpClient) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var query = string.IsNullOrWhiteSpace(q) ? "" : q;
                var tag = !string.IsNullOrWhiteSpace(pipeline_tag) ? pipeline_tag : pipeline_tags;
                
                string requestUrl;
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    if (tag.Equals("gguf", StringComparison.OrdinalIgnoreCase))
                    {
                        var qParam = string.IsNullOrWhiteSpace(query) ? "llama" : query;
                        requestUrl = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(qParam)}&filter=gguf&sort=downloads&direction=-1&limit=25";
                    }
                    else
                    {
                        var qParam = Uri.EscapeDataString(query);
                        var tagParam = Uri.EscapeDataString(tag);
                        requestUrl = $"https://huggingface.co/api/models?search={qParam}&pipeline_tag={tagParam}&sort=downloads&direction=-1&limit=25";
                    }
                }
                else
                {
                    var qParam = string.IsNullOrWhiteSpace(query) ? "llama" : query;
                    requestUrl = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(qParam)}&filter=gguf&sort=downloads&direction=-1&limit=25";
                }

                using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                req.Headers.UserAgent.ParseAdd("LocalLLMServerManager/3.5.0");
                var response = await httpClient.SendAsync(req, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(jsonStr, "application/json");
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapGet("/api/civitai/download", async (HttpContext httpContext, string fileUrl, string? modelType, string? fileName, HttpClient httpClient) =>
        {
            try
            {
                var rawName = string.IsNullOrWhiteSpace(fileName) ? "model.safetensors" : fileName;
                var safeFileName = Path.GetFileName(rawName);
                var targetDir = LocalLLMServerManager.Shared.Services.DownloadManager.ResolveTargetDirectory(modelType, safeFileName);
                Directory.CreateDirectory(targetDir);
                var targetPath = Path.Combine(targetDir, safeFileName);

                using var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted);
                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(httpContext.RequestAborted);
                    using var fileStream = File.Create(targetPath);
                    await stream.CopyToAsync(fileStream, httpContext.RequestAborted);
                    return Results.Ok(new { status = "success", path = targetPath });
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapGet("/api/hf/download", async (HttpContext httpContext, string fileUrl, string? pipelineTag, string? fileName, HttpClient httpClient) =>
        {
            try
            {
                var rawName = string.IsNullOrWhiteSpace(fileName) ? "model.safetensors" : fileName;
                var safeFileName = Path.GetFileName(rawName);
                var targetDir = LocalLLMServerManager.Shared.Services.DownloadManager.ResolveTargetDirectory(pipelineTag, safeFileName);
                Directory.CreateDirectory(targetDir);
                var targetPath = Path.Combine(targetDir, safeFileName);

                using var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, httpContext.RequestAborted);
                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(httpContext.RequestAborted);
                    using var fileStream = File.Create(targetPath);
                    await stream.CopyToAsync(fileStream, httpContext.RequestAborted);
                    return Results.Ok(new { status = "success", path = targetPath });
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapGet("/api/hf/model", async (string repoId, HttpClient httpClient) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var requestUrl = $"https://huggingface.co/api/models/{Uri.EscapeDataString(repoId)}";
                using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                req.Headers.UserAgent.ParseAdd("LocalLLMServerManager/3.5.0");
                var response = await httpClient.SendAsync(req, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(jsonStr, "application/json");
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapGet("/api/civitai/search", async (HttpClient http, string? q, string? types, string? sort) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var query = string.IsNullOrWhiteSpace(q) ? "cyberpunk" : q;
                var typeParam = string.IsNullOrWhiteSpace(types) ? "" : $"&types={Uri.EscapeDataString(types)}";
                var sortParam = string.IsNullOrWhiteSpace(sort) ? "Most Downloaded" : sort;
                var requestUrl = $"https://civitai.com/api/v1/models?query={Uri.EscapeDataString(query)}&limit=15&sort={Uri.EscapeDataString(sortParam)}{typeParam}";

                using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                req.Headers.UserAgent.ParseAdd("LocalLLMServerManager/3.6.0");
                var response = await http.SendAsync(req, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(jsonStr, "application/json");
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapGet("/api/civitai/model", async (HttpClient http, int id) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var requestUrl = $"https://civitai.com/api/v1/models/{id}";
                using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                req.Headers.UserAgent.ParseAdd("LocalLLMServerManager/3.6.0");
                var response = await http.SendAsync(req, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(jsonStr, "application/json");
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapPost("/v1/audio/speech", async (HttpContext context, ISettingsService settingsService, IHttpClientFactory clientFactory) =>
        {
            try
            {
                var settings = settingsService.LoadSettings();
                var baseUrl = (string.IsNullOrWhiteSpace(settings.AudioEngineUrl) ? "http://127.0.0.1:8880" : settings.AudioEngineUrl).TrimEnd('/');
                var targetUrl = $"{baseUrl}/v1/audio/speech";

                using var reader = new StreamReader(context.Request.Body);
                var requestBodyStr = await reader.ReadToEndAsync();

                string outgoingJson = requestBodyStr;
                if (!string.IsNullOrWhiteSpace(requestBodyStr))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(requestBodyStr);
                        var root = doc.RootElement;
                        var hasVoice = root.TryGetProperty("voice", out var voiceProp) && !string.IsNullOrWhiteSpace(voiceProp.GetString());

                        if (!hasVoice)
                        {
                            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(requestBodyStr) ?? new Dictionary<string, object>();
                            dict["voice"] = string.IsNullOrWhiteSpace(settings.PreferredAudioVoice) ? "af_heart" : settings.PreferredAudioVoice;
                            outgoingJson = JsonSerializer.Serialize(dict);
                        }
                    }
                    catch { }
                }

                var http = clientFactory.CreateClient();
                using var targetReq = new HttpRequestMessage(HttpMethod.Post, targetUrl);
                targetReq.Content = new StringContent(outgoingJson, System.Text.Encoding.UTF8, "application/json");

                var targetResponse = await http.SendAsync(targetReq, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

                context.Response.StatusCode = (int)targetResponse.StatusCode;
                var contentType = targetResponse.Content.Headers.ContentType?.ToString() ?? "audio/mpeg";
                context.Response.ContentType = contentType;

                await using var responseStream = await targetResponse.Content.ReadAsStreamAsync(context.RequestAborted);
                await responseStream.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            catch (Exception ex)
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
                }
            }
        });
    }
}
