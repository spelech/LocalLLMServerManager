# Task 5: Ambient Compatibility Badges on Search & Library Cards

## Description
Integrate `QuickFitBadge` calculations into `HuggingFaceSearchViewModel.cs`, `CivitaiSearchViewModel.cs`, and `OllamaLibraryViewModel.cs`. Update search/library card XAML views (`HuggingFaceSearchView.axaml`, `CivitaiSearchView.axaml`, `OllamaLibraryView.axaml`) to display ambient compatibility pills with click-to-inspect navigation into the `[⚡ Can I Run It]` tab.

## Requirements
1. **Search & Library Item Models & ViewModels**:
   - In `HuggingFaceModelItem` (or in `HuggingFaceSearchViewModel` item mapping):
     - Compute `QuickFitBadge FitBadge { get; }` based on model name, tags, pipeline_tag, and file size against live telemetry VRAM/RAM.
   - In `CivitaiModelItem` (or in `CivitaiSearchViewModel` item mapping):
     - Compute `QuickFitBadge FitBadge { get; }` based on checkpoint file size against live VRAM.
   - In `OllamaModelInfo` (or in `OllamaLibraryViewModel` item mapping):
     - Compute `QuickFitBadge FitBadge { get; }` based on model name and size on disk.
2. **Card XAML Layouts**:
   - In `HuggingFaceSearchView.axaml`:
     - Render `FitBadge` pill next to model name / tags with background `#1E293B`, border, and color `#22C55E` (Full VRAM), `#F59E0B` (Partial), `#EF4444` (OOM).
     - Provide a small "⚡ Check Fit" or click handler on the badge that calls `NavigateToCanIRunItCommand` with the model name and modality.
   - In `CivitaiSearchView.axaml`:
     - Render `FitBadge` pill in card footer / header.
     - Clickable to inspect in CanIRunIt tab.
   - In `OllamaLibraryView.axaml`:
     - Render `FitBadge` pill for installed models.
     - Clickable to inspect in CanIRunIt tab.
3. **Cross-ViewModel Navigation**:
   - Provide an `Action<string, string>? OnInspectModelRequested` callback or `MainViewModel` delegate so clicking a badge invokes `MainViewModel.NavigateToCanIRunIt(modelName, modality)`.
4. **Unit & Integration Tests**:
   - In `HuggingFaceSearchViewModelTests` / `CivitaiSearchViewModelTests` / `OllamaLibraryViewModelTests` (or new test file `CardFitBadgeTests.cs`):
     - Verify search items receive valid `FitBadge` instances.
     - Verify click command properly triggers navigation callback.

## Constraints & TDD
- Write failing unit tests in `LocalLLMServerManager.Tests/CardFitBadgeTests.cs`.
- Implement changes in ViewModels and XAML views.
- Run `dotnet test`.
- Run `npm run lint` and `npx tsc --noEmit`.
- Commit: `git commit -m "feat(ui): add ambient Can I Run It compatibility badges across discovery and library cards"`
- Write report to `.superpowers/sdd/2026-08-30-can-i-run-it\task-5-report.md`.
