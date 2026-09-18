# VitePress Documentation Site Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:dispatching-parallel-agents to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a complete VitePress documentation site deployed to GitHub Pages for `spelech/LocalLLMServerManager` with ASD-STE100 user guides and full technical reference preservation.

**Architecture:** VitePress documentation site rooted in `docs/` with local offline search, clean URLs, and dark mode. Modular content directories (`getting-started/`, `engines/`, `studio/`, `ai-and-mcp/`, `technical/`, `standards/`) with automated GitHub Actions Pages deployment (`.github/workflows/deploy-pages.yml`).

**Tech Stack:** VitePress 1.x, Vue 3, TypeScript, MiniSearch (local search), GitHub Actions (`actions/deploy-pages@v4`, `actions/upload-pages-artifact@v3`).

## Global Constraints

* Base URL must be `/LocalLLMServerManager/` for GitHub Pages compatibility.
* ASD-STE100 writing guidelines must be followed for user-facing guides: short sentences (<20-25 words), imperative mood, clear step-by-step actions.
* Preserve 100% of existing technical reference material in `docs/technical/` so developers and agents retain complete technical context.
* All changes must pass `npm run lint` and `npx tsc --noEmit` without error.
* `npm run docs:build` must compile cleanly with 0 broken links.

---

### Task 1: VitePress Tooling, Configuration, & CI/CD Setup

**Files:**
- Modify: `package.json`
- Modify: `eslint.config.mjs`
- Modify: `tsconfig.json`
- Create: `docs/.vitepress/config.mts`
- Create: `.github/workflows/deploy-pages.yml`

**Interfaces:**
- Produces: `docs:dev`, `docs:build`, `docs:preview` npm scripts and VitePress configuration.

- [ ] **Step 1: Update package.json with VitePress and docs scripts**
  Add `"vitepress": "^1.6.3"` to `devDependencies`. Add `"docs:dev": "vitepress dev docs"`, `"docs:build": "vitepress build docs"`, and `"docs:preview": "vitepress preview docs"` to `scripts`.

- [ ] **Step 2: Update eslint.config.mjs and tsconfig.json to ignore VitePress build output**
  Add `docs/.vitepress/dist/**` and `docs/.vitepress/cache/**` to `ignores` in `eslint.config.mjs`. Add `"docs/.vitepress/dist"` and `"docs/.vitepress/cache"` to `exclude` in `tsconfig.json`.

- [ ] **Step 3: Create docs/.vitepress/config.mts**
  Configure VitePress with:
  * `base: '/LocalLLMServerManager/'`
  * `title: 'Local LLM Server Manager'`
  * `description: 'User Guide and Documentation for Local AI Engines, Multimodal Studio, and MCP Tools'`
  * `search: { provider: 'local' }`
  * Navigation bar: Guide, Engines, Studio, AI & MCP, Technical Reference, GitHub link.
  * Sidebars for `/getting-started/`, `/engines/`, `/studio/`, `/ai-and-mcp/`, `/technical/`, and `/standards/`.

- [ ] **Step 4: Create .github/workflows/deploy-pages.yml**
  Create GitHub Actions workflow for deployment to GitHub Pages triggered on push to `main` with permissions `pages: write`, `id-token: write`, `contents: read`.

- [ ] **Step 5: Run npm install and verify tooling**
  Run `npm install` and run `npm run lint` and `npx tsc --noEmit` to verify zero errors.

- [ ] **Step 6: Commit Task 1**
  ```bash
  git add package.json package-lock.json eslint.config.mjs tsconfig.json docs/.vitepress/config.mts .github/workflows/deploy-pages.yml
  git commit -m "chore: setup VitePress configuration, scripts, and GitHub Pages workflow"
  ```

---

### Task 2: Technical Documentation Preservation & Migration

**Files:**
- Create: `docs/technical/index.md`
- Create: `docs/technical/architecture.md` (migrated from `docs/ARCHITECTURE.md`)
- Create: `docs/technical/development.md` (migrated from `docs/DEVELOPMENT_GUIDE.md`)
- Create: `docs/technical/requirements.md` (migrated from `docs/REQUIREMENTS.md`)
- Create: `docs/technical/validation.md` (migrated from `docs/WINDOWS_VALIDATION_GUIDE.md`)
- Create: `docs/technical/test-coverage.md` (migrated from `docs/TEST_COVERAGE.md`)
- Create: `docs/technical/ai-assistant-internals.md` (consolidated from `docs/ai_assistant/*`)

**Interfaces:**
- Consumes: Existing technical docs in `docs/` and `docs/ai_assistant/`.
- Produces: Structured VitePress technical section preserving all developer/agent architecture knowledge.

- [ ] **Step 1: Create docs/technical/index.md**
  Overview linking to all technical reference pages.

- [ ] **Step 2: Migrate architecture, development, requirements, validation, and test-coverage**
  Copy and format existing documents into `docs/technical/` with VitePress frontmatter, keeping all code samples, API tables, and architecture explanations intact.

- [ ] **Step 3: Create docs/technical/ai-assistant-internals.md**
  Preserve and link the AI Assistant architecture, system prompts, tool schemas, and tech stack details from `docs/ai_assistant/`.

- [ ] **Step 4: Commit Task 2**
  ```bash
  git add docs/technical/
  git commit -m "docs: migrate and preserve technical documentation under docs/technical/"
  ```

---

### Task 3: Home Page, Standards, & Getting Started (ASD-STE100)

