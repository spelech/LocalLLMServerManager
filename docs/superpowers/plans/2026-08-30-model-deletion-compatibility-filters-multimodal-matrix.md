# Model Deletion, Hardware Compatibility Filters, and Multimodal Matrix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement model deletion across Ollama and local disk directories, add "Can I Run It" compatibility verdict filter chips across search tabs, and implement a dual multi-select input/output multimodal search matrix.

**Architecture:** 
- Backend `DELETE /api/models/delete` handles safe deletion of Ollama models via Ollama API and local models across ComfyUI, Forge, Audio, and 3D paths.
- ViewModels (`OllamaLibraryViewModel`, `HuggingFaceSearchViewModel`, `CivitaiSearchViewModel`) maintain reactive `FitVerdict` filter chips (`🟢 Full VRAM`, `🟡 Partial Offload`, `🟠 CPU Only`, `🔴 Won't Fit`).
- `HuggingFaceSearchViewModel` manages dual multi-select Input (`Text`, `Image`, `Audio`, `Video`) and Output (`Text`, `Image`, `Audio`, `Video`, `3D`) modality arrays and a `🌟 Multimodal / VLM` quick preset, querying Hugging Face Hub with resolved pipeline tags.

**Tech Stack:** C# .NET 9, ASP.NET Core Minimal APIs, CommunityToolkit.Mvvm, Avalonia UI, TypeScript / ESLint.

## Global Constraints

- Always run linting and typechecking after code changes (`npm run lint` and `npx tsc --noEmit`).
- All unit and integration tests must pass via `dotnet test`.
- Path traversal protection must always be enforced using `Program.IsSafePath`.

---

### Task 1: Backend Model Deletion Endpoint & Ollama Service

**Files:**
- Modify: `Endpoints/ModelProxyEndpoints.cs`
- Modify: `LocalLLMServerManager.Shared/Interfaces/IOllamaModelService.cs`
- Modify: `LocalLLMServerManager.Shared/Services/OllamaModelService.cs`
- Test: `LocalLLMServerManager.Tests/ServerEndpointsTests.cs`

**Interfaces:**
- Produces: `DELETE /api/models/delete` endpoint accepting `DeleteModelRequest(string Target, string? Type = null)`
- Produces: `IOllamaModelService.DeleteModelAsync(string apiBase, string modelName, HttpClient http)`
- Produces: `IOllamaModelService.DeleteLocalModelFileAsync(string apiBase, string filePath, HttpClient http)`

- [ ] **Step 1: Write the failing tests for model deletion**

```csharp
[Fact]
public async Task DeleteModelEndpoint_OllamaTarget_CallsOllamaOrReturnsSuccess()
{
    using var server = new AppTestServerFixture();
    var client = server.CreateClient();
    var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/models/delete")
    {
        Content = new StringContent("{\"target\":\"llama3.2:latest\",\"type\":\"ollama\"}", Encoding.UTF8, "application/json")
    });
    Assert.True(response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadGateway);
}

[Fact]
public async Task DeleteModelEndpoint_UnsafePath_ReturnsBadRequest()
{
    using var server = new AppTestServerFixture();
    var client = server.CreateClient();
    var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/models/delete")
    {
        Content = new StringContent("{\"target\":\"../../windows/system32/cmd.exe\",\"type\":\"file\"}", Encoding.UTF8, "application/json")
    });
    Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~DeleteModelEndpoint"`
Expected: FAIL (endpoint not mapped).

- [ ] **Step 3: Implement `DELETE /api/models/delete` and `OllamaModelService` methods**

Implement `DELETE /api/models/delete` in `Endpoints/ModelProxyEndpoints.cs` and deletion methods in `OllamaModelService.cs`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~DeleteModelEndpoint"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Endpoints/ModelProxyEndpoints.cs LocalLLMServerManager.Shared/Interfaces/IOllamaModelService.cs LocalLLMServerManager.Shared/Services/OllamaModelService.cs LocalLLMServerManager.Tests/ServerEndpointsTests.cs
git commit -m "feat(backend): implement model deletion endpoint and OllamaModelService delete methods"
```

---

### Task 2: "Can I Run It" Compatibility Verdict Filter Chips & Deletion in Ollama Library

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/OllamaLibraryViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/OllamaModelsTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/CanIRunItViewModelTests.cs`

