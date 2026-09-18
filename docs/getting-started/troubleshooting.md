# Troubleshooting Guide

This guide provides solutions for common issues with GPU memory, port conflicts, process management, and network connections.

---

## 1. VRAM Out of Memory (OOM) Errors

### Symptoms
- Model loading fails with a `CUDA out of memory` message.
- Image generation or video rendering crashes abruptly during execution.

### Solutions
Follow these steps to resolve GPU memory exhaustion:

1. **Unload Inactive Models**:
   Click **Unload All VRAM** on the top status bar. This releases all loaded language models from GPU memory.

2. **Verify VRAM Orchestrator**:
   Confirm that the VRAM Orchestrator is enabled. The orchestrator unloads idle language models before diffusion or 3D generation starts.

3. **Select a Smaller Model Quantization**:
   Open **Find & Download Models**. Choose a model quantized at `Q4_K_M` or `Q3_K_M` instead of `Q8_0` or `FP16`.

4. **Reduce Context Length**:
   Open **My Models**. Move the **Interactive KV Cache Calculator** slider to a smaller context window size.

5. **Limit Concurrent Models**:
   Set the system environment variable `OLLAMA_MAX_LOADED_MODELS=1` to prevent multiple language models from loading simultaneously.

> [!WARNING]
> Running heavy video models alongside high-parameter language models can exceed total GPU capacity. Unload language models before video synthesis.

---

## 2. Network Port Conflicts

### Symptoms
- An engine status indicator displays red.
- Server logs show `Address already in use` error messages.

### Solutions
Identify the conflicting process and reassign port numbers:

1. **Locate the Conflicting Process**:
   - On Windows, open PowerShell and check the port status:
     ```powershell
     netstat -ano | findstr :5246
     ```
   - On Linux, open terminal and check the port status:
     ```bash
     ss -tulpn | grep 5246
     ```

2. **Stop the Conflicting Application**:
   Terminate the process that holds the port, or close the duplicate application instance.

3. **Change the Port Number**:
   - Open the **Settings** tab.
   - Type an available port number into the engine port field.
   - Click **Save Settings**.

> [!NOTE]
> The reverse proxy automatically updates its routes when you change port numbers in the Settings tab.

---

## 3. Engine Process Hanging or Failing to Stop

### Symptoms
- An engine process remains active in Task Manager or terminal after clicking **Stop Engine**.
- CPU or GPU usage stays elevated after an engine stops.

### Solutions
Terminate the hung process tree using system utilities:

1. **Terminate on Windows**:
   Open PowerShell as Administrator and terminate the target process tree:
   ```powershell
   Stop-Process -Name "ollama" -Force
   ```

2. **Terminate on Linux**:
   Open terminal and stop the lingering process:
   ```bash
   pkill -9 -f "ollama"
   ```

3. **Restart the Manager Service**:
   - On Windows: Run `Restart-Service -Name "LocalLLMServerManager"`.
   - On Linux: Run `sudo systemctl restart localllmmanager`.

> [!TIP]
> Local LLM Server Manager assigns Windows child processes to Win32 Job Objects. Job Objects automatically terminate child processes during standard shutdowns.

---

## 4. Firewall and Local Network Access

### Symptoms
- External network clients or AI coding assistants cannot connect to `http://<server-ip>:5246`.
- MCP connection attempts fail with connection timeout errors.

### Solutions
Configure host firewall rules to permit inbound TCP traffic on port `5246`:

1. **Configure Windows Firewall**:
   Open PowerShell as Administrator and create an inbound firewall rule:
   ```powershell
   New-NetFirewallRule -DisplayName "Local LLM Server Manager" -Direction Inbound -LocalPort 5246 -Protocol TCP -Action Allow
   ```

2. **Configure Linux Firewall**:
   Open terminal and allow traffic through `ufw`:
   ```bash
   sudo ufw allow 5246/tcp
   ```

3. **Verify Connectivity**:
   Send an HTTP request from the client computer to test the connection:
   ```bash
   curl http://<server-ip>:5246/health
   ```
   Verify that the command returns HTTP status `200 OK`.

> [!IMPORTANT]
> Do not expose port `5246` directly to the public internet without a secure reverse proxy and authentication layer.

---

## 5. Missing or Unset Tool Paths

### Symptoms
- The engine path status badge displays a red **Missing** badge in the Settings tab.
- The manager cannot start the selected engine.

### Solutions
1. Open the **Settings** tab.
2. Click **Auto-Detect Installed Tools** to scan system drives for missing tools.
3. If auto-detection cannot locate the file, click **Browse** next to the path field.
4. Select the engine executable or script file manually.
5. Click **Save Path**.

---

## Getting Additional Support

If your issue persists after you follow these troubleshooting steps:
- Open the [GitHub Issues page](https://github.com/spelech/LocalLLMServerManager/issues) to submit a bug report.
- Attach system details, GPU model specifications, and relevant log files to your report.

---

## Related Documentation

- [Getting Started Overview](./index.md)
- [First-Time Configuration](./configuration.md)
- [Quickstart Guide](./quickstart.md)
