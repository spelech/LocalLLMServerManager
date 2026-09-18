# VitePress Documentation Site Design (ASD-STE100)

## 1. Overview & Objective

This design defines the architecture, content structure, writing guidelines, and deployment pipeline for the **Local LLM Server Manager** official documentation site powered by **VitePress** and published to **GitHub Pages**.

The primary objective is to make local LLM and generative AI tools accessible, understandable, and actionable for everyday users through **ASD-STE100 (Simplified Technical English)**, while completely preserving all deep architectural, development, and agent technical documentation.

---

## 2. Goals & Non-Goals

### Goals
* **VitePress Foundation**: Configure VitePress with local offline search, clean URLs, responsive dark/light theme matching the desktop Avalonia palette, and automated GitHub Pages CI/CD deployment.
* **ASD-STE100 User Guides**: Write procedural user guides (Getting Started, Engines, Multimodal Studio, AI & MCP, Search & Downloads) using Simplified Technical English (short sentences, imperative mood, restricted ambiguous vocabulary).
* **Complete Technical Preservation**: Preserve 100% of existing technical documentation (`ARCHITECTURE.md`, `DEVELOPMENT_GUIDE.md`, `REQUIREMENTS.md`, `TEST_COVERAGE.md`, `WINDOWS_VALIDATION_GUIDE.md`, and `docs/ai_assistant/*`) under a dedicated **Technical Reference & Architecture** section so human developers and AI coding agents retain full context.
* **Zero Disruption to Existing Tooling**: Keep linting (`npm run lint`) and TypeScript checks (`npx tsc --noEmit`) passing without errors.

