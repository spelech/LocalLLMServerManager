# Task 2 Brief: Playwright WebAssembly E2E Browser Testing & Automated Screenshot Generation

**Files:**
- Test: `LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs`
- Test: `LocalLLMServerManager.Tests/PlaywrightScreenshotGenerator.cs`
- Visual Assets: `docs/images/dashboard_*.png`

**Global Constraints:**
- Target Framework: `net10.0`
- Minimum Test Pass Rate: 100%
- Web Server Port: 5246
- Platform: Windows 11 win-x64

**Requirements:**
1. Install / verify Playwright Chromium browser binary:
   ```powershell
   pwsh LocalLLMServerManager.Tests/bin/Release/net10.0/playwright.ps1 install chromium
   ```
2. Run Playwright WASM E2E tests:
   ```bash
   dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightWasmE2ETests" -c Release --nologo
   ```
   Verify 100% pass (zero 404s, zero console errors, `#out` mounted).
3. Run Playwright automated screenshot generator:
   ```bash
   dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" -c Release --nologo
   ```
4. Verify that all 6 screenshots exist in `docs/images/` with non-zero byte size:
   - `dashboard_desktop.png`
   - `dashboard_ollama.png`
   - `dashboard_huggingface.png`
   - `dashboard_civitai.png`
   - `dashboard_3d_studio.png`
   - `dashboard_settings.png`
5. Write detailed execution report to `task-2-report.md`.
