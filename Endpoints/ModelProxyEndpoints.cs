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

        app.MapGet("/api/hf/search", async (string? q, string? pipeline_tag, HttpClient httpClient) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var query = string.IsNullOrWhiteSpace(q) ? (string.IsNullOrWhiteSpace(pipeline_tag) ? "llama" : "") : q;
                
                string requestUrl;
                if (!string.IsNullOrWhiteSpace(pipeline_tag))
                {
                    if (pipeline_tag.Equals("gguf", StringComparison.OrdinalIgnoreCase))
                    {
                        var qParam = string.IsNullOrWhiteSpace(query) ? "llama" : query;
                        requestUrl = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(qParam)}&filter=gguf&sort=downloads&direction=-1&limit=20";
                    }
                    else
                    {
                        var qParam = Uri.EscapeDataString(query);
                        requestUrl = $"https://huggingface.co/api/models?search={qParam}&pipeline_tag={Uri.EscapeDataString(pipeline_tag)}&sort=downloads&direction=-1&limit=20";
                    }
                }
                else
                {
                    var qParam = string.IsNullOrWhiteSpace(query) ? "llama" : query;
                    requestUrl = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(qParam)}&filter=gguf&sort=downloads&direction=-1&limit=20";
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

        app.MapGet("/api/civitai/download", async (string fileUrl, string? modelType, string? fileName, HttpClient httpClient) =>
        {
            try
            {
                var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "model.safetensors" : fileName;
                var targetDir = LocalLLMServerManager.Shared.Services.DownloadManager.ResolveTargetDirectory(modelType, safeFileName);
                Directory.CreateDirectory(targetDir);
                var targetPath = Path.Combine(targetDir, safeFileName);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                    using var fileStream = File.Create(targetPath);
                    await stream.CopyToAsync(fileStream, cts.Token);
                    return Results.Ok(new { status = "success", path = targetPath });
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapGet("/api/hf/download", async (string fileUrl, string? pipelineTag, string? fileName, HttpClient httpClient) =>
        {
            try
            {
                var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "model.safetensors" : fileName;
                var targetDir = LocalLLMServerManager.Shared.Services.DownloadManager.ResolveTargetDirectory(pipelineTag, safeFileName);
                Directory.CreateDirectory(targetDir);
                var targetPath = Path.Combine(targetDir, safeFileName);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var response = await httpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                    using var fileStream = File.Create(targetPath);
                    await stream.CopyToAsync(fileStream, cts.Token);
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