### Non-Goals
* Re-writing backend C# services or modifying desktop UI behavior.
* Creating external cloud dependencies (search will use VitePress's built-in local client-side search, zero API keys required).

---

## 3. Directory Layout & Information Architecture

The documentation will live in [`docs/`](file:///C:/Users/Alias/repos/LocalLLMServerManager/docs/) with the following directory structure:

```
docs/
├── .vitepress/
│   ├── config.mts                 # VitePress configuration (nav, sidebar, search, base URL)
│   └── theme/                     # Custom CSS adjustments for brand styling
├── index.md                       # Main landing page (Hero, features, quick navigation cards)
├── getting-started/
│   ├── index.md                   # Overview & System Requirements
│   ├── installation.md            # Windows Installer & Linux setup
│   ├── configuration.md           # Ports, directories, hardware auto-detection
│   └── quickstart.md              # First model download and chat
├── engines/
│   ├── index.md                   # Supported engines & VRAM Orchestrator
│   ├── ollama.md                  # Ollama setup, models, and proxy
│   ├── sd-forge.md                # Stable Diffusion Forge and CivitAI downloads
│   ├── comfyui.md                 # ComfyUI workflows and execution
│   └── kokoro-tts.md              # Kokoro TTS engine, voices, and audio proxy
├── studio/
│   ├── index.md                   # Multimodal Studio overview
│   ├── image-generation.md        # FLUX, SDXL, and LoRA workflows
│   ├── video-generation.md        # Wan 2.2, LTX-2.5, HunyuanVideo & player
│   ├── audio-and-music.md         # Speech, Stable Audio Open, and YuE Music
│   └── 3d-mesh.md                 # TRELLIS V2 and Hunyuan3D WebGL viewer
├── ai-and-mcp/
│   ├── index.md                   # AI Assistant & MCP overview
│   ├── assistant.md               # Built-in chat assistant, tools, and UI
│   ├── mcp-tools.md               # Model Context Protocol tools & configuration
│   └── flows-and-presets.md       # Pre-configured workflows and custom prompts
├── technical/                     # Technical documentation preserved for developers & agents
│   ├── index.md                   # Technical reference overview
│   ├── architecture.md            # C# / Avalonia / ASP.NET Core system architecture
│   ├── development.md             # Local development, debugging, and building
│   ├── requirements.md            # Requirements specification and test matrices
│   ├── validation.md              # Windows validation and process isolation guide
│   ├── test-coverage.md           # Test suites and coverage benchmarks
│   └── ai-assistant-internals.md  # AI Assistant technical stack, prompts, and tool contracts
├── standards/
│   └── ste-100.md                 # ASD-STE100 style guide and vocabulary rules
└── images/                        # Screenshots and diagrams
```

---

## 4. Navigation & Site Configuration

### Navigation Bar
* **Guide**: Links to `getting-started/installation.md`
* **Engines**: Links to `engines/index.md`
* **Studio**: Links to `studio/index.md`
* **AI & MCP**: Links to `ai-and-mcp/index.md`
* **Technical Reference**: Links to `technical/index.md`
* **GitHub**: Link to `https://github.com/spelech/LocalLLMServerManager`

### Sidebars
Multi-sidebar configuration configured in `docs/.vitepress/config.mts`:
* `/getting-started/`: System Requirements, Installation, Configuration, Quickstart, Troubleshooting.
* `/engines/`: Overview, VRAM Orchestrator, Ollama, SD Forge, ComfyUI, Kokoro TTS.
* `/studio/`: Overview, Image Generation, Video Generation, Audio & Music, 3D Mesh Reconstruction.
* `/ai-and-mcp/`: Overview, Chat Assistant, MCP Tools, Presets & Workflows.
* `/technical/`: System Architecture, Development Guide, Requirements, Validation, Test Coverage, Assistant Internals.
* `/standards/`: ASD-STE100 Style Rules.

---

## 5. ASD-STE100 Technical Writing Framework

All user-facing pages (`getting-started/`, `engines/`, `studio/`, `ai-and-mcp/`) follow ASD-STE100 Simplified Technical English rules:

1. **Sentence Length**: Maximum 20 words per procedural instruction. Maximum 25 words per descriptive sentence.
2. **One Thought Per Sentence**: Each step has a single action.
3. **Imperative Mood**: Begin instructions with direct action verbs:
   * *Do*: "Click **Download** to save the model."
   * *Do not*: "You should now proceed by clicking on the download button."
4. **Approved Terminology**:
   * Use "Show" instead of "Display".
   * Use "Start" or "Stop" instead of "Boot up" or "Kill/Terminate".
   * Use "Turn on" or "Turn off" instead of "Toggle/Enable/Disable".
   * Use "Change" instead of "Modify".
5. **No Ambiguous Pronouns**: Explicitly name nouns instead of using "it", "this", "these", or "they".
6. **Warning & Alert Levels**: Standardized GitHub callouts:
   * `[!NOTE]`: Helpful context.
   * `[!TIP]`: Efficiency recommendation.
   * `[!IMPORTANT]`: Critical requirement.
   * `[!WARNING]`: Risk of hardware crash or out-of-memory condition.

---

## 6. GitHub Pages CI/CD Pipeline

A GitHub Actions workflow will be installed at `.github/workflows/deploy-pages.yml`:

* **Triggers**:
  * Push to `main` with changes in `docs/**` or `.github/workflows/deploy-pages.yml`.
  * Manual trigger via `workflow_dispatch`.
* **Permissions**: `pages: write`, `id-token: write`, `contents: read`.
* **Steps**:
  1. `actions/checkout@v4`.
  2. `actions/setup-node@v4` with Node.js 22.
  3. `npm ci` (or `npm install`).
  4. `npm run docs:build`.
  5. `actions/upload-pages-artifact@v3` targeting `docs/.vitepress/dist`.
  6. `actions/deploy-pages@v4`.

---

## 7. Quality Gates & Tooling Verification

* `npm run docs:build` must compile static assets with zero broken markdown links.
* `npm run lint` must pass with zero errors.
* `npx tsc --noEmit` must pass with zero type errors.
* Existing technical documents are migrated and cross-referenced without data loss.
