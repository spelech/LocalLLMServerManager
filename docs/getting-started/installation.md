# Installation Guide

This guide describes how to install Local LLM Server Manager on Windows and Linux systems.

---

## Windows Installation

Choose one of two installation methods for Windows.

### Method 1: Official Windows Installer (Recommended)

The official installer configures the application, sets up desktop shortcuts, and registers the background Windows Service.

1. Open the [GitHub Releases page](https://github.com/spelech/LocalLLMServerManager/releases) in your web browser.
2. Download the latest installer executable (`LocalLLMServerManager-Setup.exe`).
3. Right-click the downloaded file and select **Run as administrator**.
4. Select your desired installation components in the setup wizard:
   - **Install Windows Service**: Starts the server in headless mode when the machine boots.
   - **Auto-Start System Tray App**: Starts the system tray application when you sign in.
   - **Desktop and Start Menu Shortcuts**: Creates application icons for quick access.
5. Click **Install** to complete the installation process.

> [!NOTE]
> The installer performs seamless in-place upgrades. The installer terminates older background processes safely, preserves your `settings.json` file, and restarts the background service.

> [!IMPORTANT]
> Administrator privileges are required to register and start the Windows Service.

---

### Method 2: Standalone Portable Archive (.zip)

Use the portable archive to run the application without modifying system services.

1. Download the `LocalLLMServerManager-win-x64.zip` archive from the Releases page.
2. Extract the archive contents into a folder (for example: `C:\LocalLLMServerManager`).
3. Open the extracted folder in File Explorer.
4. Double-click `LocalLLMServerManager.exe` to start the application.

---

## Linux Installation

Choose the automated script for system integration, or run the application in headless service mode.

### Method 1: Automated Linux Script (`install_linux.sh`)

The automated script configures system paths, registers a `systemd` unit, and installs desktop menu entries.

1. Open a terminal on your Linux machine.
2. Clone the repository or download the source archive:
   ```bash
   git clone https://github.com/spelech/LocalLLMServerManager.git
   cd LocalLLMServerManager
   ```
3. Run the installer script with root privileges:
   ```bash
   sudo ./scripts/install_linux.sh
   ```

The script performs the following actions automatically:
- Stops any active `localllmmanager.service` instance.
- Preserves existing user settings in `settings.json`.
- Installs binaries to `/usr/local/share/LocalLLMServerManager`.
- Creates a binary symlink at `/usr/local/bin/localllmmanager`.
- Registers and enables the `localllmmanager.service` systemd daemon.
- Installs the desktop launcher file (`localllmmanager.desktop`).

---

### Method 2: Running the Native GUI on Linux Desktop

Launch the native Avalonia user interface on Linux desktop sessions:

1. Open your terminal.
2. Type `localllmmanager` and press `Enter`:
   ```bash
   localllmmanager
   ```

The native dark Avalonia desktop window opens on both X11 and Wayland display environments.

---

### Method 3: Running Headless CLI Mode

Run the server in headless mode on Linux servers without a graphical desktop:

1. Open your terminal.
2. Navigate to the installation directory.
3. Start the application with the service argument:
   ```bash
   dotnet run -- --service
   ```

Alternatively, control the background daemon with `systemctl`:

```bash
# Start the background service
sudo systemctl start localllmmanager

# Inspect service status
sudo systemctl status localllmmanager

# Stop the background service
sudo systemctl stop localllmmanager
```

---

## Remote Server & SSH Port Forwarding

Access the complete web dashboard on a headless remote server using SSH port forwarding:

1. Connect to your remote host with port `5246` forwarded to your local machine:
   ```bash
   ssh -L 5246:localhost:5246 user@your-remote-host
   ```
2. Start the headless service on the remote server:
   ```bash
   sudo systemctl start localllmmanager
   ```
3. Open `http://localhost:5246` in your local web browser.

> [!TIP]
> The web interface provides full access to VRAM telemetry, model downloads, and the 3D WebGL viewer through the forwarded SSH port.

---

## Verify Installation

Verify that the local server operates correctly:

1. Open your web browser.
2. Navigate to `http://localhost:5246/health`.
3. Verify that the response returns HTTP status `200 OK` with JSON health information.
4. Navigate to `http://localhost:5246` to view the user interface.

---

## Next Steps

Proceed to the [First-Time Configuration Guide](./configuration.md) to configure engine paths and network ports.
