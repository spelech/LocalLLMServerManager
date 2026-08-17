# Windows Testing & Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute comprehensive end-to-end Windows verification covering unit & integration tests, Playwright WASM browser tests, automated documentation screenshot generation, self-contained Windows release packaging, and runtime background service smoke testing for LocalLLMServerManager v3.4.0.

**Architecture:** A 4-stage sequential verification pipeline executed on native Windows 11 win-x64: (1) .NET 10 xUnit test suite (137 tests), (2) Playwright headless Chromium WASM E2E tests and documentation PNG generator into `docs/images/`, (3) `scripts/build_release.ps1` self-contained `win-x64` publish and Inno Setup installer packaging into `dist/`, and (4) CLI argument and `--service` background daemon execution smoke checks.

**Tech Stack:** C# .NET 10.0, ASP.NET Core Minimal APIs, Microsoft.Playwright 1.50.0+, xUnit v3, Avalonia UI Desktop & WebAssembly, PowerShell 7, Inno Setup 6.

## Global Constraints
- Platform: Windows 11 (win-x64)
- Target Framework: `net10.0`
- Minimum Test Pass Rate: 100% (137/137 passing)
- Web Server Port: 5246
- Artifact Locations: `publish/`, `dist/LocalLLMServerManager-v3.4.0-win-x64.zip`, `dist/LocalLLMServerManager-v3.4.0-Setup.exe`, `docs/images/*.png`

---

### Task 1: Unit & Integration Test Suite Verification

**Files:**
- Test: `LocalLLMServerManager.Tests/*.cs`
- Solution: `LocalLLMServerManager.sln`

**Interfaces:**
- Consumes: `LocalLLMServerManager` and `LocalLLMServerManager.Shared` assemblies.
- Produces: 100% passing xUnit test results across all 137 unit and integration tests.

- [ ] **Step 1: Clean any lingering test host processes and file locks**

```powershell
Get-Process | Where-Object { $_.ProcessName -like "*LocalLLM*" -or $_.ProcessName -like "*testhost*" } | Stop-Process -Force -ErrorAction SilentlyContinue
```

- [ ] **Step 2: Run complete unit and integration test suite**

Run:
```bash
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName!~Playwright" -c Release --nologo
```
Expected: `Passed! - Failed: 0, Passed: 137, Skipped: 0, Total: 137`

- [ ] **Step 3: Verify zero test failures**

Confirm exit code 0 and all test assertions succeeded without timeouts or errors.

---

### Task 2: Playwright WebAssembly E2E Browser Testing & Automated Screenshot Generation

**Files:**
- Test: `LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs`
- Test: `LocalLLMServerManager.Tests/PlaywrightScreenshotGenerator.cs`
- Visual Assets: `docs/images/dashboard_*.png`

**Interfaces:**
- Consumes: `Microsoft.Playwright` Chromium browser driver, `AppTestServerFixture` (Kestrel on port 5246).
- Produces: Verified zero 404 network responses, zero JS console exceptions, and updated PNG screenshots in `docs/images/`.

- [ ] **Step 1: Verify / install Playwright Chromium driver**

Run:
```powershell
pwsh LocalLLMServerManager.Tests/bin/Release/net10.0/playwright.ps1 install chromium
```
Expected: Playwright Chromium browser installed or already up to date.

- [ ] **Step 2: Execute Playwright WASM E2E browser test**

Run:
```bash
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightWasmE2ETests" -c Release --nologo
```
Expected: `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1` (zero network 404s, zero console errors).

- [ ] **Step 3: Execute Playwright documentation screenshot generator**

Run:
```bash
dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" -c Release --nologo
```
Expected: All 6 tab screenshots generated and saved to `docs/images/`.

- [ ] **Step 4: Verify generated screenshot files and dimensions**

Run:
```powershell
Get-ChildItem -Path docs/images/ -Filter "dashboard_*.png" | Select-Object Name, Length, LastWriteTime
```
Expected: 6 PNG files present with non-zero byte size (`dashboard_desktop.png`, `dashboard_ollama.png`, `dashboard_huggingface.png`, `dashboard_civitai.png`, `dashboard_3d_studio.png`, `dashboard_settings.png`).

---

### Task 3: Windows Native Packaging & Inno Setup Compilation

**Files:**
- Script: `scripts/build_release.ps1`
- Script: `scripts/installer.iss`
- Output: `publish/`, `dist/LocalLLMServerManager-v3.4.0-win-x64.zip`, `dist/LocalLLMServerManager-v3.4.0-Setup.exe`

**Interfaces:**
- Consumes: .NET 10 SDK, `ISCC.exe` (Inno Setup 6 compiler).
- Produces: Self-contained `win-x64` build directory (`publish/`), compressed distribution archive (`dist/*.zip`), and Windows installer executable (`dist/*-Setup.exe`).

- [ ] **Step 1: Execute release build and packaging script**

Run:
```powershell
pwsh scripts/build_release.ps1
```
Expected: Successful Avalonia WASM compilation, `dotnet publish` self-contained `win-x64`, ZIP archive generation, and Inno Setup installer compilation.

- [ ] **Step 2: Verify generated release artifacts in dist/ directory**

Run:
```powershell
Get-ChildItem -Path dist | Select-Object Name, Length, LastWriteTime
```
Expected:
- `LocalLLMServerManager-v3.4.0-win-x64.zip` (> 30 MB)
- `LocalLLMServerManager-v3.4.0-Setup.exe` (> 30 MB)

---

### Task 4: Native Runtime Smoke Check & Background Service Verification

**Files:**
- Executable: `publish/LocalLLMServerManager.exe`

**Interfaces:**
- Consumes: Compiled `publish/LocalLLMServerManager.exe`.
- Produces: Verified CLI `--help` output, background `--service` mode startup, `/health` HTTP 200 response, and clean termination.

- [ ] **Step 1: Test CLI help and argument parsing**

Run:
```powershell
.\publish\LocalLLMServerManager.exe --help
```
Expected: Application outputs usage instructions and flags (`--service`, `--port`, `--minimized`, etc.) with exit code 0.

- [ ] **Step 2: Start background service mode and verify health probe**

Run:
```powershell
$proc = Start-Process -FilePath ".\publish\LocalLLMServerManager.exe" -ArgumentList "--service" -PassThru
Start-Sleep -Seconds 3
$health = Invoke-RestMethod -Uri "http://127.0.0.1:5246/health" -Method Get
Write-Output "Status: $($health.status), Version: $($health.version)"
Stop-Process -Id $proc.Id -Force
```
Expected:
- `$health.status` == `"Healthy"`
- `$health.version` == `"3.4.0"`
- Process terminates cleanly.

- [ ] **Step 3: Verify clean process teardown**

Run:
```powershell
Get-Process | Where-Object { $_.ProcessName -like "*LocalLLM*" }
```
Expected: No orphaned processes remain running.
