# LiteLLM Model Capabilities, Local Model Exclusion & Multimodal Chat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pull the full LiteLLM model catalog and capabilities (modality, vision, tools, token limits), filter out local models while retaining cloud models, enable on-the-fly model switching in the chat composer bar, and support multimodal image inputs.

**Architecture:** Extend `IAiAssistantService` and `AiAssistantService` to query LiteLLM `/model/info` (falling back to `/v1/models`), parse capability metadata into `AiModelCapabilityInfo`, exclude local runtimes (`ollama`, `local`, `llama.cpp`), serialize multimodal `ChatMessage` instances with `ImageContent` into `Microsoft.Extensions.AI`, expose capabilities over `GET /api/ai/models`, and update the Avalonia composer bar with a rich capability dropdown and image staging tray.

**Tech Stack:** C# 13 / .NET 10, Microsoft.Extensions.AI, Microsoft.Extensions.AI.OpenAI, Avalonia UI 12 (Fluent), CommunityToolkit.Mvvm, xUnit, Moq, ESLint, TypeScript.

## Global Constraints

- Always run linting (`npm run lint`) and typechecking (`npx tsc --noEmit`) after code changes.
- Do NOT filter models based on IP addresses (LiteLLM itself is network-hosted on LAN). Filter local models strictly by provider (`ollama`, `local`, `llama.cpp`, `vllm_local`) or model ID prefix (`ollama/`, `local/`).
- Preserve backward compatibility with existing 637 tests.

---

### Task 1: Data Models & Attachments (`AiAssistantModels.cs`)

**Files:**
- Modify: `LocalLLMServerManager.Shared/Models/AiAssistantModels.cs`
- Test: `LocalLLMServerManager.Tests/AiAssistantServiceTests.cs`

**Interfaces:**
- Produces: `AiModelCapabilityInfo` record with properties (`Id`, `DisplayName`, `Provider`, `Mode`, `SupportsVision`, `SupportsFunctionCalling`, `SupportsAudio`, `MaxInputTokens`, `MaxOutputTokens`, `IsLocal`, `SummaryBadge`).
- Produces: `AiChatMessageAttachment` class (`Id`, `FileName`, `ContentType`, `Base64Data`, `RawBytes`).
- Modifies: `AiChatMessageItem` adding `List<AiChatMessageAttachment> Attachments` and `bool HasAttachments`.

- [ ] **Step 1: Write the failing unit test**

Create/update `LocalLLMServerManager.Tests/AiAssistantServiceTests.cs`:
```csharp
[Fact]
public void AiModelCapabilityInfo_FormattingAndProperties_AreValid()
{
    var model = new AiModelCapabilityInfo(
        Id: "vertex_ai/gemini-2.5-flash",
        DisplayName: "Gemini 2.5 Flash",
        Provider: "vertex_ai",
        Mode: "chat",
        SupportsVision: true,
        SupportsFunctionCalling: true,
        MaxInputTokens: 1000000,
        MaxOutputTokens: 8192,
        IsLocal: false
    );

    Assert.Equal("vertex_ai/gemini-2.5-flash", model.Id);
    Assert.True(model.SupportsVision);
    Assert.True(model.SupportsFunctionCalling);
    Assert.False(model.IsLocal);
    Assert.Contains("👁️", model.SummaryBadge);
    Assert.Contains("⚡", model.SummaryBadge);
    Assert.Contains("1M", model.SummaryBadge);
}

[Fact]
public void AiChatMessageItem_WithAttachments_ReportsHasAttachmentsTrue()
{
    var msg = new AiChatMessageItem
    {
        Role = "user",
        Content = "Inspect this image",
        Attachments = new List<AiChatMessageAttachment>
        {
            new() { FileName = "test.png", ContentType = "image/png", Base64Data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=" }
        }
    };

    Assert.True(msg.HasAttachments);
    Assert.Single(msg.Attachments);
    Assert.Equal("image/png", msg.Attachments[0].ContentType);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~AiModelCapabilityInfo_FormattingAndProperties_AreValid"`
