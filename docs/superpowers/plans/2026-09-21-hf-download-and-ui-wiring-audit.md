# Hugging Face Download & Full UI Wiring Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement full Hugging Face and CivitAI web launch links, wire up the Hugging Face quantization and weights modal in Avalonia XAML, implement direct file downloads to disk with target directory routing, connect Ollama GGUF pulling, add the missing model pull progress drawer to `MainView.axaml`, fix the Playwright WASM test, and ensure every tested feature is fully hooked up in the UI.

**Architecture:** 
- Expose MVVM RelayCommands for browser launching (`OpenInBrowserCommand`) across Hugging Face and CivitAI ViewModels, utilizing `BrowserLauncher.OpenUrl()`.
- Expose `OpenHfModalCommand` on `HuggingFaceSearchViewModel` and bind it to a `📂 Files & Downloads` button on each repo card in `HuggingFaceTabControl.axaml`.
- Expand `HuggingFaceSearchService` to inspect all model weight formats (`.gguf`, `.safetensors`, `.pt`, `.bin`, `.onnx`).
- Add `DownloadHfFileCommand` calling `/api/hf/download` to stream files directly into `DownloadManager.ResolveTargetDirectory()`, plus `PullHfGgufInOllamaCommand` wired to `MainViewModel.PullModelAsync`.
- Implement the slide-out Pull Progress Drawer in `MainView.axaml` bound to `Ollama.IsPullDrawerOpen`.
- Fix the Tab coordinate selection in `PlaywrightWasmE2ETests.cs` to accurately interact with Can I Run It (Tab 3).

**Tech Stack:** C# 13, .NET 10, Avalonia UI 11.2, CommunityToolkit.Mvvm, xUnit, Playwright, ASP.NET Core Minimal APIs.

## Global Constraints

- Always run linting and typechecking after making code changes: `npm run lint` and `npx tsc --noEmit`.
- Run `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj` and ensure all tests pass.
- Maintain documentation integrity and create clickable markdown links for all code symbols and files.

---

### Task 1: Hugging Face & CivitAI Web Launch Links (`BrowserLauncher`)

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/HuggingFaceSearchViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/CivitaiSearchViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/CivitaiTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/HuggingFaceSearchViewModelTests.cs`
- Test: `LocalLLMServerManager.Tests/CivitaiSearchViewModelTests.cs`

- [ ] **Step 1: Write failing unit tests for browser launch commands**
Add tests to `HuggingFaceSearchViewModelTests.cs` and `CivitaiSearchViewModelTests.cs` testing `OpenInBrowserCommand`:
```csharp
[Fact]
public void OpenInBrowser_LaunchesHuggingFaceUrl()
{
    var mockHf = new Mock<IHuggingFaceSearchService>();
    var vm = new HuggingFaceSearchViewModel(mockHf.Object);
    BrowserLauncher.SuppressProcessStart = true;
    vm.OpenInBrowser("meta-llama/Llama-3.3-8B-Instruct-GGUF");
}

[Fact]
public void OpenInBrowser_LaunchesCivitaiUrl()
{
    var mockCiv = new Mock<ICivitaiSearchService>();
    var vm = new CivitaiSearchViewModel(mockCiv.Object);
    BrowserLauncher.SuppressProcessStart = true;
    var item = new CivitaiModelItem(1234, "Model Name", "Checkpoint", "", "http://download", "model.safetensors", 4.9, 100);
    vm.OpenInBrowser(item);
}
```

- [ ] **Step 2: Run tests to verify they fail**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~OpenInBrowser"`
Expected: FAIL (method does not exist)

- [ ] **Step 3: Implement `OpenInBrowser` on `HuggingFaceSearchViewModel` and `CivitaiSearchViewModel`**
Add RelayCommands:
```csharp
[RelayCommand]
public void OpenInBrowser(string? repoId)
{
    if (string.IsNullOrWhiteSpace(repoId)) return;
    var safeId = repoId.Trim();
    BrowserLauncher.OpenUrl($"https://huggingface.co/{safeId}");
}
```
And on `CivitaiSearchViewModel`:
```csharp
[RelayCommand]
public void OpenInBrowser(CivitaiModelItem? item)
{
    if (item == null || item.Id <= 0) return;
    BrowserLauncher.OpenUrl($"https://civitai.com/models/{item.Id}");
}
```

