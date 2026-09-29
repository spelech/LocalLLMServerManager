# Live Engine Sticker Studio & v3.17.0 Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the Sticker Studio to live local diffusion and image contouring pipelines, modernize user documentation and VitePress guides for the Dynamic UI Workspace, and execute the v3.17.0 release version bump and packaging.

**Architecture:** 
1. **Engine Dispatch & Alpha Contouring**: Enhance `StickerGenerationService` to query local diffusion engines (Stable Diffusion Forge `/sdapi/v1/txt2img` and `/sdapi/v1/img2img`) with curated negative prompts and preset styles. Process generated/input RGBA pixel buffers in C# using circular kernel Euclidean distance dilation to apply crisp, configurable (0–24px) die-cut white borders and background transparency with seamless offline simulation fallback.
2. **Documentation & VitePress**: Document the Activity Rail, Telemetry Ribbon, Dynamic Stage Container, and Sticker Studio in `README.md`, `docs/USER_GUIDE.md`, and add `docs/studio/sticker-studio.md` with VitePress sidebar navigation.
3. **v3.17.0 Release Bump**: Update version `3.16.0` -> `3.17.0` across csproj files, Avalonia view titles/footers, JavaScript web bundles, installer scripts, and add changelog entries.

**Tech Stack:** C# .NET 10, Avalonia UI 12, ASP.NET Core, Playwright, VitePress, TypeScript/ESLint.

## Global Constraints
- Always run linting and typechecking after making code changes (`npm run lint` and `npx tsc --noEmit`).
- For C# / Avalonia code changes, verify with `dotnet build LocalLLMServerManager.sln` and `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`.
- Preserve existing comments and docstrings.
- Adhere to design tokens in `DesignTokens.axaml` and `MatteTheme.axaml`.

---

### Task 1: Contour & Transparency Image Processor (Alpha Dilation Engine)

**Files:**
- Create: `LocalLLMServerManager.Shared/Services/StickerContourProcessor.cs`
- Test: `LocalLLMServerManager.Tests/StickerContourProcessorTests.cs`

**Interfaces:**
- Produces: `public static class StickerContourProcessor`
  - `public static byte[] ApplyDieCutBorder(byte[] rgbaPixels, int width, int height, int borderWidthPx, bool autoCutout = true)`
  - `public static byte[] CreateMinimalTestPng(int width, int height, byte r, byte g, byte b, byte a)`

- [ ] **Step 1: Write failing unit test for StickerContourProcessor**
Create `LocalLLMServerManager.Tests/StickerContourProcessorTests.cs` testing:
1. `ApplyDieCutBorder_ZeroBorder_LeavesForegroundIntact`
2. `ApplyDieCutBorder_ExpandsContourBySpecifiedRadius`
3. `ApplyDieCutBorder_HandlesEmptyOrSmallBufferGracefully`

- [ ] **Step 2: Run test to verify it fails**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StickerContourProcessorTests"`
Expected: FAIL (compilation error, type not found).

- [ ] **Step 3: Implement StickerContourProcessor**
Implement `LocalLLMServerManager.Shared/Services/StickerContourProcessor.cs`:
- Fast circular neighborhood Euclidean dilation ($dx^2 + dy^2 \le R^2$).
- Identifies foreground pixels ($a > 32$).
- If `autoCutout` is true and background is near-white or uniform corner color, marks background transparent.
- Dilates foreground by `borderWidthPx` painting solid white `(255, 255, 255, 255)`.
- Re-lays original foreground over the white silhouette to produce crisp die-cut borders.

- [ ] **Step 4: Run test to verify it passes**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StickerContourProcessorTests"`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add LocalLLMServerManager.Shared/Services/StickerContourProcessor.cs LocalLLMServerManager.Tests/StickerContourProcessorTests.cs
git commit -m "feat(studio): implement StickerContourProcessor with die-cut alpha dilation"
```

---

### Task 2: Live Forge / Diffusion Engine Dispatch in StickerGenerationService

**Files:**
- Modify: `LocalLLMServerManager.Shared/Services/StickerGenerationService.cs`
- Modify: `LocalLLMServerManager.Tests/StickerStudioViewModelTests.cs`

**Interfaces:**
- Consumes: `StickerContourProcessor.ApplyDieCutBorder`
- Produces: `StickerGenerationService.GenerateStickerAsync` connecting to `http://127.0.0.1:7860` if available, falling back gracefully to contour processing if offline.

- [ ] **Step 1: Write test for live engine fallback and contour integration**
Add tests to `LocalLLMServerManager.Tests/StickerStudioViewModelTests.cs`:
- `GenerateStickerAsync_AppliesContourAndPresets_WhenOffline`
- `GenerateStickerAsync_ConstructsValidForgePayload`

