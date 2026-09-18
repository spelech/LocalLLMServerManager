using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class AiAssistantEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void MapAiAssistantEndpoints(this WebApplication app)
    {
        // 1. Status
        app.MapGet("/api/ai/status", async (
            ISettingsService settingsService,
            IComponentManagerService componentService,
            IPromptManagementService promptService) =>
        {
            var settings = settingsService.LoadSettings();
            var isInstalled = componentService.IsAiAssistantInstalled;
            var promptDir = PromptManagementService.ResolvePromptDirectory(settings.AiAssistantPromptsDirectory);
            var sections = await promptService.GetAllPromptsAsync(promptDir);

            return Results.Ok(new
            {
                enabled = settings.AiAssistantEnabled,
                isInstalled,
                configured = !string.IsNullOrWhiteSpace(settings.AiAssistantEndpoint),
                endpoint = settings.AiAssistantEndpoint,
                model = settings.AiAssistantModel,
                promptDirectory = promptDir,
                loadedPromptSections = sections.Keys
            });
        });

        // 2. Validate Connection
        app.MapPost("/api/ai/validate", async (
            HttpContext httpContext,
            IAiAssistantService assistantService,
            ISettingsService settingsService) =>
        {
            string? endpoint = null;
            string? apiKey = null;
            string? model = null;

            if (httpContext.Request.HasJsonContentType())
            {
                try
                {
                    using var doc = await JsonDocument.ParseAsync(httpContext.Request.Body, cancellationToken: httpContext.RequestAborted);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("endpoint", out var epEl)) endpoint = epEl.GetString();
                    if (root.TryGetProperty("apiKey", out var keyEl)) apiKey = keyEl.GetString();
                    if (root.TryGetProperty("model", out var modelEl)) model = modelEl.GetString();
                }
                catch (JsonException ex)
                {
                    return Results.BadRequest(new { error = $"Invalid JSON payload: {ex.Message}" });
                }
            }

            var result = await assistantService.ValidateConnectionAsync(endpoint, apiKey, model, httpContext.RequestAborted);
            return Results.Ok(result);
        });

        // 3. Models
        app.MapGet("/api/ai/models", async (
            HttpContext httpContext,
            IAiAssistantService assistantService,
            ISettingsService settingsService) =>
        {
            var query = httpContext.Request.Query;
            string? endpoint = query.TryGetValue("endpoint", out var epVal) && !string.IsNullOrWhiteSpace(epVal)
                ? epVal.ToString()
                : null;

            string? apiKey = query.TryGetValue("apiKey", out var keyVal) && !string.IsNullOrWhiteSpace(keyVal)
                ? keyVal.ToString()
                : null;

            try
            {
                var models = await assistantService.GetAvailableModelsAsync(endpoint, apiKey, httpContext.RequestAborted);
                return Results.Ok(models);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // 4. Prompts
        app.MapGet("/api/ai/prompts", async (
            ISettingsService settingsService,
            IPromptManagementService promptService) =>
        {
            var settings = settingsService.LoadSettings();
            var promptDir = PromptManagementService.ResolvePromptDirectory(settings.AiAssistantPromptsDirectory);
            var sections = await promptService.GetAllPromptsAsync(promptDir);

            return Results.Ok(new
            {
                promptsDirectory = promptDir,
                sections
            });
        });

        app.MapPost("/api/ai/prompts/reload", async (
            ISettingsService settingsService,
            IPromptManagementService promptService) =>
        {
            promptService.InvalidateCache();
            var settings = settingsService.LoadSettings();
            var promptDir = PromptManagementService.ResolvePromptDirectory(settings.AiAssistantPromptsDirectory);
            var sections = await promptService.GetAllPromptsAsync(promptDir);

            return Results.Ok(new
            {
                success = true,
                promptsDirectory = promptDir,
                sections
            });
        });

        // 5. Chat completion (SSE stream or non-stream JSON)
        app.MapPost("/api/ai/chat", async (
            HttpContext httpContext,
            AiChatRequest? request,
            IAiAssistantService assistantService) =>
        {
            if (request == null || request.Messages == null || request.Messages.Count == 0)
            {
                return Results.BadRequest(new { error = "Request body must contain 'messages' array." });
            }

            bool wantsStream = request.Stream ||
                httpContext.Request.Query.ContainsKey("stream") ||
                (httpContext.Request.Headers.Accept.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase));

            if (wantsStream)
            {
                httpContext.Response.ContentType = "text/event-stream";
                httpContext.Response.Headers.CacheControl = "no-cache";
                httpContext.Response.Headers.Connection = "keep-alive";

                try
                {
                    await foreach (var chunk in assistantService.StreamChatAsync(request, cancellationToken: httpContext.RequestAborted))
                    {
                        var json = JsonSerializer.Serialize(chunk, JsonOptions);
                        await httpContext.Response.WriteAsync($"data: {json}\n\n", httpContext.RequestAborted);
                        await httpContext.Response.Body.FlushAsync(httpContext.RequestAborted);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Request aborted by client
                }
                catch (Exception ex)
                {
                    var errorChunk = new AiChatChunk(Error: ex.Message, IsDone: true);
                    var json = JsonSerializer.Serialize(errorChunk, JsonOptions);
                    try
                    {
                        await httpContext.Response.WriteAsync($"data: {json}\n\n");
                        await httpContext.Response.Body.FlushAsync();
                    }
                    catch { }
                }

                return Results.Empty;
            }
            else
            {
                var response = await assistantService.SendChatAsync(request, cancellationToken: httpContext.RequestAborted);
                return Results.Ok(response);
            }
        });
    }
}