**Interfaces:**
- Produces: `OllamaLibraryViewModel.DeleteModelCommand(OllamaModelItem? item)`
- Produces: `OllamaLibraryViewModel.FilteredInstalledModels`
- Produces: `OllamaLibraryViewModel.IsFullVramActive`, `IsPartialOffloadActive`, `IsCpuOnlyActive`, `IsOomActive`
- Produces: `OllamaLibraryViewModel.ToggleFitVerdictCommand(string verdict)`

- [ ] **Step 1: Write the failing tests for OllamaLibraryViewModel deletion and compatibility filter**

```csharp
[Fact]
public async Task OllamaLibraryViewModel_DeleteModel_RemovesModelFromCollection()
{
    var mockOllama = new Mock<IOllamaModelService>();
    mockOllama.Setup(m => m.DeleteModelAsync(It.IsAny<string>(), "llama3.2:latest", It.IsAny<HttpClient>()))
        .ReturnsAsync(true);
    var vm = new OllamaLibraryViewModel(mockOllama.Object);
    vm.InstalledModels.Add(new OllamaModelItem("llama3.2:latest", "2.0 GB", "Coding", "#38BDF8", false));
    await vm.DeleteModelAsync(vm.InstalledModels[0]);
    Assert.Empty(vm.InstalledModels);
}

[Fact]
public void OllamaLibraryViewModel_FilterByFitVerdict_FiltersModelsCorrectly()
{
    var vm = new OllamaLibraryViewModel(new Mock<IOllamaModelService>().Object);
    vm.InstalledModels.Add(new OllamaModelItem("model1", "2.0 GB", "Coding", "#38BDF8", false, new QuickFitBadge("🟢 Full VRAM", "#10B981", "", FitVerdict.FullVram)));
    vm.InstalledModels.Add(new OllamaModelItem("model2", "70.0 GB", "Coding", "#38BDF8", false, new QuickFitBadge("🔴 Won't Fit (OOM)", "#EF4444", "", FitVerdict.OutOfMemory)));
    
    vm.IsOomActive = false;
    vm.ApplyFilter();
    Assert.Single(vm.FilteredInstalledModels);
    Assert.Equal("model1", vm.FilteredInstalledModels[0].Name);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~OllamaLibraryViewModel"`
Expected: FAIL.

- [ ] **Step 3: Implement delete command, filter properties, and UI in `OllamaModelsTabControl.axaml`**

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~OllamaLibraryViewModel"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/ViewModels/OllamaLibraryViewModel.cs LocalLLMServerManager.Shared/Views/Controls/OllamaModelsTabControl.axaml LocalLLMServerManager.Tests/CanIRunItViewModelTests.cs
git commit -m "feat(ollama): add delete model command, compatibility verdict filter chips, and UI controls"
```

---

### Task 3: Multimodal & Multi-Input/Output Search Matrix in Hugging Face

**Files:**
- Modify: `LocalLLMServerManager.Shared/Interfaces/IHuggingFaceSearchService.cs`
- Modify: `LocalLLMServerManager.Shared/Services/HuggingFaceSearchService.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/HuggingFaceSearchViewModel.cs`
- Modify: `Endpoints/ModelProxyEndpoints.cs`
- Test: `LocalLLMServerManager.Tests/SearchServicesTests.cs`

**Interfaces:**
- Produces: `HuggingFaceSearchViewModel.SelectedInputModalities` (ObservableCollection<string>)
- Produces: `HuggingFaceSearchViewModel.SelectedOutputModalities` (ObservableCollection<string>)
- Produces: `HuggingFaceSearchViewModel.ToggleInputModalityCommand(string modality)`
- Produces: `HuggingFaceSearchViewModel.ToggleOutputModalityCommand(string modality)`
- Produces: `HuggingFaceSearchViewModel.ApplyPresetCommand(string preset)`
- Produces: `HuggingFaceSearchViewModel.FilteredHuggingFaceResults`
- Produces: `HuggingFaceSearchViewModel.IsFullVramActive`, `IsPartialOffloadActive`, `IsCpuOnlyActive`, `IsOomActive`

- [ ] **Step 1: Write failing tests for Multimodal Input/Output Matrix and Verdict Filtering**

```csharp
[Fact]
public void HuggingFaceSearchViewModel_MultimodalPreset_SetsTextAndImageInputTextOutput()
{
    var vm = new HuggingFaceSearchViewModel(new Mock<IHuggingFaceSearchService>().Object);
    vm.ApplyPreset("Multimodal");
    Assert.Contains("Text", vm.SelectedInputModalities);
    Assert.Contains("Image", vm.SelectedInputModalities);
    Assert.Contains("Text", vm.SelectedOutputModalities);
}

