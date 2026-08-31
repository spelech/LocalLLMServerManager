# Task 6: Full Verification Suite, WebAssembly Build Sync, Playwright E2E & Final Review

## Description
Execute the full test suite across the solution, rebuild and publish the WebAssembly client bundle to `wwwroot` and `C:\Program Files\LocalLLMServerManager\wwwroot`, update Playwright WASM browser tests to exercise the new `[⚡ Can I Run It]` tab, and prepare the final verification commit.

## Requirements
1. **Playwright WASM E2E Tests (`PlaywrightWasmE2ETests.cs`)**:
   - Update `PlaywrightWasmE2ETests.cs` to test Tab #5 `[⚡ Can I Run It]`:
     - Verify clean initial boot with 0 network 404s and 0 console errors.
     - Switch to Tab #5 via mouse coordinate click on the navigation tab.
     - Verify the Can I Run It view mounts in WebAssembly with canvas interaction.
2. **Clean WebAssembly Release Build & Static Asset Sync**:
   - `dotnet clean LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj -c Release`
   - `dotnet publish LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj -c Release -r browser-wasm --nologo`
   - Sync `AppBundle/_framework/*` to:
     - `wwwroot/_framework/`
     - `C:\Program Files\LocalLLMServerManager\wwwroot\_framework/`
     - `LocalLLMServerManager.Tests\bin\Debug\net10.0\wwwroot\_framework/`
   - Sync json/main.js assets and clean stale precompressed files.
   - Restart `LocalLLMServerManager` Windows service.
3. **Verification**:
   - Run `npm run lint` (0 errors).
   - Run `npx tsc --noEmit` (0 errors).
   - Run `dotnet test LocalLLMServerManager.sln --nologo` (all 398+ unit, integration, headless UI, and Playwright tests pass 100%).
4. **Documentation & Plan Checkboxes**:
   - Check all completed task boxes in `docs/superpowers/plans/2026-08-30-can-i-run-it.md`.
5. **Commit**:
   - `git commit -m "chore(release): publish WebAssembly bundle, update Playwright WASM tests, and verify Can I Run It suite"`
6. **Report**:
   - Write report to `.superpowers/sdd/2026-08-30-can-i-run-it/task-6-report.md`.