- [ ] **Step 4: Add `🌐 View on Hub` buttons to XAML views**
In `HuggingFaceTabControl.axaml`, add a `🌐 View on Hub` button on each card and inside the modal header.
In `CivitaiTabControl.axaml`, add a `🌐 View on Hub` button on each card and featured starter item.

- [ ] **Step 5: Run tests to verify they pass**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~OpenInBrowser"`
Expected: PASS

---

### Task 2: Hugging Face Files & Quantizations Modal UI Wiring & Multi-format Inspection

**Files:**
- Modify: `LocalLLMServerManager.Shared/Services/HuggingFaceSearchService.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/HuggingFaceSearchViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml`
- Test: `LocalLLMServerManager.Tests/SearchServicesTests.cs`
- Test: `LocalLLMServerManager.Tests/HuggingFaceSearchViewModelTests.cs`

- [ ] **Step 1: Write failing unit test for `OpenHfModalCommand` with `HuggingFaceRepoItem` and multi-format files**
Add tests in `HuggingFaceSearchViewModelTests.cs` and `SearchServicesTests.cs`:
```csharp
[Fact]
public async Task OpenHfModalCommand_OpensModalForRepoItem()
{
    var mockHf = new Mock<IHuggingFaceSearchService>();
    mockHf.Setup(m => m.FetchQuantizationsAsync(It.IsAny<string>(), "test/repo", It.IsAny<HttpClient>()))
        .ReturnsAsync(new List<HfFileQuantItem> { new("model.gguf", "Q4_K_M", "4 GB", 4000000000L) });

    var vm = new HuggingFaceSearchViewModel(mockHf.Object);
    var item = new HuggingFaceRepoItem("test/repo", "test", 100, "1k downloads", "text-generation");
    await vm.OpenHfModalCommand.ExecuteAsync(item);

    Assert.True(vm.IsHfModalOpen);
    Assert.Equal("test/repo", vm.ModalRepoId);
    Assert.Single(vm.ModalHfFiles);
}
```

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~OpenHfModalCommand_OpensModalForRepoItem"`
Expected: FAIL

- [ ] **Step 3: Implement multi-format file parsing in `HuggingFaceSearchService.cs` and RelayCommand in `HuggingFaceSearchViewModel.cs`**
In `HuggingFaceSearchService.cs`:
Inspect `.gguf`, `.safetensors`, `.pt`, `.bin`, `.onnx` files.
In `HuggingFaceSearchViewModel.cs`:
```csharp
[RelayCommand]
public async Task OpenHfModalAsync(HuggingFaceRepoItem? item)
{
    if (item == null || string.IsNullOrWhiteSpace(item.Id)) return;
    await OpenHfModalAsync(item.Id, ApiBase, HttpHelper.CreateClient(ApiBase));
}
```

- [ ] **Step 4: Add `📂 Files & Downloads` button to `HuggingFaceTabControl.axaml`**
Add the button in `HuggingFaceTabControl.axaml` inside the card's actions StackPanel:
```xml
<Button Command="{Binding $parent[UserControl].((vm:HuggingFaceSearchViewModel)DataContext).OpenHfModalCommand}"
        CommandParameter="{Binding}"
        Classes="matte-primary"
        Padding="10,4"
        FontSize="11"
        ToolTip.Tip="Inspect downloadable files and quantizations">
    <StackPanel Orientation="Horizontal" Spacing="4">
        <TextBlock Text="📂 Files &amp; Downloads"/>
    </StackPanel>
</Button>
```

- [ ] **Step 5: Run tests to verify they pass**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~OpenHfModalCommand"`
Expected: PASS

---