Expected: FAIL (types do not exist yet).

- [ ] **Step 3: Implement data structures in `AiAssistantModels.cs`**

Modify `LocalLLMServerManager.Shared/Models/AiAssistantModels.cs`:
```csharp
public class AiChatMessageAttachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "image/png";
    public string Base64Data { get; set; } = "";
    public byte[]? RawBytes { get; set; }
}

public record AiModelCapabilityInfo(
    string Id,
    string DisplayName,
    string Provider,
    string Mode = "chat",
    bool SupportsVision = false,
    bool SupportsFunctionCalling = true,
    bool SupportsAudio = false,
    int? MaxInputTokens = null,
    int? MaxOutputTokens = null,
    bool IsLocal = false
)
{
    public string SummaryBadge
    {
        get
        {
            var parts = new List<string>();
            if (SupportsVision) parts.Add("👁️");
            if (SupportsFunctionCalling) parts.Add("⚡");
            if (MaxInputTokens.HasValue && MaxInputTokens.Value > 0) parts.Add(FormatTokenCount(MaxInputTokens.Value));
            var badges = parts.Count > 0 ? $"{string.Join(" ", parts)} " : "";
            return $"{badges}[{Provider}]";
        }
    }

    private static string FormatTokenCount(int tokens) =>
        tokens >= 1_000_000 ? $"{tokens / 1_000_000.0:0.#}M" : (tokens >= 1_000 ? $"{tokens / 1_000}k" : $"{tokens}");
}
```
And add `public List<AiChatMessageAttachment> Attachments { get; set; } = new();` and `public bool HasAttachments => Attachments != null && Attachments.Count > 0;` to `AiChatMessageItem`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~AiModelCapabilityInfo_FormattingAndProperties_AreValid"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Models/AiAssistantModels.cs LocalLLMServerManager.Tests/AiAssistantServiceTests.cs
git commit -m "feat(models): add AiModelCapabilityInfo and AiChatMessageAttachment structures"
```

---

### Task 2: Service Layer Model Discovery & Local Filtering (`AiAssistantService.cs`)

**Files:**
- Modify: `LocalLLMServerManager.Shared/Interfaces/IAiAssistantService.cs`
- Modify: `Services/AiAssistantService.cs`
- Test: `LocalLLMServerManager.Tests/AiAssistantServiceTests.cs`

**Interfaces:**
- Consumes: `IAiAssistantService`, `AiModelCapabilityInfo`.
- Produces: `Task<List<AiModelCapabilityInfo>> GetModelCapabilitiesAsync(string? endpoint = null, string? apiKey = null, bool includeLocal = false, CancellationToken cancellationToken = default);`
- Implements: Local model identification (`IsLocalModel`), `/model/info` and `/v1/models` JSON parsing, capability heuristics fallback.

- [ ] **Step 1: Write the failing unit tests for capability parsing and local exclusion**

Add to `LocalLLMServerManager.Tests/AiAssistantServiceTests.cs`:
```csharp
[Fact]
public void ParseModelCapabilities_ExcludesLocalModels_WhenIncludeLocalIsFalse()
{
    var sampleLiteLlmJson = """
    {
      "data": [
        {
          "id": "vertex_ai/gemini-2.5-flash",
          "model_info": {
            "mode": "chat",
            "litellm_provider": "vertex_ai",
            "supports_vision": true,
            "supports_function_calling": true,
            "max_input_tokens": 1048576,
            "max_output_tokens": 8192
          }
        },
        {
          "id": "openai/gpt-4o",
          "model_info": {
            "mode": "chat",
            "litellm_provider": "openai",
            "supports_vision": true,
            "supports_function_calling": true,
            "max_tokens": 128000
          }
        },
        {
          "id": "ollama/llama3.2:latest",
          "model_info": {
            "mode": "chat",
            "litellm_provider": "ollama",
            "supports_vision": false,
            "supports_function_calling": true
          }
        },
        {
          "id": "local/mistral-7b",
          "model_info": {
            "mode": "chat",
            "litellm_provider": "local"
          }
        }
      ]
    }
    """;

    var models = AiAssistantService.ParseModelCapabilitiesFromJson(sampleLiteLlmJson, includeLocal: false);

    Assert.Equal(2, models.Count);
    Assert.Contains(models, m => m.Id == "vertex_ai/gemini-2.5-flash" && m.SupportsVision && m.MaxInputTokens == 1048576);
    Assert.Contains(models, m => m.Id == "openai/gpt-4o" && m.SupportsVision && m.MaxInputTokens == 128000);
    Assert.DoesNotContain(models, m => m.Id.StartsWith("ollama/"));
    Assert.DoesNotContain(models, m => m.Id.StartsWith("local/"));
}

