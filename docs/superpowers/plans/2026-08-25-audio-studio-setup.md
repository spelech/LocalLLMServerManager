# Comprehensive Local Audio & Music Studio Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish an isolated, storage-conscious local multimodal audio generation suite (Kokoro TTS, AllTalk XTTS-v2 Voice Cloning, Faster-Whisper Large-v3-Turbo STT, Stable Audio Open, and MusicGen) on `D:\AI\audio\` and `D:\AI\comfy_models\` integrated with `LocalLLMServerManager`.

**Architecture:** A reverse-proxied dual-tier audio architecture running Kokoro-FastAPI on `:8880` for sub-100ms TTS and AllTalk on `:8880` for on-demand voice cloning, Faster-Whisper for OpenAI-compatible transcription (`POST /v1/audio/transcriptions`), and ComfyUI on `:8188` for Stable Audio Open & MusicGen workflows. All models and engine files are partitioned under `D:\AI\audio\`.

**Tech Stack:** C# .NET 10 (LocalLLMServerManager, YARP, ASP.NET Core), Python 3.10+ (Kokoro-FastAPI, ONNX Runtime, Faster-Whisper / CTranslate2, AllTalk XTTS-v2), ComfyUI (Stable Audio Open 1.0, MusicGen).

## Global Constraints

- Storage Target: All audio models, engines, voices, and STT weights must live under `D:\AI\audio\` and `D:\AI\comfy_models\checkpoints\`.
- Total Disk Budget: ~11.0 GB maximum for the complete studio suite.
- API Compliance: Support standard OpenAI `/v1/audio/speech` and `/v1/audio/transcriptions` schemas.
- Code Quality: Run `dotnet test`, `npm run lint`, and `npx tsc --noEmit` after every task.

---

### Task 1: Directory Structure & Engine Path Discovery in `LocalLLMServerManager`

**Files:**
- Modify: `Services/ToolDiscoveryService.cs:400-475`
- Modify: `LocalLLMServerManager.Shared/Services/DownloadManager.cs:20-30`
- Test: `LocalLLMServerManager.Tests/ToolDiscoveryServiceAudioTests.cs`

**Interfaces:**
- Consumes: `AppSettings.AudioPath`, `AppSettings.AudioEngineExecutablePath`, `IToolDiscoveryService.DetectAudioEngine()`
- Produces: Discovered audio engine pointing to `D:\AI\audio\engines\kokoro-fastapi` or `D:\AI\audio\engines\alltalk_tts` with valid status.

- [ ] **Step 1: Write the failing unit tests for audio discovery in `D:\AI\audio`**

```csharp
using System.IO;
using LocalLLMServerManager.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ToolDiscoveryServiceAudioTests
{
    [Fact]
    public void DetectAudioEngine_WhenLocatedInAudioEnginesSubdir_ReturnsIsInstalledTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_ai_audio_" + Path.GetRandomFileName());
        var kokoroDir = Path.Combine(tempDir, "audio", "engines", "kokoro-fastapi");
        Directory.CreateDirectory(kokoroDir);
        var mainPy = Path.Combine(kokoroDir, "main.py");
        File.WriteAllText(mainPy, "# test");

        try
        {
            var discovery = new ToolDiscoveryService(new[] { tempDir });
            var result = discovery.DetectAudioEngine();

            Assert.True(result.IsInstalled);
            Assert.Equal(mainPy, result.ExecutablePath);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ToolDiscoveryServiceAudioTests"`
Expected: FAIL (subpath `audio/engines/kokoro-fastapi` not in candidate list)

- [ ] **Step 3: Update `ToolDiscoveryService.cs` candidate paths & `DownloadManager.cs`**

Add `Path.Combine(baseRoot, "audio", "engines", sub)` and `Path.Combine(baseRoot, "audio", sub)` to candidate search paths in `Services/ToolDiscoveryService.cs`.
Update `LocalLLMServerManager.Shared/Services/DownloadManager.cs` to route audio downloads to `audio/engines` or `audio/stt`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ToolDiscoveryServiceAudioTests"`
Expected: PASS

- [ ] **Step 5: Run linter and typechecker**

Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 6: Commit**

```bash
git add Services/ToolDiscoveryService.cs LocalLLMServerManager.Shared/Services/DownloadManager.cs LocalLLMServerManager.Tests/ToolDiscoveryServiceAudioTests.cs
git commit -m "feat(audio): support D:\AI\audio directory hierarchy in tool discovery and download routing"
```

---

### Task 2: Kokoro TTS Engine Scaffolding & Setup Script

**Files:**
- Create: `scripts/setup_kokoro_tts.ps1`
- Modify: `Services/ComponentManagerService.cs:60-70`
- Test: `LocalLLMServerManager.Tests/ComponentManagerAudioPackTests.cs`

**Interfaces:**
- Consumes: `D:\AI\audio\engines\kokoro-fastapi`
- Produces: Automated setup script that installs `kokoro-onnx`, `soundfile`, `fastapi`, `uvicorn` and downloads `kokoro-v1.0.onnx` (~350 MB) + `voices-v1.0.bin`.

- [ ] **Step 1: Write failing test for ComponentManagerService audio pack detection**

```csharp
using LocalLLMServerManager.Services;
using Moq;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class ComponentManagerAudioPackTests
{
    [Fact]
    public async Task GetComponentsAsync_IncludesAudioTtsAndMusicPacksWithCorrectMetadata()
    {
        var settingsMock = new Mock<ISettingsService>();
        settingsMock.Setup(s => s.LoadSettings()).Returns(new AppSettings());
        var service = new ComponentManagerService(settingsMock.Object);

        var components = (await service.GetComponentsAsync()).ToList();
        var audioPack = components.FirstOrDefault(c => c.Id == "audio-tts");

        Assert.NotNull(audioPack);
        Assert.Equal("350 MB", audioPack.DiskSizeEstimate);
    }
}
```

- [ ] **Step 2: Run test to verify it fails or passes baseline**

Run: `dotnet test --filter "FullyQualifiedName~ComponentManagerAudioPackTests"`

- [ ] **Step 3: Create `scripts/setup_kokoro_tts.ps1`**

Write script that accepts `-TargetDir` (default `D:\AI\audio\engines\kokoro-fastapi`), creates target directory, downloads `kokoro-v1.0.onnx` and voice models from Hugging Face Hub (`hexgrad/Kokoro-82M`), and writes minimal `main.py` FastAPI server.

- [ ] **Step 4: Run tests**

Run: `dotnet test`
Expected: ALL PASS

- [ ] **Step 5: Run linter and typechecker**

Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 6: Commit**

```bash
git add scripts/setup_kokoro_tts.ps1 Services/ComponentManagerService.cs LocalLLMServerManager.Tests/ComponentManagerAudioPackTests.cs
git commit -m "feat(audio): add Kokoro TTS automated setup script and component pack detection"
```

---

### Task 3: Faster-Whisper STT (Speech-to-Text) Proxy Endpoint & Integration

**Files:**
- Create: `Endpoints/TranscriptionEndpoints.cs`
- Modify: `Program.cs:65-80`
- Create: `scripts/setup_whisper_stt.ps1`
- Test: `LocalLLMServerManager.Tests/TranscriptionEndpointsTests.cs`

**Interfaces:**
- Consumes: Multi-part form audio file at `POST /v1/audio/transcriptions`
- Produces: OpenAI-compatible JSON transcript response `{ "text": "...", "segments": [...] }`

- [ ] **Step 1: Write failing unit test for `POST /v1/audio/transcriptions` endpoint**

```csharp
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class TranscriptionEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TranscriptionEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostTranscription_WithNoFile_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        var response = await _client.PostAsync("/v1/audio/transcriptions", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TranscriptionEndpointsTests"`
Expected: FAIL (404 Not Found)

- [ ] **Step 3: Implement `Endpoints/TranscriptionEndpoints.cs` and map in `Program.cs`**

Implement `POST /v1/audio/transcriptions` with parameter validation (`file`, `model`, `language`, `response_format`), proxying or delegating to local STT runner when available, and returning standardized OpenAI JSON.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~TranscriptionEndpointsTests"`
Expected: PASS

- [ ] **Step 5: Run linter and typechecker**

Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 6: Commit**

```bash
git add Endpoints/TranscriptionEndpoints.cs Program.cs LocalLLMServerManager.Tests/TranscriptionEndpointsTests.cs scripts/setup_whisper_stt.ps1
git commit -m "feat(stt): add OpenAI-compatible /v1/audio/transcriptions endpoint and Faster-Whisper setup script"
```

---

### Task 4: AllTalk (XTTS-v2) Voice Cloning Setup & Custom Voices Integration

**Files:**
- Create: `scripts/setup_alltalk_xtts.ps1`
- Modify: `Services/ToolDiscoveryService.cs:410-470`
- Test: `LocalLLMServerManager.Tests/AllTalkDiscoveryTests.cs`

**Interfaces:**
- Consumes: `D:\AI\audio\engines\alltalk_tts`, `D:\AI\audio\custom_voices`
- Produces: Discovery of AllTalk instance and voice cloning directory mapping.

- [ ] **Step 1: Write failing test for AllTalk voice discovery**

```csharp
using System.IO;
using LocalLLMServerManager.Services;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AllTalkDiscoveryTests
{
    [Fact]
    public void DetectAudioEngine_WhenAllTalkInstalled_DiscoversExecutableAndVoiceDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_alltalk_" + Path.GetRandomFileName());
        var alltalkDir = Path.Combine(tempDir, "audio", "engines", "alltalk_tts");
        var voicesDir = Path.Combine(alltalkDir, "voices");
        Directory.CreateDirectory(voicesDir);
        var runBat = Path.Combine(alltalkDir, "run.bat");
        File.WriteAllText(runBat, "@echo off");
        File.WriteAllText(Path.Combine(alltalkDir, "main.py"), "# alltalk");

        try
        {
            var discovery = new ToolDiscoveryService(new[] { tempDir });
            var result = discovery.DetectAudioEngine();

            Assert.True(result.IsInstalled);
            Assert.Equal(runBat, result.ExecutablePath);
            Assert.Equal(voicesDir, result.ModelsDirectory);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails or passes**

Run: `dotnet test --filter "FullyQualifiedName~AllTalkDiscoveryTests"`

- [ ] **Step 3: Implement `scripts/setup_alltalk_xtts.ps1` and refine `ToolDiscoveryService.cs`**

Add `scripts/setup_alltalk_xtts.ps1` to automate AllTalk TTS environment setup in `D:\AI\audio\engines\alltalk_tts` and link `custom_voices` directory.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~AllTalkDiscoveryTests"`
Expected: PASS

- [ ] **Step 5: Run linter and typechecker**

Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 6: Commit**

```bash
git add scripts/setup_alltalk_xtts.ps1 Services/ToolDiscoveryService.cs LocalLLMServerManager.Tests/AllTalkDiscoveryTests.cs
git commit -m "feat(audio): add AllTalk XTTS-v2 voice cloning setup and discovery verification"
```

---

### Task 5: Stable Audio Open & MusicGen Workflow Verification

**Files:**
- Create: `Workflows/Audio/musicgen_melody.json`
- Modify: `Workflows/Audio/stable_audio_open_sfx.json`
- Test: `LocalLLMServerManager.Tests/AudioWorkflowTests.cs`

**Interfaces:**
- Consumes: `Workflows/Audio/stable_audio_open_sfx.json`, `Workflows/Audio/musicgen_melody.json`
- Produces: Validated ComfyUI API workflow payloads for audio/music generation.

- [ ] **Step 1: Write failing unit test validating audio workflow structures**

```csharp
using System.IO;
using System.Text.Json;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class AudioWorkflowTests
{
    [Fact]
    public void StableAudioWorkflow_IsValidJson_AndContainsStableAudioModelLoader()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Workflows", "Audio", "stable_audio_open_sfx.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Audio", "stable_audio_open_sfx.json");
        }

        Assert.True(File.Exists(path), $"File not found at {path}");
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("audio", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void MusicGenWorkflow_IsValidJson_AndContainsAudioType()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Workflows", "Audio", "musicgen_melody.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "Workflows", "Audio", "musicgen_melody.json");
        }

        Assert.True(File.Exists(path), $"File not found at {path}");
        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("audio", doc.RootElement.GetProperty("type").GetString());
    }
}
```

- [ ] **Step 2: Run test to verify it fails on missing `musicgen_melody.json`**

Run: `dotnet test --filter "FullyQualifiedName~AudioWorkflowTests"`
Expected: FAIL (missing `musicgen_melody.json`)

- [ ] **Step 3: Create `Workflows/Audio/musicgen_melody.json`**

Create the ComfyUI workflow JSON for MusicGen melody generation.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~AudioWorkflowTests"`
Expected: PASS

- [ ] **Step 5: Run linter and typechecker**

Run: `npm run lint` and `npx tsc --noEmit`

- [ ] **Step 6: Commit**

```bash
git add Workflows/Audio/musicgen_melody.json LocalLLMServerManager.Tests/AudioWorkflowTests.cs
git commit -m "feat(music): add MusicGen melody ComfyUI workflow preset and unit tests"
```

---

### Task 6: Full Verification Suite & Health Diagnostics

**Files:**
- Modify: `docs/superpowers/plans/2026-08-25-audio-studio-setup.md`
- Test: All project test suites (`LocalLLMServerManager.Tests`)

- [ ] **Step 1: Run complete .NET test suite**

Run: `dotnet test`
Expected: ALL PASS (0 failures)

- [ ] **Step 2: Run frontend linter and TypeScript compiler**

Run: `npm run lint` and `npx tsc --noEmit`
Expected: 0 errors, 0 warnings

- [ ] **Step 3: Final verification commit**

```bash
git add .
git commit -m "chore: verify audio studio setup implementation plan and full test pass"
```