### Task 3: Hugging Face Direct Download Implementation

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/HuggingFaceSearchViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/HuggingFaceTabControl.axaml`
- Modify: `LocalLLMServerManager.Tests/ServerEndpointsTests.cs`
- Test: `LocalLLMServerManager.Tests/HuggingFaceSearchViewModelTests.cs`

- [ ] **Step 1: Write failing unit tests for `DownloadHfFileCommand` and `PullHfGgufInOllamaCommand`**
```csharp
[Fact]
public async Task DownloadHfFileAsync_SendsDownloadRequest()
{
    var mockHf = new Mock<IHuggingFaceSearchService>();
    var vm = new HuggingFaceSearchViewModel(mockHf.Object)
    {
        ModalRepoId = "meta-llama/Llama-3.3-8B-Instruct-GGUF"
    };

    var file = new HfFileQuantItem("llama-3.3.Q4_K_M.gguf", "Q4_K_M", "4.5 GB", 4500000000L);
    var handlerMock = new Mock<HttpMessageHandler>();
    handlerMock.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

    var client = new HttpClient(handlerMock.Object);
    await vm.DownloadHfFileAsync(file, "http://localhost:5246", client);

    handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("/api/hf/download")), ItExpr.IsAny<CancellationToken>());
}
```

- [ ] **Step 2: Run test to verify failure**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~DownloadHfFileAsync"`
Expected: FAIL

- [ ] **Step 3: Implement `DownloadHfFileAsync` and `PullHfGgufInOllama` on `HuggingFaceSearchViewModel`**
Implement the commands, invoking `/api/hf/download` and raising Toasts.
Wire `OnPullModelRequested` callback to `MainViewModel.PullModelAsync`.

- [ ] **Step 4: Update `HuggingFaceTabControl.axaml` with Download & Pull buttons**
In the quant modal file list, add:
- `⬇️ Download` button.
- `🦙 Pull in Ollama` button for GGUFs.

- [ ] **Step 5: Run tests to verify they pass**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~DownloadHfFileAsync"`
Expected: PASS

---

### Task 4: Model Pull Progress Slide-Out Drawer in Avalonia XAML

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/Views/MainView.axaml`
- Test: `LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs`

- [ ] **Step 1: Write failing headless interaction test for pull drawer visibility**
```csharp
[AvaloniaFact]
public void MainView_PullProgressDrawer_RendersWhenOpen()
{
    var vm = new MainViewModel();
    var view = new MainView { DataContext = vm };
    var window = new Window { Content = view, Width = 1024, Height = 768 };
    window.Show();

    vm.Ollama.PullModelName = "test-model:latest";
    vm.Ollama.PullProgressPercent = 45.0;
    vm.Ollama.IsPullDrawerOpen = true;

    var drawer = view.FindControl<Border>("PullProgressDrawer");
    Assert.NotNull(drawer);
    Assert.True(drawer.IsVisible);

    window.Close();
}
```

- [ ] **Step 2: Run test to verify failure**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~MainView_PullProgressDrawer_RendersWhenOpen"`
Expected: FAIL (drawer control not found)

- [ ] **Step 3: Implement Pull Progress Drawer in `MainView.axaml` and `MainViewModel.cs`**
In `MainViewModel.cs`, include `Ollama.IsPullDrawerOpen` in `IsAnyDrawerOpen` logic.
In `MainView.axaml`, add the `PullProgressDrawer` Border with ProgressBar, status log, and close button.

- [ ] **Step 4: Run test to verify it passes**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~MainView_PullProgressDrawer_RendersWhenOpen"`
Expected: PASS

---

### Task 5: Audit & Fix Playwright WASM Tests and All Test Suites

**Files:**
- Modify: `LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs`

- [ ] **Step 1: Update `PlaywrightWasmE2ETests.cs` tab navigation coordinates**
Update the coordinate click to select Tab 3 (Can I Run It) cleanly without triggering off-screen clicks or 400 Bad Request console errors.
- [ ] **Step 2: Run `PlaywrightWasmE2ETests`**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightWasmE2ETests"`
Expected: PASS

---

### Task 6: Comprehensive Verification, Commit, and PR Push

- [ ] **Step 1: Run all unit and integration tests**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`
Expected: 100% tests pass (745+ passing, 0 failures)

- [ ] **Step 2: Run user-defined linting and typechecking**
Run: `npm run lint` and `npx tsc --noEmit`
Expected: 0 errors

- [ ] **Step 3: Commit all changes**
```bash
git add .
git commit -m "feat(hub): add HF and CivitAI web links, direct file download, and in-app pull drawer"
```

- [ ] **Step 4: Push feature branch and create Pull Request**
```bash
git push -u origin feat/hf-downloads-and-ui-wiring-audit
gh pr create --title "feat(hub): add HF and CivitAI web links, direct file download, and in-app pull drawer" --body "..."
```
