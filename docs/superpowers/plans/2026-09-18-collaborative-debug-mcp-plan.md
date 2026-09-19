# Collaborative UI Debugging with AvaloniaMcp Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable live, collaborative debugging between the developer and AI assistant using `AvaloniaMcp` (MCP server for Avalonia) and F12 DevTools in `Debug` builds.

**Architecture:** Integrate `AvaloniaMcp.Diagnostics` and `Avalonia.Diagnostics` conditionally for Debug builds. Wire a dedicated `UiDiagnosticLogger` to capture Avalonia binding errors and trace logs. Add user-facing documentation in VitePress and automated unit tests.

**Tech Stack:** C# 13, .NET 10, Avalonia 12.1.2, AvaloniaMcp.Diagnostics 0.4.0, Avalonia.Diagnostics 11.3.22, xUnit, VitePress.

## Global Constraints
- Do NOT create releases, git tags, or bump version numbers.
- Do NOT use the acronym "ASD-STE100" in any documentation, code, or UI copy.
- Always run `npm run lint` and `npx tsc --noEmit` after code changes.
- Ensure all packages are conditionally included only in `Debug` configuration so `Release` builds remain unaffected.

---

### Task 1: Package References & Startup Initialization

**Files:**
- Modify: `LocalLLMServerManager.csproj`
- Modify: `Program.cs`
- Modify: `Views/MainWindow.axaml.cs`

**Interfaces:**
- Produces: Application startup in `Debug` mode launches AvaloniaMcp named pipe server (`UseMcpDiagnostics()`) and enables F12 DevTools (`this.AttachDevTools()`).

- [ ] **Step 1: Update `LocalLLMServerManager.csproj`**
Add conditional PackageReferences for `AvaloniaMcp.Diagnostics` (0.4.0) and `Avalonia.Diagnostics` (11.3.22) when `'$(Configuration)' == 'Debug'`.

- [ ] **Step 2: Update `Program.cs`**
Add `#if DEBUG using AvaloniaMcp.Diagnostics; #endif` and wire `.UseMcpDiagnostics()` into `BuildAvaloniaApp()` under `#if DEBUG`.

- [ ] **Step 3: Update `Views/MainWindow.axaml.cs`**
Add `#if DEBUG this.AttachDevTools(); #endif` in `MainWindow` constructor.

- [ ] **Step 4: Verify build in both Debug and Release**
Run `dotnet build -c Debug` and `dotnet build -c Release` to confirm successful compilation.

- [ ] **Step 5: Commit**
`git commit -m "feat(debug): integrate AvaloniaMcp diagnostics and F12 DevTools for debug builds"`

---

### Task 2: UI Diagnostic Trace Interceptor & Unit Tests

**Files:**
- Create: `LocalLLMServerManager.Shared/Services/UiDiagnosticLogger.cs`
- Create: `LocalLLMServerManager.Tests/UiDiagnosticLoggerTests.cs`

**Interfaces:**
- Produces: `IUiDiagnosticLogger` / `UiDiagnosticLogger` with methods:
  - `void RecordBindingError(string message, string? source = null)`
  - `IReadOnlyList<BindingDiagnosticEntry> GetRecentErrors()`
  - `void Clear()`
  - `string ExportLogSummary()`

- [ ] **Step 1: Write failing test in `UiDiagnosticLoggerTests.cs`**
Test that `UiDiagnosticLogger` records binding errors, limits max history to prevent memory leaks, and formats timestamps accurately.

- [ ] **Step 2: Run test to verify it fails**
Run `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~UiDiagnosticLoggerTests"`.

- [ ] **Step 3: Implement `UiDiagnosticLogger.cs`**
Implement thread-safe diagnostic logger.

- [ ] **Step 4: Run test to verify it passes**
Run `dotnet test LocalLLMServerManager.Tests --filter "FullyQualifiedName~UiDiagnosticLoggerTests"`.

- [ ] **Step 5: Commit**
`git commit -m "feat(diagnostics): add UiDiagnosticLogger and unit test coverage"`

---

### Task 3: Documentation for Collaborative Debugging

**Files:**
- Create: `docs/guide/collaborative-debugging.md`
- Modify: `docs/.vitepress/config.ts`

**Interfaces:**
- Produces: Clear developer guide explaining:
  - How to start the app in Debug mode.
  - How `avaloniamcp` runs and connects via named pipes.
  - The 15 available MCP tools for AI assistants.
  - How human developers use F12 DevTools.
  - Best practices for reporting and fixing UI issues.

- [ ] **Step 1: Create `docs/guide/collaborative-debugging.md`**
Write clear, accessible guide without any "ASD-STE100" terminology.

- [ ] **Step 2: Update `docs/.vitepress/config.ts` sidebar**
Add link under Developer Guide / Advanced Features.

- [ ] **Step 3: Build docs site**
Run `npm run docs:build` to confirm 0 errors.

- [ ] **Step 4: Commit**
`git commit -m "docs: add comprehensive collaborative debugging guide for AvaloniaMcp"`

---

### Task 4: Global Tool Verification, Quality Gates & PR

**Files:**
- All modified files

- [ ] **Step 1: Install `avaloniamcp` global tool**
Run `dotnet tool install -g avaloniamcp` (or update if already installed).

- [ ] **Step 2: Run full verification suite**
Run `dotnet test -c Release --nologo`.
Run `npm run lint`.
Run `npx tsc --noEmit`.
Run `npm run docs:build`.

- [ ] **Step 3: Push branch and create Pull Request**
Push feature branch and open PR via `gh pr create`.
