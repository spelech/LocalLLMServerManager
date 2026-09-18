---
layout: home

hero:
  name: Local LLM Server Manager
  text: Unified Local AI Orchestrator
  tagline: Control Ollama, Stable Diffusion Forge, ComfyUI, and Kokoro TTS with automated VRAM orchestration.
  actions:
    - theme: brand
      text: Get Started
      link: ./getting-started/index.md
    - theme: alt
      text: View on GitHub
      link: https://github.com/spelech/LocalLLMServerManager

features:
  - icon: ⚡
    title: VRAM Orchestrator
    details: Monitor GPU memory in real time. The system unloads inactive text models before heavy image, video, or 3D generation tasks.
  - icon: 🧩
    title: Multi-Engine Support
    details: Manage Ollama, Stable Diffusion Forge, ComfyUI, and Kokoro TTS from one unified desktop window or background service.
  - icon: 🎨
    title: Multimodal Studio
    details: Create images, 3D meshes, videos, and synthesized speech with interactive WebGL, audio waveform, and video player controls.
  - icon: 🤖
    title: MCP Integration
    details: Connect external AI coding assistants directly to your local hardware with eleven official Model Context Protocol tools.
---

## Overview

Local LLM Server Manager is a cross-platform orchestrator for local artificial intelligence engines. The application operates on Windows and Linux. The system coordinates Large Language Models, image generation, 3D mesh reconstruction, video synthesis, and speech generation.

```
+-----------------------------------------------------------------------------------------+
| Local LLM Server Manager                                                                |
| GPU: NVIDIA GeForce RTX 4070 Ti SUPER -- 16 GB • Service Connected 🟢   [🔄 Refresh]    |
| GPU VRAM Allocation: 4.2 GB / 16.0 GB (26.3%)                                           |
| [========================-------------------------------------------------------------] |
+-----------------------------------------------------------------------------------------+
| [🦙 Installed Models] [🤗 Hugging Face Hub] [🎨 CivitAI Models] [📦 Studio] [⚙️ Settings]|
+-----------------------------------------------------------------------------------------+
```

## Core Capabilities

- **Automated VRAM Management**: Prevent out-of-memory errors during heavy diffusion or 3D tasks.
- **Model Discovery**: Search and download models from Hugging Face Hub and CivitAI directly.
- **Unified Reverse Proxy**: Route all AI engine traffic through a single port (`5246`).
- **Flexible Deployment**: Run as a native Avalonia desktop application, system tray app, or headless background service.
- **Model Context Protocol**: Give AI coding assistants secure control over your local AI infrastructure.

## Documentation Sections

Explore the documentation guides to set up and operate your local AI stack:

- [Getting Started Overview](./getting-started/index.md): Review system requirements and core concepts.
- [Installation Guide](./getting-started/installation.md): Install the application on Windows or Linux.
- [First-Time Configuration](./getting-started/configuration.md): Auto-detect engines, configure ports, and set model paths.
- [Quickstart Tutorial](./getting-started/quickstart.md): Download your first model and send your first prompt.
- [Troubleshooting Guide](./getting-started/troubleshooting.md): Resolve VRAM issues, port conflicts, and network errors.
- [Documentation Style Guide](./standards/ste-100.md): Review writing standards for this documentation site.
- [Technical Reference](./technical/index.md): Read system architecture specifications and developer guides.
