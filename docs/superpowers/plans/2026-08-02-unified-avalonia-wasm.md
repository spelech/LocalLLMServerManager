# Unified Avalonia UI Across Desktop & WebAssembly (v3.0.0) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a unified Avalonia UI in a shared class library (`LocalLLMServerManager.Shared`) that targets both Windows Desktop and WebAssembly (`Avalonia.Browser` compiled to `wwwroot/`), bump version to `3.0.0`, and update all documentation.

**Architecture:** Split the codebase into `LocalLLMServerManager.Shared` (100% shared XAML views and ViewModels), `LocalLLMServerManager.Web` (`Avalonia.Browser` compiled to WASM inside `wwwroot/`), and `LocalLLMServerManager` (ASP.NET Core WebHost + System Tray + Win32 Host).

**Tech Stack:** C# .NET 10.0, Avalonia UI 11.2.3, Avalonia.Browser 11.2.3, CommunityToolkit.Mvvm 8.4.0, ASP.NET Core Minimal APIs, YARP Reverse Proxy, Inno Setup 6.

## Global Constraints
- Target Framework: `net10.0`
- Application Version: `3.0.0`
- Desktop OutputType: `<OutputType>WinExe</OutputType>`
- Minimal API / MCP routes: `/health`, `/api/gpu/vram`, `/api/models`, `/api/mcp/tools` must remain intact.
- Linting & typechecking rules: `dotnet test` and `node --check wwwroot/app.js` must pass 100%.

---

### Task 1: Version Bump to v3.0.0 & Shared Class Library Creation

**Files:**
- Create: `LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`
- Create: `LocalLLMServerManager.Shared/Services/IApiClient.cs`
- Create: `LocalLLMServerManager.Shared/Services/ApiClient.cs`
- Modify: `LocalLLMServerManager.csproj`
- Modify: `installer.iss`

**Interfaces:**
- Produces: `LocalLLMServerManager.Shared` class library containing models and API client abstraction `IApiClient`.

- [ ] **Step 1: Create LocalLLMServerManager.Shared project**
Create `LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Version>3.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.2.3" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.3" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="11.2.3" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
  </ItemGroup>

  <ItemGroup>
    <AvaloniaResource Include="Assets\**" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add project reference to LocalLLMServerManager.csproj & bump version to 3.0.0**
Update `LocalLLMServerManager.csproj` to reference `LocalLLMServerManager.Shared.csproj` and set `<Version>3.0.0</Version>`.

- [ ] **Step 3: Create IApiClient and ApiClient service abstractions**
Create `LocalLLMServerManager.Shared/Services/IApiClient.cs` and `ApiClient.cs` for cross-platform HTTP requests.

- [ ] **Step 4: Update installer.iss for v3.0.0**
Update `installer.iss` `MyAppVersion` to `3.0.0`.

- [ ] **Step 5: Run tests and commit**
Run `dotnet test` and commit:
```bash
git add .
git commit -m "chore(v3.0.0): Bump version to 3.0.0 and create LocalLLMServerManager.Shared project"
```

---

### Task 2: Implement Shared Adaptive Responsive Views (MainView.axaml)

**Files:**
- Create: `LocalLLMServerManager.Shared/Views/MainView.axaml`
- Create: `LocalLLMServerManager.Shared/Views/MainView.axaml.cs`
- Move/Update: `LocalLLMServerManager.Shared/ViewModels/MainViewModel.cs`
- Modify: `Views/MainWindow.axaml`

**Interfaces:**
- Consumes: `IApiClient`, `OllamaModelItem`, `HuggingFaceRepoItem`.
- Produces: `MainView.axaml` with responsive layout breakpoints (`IsMobile`, `IsTablet`, `IsDesktop`).

- [ ] **Step 1: Create MainView.axaml responsive layout**
Define `MainView.axaml` with responsive view state properties (`IsMobile`, `IsTablet`, `IsDesktop`).

- [ ] **Step 2: Update MainViewModel.cs with breakpoint bindings**
Add viewport width observer logic to `MainViewModel.cs`.

- [ ] **Step 3: Update MainWindow.axaml to host MainView**
Update `Views/MainWindow.axaml` to embed `<views:MainView />`.

- [ ] **Step 4: Test & Commit**
Run `dotnet test` to verify build and ViewModel logic.
```bash
git add .
git commit -m "feat(v3.0.0): Add adaptive responsive MainView for mobile, tablet, and desktop"
```

---

### Task 3: Implement WebAssembly Target (LocalLLMServerManager.Web)

**Files:**
- Create: `LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj`
- Create: `LocalLLMServerManager.Web/Program.cs`
- Create: `LocalLLMServerManager.Web/App.axaml`
- Create: `LocalLLMServerManager.Web/App.axaml.cs`
- Create: `LocalLLMServerManager.Web/wwwroot/index.html`

**Interfaces:**
- Consumes: `LocalLLMServerManager.Shared`.
- Produces: WASM bundle (`main.js`, `dotnet.wasm`) in `LocalLLMServerManager/wwwroot/`.

- [ ] **Step 1: Create LocalLLMServerManager.Web project**
Configure `LocalLLMServerManager.Web.csproj` with `Avalonia.Browser` dependencies.

- [ ] **Step 2: Implement WASM Program.cs and App.axaml**
Set up WebAssembly application lifetime rendering `MainView`.

- [ ] **Step 3: Build & Publish WASM bundle into wwwroot/**
Compile `LocalLLMServerManager.Web` to WebAssembly and copy static WASM assets into `LocalLLMServerManager/wwwroot/`.

- [ ] **Step 4: Commit**
```bash
git add .
git commit -m "feat(v3.0.0): Add Avalonia.Browser WebAssembly target compiled to wwwroot/"
```

---

### Task 4: Documentation Updates for v3.0.0

**Files:**
- Modify: `README.md`
- Modify: `docs/`

**Interfaces:**
- Produces: Updated `README.md` reflecting v3.0.0 unified WASM + Desktop architecture.

- [ ] **Step 1: Update README.md version and feature badges**
Bump version in `README.md` to `v3.0.0`. Document WebAssembly UI, System Tray app, MCP server tool API, and dual-mode architecture.

- [ ] **Step 2: Commit documentation changes**
```bash
git add README.md
git commit -m "docs(v3.0.0): Update README with v3.0.0 architecture, WASM, and MCP tools documentation"
```

---

### Task 5: Build Final Installer & End-to-End Verification

**Files:**
- Output: `LocalLLMServerManager-v3.0.0-Setup.exe`

- [ ] **Step 1: Run unit tests and typechecking**
Run `dotnet test` and `node --check wwwroot/app.js`.

- [ ] **Step 2: Compile Inno Setup installer**
Run `ISCC.exe installer.iss` to build `LocalLLMServerManager-v3.0.0-Setup.exe`.

- [ ] **Step 3: Run silent update and verify API health**
Update installed app and verify `http://127.0.0.1:5246/health` returns version `3.0.0`.

- [ ] **Step 4: Push to origin/main**
Push final `v3.0.0` commit and tags to `origin/main`.
