# First-Time Configuration

Configure your engine paths, network ports, and model directories after installation. The application stores all configuration values in `settings.json`.

---

## Step 1: Open the Settings Tab

Open the configuration screen in your desktop application or web browser:

1. Open the Local LLM Server Manager desktop window, or navigate to `http://localhost:5246` in your web browser.
2. Click the **Settings** tab on the top navigation bar.

---

## Step 2: Auto-Detect Installed Engines

Use the built-in discovery service to locate installed tools automatically:

1. Locate the **Engine Detection** panel in the Settings tab.
2. Click **Auto-Detect Installed Tools**.

The discovery service scans your system drives, environment variables, and standard folder locations:
- **Ollama**: Checks `%LOCALAPPDATA%\Programs\Ollama\ollama.exe` and `~/.ollama/models`.
- **Stable Diffusion Forge**: Checks `webui-user.bat`, `webui.sh`, and `models/Stable-diffusion` folders.
- **ComfyUI**: Checks `run_nvidia_gpu.bat`, `main.py`, and `models/` folders.
- **Kokoro TTS Engine**: Checks Python virtual environments and audio scripts.

> [!NOTE]
> Auto-detection only updates empty or invalid path entries. The scanner does not overwrite existing valid configurations.

### Inspect Path Status Badges

Inspect the color-coded badge next to each configured path:

- 🟢 **Valid**: The executable file exists or the target folder is accessible.
- 🔴 **Missing**: The specified target path does not exist on disk.
- ⚪ **Unset**: The path is empty. The system uses standard system fallbacks.

---

## Step 3: Configure Network Ports

Verify and customize the network port numbers for each supervised engine.

| Engine Service | Default Port | Description |
|---|---|---|
| **Local LLM Server Manager** | `5246` | Unified reverse proxy, MCP server, and dashboard. |
| **Ollama LLM Engine** | `11434` | Language model inference server. |
| **Stable Diffusion Forge** | `7860` | Image generation web server. |
| **ComfyUI Engine** | `8188` | Node-based workflow and 3D generation server. |
| **Kokoro TTS Engine** | `8880` | Local speech synthesis API server. |

To change an engine port number:
1. Click the port input field for the target engine.
2. Type the new port number.
3. Click **Save Settings**.

> [!WARNING]
> Ensure no other application uses the assigned port numbers. Port conflicts prevent AI engines from starting.

---

## Step 4: Configure Storage Directories

Set custom storage paths to organize your model weights, checkpoints, and generated media files.

1. Locate the **Storage Locations** section in the Settings tab.
2. Set the directory paths for each engine:
   - **Ollama Models Directory**: Folder containing Ollama model manifests and layer blobs.
   - **Forge Models Directory**: Root folder containing checkpoints, LoRAs, and VAE weights.
   - **ComfyUI Models Directory**: Root folder containing diffusion UNETs, text encoders, and 3D models.
   - **Media Output Directory**: Destination folder for generated images, videos, audio, and 3D files.
3. Click **Browse** to select a directory with the system folder dialog, or type the absolute path manually.
4. Click **Save Path** to confirm your changes.

> [!TIP]
> Select a fast NVMe solid-state drive for your model directories. Fast disk access speeds up model preloading.

---

## Step 5: Configure Modular Feature Packs

Enable or disable optional capabilities through the **Component Manager** in the Settings tab:

- **Video Feature Pack (`ext_video`)**: Installs ComfyUI workflow presets for Wan 2.2, LTX-2.5, and HunyuanVideo.
- **Audio Feature Pack (`ext_audio`)**: Installs Kokoro TTS scripts, Stable Audio presets, and YuE music workflows.

1. Scroll to the **Component Manager** panel.
2. Click **Install** next to the desired feature pack.
3. Wait for the component installer to complete the process.

---

## Next Steps

Proceed to the [Quickstart Guide](./quickstart.md) to download your first model and run your first prompt.
