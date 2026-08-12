# Task 2 Brief: Implement Microsoft.Playwright Automated E2E Browser Testing

## Requirements
1. Add `<PackageReference Include="Microsoft.Playwright" Version="1.50.0" />` to `LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`.
2. Create `LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs` using `AppTestServerFixture` for Kestrel web host initialization.
3. Test method `WebDashboard_BootsCleanlyWithoutConsoleOr404Errors()` must:
   - Launch headless Chromium using `Playwright.CreateAsync()` and `playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true })`.
   - Listen to `page.Console` events for errors (`msg.Type == "error"`).
   - Listen to `page.PageError` events for uncaught exceptions.
   - Listen to `page.Response` events for HTTP 404 responses targeting static framework assets (`/_framework/`).
   - Navigate to `AppTestServerFixture.TestBaseUrl`.
   - Wait 5 seconds (`await page.WaitForTimeoutAsync(5000)`) for WASM runtime initialization.
   - Assert `network404s` is empty (`Assert.Empty(network404s)`).
   - Assert `consoleErrors` is empty (`Assert.Empty(consoleErrors)`).
   - Assert element `#out` container is present in the DOM (`Assert.NotNull(outputContainer)`).

## Files
- Modify: `LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`
- Create: `LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs`

## Verification Command
`dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightWasmE2ETests" -c Release`