[Fact]
public void ParseModelCapabilities_IncludesLocalModels_WhenIncludeLocalIsTrue()
{
    var sampleLiteLlmJson = """
    {
      "data": [
        {
          "id": "vertex_ai/gemini-2.5-flash",
          "model_info": { "litellm_provider": "vertex_ai" }
        },
        {
          "id": "ollama/llama3.2",
          "model_info": { "litellm_provider": "ollama" }
        }
      ]
    }
    """;

    var models = AiAssistantService.ParseModelCapabilitiesFromJson(sampleLiteLlmJson, includeLocal: true);

    Assert.Equal(2, models.Count);
    Assert.Contains(models, m => m.Id == "ollama/llama3.2" && m.IsLocal);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~ParseModelCapabilities"`
Expected: FAIL (method not defined).

- [ ] **Step 3: Update `IAiAssistantService` and implement in `AiAssistantService.cs`**

1. Update `IAiAssistantService.cs` with:
```csharp
Task<List<AiModelCapabilityInfo>> GetModelCapabilitiesAsync(
    string? endpoint = null,
    string? apiKey = null,
    bool includeLocal = false,
    CancellationToken cancellationToken = default);
```

2. In `AiAssistantService.cs`, implement:
- `public static bool IsLocalModel(string id, string? provider)` checking `provider` against `ollama`, `local`, `llama.cpp`, `vllm_local` or ID starting with `ollama/`, `local/`, `llama/`, `ollama_chat/`.
- `public static List<AiModelCapabilityInfo> ParseModelCapabilitiesFromJson(string json, bool includeLocal = false)`:
  - Iterates `data` elements.
  - Extracts ID, provider, mode, `supports_vision`, `supports_function_calling`, `supports_audio`, `max_input_tokens`, `max_output_tokens`.
  - Applies heuristic defaults (e.g. if `id` contains `gemini` or `gpt-4o` or `claude-3`, set vision/tools true).
  - Flags `isLocal`.
  - Filters out local models when `!includeLocal`.
- Implement `GetModelCapabilitiesAsync`:
  - Calls `GET /model/info` (or `/v1/model_info`).
  - If unsuccessful, falls back to `GET /v1/models` or `GET /models`.
  - Returns parsed `List<AiModelCapabilityInfo>`.
- Update `GetAvailableModelsAsync` to call `GetModelCapabilitiesAsync(endpoint, apiKey, includeLocal: false)` and project `m.Id`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~ParseModelCapabilities"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Interfaces/IAiAssistantService.cs Services/AiAssistantService.cs LocalLLMServerManager.Tests/AiAssistantServiceTests.cs
git commit -m "feat(orchestration): implement LiteLLM capability extraction and local model exclusion"
```

---

### Task 3: Multimodal Message Formatting with `Microsoft.Extensions.AI` (`AiAssistantService.cs`)

**Files:**
- Modify: `Services/AiAssistantService.cs`
- Test: `LocalLLMServerManager.Tests/AiAssistantServiceTests.cs`

**Interfaces:**
- Modifies `AiAssistantService.SendChatAsync` and `StreamChatAsync` to construct `Microsoft.Extensions.AI.ChatMessage` with `TextContent` and `ImageContent` from `AiChatMessageItem.Attachments`.

- [ ] **Step 1: Write the failing unit test**

Add to `LocalLLMServerManager.Tests/AiAssistantServiceTests.cs`:
```csharp
[Fact]
public void BuildExtensionsAiChatMessage_WithImageAttachment_CreatesMultimodalChatMessage()
{
    var item = new AiChatMessageItem
    {
        Role = "user",
        Content = "What is in this diagram?",
        Attachments = new List<AiChatMessageAttachment>
        {
            new()
            {
                FileName = "diagram.png",
                ContentType = "image/png",
                Base64Data = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
            }
        }
    };

    var chatMessage = AiAssistantService.ToExtensionsAiChatMessage(item);

    Assert.Equal(Microsoft.Extensions.AI.ChatRole.User, chatMessage.Role);
    Assert.Equal(2, chatMessage.Contents.Count);
    Assert.IsType<Microsoft.Extensions.AI.TextContent>(chatMessage.Contents[0]);
    Assert.IsType<Microsoft.Extensions.AI.ImageContent>(chatMessage.Contents[1]);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~BuildExtensionsAiChatMessage"`
Expected: FAIL.

- [ ] **Step 3: Implement `ToExtensionsAiChatMessage` in `AiAssistantService.cs`**

Implement helper method:
```csharp
public static Microsoft.Extensions.AI.ChatMessage ToExtensionsAiChatMessage(AiChatMessageItem msg)
{
    var role = msg.Role.ToLowerInvariant() switch
    {
        "assistant" => Microsoft.Extensions.AI.ChatRole.Assistant,
        "system" => Microsoft.Extensions.AI.ChatRole.System,
        _ => Microsoft.Extensions.AI.ChatRole.User
    };

    if (msg.Attachments == null || msg.Attachments.Count == 0)
    {
        return new Microsoft.Extensions.AI.ChatMessage(role, msg.Content ?? "");
    }

    var contents = new List<Microsoft.Extensions.AI.AIContent>();
    if (!string.IsNullOrWhiteSpace(msg.Content))
    {
        contents.Add(new Microsoft.Extensions.AI.TextContent(msg.Content));
    }

    foreach (var att in msg.Attachments)
    {
        if (att.RawBytes != null && att.RawBytes.Length > 0)
        {
            contents.Add(new Microsoft.Extensions.AI.ImageContent(att.RawBytes, att.ContentType));
        }
        else if (!string.IsNullOrWhiteSpace(att.Base64Data))
        {
            var uri = att.Base64Data.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? new Uri(att.Base64Data)
                : new Uri($"data:{att.ContentType};base64,{att.Base64Data}");
            contents.Add(new Microsoft.Extensions.AI.ImageContent(uri, att.ContentType));
        }
    }

    return new Microsoft.Extensions.AI.ChatMessage(role, contents);
}
```
Update `SendChatAsync` and `StreamChatAsync` in `AiAssistantService.cs` to call `ToExtensionsAiChatMessage(msg)` when building the message list.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~BuildExtensionsAiChatMessage"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/AiAssistantService.cs LocalLLMServerManager.Tests/AiAssistantServiceTests.cs
git commit -m "feat(orchestration): support multimodal ImageContent in chat completions and streaming"
```

---

### Task 4: Web Endpoints for Model Capabilities (`AiAssistantEndpoints.cs`)

**Files:**
- Modify: `Endpoints/AiAssistantEndpoints.cs`
- Test: `LocalLLMServerManager.Tests/AiAssistantEndpointsTests.cs`

**Interfaces:**
- Modifies `GET /api/ai/models` to support `?includeLocal=false&refresh=false` and return `List<AiModelCapabilityInfo>`.

- [ ] **Step 1: Write the failing endpoint test**

Add to `LocalLLMServerManager.Tests/AiAssistantEndpointsTests.cs`:
```csharp
[Fact]
public async Task GetModels_ReturnsModelCapabilitiesList()
{
    var mockService = new Mock<IAiAssistantService>();
    var expectedList = new List<AiModelCapabilityInfo>
    {
        new("vertex_ai/gemini-2.5-flash", "Gemini 2.5 Flash", "vertex_ai", SupportsVision: true, SupportsFunctionCalling: true)
    };
    mockService
        .Setup(s => s.GetModelCapabilitiesAsync(It.IsAny<string?>(), It.IsAny<string?>(), false, It.IsAny<CancellationToken>()))
        .ReturnsAsync(expectedList);

    // Test helper or endpoint invocation
    var result = await mockService.Object.GetModelCapabilitiesAsync(null, null, false);
    Assert.Single(result);
    Assert.Equal("vertex_ai/gemini-2.5-flash", result[0].Id);
    Assert.True(result[0].SupportsVision);
}
```

- [ ] **Step 2: Update `AiAssistantEndpoints.cs`**

Modify `GET /api/ai/models` in `Endpoints/AiAssistantEndpoints.cs`:
- Extract `includeLocal` boolean from query string (default `false`).
- Call `assistantService.GetModelCapabilitiesAsync(endpoint, apiKey, includeLocal, httpContext.RequestAborted)`.
- Return `Results.Ok(capabilities)`.

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~GetModels"`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Endpoints/AiAssistantEndpoints.cs LocalLLMServerManager.Tests/AiAssistantEndpointsTests.cs
git commit -m "feat(endpoints): expose rich model capabilities on GET /api/ai/models"
```

---

### Task 5: ViewModel Capability-Aware Model Switching & Image Staging (`AiAssistantViewModel.cs`)

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/AiAssistantViewModel.cs`
- Test: `LocalLLMServerManager.Tests/AiAssistantViewModelTests.cs`

**Interfaces:**
- Produces:
  - `ObservableCollection<AiModelCapabilityInfo> AvailableModelCapabilities`
  - `AiModelCapabilityInfo? SelectedModelCapability`
  - `ObservableCollection<AiChatMessageAttachment> StagedAttachments`
  - `bool HasStagedAttachments`
  - `AttachImageCommand(AiChatMessageAttachment)`
  - `RemoveAttachmentCommand(string attachmentId)`
  - `PasteImageBytes(byte[] bytes, string mimeType)`
  - `LoadAvailableModelsAsync(bool refresh = false)`

- [ ] **Step 1: Write the failing unit tests in `AiAssistantViewModelTests.cs`**

Add tests:
```csharp
[Fact]
public void StagedAttachments_CanAddAndRemove()
{
    var vm = new AiAssistantViewModel();
    Assert.False(vm.HasStagedAttachments);

    var att = new AiChatMessageAttachment
    {
        FileName = "screen.png",
        ContentType = "image/png",
        Base64Data = "abc"
    };

    vm.AddStagedAttachment(att);
    Assert.True(vm.HasStagedAttachments);
    Assert.Single(vm.StagedAttachments);

    vm.RemoveStagedAttachment(att.Id);
    Assert.False(vm.HasStagedAttachments);
    Assert.Empty(vm.StagedAttachments);
}

[Fact]
public void ChangingSelectedModelCapability_UpdatesSelectedModelString()
{
    var vm = new AiAssistantViewModel();
    var cap = new AiModelCapabilityInfo("openai/gpt-4o", "GPT-4o", "openai", SupportsVision: true);

    vm.SelectedModelCapability = cap;
    Assert.Equal("openai/gpt-4o", vm.SelectedModel);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~StagedAttachments"`
Expected: FAIL.

- [ ] **Step 3: Implement features in `AiAssistantViewModel.cs`**

1. Add observable properties:
   - `[ObservableProperty] private ObservableCollection<AiModelCapabilityInfo> _availableModelCapabilities = new();`
   - `[ObservableProperty] private AiModelCapabilityInfo? _selectedModelCapability;`
   - `[ObservableProperty] private ObservableCollection<AiChatMessageAttachment> _stagedAttachments = new();`
   - `public bool HasStagedAttachments => StagedAttachments.Count > 0;`
2. Sync `SelectedModelCapability` with `SelectedModel`:
   - Partial method `OnSelectedModelCapabilityChanged(AiModelCapabilityInfo? value)` setting `SelectedModel = value?.Id ?? "";`
   - When models are loaded via `TestConnectionAsync` or on init, populate `AvailableModelCapabilities` and select the current model.
3. Attachment helpers:
   - `public void AddStagedAttachment(AiChatMessageAttachment attachment)`
   - `[RelayCommand] public void RemoveStagedAttachment(string? attachmentId)`
   - `[RelayCommand] public void ClearStagedAttachments()`
4. In `SendMessageAsync`:
   - Copy `StagedAttachments` to `userMsg.Attachments`.
   - Clear `StagedAttachments`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~AiAssistantViewModelTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/ViewModels/AiAssistantViewModel.cs LocalLLMServerManager.Tests/AiAssistantViewModelTests.cs
git commit -m "feat(ui): add capability-aware model switching and attachment staging to AiAssistantViewModel"
```

---

### Task 6: UI Chat Composer & Model Picker (`AiAssistantTabControl.axaml`)

**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/Controls/AiAssistantTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/AiAssistantTabControl.axaml.cs`

**Interfaces:**
- Adds staged attachment preview chips with `✕` button.
- Adds 📎 attachment button triggering file dialog.
- Adds compact model selector ComboBox with capability badges in composer row.
- Adds thumbnail image rendering inside user message bubbles.

- [ ] **Step 1: Update `AiAssistantTabControl.axaml`**

1. Inside message bubble template:
   - If `HasAttachments`, render an `ItemsControl` of attached images (thumbnails) above or below user text.
2. Above composer input box:
   - Add staged attachments preview tray (`IsVisible="{Binding HasStagedAttachments}"`) with image thumbnail, file name, and remove button.
3. In composer row:
   - Add 📎 Attach button: `<Button Content="📎" Command="{Binding OpenAttachmentDialogCommand}" ... ToolTip.Tip="Attach image (PNG, JPG, WEBP)"/>`
   - Add Model Selector ComboBox:
     ```xml
     <ComboBox ItemsSource="{Binding AvailableModelCapabilities}"
               SelectedItem="{Binding SelectedModelCapability}"
               MinWidth="220"
               MaxDropDownHeight="300"
               VerticalAlignment="Center">
         <ComboBox.ItemTemplate>
             <DataTemplate DataType="models:AiModelCapabilityInfo">
                 <StackPanel Orientation="Horizontal" Spacing="6">
                     <TextBlock Text="{Binding SummaryBadge}" FontSize="10" Foreground="{StaticResource PrimaryBrush}"/>
                     <TextBlock Text="{Binding Id}" FontSize="11" Foreground="{StaticResource TextMainBrush}"/>
                 </StackPanel>
             </DataTemplate>
         </ComboBox.ItemTemplate>
     </ComboBox>
     ```
4. In `AiAssistantTabControl.axaml.cs`:
   - Implement file picker interaction using `TopLevel.GetTopLevel(this)?.StorageProvider` and route selected image to ViewModel.
   - Attach clipboard paste handler (`KeyDown` / `Paste`) to stage images copied from clipboard.

- [ ] **Step 2: Verify UI compilation & typecheck**

Run: `dotnet build LocalLLMServerManager.sln`
Run: `npm run lint`
Run: `npx tsc --noEmit`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add LocalLLMServerManager.Shared/Views/Controls/AiAssistantTabControl.axaml LocalLLMServerManager.Shared/Views/Controls/AiAssistantTabControl.axaml.cs
git commit -m "feat(ui): add composer model selector with capability badges and image attachment tray"
```

---

### Task 7: Live LLM E2E Tests & Comprehensive Verification (`AiAssistantLiveLlmTests.cs`)

**Files:**
- Modify: `LocalLLMServerManager.Tests/AiAssistantLiveLlmTests.cs`

**Interfaces:**
- Adds Live E2E tests:
  1. `LiveModelDiscovery_ExcludesLocal_AndLoadsCapabilitiesAsync`
  2. `LiveMultimodalChat_SendsImageAndReceivesResponseAsync`
  3. `LiveMultimodalToolCall_ExecutesSuccessfullyAsync`

- [ ] **Step 1: Add live E2E tests in `AiAssistantLiveLlmTests.cs`**

Add tests:
```csharp
[Fact]
public async Task LiveModelDiscovery_ExcludesLocal_AndLoadsCapabilitiesAsync()
{
    var endpoint = GetLiveEndpoint();
    if (string.IsNullOrWhiteSpace(endpoint) || !await IsEndpointReachableAsync(endpoint))
    {
        return; // Gracefully skip in headless CI without live endpoint
    }

    var service = CreateLiveAssistantService();
    var models = await service.GetModelCapabilitiesAsync(endpoint, GetLiveApiKey(), includeLocal: false);

    Assert.NotEmpty(models);
    Assert.DoesNotContain(models, m => m.Id.StartsWith("ollama/", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(models, m => m.IsLocal);

    // Verify at least one model has valid capabilities
    var primary = models[0];
    Assert.False(string.IsNullOrWhiteSpace(primary.Id));
    Assert.False(string.IsNullOrWhiteSpace(primary.Provider));
}

[Fact]
public async Task LiveMultimodalChat_SendsImageAndReceivesResponseAsync()
{
    var endpoint = GetLiveEndpoint();
    if (string.IsNullOrWhiteSpace(endpoint) || !await IsEndpointReachableAsync(endpoint))
    {
        return;
    }

    var service = CreateLiveAssistantService();
    var models = await service.GetModelCapabilitiesAsync(endpoint, GetLiveApiKey(), includeLocal: false);
    var visionModel = models.FirstOrDefault(m => m.SupportsVision)?.Id ?? "vertex_ai/gemini-2.5-flash";

    // 1x1 red PNG base64
    var samplePngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    var request = new AiChatRequest(
        Messages: new List<AiChatMessageItem>
        {
            new()
            {
                Role = "user",
                Content = "What color is this single pixel image? Respond in one word.",
                Attachments = new List<AiChatMessageAttachment>
                {
                    new()
                    {
                        FileName = "pixel.png",
                        ContentType = "image/png",
                        Base64Data = samplePngBase64
                    }
                }
            }
        },
        Model: visionModel
    );

    var response = await service.SendChatAsync(request);
    Assert.True(response.Success);
    Assert.NotNull(response.Message);
    Assert.Contains("red", response.Message.Content, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the full test suite**

Run: `dotnet test LocalLLMServerManager.sln`
Expected: All tests pass.

- [ ] **Step 3: Run linting and typechecking**

Run: `npm run lint`
Run: `npx tsc --noEmit`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add LocalLLMServerManager.Tests/AiAssistantLiveLlmTests.cs
git commit -m "test(e2e): add live LiteLLM model capability and multimodal chat E2E tests"
```

---

### Task 8: Documentation & User Rules Verification

**Files:**
- Modify: `docs/ai_assistant/ARCHITECTURE.md`
- Modify: `docs/ai_assistant/PROMPTS_AND_TOOLS.md`

- [ ] **Step 1: Update documentation files**

Document:
- `AiModelCapabilityInfo` schema and `/model/info` discovery flow.
- Local model exclusion criteria.
- Multimodal input handling in the composer bar.

- [ ] **Step 2: Run final verification commands**

Run:
1. `dotnet test LocalLLMServerManager.sln`
2. `npm run lint`
3. `npx tsc --noEmit`

- [ ] **Step 3: Commit**

```bash
git add docs/ai_assistant/
git commit -m "docs: update AI assistant documentation with model capabilities and multimodal support"
```
