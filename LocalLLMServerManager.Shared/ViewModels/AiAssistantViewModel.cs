using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

/// <summary>
/// ViewModel managing the In-App AI Assistant & Copilot interface.
/// Provides multi-turn chat orchestration, streaming token rendering, tool invocation cards,
/// setup wizard with connection validation, and natural language app interaction.
/// </summary>
public partial class AiAssistantViewModel : ObservableObject
{
    private readonly IAiAssistantService? _assistantService;
    private readonly ISettingsService? _settingsService;
    private readonly IPromptManagementService? _promptService;
    private readonly HttpClient? _httpClient;
    private CancellationTokenSource? _generationCts;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Chat History
    public ObservableCollection<AiChatMessageItem> Messages { get; } = new();

    // Setup & Configuration
    [ObservableProperty] private string _endpoint = "http://127.0.0.1:4000/v1";
    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _selectedModel = "vertex_ai/gemini-2.5-flash";
    [ObservableProperty] private ObservableCollection<string> _availableModels = new();
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private bool _isSetupCardVisible = false;
    [ObservableProperty] private bool _isTestingConnection = false;
    [ObservableProperty] private string _connectionStatusMessage = "";
    [ObservableProperty] private bool? _isConnectionSuccess = null;
    [ObservableProperty] private string _promptDirectory = "";

    // Chat UI States
    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private bool _isGenerating = false;
    [ObservableProperty] private string _currentStatus = "";
    [ObservableProperty] private bool _autoScrollEnabled = true;

    // Quick Suggestion Chips
    public IReadOnlyList<string> SuggestionChips { get; } = new[]
    {
        "⚡ Check live VRAM and GPU telemetry",
        "🦙 Can I run Llama 3.3 70B on my current hardware?",
        "🎨 Generate a scenic mountain landscape with Flux",
        "📦 List all installed models and their sizes",
        "🗣️ Speak 'Welcome to Local LLM Server Manager' using Kokoro",
        "🛑 Unload all models from VRAM"
    };

    public AiAssistantViewModel()
        : this(null, null, null, null)
    {
    }

    public AiAssistantViewModel(
        IAiAssistantService? assistantService,
        ISettingsService? settingsService = null,
        IPromptManagementService? promptService = null,
        HttpClient? httpClient = null)
    {
        _assistantService = assistantService;
        _settingsService = settingsService;
        _promptService = promptService;
        _httpClient = httpClient;

        if (_settingsService != null)
        {
            var s = _settingsService.LoadSettings();
            _isEnabled = s.AiAssistantEnabled;
            _endpoint = string.IsNullOrWhiteSpace(s.AiAssistantEndpoint) ? "http://127.0.0.1:4000/v1" : s.AiAssistantEndpoint;
            _apiKey = s.AiAssistantApiKey ?? "";
            _selectedModel = string.IsNullOrWhiteSpace(s.AiAssistantModel) ? "vertex_ai/gemini-2.5-flash" : s.AiAssistantModel;
            _promptDirectory = PromptManagementService.ResolvePromptDirectory(s.AiAssistantPromptsDirectory) ?? "";
        }
        else
        {
            _promptDirectory = PromptManagementService.ResolvePromptDirectory() ?? "";
        }

        // Add initial model to dropdown if empty
        if (!string.IsNullOrWhiteSpace(_selectedModel))
        {
            _availableModels.Add(_selectedModel);
        }

        // Add welcome message
        Messages.Add(new AiChatMessageItem
        {
            Role = "assistant",
            Content = "👋 Hello! I am your AI Assistant and App Copilot. I can query live VRAM telemetry, evaluate model hardware fit, inspect or update app settings, launch workflows, and answer questions about the platform.\n\nType a request below or tap any suggestion chip to get started!",
            Timestamp = DateTime.UtcNow
        });
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        var text = InputText?.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsGenerating)
        {
            return;
        }

        // 1. Add user message
        var userMsg = new AiChatMessageItem
        {
            Role = "user",
            Content = text,
            Timestamp = DateTime.UtcNow
        };
        Messages.Add(userMsg);
        InputText = "";

        // 2. Prepare assistant placeholder
        var assistantMsg = new AiChatMessageItem
        {
            Role = "assistant",
            Content = "",
            IsLoading = true,
            StatusText = "Connecting to LLM...",
            Timestamp = DateTime.UtcNow
        };
        Messages.Add(assistantMsg);

        IsGenerating = true;
        CurrentStatus = "Thinking...";
        _generationCts = new CancellationTokenSource();
        var ct = _generationCts.Token;

