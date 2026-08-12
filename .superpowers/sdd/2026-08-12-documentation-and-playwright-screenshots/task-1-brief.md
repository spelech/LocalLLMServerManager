# Task 1 Brief: Create Playwright Automated Screenshot Generator

## Requirements
1. Create `LocalLLMServerManager.Tests/PlaywrightScreenshotGenerator.cs` using `AppTestServerFixture` for host initialization.
2. Launch Playwright headless Chromium browser with viewport size `1280x800` and `--use-gl=angle --use-angle=swiftshader --enable-webgl` browser flags.
3. Navigate to `AppTestServerFixture.TestBaseUrl` and wait 5000ms for WASM rendering.
4. Capture high-resolution PNG browser screenshots and write to `docs/images/`:
   - `docs/images/dashboard_desktop.png` (Main Overview & VRAM header)
   - `docs/images/dashboard_ollama.png` (Ollama Installed Models tab)
   - `docs/images/dashboard_huggingface.png` (Hugging Face Search tab)
   - `docs/images/dashboard_civitai.png` (CivitAI Search tab)
   - `docs/images/dashboard_3d_studio.png` (3D & ComfyUI Studio tab)
   - `docs/images/dashboard_settings.png` (Settings tab)
5. Assert that `docs/images/dashboard_desktop.png` exists and is non-empty (`Assert.True(File.Exists(...))`).

## Files
- Create: `LocalLLMServerManager.Tests/PlaywrightScreenshotGenerator.cs`
- Output: `docs/images/*.png`

## Verification Command
`dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~PlaywrightScreenshotGenerator" -c Release`
