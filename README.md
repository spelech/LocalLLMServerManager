# Local LLM Server Manager

An orchestrator, proxy, and visual dashboard to manage local Large Language Models (**Ollama**) and Image Generation (**Stable Diffusion / Forge**) on Windows. It tracks GPU VRAM usage, profiles model capabilities, computes KV Cache memory footprints, and integrates with the **Hugging Face Hub** to search and pull GGUF models directly.

---

## 📸 Web UI Screenshots (From Running Instance)

### 1. Model Management Dashboard
*View local models, loaded status, active VRAM usage, and compute context requirements.*
![Installed Models Dashboard](screenshots/dashboard.png)

### 2. Discover & Download Models Tab
*Search Hugging Face GGUF repositories, inspect file sizes, and pull quantizations with live speed tracking.*
![Discover and Download Models Panel](screenshots/discover.png)

---

## 🌟 Key Features

1. **Active Service Health Checks**: Pulsing, real-time indicators monitoring the status of the Ollama API (`11434`) and Stable Diffusion / Forge API (`7860`).
2. **VRAM Usage Visualizer**: A stacked memory bar showing VRAM occupied by loaded LLMs versus free GPU memory.
3. **KV Cache Context Calculator**:
   - Slide target token context sizes (e.g. up to 16,384 tokens) to view estimated Weights vs KV Cache sizes.
   - Calculates allocations using layers, KV heads, and hidden dimensions parsed from model architectures.
   - Warns you if the model exceeds available VRAM (leading to a slower CPU offload split).
4. **Model Strengths Profile**: Profiles model families (Llama, Gemma, Qwen, Phi, Mistral) to show use-case tags (`Coding`, `Reasoning`, `Math`) and capabilities descriptions.
5. **Hugging Face Hub Integration**: Search for GGUF repos, select recommended quantizations (like `Q4_K_M`, `Q5_K_M`, `Q8_0`), inspect file sizes, and download them with live progress streams.
6. **Concurrent Model Preloading**: Trigger manual loads into GPU VRAM (indefinite hold via `keep_alive: -1`) to run multiple models side-by-side.
7. **Optional Windows Service**: Run the manager headlessly as a background service starting automatically on boot.

---

## 🚀 Installation & Setup

We provide a PowerShell installer script to compile, publish, and optionally register the app as a Windows Service.

### Quick Install:
1. Open PowerShell as **Administrator**.
2. Navigate to the project directory.
3. Run the installer:
   ```powershell
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force
   .\install.ps1
   ```

### Installer Prompts:
*   **Installation Directory**: Specify where to install the published application binaries (Default: `C:\LocalLLMServerManager`).
*   **Windows Service**: Choose whether to register and launch the app as a background service (`Y/N`).

---

## ⚙️ Service Control Commands

If installed as a Windows Service, open PowerShell as **Administrator** to manage it:

*   **Start the Service**:
    ```powershell
    Start-Service -Name "LocalLLMServerManager"
    ```
*   **Stop the Service**:
    ```powershell
    Stop-Service -Name "LocalLLMServerManager"
    ```
*   **Check Status**:
    ```powershell
    Get-Service -Name "LocalLLMServerManager"
    ```
*   **Uninstall the Service**:
    ```powershell
    sc.exe delete "LocalLLMServerManager"
    ```

If not running as a service, execute the published binary directly:
```cmd
C:\LocalLLMServerManager\LocalLLMServerManager.exe
```
The dashboard will be available at [http://localhost:5246/](http://localhost:5246/).