        try
        {
            var chatMessages = Messages
                .Where(m => !m.IsError && !m.IsLoading && !string.IsNullOrWhiteSpace(m.Content))
                .ToList();

            var request = new AiChatRequest(
                Messages: chatMessages,
                Stream: true,
                Model: SelectedModel
            );

            if (_assistantService != null)
            {
                // Direct local in-process streaming
                await foreach (var chunk in _assistantService.StreamChatAsync(request, ct))
                {
                    if (chunk.ToolCall != null)
                    {
                        assistantMsg.ToolCalls.Add(chunk.ToolCall);
                        assistantMsg.StatusText = $"Executing {chunk.ToolCall.ToolName}...";
                    }

                    if (!string.IsNullOrEmpty(chunk.DeltaText))
                    {
                        assistantMsg.Content += chunk.DeltaText;
                        assistantMsg.StatusText = null;
                    }

                    if (!string.IsNullOrEmpty(chunk.Error))
                    {
                        assistantMsg.IsError = true;
                        assistantMsg.Content += $"\n⚠️ {chunk.Error}";
                    }
                }
            }
            else if (_httpClient != null)
            {
                // Remote / WASM client streaming via SSE or POST
                var response = await _httpClient.PostAsJsonAsync("/api/ai/chat", request with { Stream = false }, ct);
                if (response.IsSuccessStatusCode)
                {
                    var chatResp = await response.Content.ReadFromJsonAsync<AiChatResponse>(JsonOptions, ct);
                    if (chatResp != null && chatResp.Success && chatResp.Message != null)
                    {
                        assistantMsg.Content = chatResp.Message.Content;
                        if (chatResp.Message.ToolCalls != null)
                        {
                            foreach (var tc in chatResp.Message.ToolCalls)
                            {
                                assistantMsg.ToolCalls.Add(tc);
                            }
                        }
                    }
                    else
                    {
                        assistantMsg.IsError = true;
                        assistantMsg.Content = chatResp?.Error ?? "Unknown error received from AI server.";
                    }
                }
                else
                {
                    assistantMsg.IsError = true;
                    assistantMsg.Content = $"Server error {(int)response.StatusCode}: {response.ReasonPhrase}";
                }
            }
            else
            {
                assistantMsg.IsError = true;
                assistantMsg.Content = "AI service is not configured or available.";
            }

            if (string.IsNullOrWhiteSpace(assistantMsg.Content) && assistantMsg.ToolCalls.Count > 0)
            {
                assistantMsg.Content = "Tools executed successfully.";
            }
        }
        catch (OperationCanceledException)
        {
            if (string.IsNullOrWhiteSpace(assistantMsg.Content))
            {
                assistantMsg.Content = "⏹️ Generation stopped by user.";
            }
        }
        catch (Exception ex)
        {
            assistantMsg.IsError = true;
            assistantMsg.Content = $"⚠️ Error: {ex.Message}";
        }
        finally
        {
            assistantMsg.IsLoading = false;
            assistantMsg.StatusText = null;
            IsGenerating = false;
            CurrentStatus = "";
            _generationCts = null;
        }
    }

    [RelayCommand]
    public void StopGeneration()
    {
        _generationCts?.Cancel();
    }

    [RelayCommand]
    public void ClearChat()
    {
        Messages.Clear();
        Messages.Add(new AiChatMessageItem
        {
            Role = "assistant",
            Content = "Conversation cleared. Ready for your next question or task!",
            Timestamp = DateTime.UtcNow
        });
    }

    [RelayCommand]
    public async Task SelectSuggestionAsync(string? suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion) || IsGenerating)
        {
            return;
        }

        InputText = suggestion;
        await SendMessageAsync();
    }

    [RelayCommand]
    public void ToggleSetupCard()
    {
        IsSetupCardVisible = !IsSetupCardVisible;
    }

    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        IsTestingConnection = true;
        ConnectionStatusMessage = "Testing endpoint connectivity and model availability...";
        IsConnectionSuccess = null;

        try
        {
            AiValidationResult? result = null;
            if (_assistantService != null)
            {
                result = await _assistantService.ValidateConnectionAsync(Endpoint, ApiKey, SelectedModel);
            }
            else if (_httpClient != null)
            {
                var payload = new { endpoint = Endpoint, apiKey = ApiKey, model = SelectedModel };
                var resp = await _httpClient.PostAsJsonAsync("/api/ai/validate", payload);
                if (resp.IsSuccessStatusCode)
                {
                    result = await resp.Content.ReadFromJsonAsync<AiValidationResult>(JsonOptions);
                }
            }

            if (result != null)
            {
                IsConnectionSuccess = result.Success;
                ConnectionStatusMessage = result.Message;

                if (result.AvailableModels != null && result.AvailableModels.Count > 0)
                {
                    AvailableModels.Clear();
                    foreach (var m in result.AvailableModels)
                    {
                        AvailableModels.Add(m);
                    }
                    if (!AvailableModels.Contains(SelectedModel) && AvailableModels.Count > 0)
                    {
                        SelectedModel = AvailableModels[0];
                    }
                }
            }
            else
            {
                IsConnectionSuccess = false;
                ConnectionStatusMessage = "Failed to communicate with the validation service.";
            }
        }
        catch (Exception ex)
        {
            IsConnectionSuccess = false;
            ConnectionStatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    [RelayCommand]
    public void SaveConfiguration()
    {
        if (_settingsService != null)
        {
            var s = _settingsService.LoadSettings();
            var updated = s with
            {
                AiAssistantEnabled = IsEnabled,
                AiAssistantEndpoint = Endpoint,
                AiAssistantApiKey = ApiKey,
                AiAssistantModel = SelectedModel
            };
            _settingsService.SaveSettings(updated);
        }

        IsSetupCardVisible = false;
        ConnectionStatusMessage = "Settings saved successfully.";
    }

    [RelayCommand]
    public async Task ReloadPromptsAsync()
    {
        if (_promptService != null)
        {
            _promptService.InvalidateCache();
            PromptDirectory = PromptManagementService.ResolvePromptDirectory() ?? "";
            ConnectionStatusMessage = "Living prompts reloaded from disk.";
        }
        else if (_httpClient != null)
        {
            await _httpClient.PostAsync("/api/ai/prompts/reload", null);
            ConnectionStatusMessage = "Prompts reloaded via API.";
        }
    }
}
