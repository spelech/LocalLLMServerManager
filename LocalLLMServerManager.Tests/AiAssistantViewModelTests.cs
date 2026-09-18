using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AiAssistantViewModelTests
{
    [Fact]
    public void InitialState_HasWelcomeMessage_AndDefaultModel()
    {
        var vm = new AiAssistantViewModel();

        Assert.Single(vm.Messages);
        Assert.True(vm.Messages[0].IsAssistant);
        Assert.Contains("Copilot", vm.Messages[0].Content);
        Assert.False(string.IsNullOrWhiteSpace(vm.SelectedModel));
        Assert.NotEmpty(vm.SuggestionChips);
        Assert.False(vm.IsGenerating);
        Assert.False(vm.IsSetupCardVisible);
    }

    [Fact]
    public void ToggleSetupCard_InvertsVisibility()
    {
        var vm = new AiAssistantViewModel();
        Assert.False(vm.IsSetupCardVisible);

        vm.ToggleSetupCard();
        Assert.True(vm.IsSetupCardVisible);

        vm.ToggleSetupCard();
        Assert.False(vm.IsSetupCardVisible);
    }

    [Fact]
    public void ClearChat_ResetsMessagesToGreeting()
    {
        var vm = new AiAssistantViewModel();
        vm.Messages.Add(new AiChatMessageItem { Role = "user", Content = "test 1" });
        vm.Messages.Add(new AiChatMessageItem { Role = "assistant", Content = "test 2" });
        Assert.Equal(3, vm.Messages.Count);

        vm.ClearChat();
        Assert.Single(vm.Messages);
        Assert.True(vm.Messages[0].IsAssistant);
    }

    [Fact]
    public void SaveConfiguration_UpdatesSettingsService()
    {
        var mockSettings = new Mock<ISettingsService>();
        var settings = new AppSettings();
        mockSettings.Setup(s => s.LoadSettings()).Returns(settings);
        mockSettings.Setup(s => s.SaveSettings(It.IsAny<AppSettings>()));

        var vm = new AiAssistantViewModel(null, mockSettings.Object);
        vm.Endpoint = "http://my-litellm:4000/v1";
        vm.ApiKey = "sk-custom-key";
        vm.SelectedModel = "gemini-2.5-flash";
        vm.IsEnabled = true;

        vm.SaveConfiguration();

        mockSettings.Verify(s => s.SaveSettings(It.Is<AppSettings>(st =>
            st.AiAssistantEndpoint == "http://my-litellm:4000/v1" &&
            st.AiAssistantApiKey == "sk-custom-key" &&
            st.AiAssistantModel == "gemini-2.5-flash" &&
            st.AiAssistantEnabled == true)), Times.Once);

        Assert.False(vm.IsSetupCardVisible);
    }

    [Fact]
    public async Task TestConnectionAsync_WithSuccessfulValidation_UpdatesStatusAndModels()
    {
        var mockAssistant = new Mock<IAiAssistantService>();
        mockAssistant.Setup(a => a.ValidateConnectionAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiValidationResult(
                Success: true,
                Message: "Connected successfully (Latency: 15 ms, 2 models found)",
                AvailableModels: new List<string> { "model-a", "model-b" },
                LatencyMs: 15));

        var vm = new AiAssistantViewModel(mockAssistant.Object);
        vm.Endpoint = "http://127.0.0.1:4000/v1";
        vm.SelectedModel = "model-a";

        await vm.TestConnectionAsync();

        Assert.True(vm.IsConnectionSuccess);
        Assert.Contains("Connected successfully", vm.ConnectionStatusMessage);
        Assert.Equal(2, vm.AvailableModels.Count);
        Assert.Contains("model-a", vm.AvailableModels);
        Assert.Contains("model-b", vm.AvailableModels);
    }

    [Fact]
    public async Task TestConnectionAsync_WithFailedValidation_SetsFailureState()
    {
        var mockAssistant = new Mock<IAiAssistantService>();
        mockAssistant.Setup(a => a.ValidateConnectionAsync(
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiValidationResult(
                Success: false,
                Message: "Connection refused on port 4000",
                AvailableModels: new List<string>()));

        var vm = new AiAssistantViewModel(mockAssistant.Object);

        await vm.TestConnectionAsync();

        Assert.False(vm.IsConnectionSuccess);
        Assert.Contains("Connection refused", vm.ConnectionStatusMessage);
    }

    [Fact]
    public async Task SendMessageAsync_DirectService_StreamsTokensAndRecordsToolCalls()
    {
        var mockAssistant = new Mock<IAiAssistantService>();

        async IAsyncEnumerable<AiChatChunk> MockStream(AiChatRequest req)
        {
            await Task.Yield();
            yield return new AiChatChunk(ToolCall: new AiToolExecutionItem(ToolName: "GetVramTelemetry", Result: "VRAM: 8GB free", IsSuccess: true));
            yield return new AiChatChunk(DeltaText: "You have 8GB ");
            yield return new AiChatChunk(DeltaText: "of VRAM available.");
        }

        mockAssistant.Setup(a => a.StreamChatAsync(It.IsAny<AiChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns((AiChatRequest req, CancellationToken ct) => MockStream(req));

        var vm = new AiAssistantViewModel(mockAssistant.Object);
        vm.InputText = "How much VRAM is free?";

        await vm.SendMessageAsync();

        // Should have initial greeting, user message, and assistant response
        Assert.Equal(3, vm.Messages.Count);

        var userMsg = vm.Messages[1];
        Assert.True(userMsg.IsUser);
        Assert.Equal("How much VRAM is free?", userMsg.Content);

        var assistantMsg = vm.Messages[2];
        Assert.True(assistantMsg.IsAssistant);
        Assert.Equal("You have 8GB of VRAM available.", assistantMsg.Content);
        Assert.True(assistantMsg.HasToolCalls);
        Assert.Single(assistantMsg.ToolCalls);
        Assert.Equal("GetVramTelemetry", assistantMsg.ToolCalls[0].ToolName);
        Assert.False(assistantMsg.IsLoading);
        Assert.False(vm.IsGenerating);
    }

    [Fact]
    public async Task SelectSuggestionAsync_PopulatesInputAndSends()
    {
        var mockAssistant = new Mock<IAiAssistantService>();

        async IAsyncEnumerable<AiChatChunk> MockStream(AiChatRequest req)
        {
            await Task.Yield();
            yield return new AiChatChunk(DeltaText: "Telemetry OK");
        }

        mockAssistant.Setup(a => a.StreamChatAsync(It.IsAny<AiChatRequest>(), It.IsAny<CancellationToken>()))
            .Returns((AiChatRequest req, CancellationToken ct) => MockStream(req));

        var vm = new AiAssistantViewModel(mockAssistant.Object);

        await vm.SelectSuggestionAsync("⚡ Check live VRAM and GPU telemetry");

        Assert.Equal(3, vm.Messages.Count);
        Assert.Equal("⚡ Check live VRAM and GPU telemetry", vm.Messages[1].Content);
        Assert.Equal("Telemetry OK", vm.Messages[2].Content);
    }
}
