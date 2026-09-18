using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace LocalLLMServerManager.Services;

public class AiAssistantService : IAiAssistantService
{
    private readonly ISettingsService _settingsService;
    private readonly IPromptManagementService _promptService;
    private readonly IAiAppTools _appTools;
    private readonly IHttpClientFactory _httpClientFactory;

    public AiAssistantService(
        ISettingsService settingsService,
        IPromptManagementService promptService,
        IAiAppTools appTools,
        IHttpClientFactory httpClientFactory)
    {
        _settingsService = settingsService;
        _promptService = promptService;
        _appTools = appTools;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AiValidationResult> ValidateConnectionAsync(
        string? endpoint = null,
        string? apiKey = null,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.LoadSettings();
        var targetEndpoint = endpoint != null ? endpoint.Trim() : settings.AiAssistantEndpoint;
        var targetKey = apiKey != null ? apiKey.Trim() : settings.AiAssistantApiKey;
        var targetModel = model != null ? model.Trim() : settings.AiAssistantModel;

        if (string.IsNullOrWhiteSpace(targetEndpoint))
        {
            return new AiValidationResult(false, "API Endpoint URL is required.", new List<string>());
        }

        var sw = Stopwatch.StartNew();
        var availableModels = new List<string>();

        try
        {
            var uri = new Uri(targetEndpoint.TrimEnd('/'));
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            if (!string.IsNullOrWhiteSpace(targetKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", targetKey);
            }

            // 1. Fetch available models from /models endpoint
            var modelsUrl = targetEndpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                ? $"{targetEndpoint}/models"
                : (targetEndpoint.Contains("/v1/") ? $"{targetEndpoint.Substring(0, targetEndpoint.IndexOf("/v1/") + 3)}/models" : $"{targetEndpoint.TrimEnd('/')}/models");

            HttpResponseMessage? modelsResp = null;
            string? modelsError = null;
            try
            {
                modelsResp = await client.GetAsync(modelsUrl, cancellationToken);
                if (modelsResp.IsSuccessStatusCode)
                {
                    var json = await modelsResp.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in dataProp.EnumerateArray())
                        {
                            if (el.TryGetProperty("id", out var idProp) && !string.IsNullOrWhiteSpace(idProp.GetString()))
                            {
                                availableModels.Add(idProp.GetString()!);
                            }
                        }
                    }
                }
                else
                {
                    var errBody = await modelsResp.Content.ReadAsStringAsync(cancellationToken);
                    modelsError = $"Endpoint responded with status {(int)modelsResp.StatusCode} ({modelsResp.ReasonPhrase}): {ExtractErrorMessage(errBody)}";
                }
            }
            catch (Exception ex)
            {
                modelsError = ex.Message;
            }

            // 2. Test ping completion with configured or selected model
            if (!string.IsNullOrWhiteSpace(targetModel))
            {
                var completionsUrl = targetEndpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
                    ? $"{targetEndpoint}/chat/completions"
                    : $"{targetEndpoint.TrimEnd('/')}/chat/completions";

                var pingPayload = new
                {
                    model = targetModel,
                    messages = new[] { new { role = "user", content = "ping" } },
                    max_tokens = 5
                };

                var postContent = new StringContent(JsonSerializer.Serialize(pingPayload), Encoding.UTF8, "application/json");
                var pingResp = await client.PostAsync(completionsUrl, postContent, cancellationToken);
                sw.Stop();

                if (!pingResp.IsSuccessStatusCode)
                {
                    var errBody = await pingResp.Content.ReadAsStringAsync(cancellationToken);
                    return new AiValidationResult(
                        false,
                        $"Endpoint responded with status {(int)pingResp.StatusCode} ({pingResp.ReasonPhrase}): {ExtractErrorMessage(errBody)}",
                        availableModels,
                        sw.ElapsedMilliseconds);
                }
            }
            else
            {
                sw.Stop();
                if (modelsError != null)
                {
                    return new AiValidationResult(
                        false,
                        $"Failed to query models from endpoint: {modelsError}",
                        availableModels,
                        sw.ElapsedMilliseconds);
                }
            }

            return new AiValidationResult(
                true,
                $"Connected successfully to {uri.Host} (Latency: {sw.ElapsedMilliseconds} ms, {availableModels.Count} models found)",
                availableModels,
                sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new AiValidationResult(false, $"Connection failed: {ex.Message}", availableModels, sw.ElapsedMilliseconds);
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync(
        string? endpoint = null,
        string? apiKey = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ValidateConnectionAsync(endpoint, apiKey, model: null, cancellationToken);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Message);
        }
        return result.AvailableModels;
    }

    public async Task<AiChatResponse> SendChatAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.LoadSettings();
        var targetEndpoint = string.IsNullOrWhiteSpace(settings.AiAssistantEndpoint) ? "http://127.0.0.1:4000/v1" : settings.AiAssistantEndpoint;
        var targetKey = settings.AiAssistantApiKey ?? "";
        var targetModel = !string.IsNullOrWhiteSpace(request.Model) ? request.Model : (string.IsNullOrWhiteSpace(settings.AiAssistantModel) ? "google/gemini-2.5-flash" : settings.AiAssistantModel);

        try
        {
            // Build tool set
            var tools = BuildAiFunctions();

            // Build chat client
            var credential = new ApiKeyCredential(string.IsNullOrWhiteSpace(targetKey) ? "placeholder-key" : targetKey);
            var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(targetEndpoint.TrimEnd('/')) };
            var openAiChat = new OpenAI.Chat.ChatClient(targetModel, credential, clientOptions);

            var innerChatClient = openAiChat.AsIChatClient();
            using var invokingClient = new FunctionInvokingChatClient(innerChatClient);

            // Assemble system prompt
            var systemPrompt = await _promptService.BuildFullSystemPromptAsync(settings.AiAssistantPromptsDirectory, cancellationToken);
            if (!string.IsNullOrWhiteSpace(settings.AiAssistantCustomSystemPrompt))
            {
                systemPrompt = $"{settings.AiAssistantCustomSystemPrompt.Trim()}\n\n{systemPrompt}";
            }

            var messages = new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new(Microsoft.Extensions.AI.ChatRole.System, systemPrompt)
            };

