# Getting Started & System Requirements

Local LLM Server Manager manages local artificial intelligence engines through a unified interface. The application coordinates language models, image diffusion, 3D mesh reconstruction, video synthesis, and speech generation.

The software runs as a native desktop application, a system tray application, and a headless background service.

---

## System Requirements

Review the hardware and operating system specifications before you install the software.

### Operating System Support

| Platform | Supported Versions | Display Environments |
|---|---|---|
| **Windows** | Windows 10 (64-bit, 21H2+) and Windows 11 | Desktop Shell, System Tray, Windows Service |
| **Linux** | Ubuntu 22.04+, Debian 12+, Fedora 38+, Arch Linux | X11, Wayland, Headless `systemd` daemon |

---

### Hardware Requirements

| Hardware Component | Minimum Requirement | Recommended Specification |
|---|---|---|
| **Processor (CPU)** | 4-core 64-bit x64 processor | 8-core modern x64 processor |
| **System Memory (RAM)** | 16 GB RAM | 32 GB RAM or higher |
| **Graphics Card (GPU)** | NVIDIA GPU with 8 GB VRAM | NVIDIA RTX 3060/4070+ with 12 GB to 24 GB VRAM |
| **Disk Storage** | 20 GB free disk space | 200 GB+ free space on NVMe SSD |
| **Network** | Loopback network interface | High-speed internet connection for model downloads |

> [!IMPORTANT]
> Install the latest NVIDIA GPU drivers and CUDA toolkit for hardware acceleration. Verify your GPU setup by running `nvidia-smi` in your terminal.

> [!NOTE]
> The system can run language models on CPU cores when a dedicated GPU is absent. However, CPU inference operates at reduced generation speeds.

> [!TIP]
> Place your model storage directories on a fast NVMe solid-state drive. Fast storage significantly reduces model loading times.

---

## Supported AI Engines

Local LLM Server Manager connects to and orchestrates multiple local inference engines:

- **Ollama Engine**: Runs Large Language Models (LLMs) locally through port `11434`.
- **Stable Diffusion Forge**: Generates images and manages CivitAI checkpoints through port `7860`.
- **ComfyUI Engine**: Generates 3D meshes, videos, and complex diffusion workflows through port `8188`.
- **Kokoro TTS Engine**: Synthesizes speech with OpenAI-compatible audio endpoints through port `8880`.

The manager coordinates these engines through a unified reverse proxy on port `5246`.

---

## Getting Started Roadmap

Follow these sequential steps to set up and use Local LLM Server Manager:

1. **[Installation Guide](./installation.md)**  
   Install the application on Windows using the official installer, or on Linux using the automated installation script.

2. **[First-Time Configuration](./configuration.md)**  
   Auto-detect installed engines, configure engine port numbers, and set your model storage directories.

3. **[Quickstart Guide](./quickstart.md)**  
   Download your first language model from Hugging Face or Ollama, and send your first chat prompt.

4. **[Troubleshooting Guide](./troubleshooting.md)**  
   Resolve common operational issues, handle VRAM out-of-memory errors, and eliminate port conflicts.

---

## Next Steps

Proceed to the [Installation Guide](./installation.md) to install the software on your system.