[Fact]
public void HuggingFaceSearchViewModel_ResolvesPipelineTags_ForVisionLanguageModels()
{
    var tags = HuggingFaceSearchViewModel.ResolvePipelineTags(new[] { "Text", "Image" }, new[] { "Text" });
    Assert.Contains("image-text-to-text", tags);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~HuggingFaceSearchViewModel"`
Expected: FAIL.

- [ ] **Step 3: Implement Multimodal Input/Output matrix logic and reactive filtering**

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~HuggingFaceSearchViewModel"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/Interfaces/IHuggingFaceSearchService.cs LocalLLMServerManager.Shared/Services/HuggingFaceSearchService.cs LocalLLMServerManager.Shared/ViewModels/HuggingFaceSearchViewModel.cs Endpoints/ModelProxyEndpoints.cs LocalLLMServerManager.Tests/SearchServicesTests.cs
git commit -m "feat(hf): implement multimodal input/output search matrix and reactive compatibility filtering"
```

---

### Task 4: Hardware Compatibility Filter Chips in CivitAI Search

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/CivitaiSearchViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/CivitaiTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/SearchServicesTests.cs`

**Interfaces:**
- Produces: `CivitaiSearchViewModel.FilteredCivitaiResults`
- Produces: `CivitaiSearchViewModel.IsFullVramActive`, `IsPartialOffloadActive`, `IsCpuOnlyActive`, `IsOomActive`
- Produces: `CivitaiSearchViewModel.ToggleFitVerdictCommand(string verdict)`

- [ ] **Step 1: Write failing tests for CivitaiSearchViewModel compatibility filter**

```csharp
[Fact]
public void CivitaiSearchViewModel_FilterByFitVerdict_FiltersResultsCorrectly()
{
    var vm = new CivitaiSearchViewModel(new Mock<ICivitaiSearchService>().Object);
    vm.CivitaiResults.Add(new CivitaiModelItem(1, "Flux Checkpoint", "Checkpoint", "", "", "flux.safetensors", 5.0, 100, new QuickFitBadge("🟢 Full VRAM", "#10B981", "", FitVerdict.FullVram)));
    vm.CivitaiResults.Add(new CivitaiModelItem(2, "Giant Checkpoint", "Checkpoint", "", "", "giant.safetensors", 5.0, 100, new QuickFitBadge("🔴 Won't Fit (OOM)", "#EF4444", "", FitVerdict.OutOfMemory)));
    
    vm.IsOomActive = false;
    vm.ApplyFilter();
    Assert.Single(vm.FilteredCivitaiResults);
    Assert.Equal(1, vm.FilteredCivitaiResults[0].Id);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~CivitaiSearchViewModel"`
Expected: FAIL.

- [ ] **Step 3: Implement compatibility filtering and update `CivitaiTabControl.axaml`**

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~CivitaiSearchViewModel"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add LocalLLMServerManager.Shared/ViewModels/CivitaiSearchViewModel.cs LocalLLMServerManager.Shared/Views/Controls/CivitaiTabControl.axaml LocalLLMServerManager.Tests/SearchServicesTests.cs
git commit -m "feat(civitai): add compatibility verdict filter chips to CivitAI tab"
```

---

### Task 5: Avalonia UI Integration for Hugging Face Tab Control

**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs`

- [ ] **Step 1: Add Input/Output matrix chips, Multimodal preset pill, and FitVerdict chips in `HuggingFaceTabControl.axaml`**
- [ ] **Step 2: Bind items to `FilteredHuggingFaceResults`**
- [ ] **Step 3: Run UI tests and verify compilation**
- [ ] **Step 4: Commit**

```bash
git add LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs
git commit -m "feat(ui): add multimodal input/output matrix and compatibility filter chips to HuggingFaceTabControl"
```

---

### Task 6: Full Verification, Linting, Typecheck, and PR Preparation

**Files:**
- Modify: Any relevant tests or documentation

- [ ] **Step 1: Run `npm run lint` and `npx tsc --noEmit`**
- [ ] **Step 2: Run all .NET tests with `dotnet test`**
- [ ] **Step 3: Commit any final test updates**
- [ ] **Step 4: Push branch and create PR**