- [ ] **Step 2: Run test to verify it fails or validates new assertions**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StickerStudioViewModelTests"`

- [ ] **Step 3: Update StickerGenerationService with Forge dispatch and contour processor**
In `LocalLLMServerManager.Shared/Services/StickerGenerationService.cs`:
- Query Forge (`POST /sdapi/v1/txt2img` or `/sdapi/v1/img2img`) with HTTP timeout (5s probe, 60s generation).
- Extract base64 image from JSON response `{ "images": [...] }`.
- If Forge unreachable, fallback to simulation buffer.
- Pass result bytes through `StickerContourProcessor.ApplyDieCutBorder` with `request.BorderWidth` and `request.IsAutoCutoutEnabled`.

- [ ] **Step 4: Run tests to verify all pass**
Run: `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj --filter "FullyQualifiedName~StickerStudioViewModelTests"`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add LocalLLMServerManager.Shared/Services/StickerGenerationService.cs LocalLLMServerManager.Tests/StickerStudioViewModelTests.cs
git commit -m "feat(studio): wire Forge diffusion dispatch and contour processing in StickerGenerationService"
```

---

### Task 3: VitePress Documentation & README Modernization

**Files:**
- Create: `docs/studio/sticker-studio.md`
- Modify: `docs/.vitepress/config.mts`
- Modify: `README.md`
- Modify: `docs/USER_GUIDE.md`

- [ ] **Step 1: Create Sticker Studio VitePress Guide**
Create `docs/studio/sticker-studio.md` explaining:
- Overview and key benefits (die-cut vinyl, holographic, chibi, retro, pop art, watercolor).
- Drag-and-drop reference image workflows.
- Auto-cutout subject isolation and adjustable border width ($0\text{--}24\,\text{px}$).
- Local inference with Stable Diffusion Forge / ComfyUI.
- Keyboard shortcuts and export capabilities (Copy PNG, Save File).

- [ ] **Step 2: Update VitePress Navigation in `docs/.vitepress/config.mts`**
Add `Sticker Studio` under the `Studio & Generative Tools` sidebar section.

- [ ] **Step 3: Update `README.md` and `docs/USER_GUIDE.md`**
- Replace the legacy horizontal tabs table in `README.md` with the new Dynamic Workspace breakdown (Activity Rail, Titlebar Telemetry Ribbon, Dynamic Stage Container, Sticker Studio).
- Update `docs/USER_GUIDE.md` with the updated layout instructions.

- [ ] **Step 4: Verify VitePress build & markdown linting**
Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 5: Commit**
```bash
git add docs/studio/sticker-studio.md docs/.vitepress/config.mts README.md docs/USER_GUIDE.md
git commit -m "docs: add Sticker Studio guide and update README for Dynamic Workspace"
```

---

### Task 4: Version Bump to v3.17.0 & Release Packaging

**Files:**
- Modify: `LocalLLMServerManager.csproj`
- Modify: `LocalLLMServerManager.Shared/LocalLLMServerManager.Shared.csproj`
- Modify: `LocalLLMServerManager.Web/LocalLLMServerManager.Web.csproj`
- Modify: `App.axaml`
- Modify: `Views/MainWindow.axaml`
- Modify: `Endpoints/HealthEndpoints.cs`
- Modify: `LocalLLMServerManager.Web/main.js`
- Modify: `wwwroot/main.js`
- Modify: `wwwroot/index.html`
- Modify: `scripts/build_release.ps1`
- Modify: `scripts/installer.iss`
- Modify: `LocalLLMServerManager.Tests/AvaloniaHeadlessInteractionTests.cs`
- Modify: `LocalLLMServerManager.Tests/PlaywrightWasmE2ETests.cs`
- Modify: `LocalLLMServerManager.Tests/WasmAssetFreshnessTests.cs`
- Modify: `README.md`

- [ ] **Step 1: Bump version numbers from 3.16.0 to 3.17.0**
Update all project files, view titles, scripts, and endpoints to `3.17.0`.

- [ ] **Step 2: Update tests expecting version string**
Update `AvaloniaHeadlessInteractionTests.cs`, `PlaywrightWasmE2ETests.cs`, and `WasmAssetFreshnessTests.cs`.

- [ ] **Step 3: Add v3.17.0 Changelog entry in `README.md`**
Document the Dynamic UI Workspace, Activity Rail, Telemetry Ribbon, and Sticker Studio.

- [ ] **Step 4: Run full test suite, linting, and typechecking**
Run:
- `npm run lint`
- `npx tsc --noEmit`
- `dotnet test LocalLLMServerManager.Tests/LocalLLMServerManager.Tests.csproj`

- [ ] **Step 5: Commit**
```bash
git add .
git commit -m "chore(release): bump version to v3.17.0 and update release metadata"
```
