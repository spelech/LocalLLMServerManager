# Local Model Scanning & Multi-Studio Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide dynamic local model scanning for diffusion checkpoints, LoRAs, video pipelines, audio models, and text LLMs, upgrade "Downloaded & Manage" into a comprehensive multi-engine manager, and enable real-time model selection in the Studio Creative Prompt Dock and Parameters Flyout.

**Architecture:** A new `LocalModelScannerService` discovers local model files across standard and configured directory paths. `MainViewModel` and `EngineStudioViewModel` bind scanned model collections to an interactive dropdown pill and parameters flyout. The "Downloaded & Manage" tab gains categorized sub-views (Ollama, Diffusion/Checkpoints, Video, Audio) with "Use in Studio" routing.

**Tech Stack:** .NET 10, Avalonia 12.1.2, CommunityToolkit.Mvvm 8.4.2, C# 13, XAML.

## Global Constraints
- Target framework: `net10.0`
- Maintain MVVM architecture and UI styling conventions from `DesignTokens.axaml` and `MatteTheme.axaml`.
- Run `npm run lint` and `npx tsc --noEmit` after code changes.
- Ensure all file operations are safe against missing directories or invalid paths.

---

### Task 1: Create `LocalModelScannerService` & Data Models

**Files:**
- Create: `LocalLLMServerManager.Shared/Models/LocalModelModels.cs`
- Create: `LocalLLMServerManager.Shared/Interfaces/ILocalModelScannerService.cs`
- Create: `LocalLLMServerManager.Shared/Services/LocalModelScannerService.cs`
- Test: `LocalLLMServerManager.Tests/LocalModelScannerServiceTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public record LocalModelItem(
      string Id,
      string Name,
      string FileName,
      string FullPath,
      string Modality,
      string Architecture,
      long SizeBytes,
      string FormattedSize,
      string SourceLocation,
      DateTime CreatedAt
  );

  public interface ILocalModelScannerService
  {
      Task<IReadOnlyList<LocalModelItem>> ScanAllModelsAsync(AppSettings? settings = null, string? baseDirectory = null);
      IReadOnlyList<LocalModelItem> GetCachedModels();
  }
  ```

- [ ] **Step 1: Write unit tests for `LocalModelScannerService`**
- [ ] **Step 2: Implement `LocalModelModels.cs`, `ILocalModelScannerService.cs`, and `LocalModelScannerService.cs`**
- [ ] **Step 3: Run tests to verify scanner correctly identifies files, computes sizes, and infers architectures**
- [ ] **Step 4: Commit Task 1**

---

### Task 2: Integrate Scanner into ViewModels (`MainViewModel` & `EngineStudioViewModel`)

**Files:**
- Modify: `LocalLLMServerManager.Shared/ViewModels/EngineStudioViewModel.cs`
- Modify: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Test: `LocalLLMServerManager.Tests/EngineStudioViewModelTests.cs`

**Interfaces:**
- Exposes:
  - `ObservableCollection<LocalModelItem> ScannedImageModels`
  - `ObservableCollection<LocalModelItem> ScannedVideoModels`
  - `ObservableCollection<LocalModelItem> ScannedAudioModels`
  - `ObservableCollection<string> AvailableCurrentModels`
  - `IRelayCommand<string> SelectModelCommand`
  - `IRelayCommand<LocalModelItem> UseModelInStudioCommand`
  - `IRelayCommand RefreshScannedModelsCommand`

- [ ] **Step 1: Write ViewModel tests for model selection, modality changes, and dynamic model lists**
- [ ] **Step 2: Implement dynamic collections, commands, and active model synchronization in ViewModels**
- [ ] **Step 3: Run tests to verify all properties update as expected**
- [ ] **Step 4: Commit Task 2**

---

### Task 3: Build Interactive Model Selector in Studio Creative Prompt Dock & Flyout

**Files:**
- Modify: `LocalLLMServerManager.Shared/Views/Controls/EngineStudioTabControl.axaml`

- [ ] **Step 1: Transform static prompt dock badge into an interactive dropdown button with Flyout**
- [ ] **Step 2: Add Model / Checkpoint ComboBox into the Parameters Flyout (`⚙️ Settings`)**
- [ ] **Step 3: Verify XAML layout, alignment, and theme styles**
- [ ] **Step 4: Commit Task 3**

---

### Task 4: Upgrade "Downloaded & Manage" in `ModelsTabControl.axaml` to Multi-Engine Hub

**Files:**
- Create: `LocalLLMServerManager.Shared/Views/Controls/LocalModelsManagerControl.axaml`
- Create: `LocalLLMServerManager.Shared/Views/Controls/LocalModelsManagerControl.axaml.cs`
- Modify: `LocalLLMServerManager.Shared/Views/Controls/ModelsTabControl.axaml`

- [ ] **Step 1: Create `LocalModelsManagerControl` showing categorized views for Ollama, Image Checkpoints, Video, and Audio with "Use in Studio", "Reveal", and "Delete" actions**
- [ ] **Step 2: Embed `LocalModelsManagerControl` inside `ModelsTabControl.axaml`**
- [ ] **Step 3: Verify XAML compiles cleanly and conforms to DesignTokens**
- [ ] **Step 4: Commit Task 4**

---

### Task 5: End-to-End Verification & Verification Suite

**Files:**
- Run test suites and linting.
- Verify zero compiler warnings/errors and clean test runs.