**Files:**
- Create: `docs/index.md`
- Create: `docs/standards/ste-100.md`
- Create: `docs/getting-started/index.md`
- Create: `docs/getting-started/installation.md`
- Create: `docs/getting-started/configuration.md`
- Create: `docs/getting-started/quickstart.md`
- Create: `docs/getting-started/troubleshooting.md`

**Interfaces:**
- Produces: Complete landing page and onboarding guide written in ASD-STE100.

- [ ] **Step 1: Create docs/index.md**
  VitePress home layout with Hero title, tagline, action buttons, and feature grid cards.

- [ ] **Step 2: Create docs/standards/ste-100.md**
  Document the ASD-STE100 guidelines (sentence lengths, imperative verb rules, approved vocabulary, alert callout conventions).

- [ ] **Step 3: Create Getting Started pages**
  Write `index.md`, `installation.md` (Windows MSI/installer & Linux desktop/headless), `configuration.md` (ports, directories, auto-detect), `quickstart.md` (pulling first GGUF/Ollama model), and `troubleshooting.md`.

- [ ] **Step 4: Commit Task 3**
  ```bash
  git add docs/index.md docs/standards/ docs/getting-started/
  git commit -m "docs: add home page, STE-100 guide, and getting started guides"
  ```

---

### Task 4: Engines & Model Management (ASD-STE100)

**Files:**
- Create: `docs/engines/index.md`
- Create: `docs/engines/ollama.md`
- Create: `docs/engines/sd-forge.md`
- Create: `docs/engines/comfyui.md`
- Create: `docs/engines/kokoro-tts.md`
- Create: `docs/engines/model-management.md`

**Interfaces:**
- Produces: Actionable, plain-English guides for each supported engine, VRAM orchestration, Hugging Face Hub, and CivitAI downloads.

- [ ] **Step 1: Create docs/engines/index.md**
  Overview of supported engines and the VRAM Orchestrator.

- [ ] **Step 2: Create docs/engines/ollama.md and model-management.md**
  How to manage Ollama models, Hugging Face GGUF downloads, multi-model concurrency (`OLLAMA_MAX_LOADED_MODELS`), and the reverse proxy.

- [ ] **Step 3: Create docs/engines/sd-forge.md, comfyui.md, and kokoro-tts.md**
  Guides for SD Forge (boot/stop, CivitAI downloader), ComfyUI (backend connection, workflows), and Kokoro TTS (OpenAI-compatible speech proxy, voice catalog).

- [ ] **Step 4: Commit Task 4**
  ```bash
  git add docs/engines/
  git commit -m "docs: add engine and model management guides in ASD-STE100"
  ```

---

### Task 5: Multimodal Studio (ASD-STE100)

**Files:**
- Create: `docs/studio/index.md`
- Create: `docs/studio/image-generation.md`
- Create: `docs/studio/video-generation.md`
- Create: `docs/studio/audio-and-music.md`
- Create: `docs/studio/3d-mesh.md`

**Interfaces:**
- Produces: User guides for all 4 creative studio modalities.

- [ ] **Step 1: Create docs/studio/index.md**
  Overview of Multimodal Studio and feature packs (`ext_video`, `ext_audio`).

- [ ] **Step 2: Create image-generation.md and video-generation.md**
  Image generation (FLUX, SDXL, LoRA checkpoints) and video generation (Wan 2.2, LTX-2.5, HunyuanVideo, interactive player).

- [ ] **Step 3: Create audio-and-music.md and 3d-mesh.md**
  Audio & speech generation (Kokoro TTS, Stable Audio Open, YuE music, waveform visualizer) and 3D mesh reconstruction (TRELLIS V2, Hunyuan3D v2, orbital WebGL viewer).

- [ ] **Step 4: Commit Task 5**
  ```bash
  git add docs/studio/
  git commit -m "docs: add multimodal studio guides in ASD-STE100"
  ```

---

### Task 6: AI Assistant, MCP Features, & Presets (ASD-STE100)

**Files:**
- Create: `docs/ai-and-mcp/index.md`
- Create: `docs/ai-and-mcp/assistant.md`
- Create: `docs/ai-and-mcp/mcp-tools.md`
- Create: `docs/ai-and-mcp/flows-and-presets.md`

**Interfaces:**
- Produces: User-facing guides explaining AI assistant features, tool calling, Model Context Protocol integration, and custom workflows.

- [ ] **Step 1: Create docs/ai-and-mcp/index.md**
  Introduction to AI orchestration and MCP in Local LLM Server Manager.

- [ ] **Step 2: Create assistant.md**
  How to chat with local models, select system prompts, and execute tools.

- [ ] **Step 3: Create mcp-tools.md**
  What MCP is, how to configure external MCP servers, and how the manager connects tools to local LLMs.

- [ ] **Step 4: Create flows-and-presets.md**
  Using workflow presets, prompt templates, and combining models for complex tasks.

- [ ] **Step 5: Commit Task 6**
  ```bash
  git add docs/ai-and-mcp/
  git commit -m "docs: add AI assistant, MCP tools, and workflow presets guides in ASD-STE100"
  ```

---

### Task 7: Quality Verification, Push Branch, and Pull Request

**Files:**
- All modified and created documentation files.

- [ ] **Step 1: Verify documentation build**
  Run `npm run docs:build` and confirm 0 build errors and 0 dead links.

- [ ] **Step 2: Run linting and TypeScript checks**
  Run `npm run lint` and `npx tsc --noEmit` and confirm 0 errors.

- [ ] **Step 3: Push feature branch to remote origin**
  Run `git push -u origin feature/vitepress-docs`.

- [ ] **Step 4: Create Pull Request / Merge Request**
  Use `gh pr create` or output the pull request creation instructions with a comprehensive PR description.
