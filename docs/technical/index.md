# Technical Reference & Architecture

Welcome to the **Technical Reference** for the **Local LLM Server Manager**. This section preserves the complete developer guides, architectural specifications, requirements matrices, and internal subsystems for contributors, maintainers, and AI coding agents working within the repository.

---

## Technical Guides Index

| Document | Description |
| :--- | :--- |
| [System Architecture](./architecture.md) | High-level system architecture, Avalonia desktop UI, ASP.NET Core proxy, and job management. |
| [Development & Build Guide](./development.md) | Environment setup, .NET 10.0 compilation, Playwright testing, and installer packaging. |
| [Requirements Specification](./requirements.md) | Functional requirements, platform support, and acceptance test criteria. |
| [Windows Process Validation](./validation.md) | Win32 Job Object process containment, tree termination, and VRAM leak prevention. |
| [Test Coverage & Benchmarks](./test-coverage.md) | Unit, integration, and E2E coverage reports and performance baselines. |
| [AI Assistant Internals](./ai-assistant-internals.md) | AI Assistant backend, MCP tool schemas, system prompt templates, and streaming contracts. |

---

## Core Technologies

* **Framework**: .NET 10.0 (C#)
* **Desktop UI**: Avalonia UI 11.x (XAML cross-platform dark interface)
* **Web UI & Proxy**: ASP.NET Core Minimal APIs + Kestrel reverse proxy
* **Process Management**: Win32 Job Objects (Windows) / `kill -TERM` process groups (Linux)
* **Testing**: xUnit, Moq, Playwright, Playwright Layout Inspector
* **Documentation**: VitePress with clear, structured writing guidelines