            foreach (var msg in request.Messages.Where(m => !string.IsNullOrWhiteSpace(m.Content)))
            {
                var role = msg.Role.ToLowerInvariant() switch
                {
                    "assistant" => Microsoft.Extensions.AI.ChatRole.Assistant,
                    "system" => Microsoft.Extensions.AI.ChatRole.System,
                    _ => Microsoft.Extensions.AI.ChatRole.User
                };
                messages.Add(new(role, msg.Content));
            }

            var chatOptions = new Microsoft.Extensions.AI.ChatOptions
            {
                ModelId = targetModel,
                Temperature = (float)(request.Temperature ?? 0.7),
                Tools = tools
            };

            var executedTools = new List<AiToolExecutionItem>();

            var completion = await invokingClient.GetResponseAsync(messages, chatOptions, cancellationToken);

            var assistantItem = new AiChatMessageItem
            {
                Role = "assistant",
                Content = completion.Text ?? "",
                Timestamp = DateTime.UtcNow,
                ToolCalls = executedTools
            };

            return new AiChatResponse(true, assistantItem, TotalTokens: (int?)completion.Usage?.TotalTokenCount);
        }
        catch (Exception ex)
        {
            return new AiChatResponse(false, Error: $"AI Assistant error: {ex.Message}");
        }
    }

    public async IAsyncEnumerable<AiChatChunk> StreamChatAsync(
        AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.LoadSettings();
        var targetEndpoint = string.IsNullOrWhiteSpace(settings.AiAssistantEndpoint) ? "http://127.0.0.1:4000/v1" : settings.AiAssistantEndpoint;
        var targetKey = settings.AiAssistantApiKey ?? "";
        var targetModel = !string.IsNullOrWhiteSpace(request.Model) ? request.Model : (string.IsNullOrWhiteSpace(settings.AiAssistantModel) ? "google/gemini-2.5-flash" : settings.AiAssistantModel);

        var credential = new ApiKeyCredential(string.IsNullOrWhiteSpace(targetKey) ? "placeholder-key" : targetKey);
        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(targetEndpoint.TrimEnd('/')) };
        var openAiChat = new OpenAI.Chat.ChatClient(targetModel, credential, clientOptions);

        var innerChatClient = openAiChat.AsIChatClient();
        using var invokingClient = new FunctionInvokingChatClient(innerChatClient);

        var systemPrompt = await _promptService.BuildFullSystemPromptAsync(settings.AiAssistantPromptsDirectory, cancellationToken);
        if (!string.IsNullOrWhiteSpace(settings.AiAssistantCustomSystemPrompt))
        {
            systemPrompt = $"{settings.AiAssistantCustomSystemPrompt.Trim()}\n\n{systemPrompt}";
        }

        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(Microsoft.Extensions.AI.ChatRole.System, systemPrompt)
        };

        foreach (var msg in request.Messages.Where(m => !string.IsNullOrWhiteSpace(m.Content)))
        {
            var role = msg.Role.ToLowerInvariant() switch
            {
                "assistant" => Microsoft.Extensions.AI.ChatRole.Assistant,
                "system" => Microsoft.Extensions.AI.ChatRole.System,
                _ => Microsoft.Extensions.AI.ChatRole.User
            };
            messages.Add(new(role, msg.Content));
        }

        var chatOptions = new Microsoft.Extensions.AI.ChatOptions
        {
            ModelId = targetModel,
            Temperature = (float)(request.Temperature ?? 0.7),
            Tools = BuildAiFunctions()
        };

        await foreach (var update in invokingClient.GetStreamingResponseAsync(messages, chatOptions, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return new AiChatChunk(DeltaText: update.Text);
            }
        }
        yield return new AiChatChunk(IsDone: true);
    }

    public IList<AITool> BuildAiFunctions()
    {
        var list = new List<AITool>
        {
            AIFunctionFactory.Create(
                (Func<CancellationToken, Task<string>>)_appTools.GetGpuVramTelemetryAsync,
                "get_gpu_vram_telemetry",
                "Get real-time GPU VRAM allocation, total memory, used memory, and GPU hardware name."),

            AIFunctionFactory.Create(
                (Func<CancellationToken, Task<string>>)_appTools.CheckServicesHealthAsync,
                "check_services_health",
                "Check real-time health and connectivity of Ollama, Stable Diffusion Forge, ComfyUI, and Kokoro TTS backend ports."),

            AIFunctionFactory.Create(
                (Func<CancellationToken, Task<string>>)_appTools.ListInstalledModelsAsync,
                "list_installed_models",
                "List all installed Ollama LLM models, quantization formats, and memory footprint."),

            AIFunctionFactory.Create(
                (Func<string, CancellationToken, Task<string>>)_appTools.StartAiEngineAsync,
                "start_ai_engine",
                "Start an AI backend engine process ('forge', 'comfyui', or 'ollama')."),

            AIFunctionFactory.Create(
                (Func<string, CancellationToken, Task<string>>)_appTools.StopAiEngineAsync,
                "stop_ai_engine",
                "Gracefully terminate an AI backend engine process ('forge' or 'comfyui')."),

            AIFunctionFactory.Create(
                (Func<CancellationToken, Task<string>>)_appTools.UnloadVramAsync,
                "unload_vram",
                "Unload all LLM models currently residing in GPU VRAM to free memory for diffusion or heavy workflows."),

            AIFunctionFactory.Create(
                (Func<CancellationToken, Task<string>>)_appTools.GetAppSettingsAsync,
                "get_app_settings",
                "Get current application configuration settings."),

            AIFunctionFactory.Create(
                (Func<string, string, CancellationToken, Task<string>>)_appTools.UpdateAppSettingAsync,
                "update_app_setting",
                "Update a specific application setting key (e.g. 'PreferredImageEngine', 'ComfyUiUrl', 'SelectedThemeStyle', 'AiAssistantModel') and persist changes to disk."),

            AIFunctionFactory.Create(
                (Func<string, double?, string?, string?, CancellationToken, Task<string>>)_appTools.CalculateHardwareFitAsync,
                "calculate_hardware_fit",
                "Calculate hardware compatibility and VRAM fit for an LLM, diffusion, or video model."),

            AIFunctionFactory.Create(
                (Func<string, string?, string?, int, int, CancellationToken, Task<string>>)_appTools.GenerateImageAsync,
                "generate_image",
                "Generate an image using Stable Diffusion Forge or ComfyUI."),

            AIFunctionFactory.Create(
                (Func<string, string, string, CancellationToken, Task<string>>)_appTools.SynthesizeSpeechAsync,
                "synthesize_speech",
                "Synthesize spoken audio from text using local Kokoro TTS."),

            AIFunctionFactory.Create(
                (Func<string, CancellationToken, Task<string>>)_appTools.QueryAppDocumentationAsync,
                "query_app_documentation",
                "Search in-app technical documentation procedures, steps, prerequisites, and warnings.")
        };

        return list;
    }

    private static string ExtractErrorMessage(string jsonOrRaw)
    {
        if (string.IsNullOrWhiteSpace(jsonOrRaw)) return "Unknown error";
        try
        {
            using var doc = JsonDocument.Parse(jsonOrRaw);
            if (doc.RootElement.TryGetProperty("error", out var errProp))
            {
                if (errProp.ValueKind == JsonValueKind.Object && errProp.TryGetProperty("message", out var msgProp))
                {
                    return msgProp.GetString() ?? jsonOrRaw;
                }
                if (errProp.ValueKind == JsonValueKind.String)
                {
                    return errProp.GetString() ?? jsonOrRaw;
                }
            }
            if (doc.RootElement.TryGetProperty("message", out var topMsg))
            {
                return topMsg.GetString() ?? jsonOrRaw;
            }
        }
        catch { }

        return jsonOrRaw.Length > 200 ? jsonOrRaw[..200] + "..." : jsonOrRaw;
    }
}
